using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace OpcDaProxyService
{
    /// <summary>
    /// OPC DA 中心采集代理服务
    ///
    /// 接收 collector 的 TCP 请求，通过远程 DCOM 访问 OPC DA 服务器。
    /// 协议格式: 4字节长度头 + JSON 数据
    /// </summary>
    internal static class Program
    {
        private static volatile bool _stopping;
        private static bool _loopbackOnly = true;
        private static ProxyConfig _config;
        private static TcpListener _listener;
        private static readonly List<TcpClient> _activeClients = new List<TcpClient>();
        private static readonly object _clientsLock = new object();

        // 并发连接限制器
        private static SemaphoreSlim _connectionLimiter;

        // 默认最大并发连接数
        private const int DEFAULT_MAX_CONNECTIONS = 20;

        // 日志分级与读取统计
        private static bool _debugEnabled;
        private static int _summaryIntervalSec = 10;
        private const int WARN_THROTTLE_MS = 5000;  // 同一 key 的 WARN 最多每 5 秒打一次
        private static long _statCycles;
        private static long _statItems;
        private static long _statGood;
        private static long _statBad;
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> _lastWarnTicks =
            new System.Collections.Concurrent.ConcurrentDictionary<string, long>();

        public static bool IsStopping { get { return _stopping; } }

        [STAThread]
        private static int Main(string[] args)
        {
            string configPath = GetConfigPath(args);
            if (configPath == null)
            {
                PrintHelp();
                return 0;
            }

            try
            {
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    _stopping = true;
                    Log("Stop signal received...");
                    StopListener();
                };

                _config = ProxyConfig.Load(configPath);
                _debugEnabled = ResolveDebugEnabled(_config);
                _summaryIntervalSec = _config.LogSummaryIntervalSec > 0 ? _config.LogSummaryIntervalSec : 10;
                Log("Starting OPC DA Proxy Service on port " + _config.ListenPort);
                Log("Configured OPC servers: " + _config.OpcServers.Count);
                Log("Log level: " + (_debugEnabled ? "DEBUG" : "INFO") + ", summary interval: " + _summaryIntervalSec + "s");

                StartListener();

                Log("Proxy service stopped.");
                return 0;
            }
            catch (Exception ex)
            {
                LogError("Proxy service failed: " + ex.GetType().FullName + ": " + ex.Message);
                LogError(ex.StackTrace);
                return 1;
            }
        }

        private static string GetConfigPath(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if ((args[i] == "--config" || args[i] == "-c") && i + 1 < args.Length)
                {
                    return args[i + 1];
                }
                if (args[i] == "--help" || args[i] == "-h" || args[i] == "/?")
                {
                    return null;
                }
            }

            string defaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "opcda_proxy_config.json");
            return File.Exists(defaultPath) ? defaultPath : null;
        }

        private static void PrintHelp()
        {
            Console.WriteLine("OPC DA Proxy Service - Center Collection Mode");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  OpcDaProxyService.exe --config opcda_proxy_config.json");
            Console.WriteLine();
            Console.WriteLine("This x86 service accepts TCP connections from collector and");
            Console.WriteLine("reads data from remote OPC DA servers via DCOM.");
        }

        private static void StartListener()
        {
            // 初始化连接限制器
            int maxConnections = _config.MaxConnections > 0 ? _config.MaxConnections : DEFAULT_MAX_CONNECTIONS;
            _connectionLimiter = new SemaphoreSlim(maxConnections, maxConnections);
            Log("Max concurrent connections: " + maxConnections);

            // B023：默认只监听回环（Collector 只连 127.0.0.1）；显式配置非回环地址时
            // 必须同时配置 authToken，否则拒绝启动，避免无鉴权暴露到网络。
            IPAddress listenAddress;
            if (!IPAddress.TryParse(_config.ListenAddress ?? "", out listenAddress))
            {
                throw new InvalidOperationException("Invalid listenAddress: " + _config.ListenAddress);
            }
            _loopbackOnly = IPAddress.IsLoopback(listenAddress);
            if (!_loopbackOnly && string.IsNullOrEmpty(_config.AuthToken))
            {
                throw new InvalidOperationException(
                    "listenAddress " + listenAddress + " is not loopback; authToken must be configured");
            }

            _listener = new TcpListener(listenAddress, _config.ListenPort);
            _listener.Start();
            Log("Listening on " + listenAddress + ":" + _config.ListenPort +
                (string.IsNullOrEmpty(_config.AuthToken) ? "" : " (auth token required)"));

            StartSummaryLogger();

            while (!_stopping)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();

                    // B023：回环监听时只接受回环来源（纵深防御）。
                    if (_loopbackOnly && !IsLoopbackClient(client))
                    {
                        LogWarn("Connection rejected (non-loopback client): " + client.Client.RemoteEndPoint);
                        client.Close();
                        continue;
                    }

                    // 检查并发连接限制
                    if (!_connectionLimiter.Wait(0))
                    {
                        string clientEndpoint = client.Client.RemoteEndPoint != null
                            ? client.Client.RemoteEndPoint.ToString()
                            : "unknown";
                        LogWarn("Connection rejected (max connections reached): " + clientEndpoint);
                        client.Close();
                        continue;
                    }

                    lock (_clientsLock)
                    {
                        _activeClients.Add(client);
                    }
                    ThreadPool.QueueUserWorkItem(HandleClient, client);
                }
                catch (SocketException)
                {
                    // Listener stopped
                    break;
                }
                catch (Exception ex)
                {
                    LogError("Accept error: " + ex.Message);
                }
            }
        }

        private static void StopListener()
        {
            try
            {
                if (_listener != null)
                {
                    _listener.Stop();
                }
            }
            catch { }

            lock (_clientsLock)
            {
                foreach (var client in _activeClients)
                {
                    try { client.Close(); } catch { }
                }
                _activeClients.Clear();
            }
        }

        private static void HandleClient(object state)
        {
            TcpClient client = (TcpClient)state;
            string clientEndpoint = client.Client.RemoteEndPoint != null
                ? client.Client.RemoteEndPoint.ToString()
                : "unknown";
            Log("Client connected: " + clientEndpoint);

            NetworkStream stream = null;
            OpcConnectionManager connectionManager = new OpcConnectionManager(_config);
            bool loopbackClient = IsLoopbackClient(client);
            int frameTimeoutMs = _config.FrameReadTimeoutMs > 0 ? _config.FrameReadTimeoutMs : 30000;

            try
            {
                stream = client.GetStream();
                stream.WriteTimeout = frameTimeoutMs;
                byte[] lengthBuffer = new byte[4];

                while (!_stopping && IsClientConnected(client))
                {
                    // 读取消息长度：首字节可无限等待（长连接空闲），帧内其余字节设读期限，
                    // 避免只发半帧的连接永久占用并发配额（B023 关联）。
                    stream.ReadTimeout = Timeout.Infinite;
                    int bytesRead = ReadExact(stream, lengthBuffer, 0, 1);
                    if (bytesRead == 0)
                    {
                        break; // Connection closed
                    }
                    stream.ReadTimeout = frameTimeoutMs;
                    bytesRead = ReadExact(stream, lengthBuffer, 1, 3);
                    if (bytesRead == 0)
                    {
                        break; // Connection closed
                    }

                    // 使用大端序（网络字节序）读取消息长度，与 Python 客户端一致
                    int messageLength = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lengthBuffer, 0));
                    if (messageLength <= 0 || messageLength > 10 * 1024 * 1024)
                    {
                        LogError("Invalid message length: " + messageLength);
                        break;
                    }

                    // 读取消息内容
                    byte[] messageBuffer = new byte[messageLength];
                    bytesRead = ReadExact(stream, messageBuffer, 0, messageLength);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    string messageJson = Encoding.UTF8.GetString(messageBuffer);
                    IpcRequest request = IpcRequest.FromJson(messageJson);

                    // B023：配置了 authToken 时每个请求都必须携带匹配的 auth_token。
                    if (!IsAuthorized(request))
                    {
                        LogWarn("Unauthorized request from " + clientEndpoint + ": " + request.RequestType);
                        SendResponse(stream, IpcResponse.Fail(request.RequestId, "unauthorized"));
                        break;
                    }

                    // 处理请求
                    IpcResponse response = ProcessRequest(request, connectionManager, loopbackClient);

                    // 发送响应
                    SendResponse(stream, response);
                }
            }
            catch (Exception ex)
            {
                if (!_stopping)
                {
                    LogError("Client error " + clientEndpoint + ": " + ex.Message);
                }
            }
            finally
            {
                if (connectionManager != null)
                {
                    connectionManager.Dispose();
                }
                if (stream != null)
                {
                    stream.Close();
                }
                client.Close();
                lock (_clientsLock)
                {
                    _activeClients.Remove(client);
                }
                // 释放连接限制器
                if (_connectionLimiter != null)
                {
                    _connectionLimiter.Release();
                }
                Log("Client disconnected: " + clientEndpoint);
            }
        }

        /// <summary>
        /// 可靠地检测 TCP 客户端连接状态
        /// TcpClient.Connected 只反映最后一次 I/O 操作时的状态，不可靠
        /// </summary>
        private static bool IsClientConnected(TcpClient client)
        {
            if (!client.Connected)
            {
                return false;
            }

            try
            {
                Socket socket = client.Client;
                // Poll 返回 true 表示 socket 可读（有数据或已关闭）
                bool readable = socket.Poll(0, SelectMode.SelectRead);
                bool hasData = socket.Available > 0;

                // 如果可读但没有数据，说明对端已关闭连接
                if (readable && !hasData)
                {
                    return false;
                }

                return true;
            }
            catch (SocketException)
            {
                return false;
            }
        }

        private static bool IsLoopbackClient(TcpClient client)
        {
            try
            {
                IPEndPoint remote = client.Client.RemoteEndPoint as IPEndPoint;
                return remote != null && IPAddress.IsLoopback(remote.Address);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsAuthorized(IpcRequest request)
        {
            string expected = _config.AuthToken;
            if (string.IsNullOrEmpty(expected))
            {
                return true;
            }
            string actual = request.AuthToken ?? "";
            // 定长比较，避免按字节早退泄露时序。
            byte[] a = Encoding.UTF8.GetBytes(expected);
            byte[] b = Encoding.UTF8.GetBytes(actual);
            int diff = a.Length ^ b.Length;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ (i < b.Length ? b[i] : 0);
            }
            return diff == 0;
        }

        private static string NormalizeVmHost(string vmHost)
        {
            string host = (vmHost ?? "").Trim();
            if (host.Length == 0
                || host == "."
                || host == "127.0.0.1"
                || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                return "";
            }
            return host.ToLowerInvariant();
        }

        /// <summary>
        /// B023：connect/read 的目标 vm_host 必须属于配置的 opcServers（配置为空时不限制，兼容旧配置）。
        /// opc_server 来自中心 comm_protocol.server，可能与 Agent 配置的 serverProgId 不同，仅告警不拒绝。
        /// </summary>
        private static string CheckTargetAllowed(string opcServer, string vmHost)
        {
            if (_config.OpcServers == null || _config.OpcServers.Count == 0)
            {
                return null;
            }
            string host = NormalizeVmHost(vmHost);
            bool hostAllowed = false;
            bool progIdKnown = false;
            foreach (OpcServerConfig server in _config.OpcServers)
            {
                if (NormalizeVmHost(server.VmHost) != host)
                {
                    continue;
                }
                hostAllowed = true;
                if (string.IsNullOrEmpty(server.ServerProgId)
                    || string.Equals(server.ServerProgId, opcServer, StringComparison.OrdinalIgnoreCase))
                {
                    progIdKnown = true;
                }
            }
            if (!hostAllowed)
            {
                return "vm_host not allowed by proxy config: " + (vmHost ?? "");
            }
            if (!progIdKnown)
            {
                LogWarnThrottled("progid:" + opcServer, "opc_server " + opcServer + " differs from configured serverProgId for vm_host " + (vmHost ?? ""));
            }
            return null;
        }

        private static int ReadExact(NetworkStream stream, byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = stream.Read(buffer, offset + totalRead, count - totalRead);
                if (read == 0)
                {
                    return 0; // Connection closed
                }
                totalRead += read;
            }
            return totalRead;
        }

        private static void SendResponse(NetworkStream stream, IpcResponse response)
        {
            string json = response.ToJson();
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
            // 使用大端序（网络字节序）发送长度，与 Python 客户端一致
            byte[] lengthBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(jsonBytes.Length));

            stream.Write(lengthBytes, 0, 4);
            stream.Write(jsonBytes, 0, jsonBytes.Length);
            stream.Flush();
        }

        private static IpcResponse ProcessRequest(IpcRequest request, OpcConnectionManager connectionManager, bool loopbackClient)
        {
            try
            {
                switch (request.RequestType)
                {
                    case "ping":
                        return IpcResponse.Ok(request.RequestId, new { pong = true });

                    case "connect":
                        return ProcessConnect(request, connectionManager);

                    case "disconnect":
                        return ProcessDisconnect(request, connectionManager);

                    case "read":
                        return ProcessRead(request, connectionManager);

                    case "shutdown":
                        // B023：停服只允许本机管理通道发起。
                        if (!loopbackClient)
                        {
                            return IpcResponse.Fail(request.RequestId, "shutdown is only allowed from loopback");
                        }
                        _stopping = true;
                        return IpcResponse.Ok(request.RequestId, new { message = "Shutting down" });

                    default:
                        return IpcResponse.Fail(request.RequestId, "Unknown request type: " + request.RequestType);
                }
            }
            catch (Exception ex)
            {
                LogError("Request failed: " + request.RequestType + " - " + ex.Message);
                return IpcResponse.Fail(request.RequestId, ex.Message);
            }
        }

        private static IpcResponse ProcessConnect(IpcRequest request, OpcConnectionManager connectionManager)
        {
            var payload = request.Payload;
            string opcServer = payload != null && payload.ContainsKey("opc_server")
                ? Convert.ToString(payload["opc_server"])
                : null;
            string vmHost = payload != null && payload.ContainsKey("vm_host")
                ? Convert.ToString(payload["vm_host"])
                : null;
            int updateRateMs = payload != null && payload.ContainsKey("update_rate_ms")
                ? Convert.ToInt32(payload["update_rate_ms"])
                : 500;

            if (string.IsNullOrEmpty(opcServer))
            {
                return IpcResponse.Fail(request.RequestId, "opc_server is required");
            }
            string denied = CheckTargetAllowed(opcServer, vmHost);
            if (denied != null)
            {
                return IpcResponse.Fail(request.RequestId, denied);
            }

            bool connected = connectionManager.Connect(opcServer, vmHost, updateRateMs);
            if (connected)
            {
                return IpcResponse.Ok(request.RequestId, new { connected = true });
            }
            else
            {
                return IpcResponse.Fail(request.RequestId, "Failed to connect to OPC server: " + opcServer);
            }
        }

        private static IpcResponse ProcessDisconnect(IpcRequest request, OpcConnectionManager connectionManager)
        {
            connectionManager.Disconnect();
            return IpcResponse.Ok(request.RequestId, new { disconnected = true });
        }

        private static IpcResponse ProcessRead(IpcRequest request, OpcConnectionManager connectionManager)
        {
            var payload = request.Payload;
            if (payload == null || !payload.ContainsKey("item_ids"))
            {
                return IpcResponse.Fail(request.RequestId, "item_ids is required");
            }

            var itemIdsObj = payload["item_ids"];
            List<string> itemIds = new List<string>();
            IEnumerable enumerable = itemIdsObj as IEnumerable;
            if (enumerable != null && !(itemIdsObj is string))
            {
                foreach (object item in enumerable)
                {
                    itemIds.Add(item != null ? item.ToString() : "");
                }
            }
            else if (itemIdsObj != null)
            {
                itemIds.Add(itemIdsObj.ToString());
            }

            if (itemIds.Count == 0)
            {
                return IpcResponse.Fail(request.RequestId, "item_ids is empty or cannot be parsed");
            }

            // 可选：指定 OPC 服务器（如未连接）
            string opcServer = payload.ContainsKey("opc_server")
                ? Convert.ToString(payload["opc_server"])
                : null;
            string vmHost = payload.ContainsKey("vm_host")
                ? Convert.ToString(payload["vm_host"])
                : null;

            if (!string.IsNullOrEmpty(opcServer))
            {
                string denied = CheckTargetAllowed(opcServer, vmHost);
                if (denied != null)
                {
                    return IpcResponse.Fail(request.RequestId, denied);
                }
            }

            LogDebug("HandleReadItems begin: count=" + itemIds.Count + ", opc_server=" + opcServer + ", vm_host=" + vmHost + ", first_item=" + itemIds[0]);
            List<Dictionary<string, object>> results = connectionManager.ReadItems(itemIds, opcServer, vmHost);

            // 聚合统计：good = quality 0xC0，其余计 bad（含坏质量/读取失败/加点失败）。
            int good = 0;
            int bad = 0;
            foreach (Dictionary<string, object> r in results)
            {
                object q;
                if (r.TryGetValue("quality", out q) && Convert.ToInt32(q) == 0x00C0)
                {
                    good++;
                }
                else
                {
                    bad++;
                }
            }
            RecordRead(results.Count, good, bad);

            if (results.Count > 0)
            {
                Dictionary<string, object> first = results[0];
                object resultItemId = first.ContainsKey("item_id") ? first["item_id"] : "<missing>";
                object resultQuality = first.ContainsKey("quality") ? first["quality"] : "<missing>";
                object resultValue = first.ContainsKey("value") ? first["value"] : "<missing>";
                object resultError = first.ContainsKey("error") ? first["error"] : "";
                LogDebug("HandleReadItems first_result: item_id=" + resultItemId + ", quality=" + resultQuality + ", value=" + (resultValue == null ? "<null>" : resultValue.ToString()) + ", error=" + resultError);
            }
            LogDebug("HandleReadItems end: result_count=" + results.Count);
            return IpcResponse.Ok(request.RequestId, results);
        }

        internal static void Log(string message)
        {
            Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + " [INFO] " + message);
        }

        internal static void LogError(string message)
        {
            Console.Error.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + " [ERROR] " + message);
        }

        internal static void LogWarn(string message)
        {
            Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + " [WARN] " + message);
        }

        internal static void LogDebug(string message)
        {
            if (!_debugEnabled) return;
            Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + " [DEBUG] " + message);
        }

        /// <summary>
        /// 限流 WARN：同一 key（通常是 item_id）在 WARN_THROTTLE_MS 内最多打印一次，
        /// 避免某点位持续坏质量/加点失败时刷屏。
        /// </summary>
        internal static void LogWarnThrottled(string key, string message)
        {
            long now = Environment.TickCount;
            long last;
            if (_lastWarnTicks.TryGetValue(key, out last) && unchecked((int)(now - last)) < WARN_THROTTLE_MS)
            {
                return;
            }
            _lastWarnTicks[key] = now;
            LogWarn(message);
        }

        /// <summary>累加一轮读取统计（线程安全）。</summary>
        internal static void RecordRead(int items, int good, int bad)
        {
            Interlocked.Increment(ref _statCycles);
            Interlocked.Add(ref _statItems, items);
            Interlocked.Add(ref _statGood, good);
            Interlocked.Add(ref _statBad, bad);
        }

        private static bool ResolveDebugEnabled(ProxyConfig config)
        {
            // 环境变量 OPCDA_PROXY_LOG_LEVEL 优先于配置文件 logLevel。
            string level = Environment.GetEnvironmentVariable("OPCDA_PROXY_LOG_LEVEL");
            if (string.IsNullOrEmpty(level))
            {
                level = config.LogLevel;
            }
            return string.Equals(level, "DEBUG", StringComparison.OrdinalIgnoreCase);
        }

        private static void StartSummaryLogger()
        {
            // DEBUG 下逐点已全量打印，无需聚合统计。
            if (_debugEnabled) return;
            Thread t = new Thread(SummaryLoop);
            t.IsBackground = true;
            t.Start();
        }

        private static void SummaryLoop()
        {
            while (!_stopping)
            {
                Thread.Sleep(_summaryIntervalSec * 1000);
                long cycles = Interlocked.Exchange(ref _statCycles, 0);
                long items = Interlocked.Exchange(ref _statItems, 0);
                long good = Interlocked.Exchange(ref _statGood, 0);
                long bad = Interlocked.Exchange(ref _statBad, 0);
                if (cycles > 0)
                {
                    Log("Read summary (" + _summaryIntervalSec + "s): cycles=" + cycles +
                        " items=" + items + " good=" + good + " bad=" + bad);
                }
            }
        }
    }

    /// <summary>
    /// IPC 请求
    /// </summary>
    internal class IpcRequest
    {
        public string RequestId { get; set; }
        public string RequestType { get; set; }
        public string AuthToken { get; set; }
        public Dictionary<string, object> Payload { get; set; }

        public static IpcRequest FromJson(string json)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            var dict = serializer.Deserialize<Dictionary<string, object>>(json);
            Dictionary<string, object> payload = null;
            if (dict.ContainsKey("payload"))
            {
                payload = dict["payload"] as Dictionary<string, object>;
            }
            return new IpcRequest
            {
                RequestId = Convert.ToString(dict["request_id"]),
                RequestType = Convert.ToString(dict["request_type"]),
                AuthToken = dict.ContainsKey("auth_token") ? Convert.ToString(dict["auth_token"]) : null,
                Payload = payload ?? new Dictionary<string, object>()
            };
        }
    }

    /// <summary>
    /// IPC 响应
    /// </summary>
    internal class IpcResponse
    {
        public string RequestId { get; set; }
        public string Status { get; set; }
        public object Data { get; set; }
        public string Error { get; set; }

        public static IpcResponse Ok(string requestId, object data)
        {
            return new IpcResponse
            {
                RequestId = requestId,
                Status = "ok",
                Data = data
            };
        }

        public static IpcResponse Fail(string requestId, string error)
        {
            return new IpcResponse
            {
                RequestId = requestId,
                Status = "error",
                Error = error
            };
        }

        public string ToJson()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict["request_id"] = RequestId;
            dict["status"] = Status;
            dict["version"] = "1.0";
            if (Data != null)
            {
                dict["data"] = Data;
            }
            if (!string.IsNullOrEmpty(Error))
            {
                dict["error"] = Error;
            }

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            return serializer.Serialize(dict);
        }
    }

    /// <summary>
    /// 代理配置
    /// </summary>
    internal class ProxyConfig
    {
        public int ListenPort { get; set; }
        public List<OpcServerConfig> OpcServers { get; set; }
        public int UpdateRateMs { get; set; }
        public int ConnectionTimeoutMs { get; set; }
        public int MaxConnections { get; set; }  // 最大并发连接数
        public string LogLevel { get; set; }  // INFO（默认，简略）/ DEBUG（逐点全量）
        public int LogSummaryIntervalSec { get; set; }  // 非 DEBUG 下聚合统计打印间隔（秒）
        public string ListenAddress { get; set; }  // B023：监听地址，默认 127.0.0.1
        public string AuthToken { get; set; }  // B023：预共享令牌；非回环监听时必填
        public int FrameReadTimeoutMs { get; set; }  // 帧内读/写期限（毫秒）

        public static ProxyConfig Load(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Config file not found", path);
            }

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            var dict = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path, Encoding.UTF8));

            ProxyConfig config = new ProxyConfig();
            config.ListenPort = dict.ContainsKey("listenPort") ? Convert.ToInt32(dict["listenPort"]) : 9090;
            config.UpdateRateMs = dict.ContainsKey("updateRateMs") ? Convert.ToInt32(dict["updateRateMs"]) : 500;
            config.ConnectionTimeoutMs = dict.ContainsKey("connectionTimeoutMs") ? Convert.ToInt32(dict["connectionTimeoutMs"]) : 10000;
            config.MaxConnections = dict.ContainsKey("maxConnections") ? Convert.ToInt32(dict["maxConnections"]) : 20;
            config.LogLevel = dict.ContainsKey("logLevel") ? Convert.ToString(dict["logLevel"]) : "INFO";
            config.LogSummaryIntervalSec = dict.ContainsKey("logSummaryIntervalSec") ? Convert.ToInt32(dict["logSummaryIntervalSec"]) : 10;
            config.ListenAddress = dict.ContainsKey("listenAddress") ? Convert.ToString(dict["listenAddress"]) : "127.0.0.1";
            if (string.IsNullOrWhiteSpace(config.ListenAddress))
            {
                config.ListenAddress = "127.0.0.1";
            }
            config.AuthToken = dict.ContainsKey("authToken") ? Convert.ToString(dict["authToken"]) : "";
            config.FrameReadTimeoutMs = dict.ContainsKey("frameReadTimeoutMs") ? Convert.ToInt32(dict["frameReadTimeoutMs"]) : 30000;

            config.OpcServers = new List<OpcServerConfig>();
            ArrayList servers = dict.ContainsKey("opcServers") ? dict["opcServers"] as ArrayList : null;
            if (servers != null)
            {
                foreach (var server in servers)
                {
                    Dictionary<string, object> serverDict = server as Dictionary<string, object>;
                    if (serverDict != null)
                    {
                        OpcServerConfig sc = new OpcServerConfig();
                        sc.VmHost = serverDict.ContainsKey("vmHost") ? Convert.ToString(serverDict["vmHost"]) : "";
                        sc.ServerProgId = serverDict.ContainsKey("serverProgId") ? Convert.ToString(serverDict["serverProgId"]) : "";
                        sc.Channel = serverDict.ContainsKey("channel") ? Convert.ToString(serverDict["channel"]) : "";

                        sc.Devices = new List<string>();
                        ArrayList devices = serverDict.ContainsKey("devices") ? serverDict["devices"] as ArrayList : null;
                        if (devices != null)
                        {
                            foreach (var device in devices)
                            {
                                sc.Devices.Add(device != null ? device.ToString() : "");
                            }
                        }

                        config.OpcServers.Add(sc);
                    }
                }
            }

            return config;
        }
    }

    internal class OpcServerConfig
    {
        public string VmHost { get; set; }
        public string ServerProgId { get; set; }
        public string Channel { get; set; }
        public List<string> Devices { get; set; }
    }
}
