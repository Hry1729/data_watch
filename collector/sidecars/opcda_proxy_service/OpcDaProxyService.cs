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
        private static ProxyConfig _config;
        private static TcpListener _listener;
        private static readonly List<TcpClient> _activeClients = new List<TcpClient>();
        private static readonly object _clientsLock = new object();

        // 并发连接限制器
        private static SemaphoreSlim _connectionLimiter;

        // 默认最大并发连接数
        private const int DEFAULT_MAX_CONNECTIONS = 20;

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
                Log("Starting OPC DA Proxy Service on port " + _config.ListenPort);
                Log("Configured OPC servers: " + _config.OpcServers.Count);

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

            _listener = new TcpListener(IPAddress.Any, _config.ListenPort);
            _listener.Start();
            Log("Listening on 0.0.0.0:" + _config.ListenPort);

            while (!_stopping)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();

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

            try
            {
                stream = client.GetStream();
                byte[] lengthBuffer = new byte[4];

                while (!_stopping && IsClientConnected(client))
                {
                    // 读取消息长度
                    int bytesRead = ReadExact(stream, lengthBuffer, 0, 4);
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

                    // 处理请求
                    IpcResponse response = ProcessRequest(request, connectionManager);

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

        private static IpcResponse ProcessRequest(IpcRequest request, OpcConnectionManager connectionManager)
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

            List<Dictionary<string, object>> results = connectionManager.ReadItems(itemIds, opcServer, vmHost);
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
    }

    /// <summary>
    /// IPC 请求
    /// </summary>
    internal class IpcRequest
    {
        public string RequestId { get; set; }
        public string RequestType { get; set; }
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
