using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using ComFileTime = System.Runtime.InteropServices.ComTypes.FILETIME;

namespace OpcDaPushService
{
    internal static class Program
    {
        private static volatile bool _stopping;

        public static bool IsStopping
        {
            get { return _stopping; }
        }

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
                Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs eventArgs)
                {
                    eventArgs.Cancel = true;
                    _stopping = true;
                    Log("Stop signal received, draining current loop...");
                };

                AppConfig config = AppConfig.Load(configPath);
                config.Normalize(Path.GetDirectoryName(Path.GetFullPath(configPath)));
                Log("Starting OPC DA " + config.ReadMode + " mode: " + config.Devices.Count + " devices, " + config.TotalPointCount + " points, updateRate=" + config.UpdateRateMs + "ms");

                if (config.IsSubscriptionMode)
                {
                    RunSubscription(config);
                }
                else
                {
                    RunPoll(config);
                }

                Log("OPC DA push service stopped.");
                return 0;
            }
            catch (Exception ex)
            {
                LogError("OPC DA push service failed: " + ex.GetType().FullName + ": " + ex.Message);
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

            string defaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "opcda_push_config.json");
            return File.Exists(defaultPath) ? defaultPath : null;
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  OpcDaPushService.exe --config opcda_push_config.json");
            Console.WriteLine();
            Console.WriteLine("This x86 process reads local 32-bit OPC DA items and pushes tag-based batches to the center collector.");
        }

        private static void RunPoll(AppConfig config)
        {
            int reconnectDelayMs = 1000;  // 初始重连延迟
            int maxReconnectDelayMs = 60000;  // 最大重连延迟 60 秒

            while (!_stopping)
            {
                try
                {
                    using (OpcDaClient client = new OpcDaClient(config))
                    {
                        DateTime registrationStart = DateTime.Now;
                        client.Connect();
                        client.RegisterAllItems();
                        Log("Poll mode ready groups=" + client.GroupCount.ToString(CultureInfo.InvariantCulture)
                            + " registeredItems=" + client.RegisteredItemCount.ToString(CultureInfo.InvariantCulture)
                            + " registrationElapsedMs=" + ((int)(DateTime.Now - registrationStart).TotalMilliseconds).ToString(CultureInfo.InvariantCulture));

                        // 连接成功，重置重连延迟
                        reconnectDelayMs = 1000;

                        while (!_stopping)
                        {
                            DateTime cycleStart = DateTime.Now;
                            List<Dictionary<string, object>> points = client.ReadAll();
                            if (points.Count > 0 && config.PostEnabled)
                            {
                                if (!PushClient.PostPoints(config, points))
                                {
                                    LogWarn("Poll cycle push was not fully acknowledged; no local durable failure queue is configured; a later read cycle may retry current values.");
                                }
                            }
                            else if (points.Count > 0)
                            {
                                Log("Post disabled; read points=" + points.Count.ToString(CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                Log("No points read in this cycle.");
                            }

                            int elapsedMs = (int)(DateTime.Now - cycleStart).TotalMilliseconds;
                            int sleepMs = Math.Max(0, config.ReadIntervalMs - elapsedMs);
                            if (sleepMs == 0 && elapsedMs > config.ReadIntervalMs)
                            {
                                LogWarn("Poll cycle overrun elapsedMs=" + elapsedMs.ToString(CultureInfo.InvariantCulture)
                                    + " targetMs=" + config.ReadIntervalMs.ToString(CultureInfo.InvariantCulture));
                            }
                            SleepWithStop(sleepMs);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_stopping)
                    {
                        break;
                    }

                    LogError("OPC connection lost: " + ex.Message + ", reconnecting in " + reconnectDelayMs + "ms...");

                    // 等待后重连
                    SleepWithStop(reconnectDelayMs);

                    // 指数退避
                    reconnectDelayMs = Math.Min(reconnectDelayMs * 2, maxReconnectDelayMs);
                }
            }
        }

        private static void RunSubscription(AppConfig config)
        {
            using (SubscriptionStaRunner runner = new SubscriptionStaRunner(config))
            {
                runner.Start();
                while (!_stopping && !runner.HasStopped)
                {
                    Thread.Sleep(100);
                }
                runner.Stop();
                runner.Wait();
            }
        }

        private static void SleepWithStop(int milliseconds)
        {
            int remaining = milliseconds;
            while (remaining > 0 && !_stopping)
            {
                int part = Math.Min(remaining, 500);
                Thread.Sleep(part);
                remaining -= part;
            }
        }

        internal static void Log(string message)
        {
            Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + " [INFO] " + message);
        }

        internal static void LogWarn(string message)
        {
            Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + " [WARN] " + message);
        }

        internal static void LogError(string message)
        {
            Console.Error.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + " [ERROR] " + message);
        }
    }

    internal sealed class SubscriptionStaRunner : IDisposable
    {
        private readonly AppConfig _config;
        private readonly ManualResetEvent _ready = new ManualResetEvent(false);
        private readonly ManualResetEvent _stopped = new ManualResetEvent(false);
        private Thread _thread;
        private volatile bool _stopRequested;
        private Exception _startupException;
        private Exception _runException;
        private OpcDaClient _client;
        private long _totalPushedPoints;
        private long _totalPushBatches;
        private DateTime _lastFullSnapshotAt = DateTime.MinValue;

        public SubscriptionStaRunner(AppConfig config)
        {
            _config = config;
        }

        public bool HasStopped
        {
            get { return _stopped.WaitOne(0); }
        }

        public void Start()
        {
            _thread = new Thread(ThreadMain);
            _thread.Name = "OpcDaSubscriptionSta";
            _thread.IsBackground = true;
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
            _ready.WaitOne();
            if (_startupException != null)
            {
                throw new InvalidOperationException("Subscription STA startup failed.", _startupException);
            }
        }

        public void Stop()
        {
            _stopRequested = true;
        }

        public void Wait()
        {
            _stopped.WaitOne();
            if (_runException != null)
            {
                throw new InvalidOperationException("Subscription STA failed after startup.", _runException);
            }
        }

        public void Dispose()
        {
            Stop();
            if (_thread != null && !_stopped.WaitOne(0))
            {
                _stopped.WaitOne(5000);
            }
            _ready.Close();
            _stopped.Close();
        }

        private void ThreadMain()
        {
            try
            {
                using (_client = new OpcDaClient(_config))
                {
                    DateTime registrationStart = DateTime.Now;
                    _client.Connect();
                    _client.RegisterAllItems();
                    _client.StartSubscriptions();
                    Program.Log("Ready: " + _client.GroupCount + " groups, " + _client.RegisteredItemCount + " items, " + ((int)(DateTime.Now - registrationStart).TotalMilliseconds) + "ms");
                    _ready.Set();
                    RunMessagePump();
                }
            }
            catch (Exception ex)
            {
                if (!_ready.WaitOne(0))
                {
                    _startupException = ex;
                }
                else
                {
                    _runException = ex;
                }
                _ready.Set();
            }
            finally
            {
                _stopped.Set();
            }
        }

        private void RunMessagePump()
        {
            DateTime nextPush = DateTime.Now.AddMilliseconds(_config.PushIntervalMs);
            DateTime nextReport = DateTime.Now.AddMilliseconds(_config.ReportIntervalMs);

            while (!_stopRequested)
            {
                PumpWindowsMessages();
                DateTime now = DateTime.Now;

                // 定时推送缓存数据
                if (_config.PostEnabled && now >= nextPush)
                {
                    PushCachedPoints();
                    nextPush = now.AddMilliseconds(_config.PushIntervalMs);
                }

                // 定时报告统计
                if (now >= nextReport)
                {
                    _client.LogSubscriptionStats();
                    nextReport = now.AddMilliseconds(_config.ReportIntervalMs);
                }

                Thread.Sleep(10);
            }

            Program.Log("Stopping...");
            PumpWindowsMessages();
        }

        private void PushCachedPoints()
        {
            bool fullSnapshot = ShouldUseFullSnapshot(DateTime.Now);
            List<Dictionary<string, object>> points = fullSnapshot
                ? _client.GetAllCachedPoints(_config.IncludeBadQuality, true)
                : _client.GetChangedCachedPoints(_config.IncludeBadQuality);
            if (points.Count == 0)
            {
                return;
            }

            try
            {
                if (PushClient.PostPoints(_config, points))
                {
                    _totalPushBatches++;
                    _totalPushedPoints += points.Count;
                }
                else
                {
                    _client.MarkAllCachedPointsDirty();
                    Program.LogWarn("Subscription push was not fully acknowledged; cache points were marked dirty for retry; no local durable failure queue is configured, so process crashes can lose unsent points.");
                }
            }
            catch (Exception ex)
            {
                _client.MarkAllCachedPointsDirty();
                Program.LogError("Push cached points failed: " + ex.Message);
            }
        }

        private bool ShouldUseFullSnapshot(DateTime now)
        {
            if (_config.PushMode == "full")
            {
                return true;
            }

            if (_lastFullSnapshotAt == DateTime.MinValue
                || (now - _lastFullSnapshotAt).TotalMilliseconds >= _config.FullSnapshotIntervalMs)
            {
                _lastFullSnapshotAt = now;
                return true;
            }

            return false;
        }

        private static void PumpWindowsMessages()
        {
            NativeMethods.MSG msg;
            while (NativeMethods.PeekMessage(out msg, IntPtr.Zero, 0, 0, NativeMethods.PM_REMOVE))
            {
                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }
        }
    }

    internal static class NativeMethods
    {
        public const uint PM_REMOVE = 0x0001;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        public static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG
        {
            public IntPtr Hwnd;
            public uint Message;
            public UIntPtr WParam;
            public IntPtr LParam;
            public uint Time;
            public POINT Pt;
        }
    }

    internal sealed class AppConfig
    {
        private bool _postEnabled = true;

        public string ServerProgId { get; set; }
        public string ComHost { get; set; }
        public string Channel { get; set; }
        public string ItemFormat { get; set; }
        public string CollectorUrl { get; set; }
        public string ApiKey { get; set; }
        public string Source { get; set; }
        public string ReadMode { get; set; }
        public int UpdateRateMs { get; set; }
        public int ReadIntervalMs { get; set; }
        public int PushIntervalMs { get; set; }
        public int ReportIntervalMs { get; set; }
        public string PushMode { get; set; }
        public int FullSnapshotIntervalMs { get; set; }
        public int BatchSize { get; set; }
        public int PostTimeoutMs { get; set; }
        public int MaxPostRetries { get; set; }
        public int RetryDelayMs { get; set; }
        public bool IncludeBadQuality { get; set; }
        public bool PostEnabled
        {
            get { return _postEnabled; }
            set { _postEnabled = value; }
        }
        public List<DeviceSpec> Devices { get; set; }

        public string BaseDir { get; private set; }
        public OPCDATASOURCE DataSource { get; private set; }
        public bool IsSubscriptionMode { get; private set; }
        public int TotalPointCount
        {
            get
            {
                int total = 0;
                if (Devices == null)
                {
                    return total;
                }
                for (int d = 0; d < Devices.Count; d++)
                {
                    if (Devices[d].Groups == null)
                    {
                        continue;
                    }
                    for (int g = 0; g < Devices[d].Groups.Count; g++)
                    {
                        if (Devices[d].Groups[g].Points != null)
                        {
                            total += Devices[d].Groups[g].Points.Count;
                        }
                    }
                }
                return total;
            }
        }

        public static AppConfig Load(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Config file not found", path);
            }

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            return serializer.Deserialize<AppConfig>(File.ReadAllText(path, Encoding.UTF8));
        }

        public void Normalize(string baseDir)
        {
            BaseDir = string.IsNullOrEmpty(baseDir) ? Environment.CurrentDirectory : baseDir;
            ServerProgId = Default(ServerProgId, "ZPMC.OPCServer");
            Channel = Default(Channel, "OPD_Server");
            ItemFormat = Default(ItemFormat, "{channel}.{device}.{group}.{tag}");
            Source = Default(Source, "cache");
            ReadMode = Default(ReadMode, "poll").ToLowerInvariant();
            if (ReadMode != "poll" && ReadMode != "subscription")
            {
                throw new InvalidOperationException("readMode must be 'poll' or 'subscription'.");
            }
            IsSubscriptionMode = ReadMode == "subscription";
            UpdateRateMs = UpdateRateMs > 0 ? UpdateRateMs : 1000;
            ReadIntervalMs = ReadIntervalMs > 0 ? ReadIntervalMs : 1000;
            PushIntervalMs = PushIntervalMs > 0 ? PushIntervalMs : 500;
            ReportIntervalMs = ReportIntervalMs > 0 ? ReportIntervalMs : ReadIntervalMs;
            PushMode = Default(PushMode, "changed_only").ToLowerInvariant();
            if (PushMode != "changed_only" && PushMode != "full")
            {
                throw new InvalidOperationException("pushMode must be 'changed_only' or 'full'.");
            }
            FullSnapshotIntervalMs = FullSnapshotIntervalMs > 0 ? FullSnapshotIntervalMs : 60000;
            BatchSize = BatchSize > 0 ? BatchSize : 500;
            PostTimeoutMs = PostTimeoutMs > 0 ? PostTimeoutMs : 5000;
            MaxPostRetries = MaxPostRetries > 0 ? MaxPostRetries : 2;
            RetryDelayMs = RetryDelayMs > 0 ? RetryDelayMs : 1000;
            DataSource = Source.Trim().ToLowerInvariant() == "device" ? OPCDATASOURCE.OPC_DS_DEVICE : OPCDATASOURCE.OPC_DS_CACHE;

            if (PostEnabled && string.IsNullOrWhiteSpace(CollectorUrl))
            {
                throw new InvalidOperationException("collectorUrl is required when postEnabled=true.");
            }
            if (Devices == null || Devices.Count == 0)
            {
                throw new InvalidOperationException("devices cannot be empty.");
            }

            for (int i = 0; i < Devices.Count; i++)
            {
                Devices[i].Normalize(this);
            }
        }

        private static string Default(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }

    internal sealed class DeviceSpec
    {
        public string DeviceId { get; set; }
        public string OpcDevice { get; set; }
        public List<GroupSpec> Groups { get; set; }

        public void Normalize(AppConfig config)
        {
            if (string.IsNullOrWhiteSpace(DeviceId))
            {
                throw new InvalidOperationException("deviceId is required.");
            }
            DeviceId = DeviceId.Trim().ToLowerInvariant();
            OpcDevice = string.IsNullOrWhiteSpace(OpcDevice) ? DeviceId.ToUpperInvariant() : OpcDevice.Trim();
            if (Groups == null || Groups.Count == 0)
            {
                throw new InvalidOperationException("device " + DeviceId + " must configure at least one group.");
            }
            for (int i = 0; i < Groups.Count; i++)
            {
                Groups[i].Normalize(config, DeviceId);
            }
        }
    }

    internal sealed class GroupSpec
    {
        public string Name { get; set; }
        public string Csv { get; set; }
        public int Skip { get; set; }
        public int Limit { get; set; }
        public List<PointSpec> Points { get; set; }

        public void Normalize(AppConfig config, string deviceId)
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                throw new InvalidOperationException("group name is required for device " + deviceId + ".");
            }
            Name = Name.Trim();

            List<PointSpec> merged = new List<PointSpec>();
            if (!string.IsNullOrWhiteSpace(Csv))
            {
                string csvPath = Path.IsPathRooted(Csv) ? Csv : Path.Combine(config.BaseDir, Csv);
                merged.AddRange(CsvLoader.Load(csvPath, Skip, Limit));
            }
            if (Points != null)
            {
                merged.AddRange(Points);
            }

            Points = new List<PointSpec>();
            for (int i = 0; i < merged.Count; i++)
            {
                PointSpec point = merged[i];
                if (point == null)
                {
                    continue;
                }
                point.Normalize();
                if (point.Enabled)
                {
                    Points.Add(point);
                }
            }

            if (Points.Count == 0)
            {
                throw new InvalidOperationException("group " + Name + " for device " + deviceId + " has no enabled points.");
            }
        }
    }

    internal sealed class PointSpec
    {
        public string Tag { get; set; }
        public string Address { get; set; }
        public string DataType { get; set; }
        public bool Enabled { get; set; }

        public PointSpec()
        {
            Enabled = true;
        }

        public void Normalize()
        {
            Tag = Tag == null ? "" : Tag.Trim();
            Address = Address == null ? "" : Address.Trim();
            DataType = DataType == null ? "" : DataType.Trim();
            if (string.IsNullOrWhiteSpace(Tag))
            {
                Enabled = false;
            }
        }
    }

    internal static class CsvLoader
    {
        public static List<PointSpec> Load(string path, int skip, int limit)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Point CSV file not found", path);
            }

            List<PointSpec> rows = new List<PointSpec>();
            string[] lines = File.ReadAllLines(path, Encoding.Default);
            int seen = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }
                List<string> cols = SplitCsvLine(lines[i]);
                if (cols.Count < 1)
                {
                    continue;
                }
                if (i == 0 && cols[0].Trim().Equals("tag", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                seen++;
                if (skip > 0 && seen <= skip)
                {
                    continue;
                }

                PointSpec row = new PointSpec();
                row.Tag = cols.Count > 0 ? cols[0].Trim() : "";
                row.Address = cols.Count > 1 ? cols[1].Trim() : "";
                row.DataType = cols.Count > 2 ? cols[2].Trim() : "";
                row.Enabled = true;
                rows.Add(row);
                if (limit > 0 && rows.Count >= limit)
                {
                    break;
                }
            }
            return rows;
        }

        private static List<string> SplitCsvLine(string line)
        {
            List<string> cols = new List<string>();
            StringBuilder current = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    cols.Add(current.ToString());
                    current.Length = 0;
                }
                else
                {
                    current.Append(c);
                }
            }
            cols.Add(current.ToString());
            return cols;
        }
    }

    internal sealed class OpcDaClient : IDisposable
    {
        private static readonly Guid IidOpcItemMgt = new Guid("39C13A54-011E-11D0-9675-0020AFD8ADB3");
        private static readonly Guid IidOpcDataCallback = new Guid("39C13A70-011E-11D0-9675-0020AFD8ADB3");

        private readonly AppConfig _config;
        private object _serverObject;
        private IOPCServer _server;
        private readonly List<GroupBinding> _groups = new List<GroupBinding>();
        private readonly SubscriptionStats _subscriptionStats = new SubscriptionStats();

        public OpcDaClient(AppConfig config)
        {
            _config = config;
        }

        public int GroupCount
        {
            get { return _groups.Count; }
        }

        public int RegisteredItemCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _groups.Count; i++)
                {
                    count += _groups[i].ClientHandleMap.Count;
                }
                return count;
            }
        }

        public void Connect()
        {
            Type type = string.IsNullOrWhiteSpace(_config.ComHost)
                ? Type.GetTypeFromProgID(_config.ServerProgId, true)
                : Type.GetTypeFromProgID(_config.ServerProgId, _config.ComHost, true);
            _serverObject = Activator.CreateInstance(type);
            if (_serverObject == null)
            {
                throw new InvalidOperationException("Could not create COM server: " + _config.ServerProgId);
            }
            _server = (IOPCServer)_serverObject;
            Program.Log("Connected OPC DA server.");
        }

        public void RegisterAllItems()
        {
            for (int d = 0; d < _config.Devices.Count; d++)
            {
                DeviceSpec device = _config.Devices[d];
                for (int g = 0; g < device.Groups.Count; g++)
                {
                    RegisterGroup(device, device.Groups[g]);
                }
            }
        }

        public List<Dictionary<string, object>> ReadAll()
        {
            List<Dictionary<string, object>> points = new List<Dictionary<string, object>>();
            for (int i = 0; i < _groups.Count; i++)
            {
                try
                {
                    points.AddRange(ReadGroup(_groups[i]));
                }
                catch (Exception ex)
                {
                    Program.LogError("Read group failed device=" + _groups[i].Device.DeviceId + " group=" + _groups[i].Group.Name + ": " + ex.Message);
                }
            }
            Program.Log("Read cycle collected points=" + points.Count.ToString(CultureInfo.InvariantCulture));
            return points;
        }

        public void StartSubscriptions()
        {
            if (_groups.Count == 0)
            {
                throw new InvalidOperationException("No OPC groups registered.");
            }

            int skippedCount = 0;
            for (int i = 0; i < _groups.Count; i++)
            {
                if (_groups[i].ClientHandleMap.Count == 0)
                {
                    skippedCount++;
                    continue;
                }
                SubscribeGroup(_groups[i]);
            }
            if (skippedCount > 0)
            {
                Program.Log("Skipped " + skippedCount + " empty groups (no valid items)");
            }
        }

        public void LogSubscriptionStats()
        {
            SubscriptionStatsSnapshot snapshot = _subscriptionStats.SnapshotAndResetWindow(GetCachedCount());
            Program.Log("Stats: changed=" + snapshot.Changed + " cached=" + snapshot.CachedCount + " good=" + snapshot.Good + " bad=" + snapshot.Bad + " callbacks=" + snapshot.TotalCallbacks);
        }

        public List<Dictionary<string, object>> GetAllCachedPoints(
            bool includeBadQuality, bool clearDirty = false)
        {
            List<Dictionary<string, object>> points = new List<Dictionary<string, object>>();
            DateTime now = DateTime.Now;

            for (int g = 0; g < _groups.Count; g++)
            {
                GroupBinding group = _groups[g];
                lock (group.SyncRoot)
                {
                    foreach (KeyValuePair<int, CachedPoint> entry in group.Cache)
                    {
                        CachedPoint cached = entry.Value;
                        ItemBinding item;
                        if (!group.ClientHandleMap.TryGetValue(cached.ClientHandle, out item))
                        {
                            continue;
                        }

                        if (!includeBadQuality && !cached.QualityGood)
                        {
                            continue;
                        }

                        Dictionary<string, object> point = new Dictionary<string, object>();
                        point["device_id"] = group.Device.DeviceId;
                        point["opc_device"] = group.Device.OpcDevice;
                        point["group"] = group.Group.Name;
                        point["tag"] = item.Point.Tag;
                        point["address"] = item.Point.Address;
                        point["data_type"] = item.Point.DataType;
                        point["item_id"] = item.ItemId;
                        point["value"] = ConvertJsonValue(cached.Value);
                        point["quality"] = cached.Quality;
                        point["quality_hex"] = "0x" + cached.Quality.ToString("X4", CultureInfo.InvariantCulture);
                        point["quality_good"] = cached.QualityGood;
                        point["opc_timestamp"] = cached.OpcTimestamp == DateTime.MinValue ? "" : cached.OpcTimestamp.ToString("o", CultureInfo.InvariantCulture);
                        point["timestamp"] = now.ToString("o", CultureInfo.InvariantCulture);
                        points.Add(point);
                        if (clearDirty)
                        {
                            cached.Dirty = false;
                        }
                    }
                }
            }

            return points;
        }

        public List<Dictionary<string, object>> GetChangedCachedPoints(bool includeBadQuality)
        {
            List<Dictionary<string, object>> points = new List<Dictionary<string, object>>();
            DateTime now = DateTime.Now;

            for (int g = 0; g < _groups.Count; g++)
            {
                GroupBinding group = _groups[g];
                lock (group.SyncRoot)
                {
                    foreach (KeyValuePair<int, CachedPoint> entry in group.Cache)
                    {
                        CachedPoint cached = entry.Value;
                        if (!cached.Dirty)
                        {
                            continue;
                        }

                        ItemBinding item;
                        if (!group.ClientHandleMap.TryGetValue(cached.ClientHandle, out item))
                        {
                            continue;
                        }

                        if (!includeBadQuality && !cached.QualityGood)
                        {
                            continue;
                        }

                        Dictionary<string, object> point = new Dictionary<string, object>();
                        point["device_id"] = group.Device.DeviceId;
                        point["opc_device"] = group.Device.OpcDevice;
                        point["group"] = group.Group.Name;
                        point["tag"] = item.Point.Tag;
                        point["address"] = item.Point.Address;
                        point["data_type"] = item.Point.DataType;
                        point["item_id"] = item.ItemId;
                        point["value"] = ConvertJsonValue(cached.Value);
                        point["quality"] = cached.Quality;
                        point["quality_hex"] = "0x" + cached.Quality.ToString("X4", CultureInfo.InvariantCulture);
                        point["quality_good"] = cached.QualityGood;
                        point["opc_timestamp"] = cached.OpcTimestamp == DateTime.MinValue ? "" : cached.OpcTimestamp.ToString("o", CultureInfo.InvariantCulture);
                        point["timestamp"] = now.ToString("o", CultureInfo.InvariantCulture);
                        points.Add(point);
                        // Clear only after taking the immutable payload snapshot. A callback
                        // racing with the HTTP call sets Dirty again and is retained.
                        cached.Dirty = false;
                    }
                }
            }

            return points;
        }

        public void MarkAllCachedPointsDirty()
        {
            for (int g = 0; g < _groups.Count; g++)
            {
                GroupBinding group = _groups[g];
                lock (group.SyncRoot)
                {
                    foreach (CachedPoint cached in group.Cache.Values)
                    {
                        cached.Dirty = true;
                    }
                }
            }
        }

        private void SubscribeGroup(GroupBinding group)
        {
            try
            {
                group.AsyncIO2 = (IOPCAsyncIO2)group.GroupObject;
            }
            catch (InvalidCastException ex)
            {
                throw new NotSupportedException("OPC group does not support IOPCAsyncIO2; subscription readMode cannot be used for device="
                    + group.Device.DeviceId + " group=" + group.Group.Name + ".", ex);
            }

            IConnectionPointContainer container;
            try
            {
                container = (IConnectionPointContainer)group.GroupObject;
            }
            catch (InvalidCastException ex)
            {
                throw new NotSupportedException("OPC group does not support IConnectionPointContainer; IOPCDataCallback cannot be advised for device="
                    + group.Device.DeviceId + " group=" + group.Group.Name + ".", ex);
            }

            try
            {
                Guid iid = IidOpcDataCallback;
                container.FindConnectionPoint(ref iid, out group.ConnectionPoint);
                group.CallbackSink = new OpcDataCallbackSink(group, _subscriptionStats);
                group.ConnectionPoint.Advise(group.CallbackSink, out group.ConnectionCookie);

                int hrEnable = group.AsyncIO2.SetEnable(true);
                CheckHResult(hrEnable, "IOPCAsyncIO2.SetEnable");

                int cancelId;
                int hrRefresh = group.AsyncIO2.Refresh2(_config.DataSource, group.ClientGroupHandle, out cancelId);
                CheckHResult(hrRefresh, "IOPCAsyncIO2.Refresh2");
            }
            catch (COMException ex)
            {
                throw new NotSupportedException("OPC DA IOPCDataCallback subscription failed for device="
                    + group.Device.DeviceId + " group=" + group.Group.Name
                    + " hresult=0x" + ex.ErrorCode.ToString("X8", CultureInfo.InvariantCulture) + ".", ex);
            }
        }

        private int GetCachedCount()
        {
            int count = 0;
            for (int i = 0; i < _groups.Count; i++)
            {
                lock (_groups[i].SyncRoot)
                {
                    count += _groups[i].Cache.Count;
                }
            }
            return count;
        }

        private void RegisterGroup(DeviceSpec device, GroupSpec group)
        {
            int serverGroupHandle;
            int revisedRate;
            object groupObject;
            Guid iid = IidOpcItemMgt;
            int clientGroupHandle = _groups.Count + 1;
            string groupName = "OpcDaPush_" + device.OpcDevice + "_" + group.Name + "_" + DateTime.Now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
            int hr = _server.AddGroup(
                groupName,
                true,
                _config.UpdateRateMs,
                clientGroupHandle,
                IntPtr.Zero,
                IntPtr.Zero,
                0,
                out serverGroupHandle,
                out revisedRate,
                ref iid,
                out groupObject);
            CheckHResult(hr, "IOPCServer.AddGroup");

            GroupBinding binding = new GroupBinding();
            binding.Device = device;
            binding.Group = group;
            binding.ClientGroupHandle = clientGroupHandle;
            binding.ServerGroupHandle = serverGroupHandle;
            binding.RevisedUpdateRateMs = revisedRate;
            binding.GroupObject = groupObject;
            binding.ItemMgt = (IOPCItemMgt)groupObject;
            binding.SyncIO = (IOPCSyncIO)groupObject;
            binding.Items = AddItems(binding);
            binding.ClientHandleMap = new Dictionary<int, ItemBinding>();
            _groups.Add(binding);

            int okCount = 0;
            int failedCount = 0;
            for (int i = 0; i < binding.Items.Count; i++)
            {
                if (binding.Items[i].AddSucceeded)
                {
                    binding.ClientHandleMap[binding.Items[i].ClientHandle] = binding.Items[i];
                    okCount++;
                }
                else
                {
                    failedCount++;
                }
            }
            Program.Log("Group " + device.OpcDevice + "." + group.Name + ": " + okCount + "/" + binding.Items.Count + " items" + (failedCount > 0 ? " (" + failedCount + " failed)" : ""));
            if (revisedRate > _config.UpdateRateMs + 50)
            {
                Program.LogWarn("OPC server revised update rate slower than requested device=" + device.DeviceId
                    + " group=" + group.Name
                    + " requestedMs=" + _config.UpdateRateMs.ToString(CultureInfo.InvariantCulture)
                    + " revisedMs=" + revisedRate.ToString(CultureInfo.InvariantCulture));
            }
        }

        private List<ItemBinding> AddItems(GroupBinding group)
        {
            OPCITEMDEF[] defs = new OPCITEMDEF[group.Group.Points.Count];
            for (int i = 0; i < group.Group.Points.Count; i++)
            {
                PointSpec point = group.Group.Points[i];
                defs[i] = new OPCITEMDEF();
                defs[i].szAccessPath = "";
                defs[i].szItemID = FormatItemId(_config, group.Device, group.Group, point);
                defs[i].bActive = true;
                defs[i].hClient = i + 1;
                defs[i].dwBlobSize = 0;
                defs[i].pBlob = IntPtr.Zero;
                defs[i].vtRequestedDataType = 0;
                defs[i].wReserved = 0;
            }

            IntPtr resultsPtr = IntPtr.Zero;
            IntPtr errorsPtr = IntPtr.Zero;
            int hr = group.ItemMgt.AddItems(defs.Length, defs, out resultsPtr, out errorsPtr);
            if (hr < 0)
            {
                throw new COMException("IOPCItemMgt.AddItems failed", hr);
            }

            List<ItemBinding> bindings = new List<ItemBinding>();
            try
            {
                int resultSize = Marshal.SizeOf(typeof(OPCITEMRESULT));
                for (int i = 0; i < defs.Length; i++)
                {
                    int itemError = Marshal.ReadInt32(AddPtr(errorsPtr, i * 4));
                    OPCITEMRESULT result = new OPCITEMRESULT();
                    if (resultsPtr != IntPtr.Zero)
                    {
                        result = (OPCITEMRESULT)Marshal.PtrToStructure(AddPtr(resultsPtr, i * resultSize), typeof(OPCITEMRESULT));
                    }

                    ItemBinding item = new ItemBinding();
                    item.Point = group.Group.Points[i];
                    item.ItemId = defs[i].szItemID;
                    item.ClientHandle = i + 1;
                    item.ServerHandle = result.hServer;
                    item.AddError = itemError;
                    item.AddSucceeded = itemError >= 0;
                    bindings.Add(item);

                    if (result.pBlob != IntPtr.Zero)
                    {
                        Marshal.FreeCoTaskMem(result.pBlob);
                    }
                }
            }
            finally
            {
                if (resultsPtr != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(resultsPtr);
                }
                if (errorsPtr != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(errorsPtr);
                }
            }

            return bindings;
        }

        private List<Dictionary<string, object>> ReadGroup(GroupBinding group)
        {
            List<ItemBinding> successful = new List<ItemBinding>();
            for (int i = 0; i < group.Items.Count; i++)
            {
                if (group.Items[i].AddSucceeded)
                {
                    successful.Add(group.Items[i]);
                }
            }

            List<Dictionary<string, object>> points = new List<Dictionary<string, object>>();
            if (successful.Count == 0)
            {
                return points;
            }

            int[] handles = new int[successful.Count];
            for (int i = 0; i < successful.Count; i++)
            {
                handles[i] = successful[i].ServerHandle;
            }

            IntPtr statesPtr = IntPtr.Zero;
            IntPtr errorsPtr = IntPtr.Zero;
            int hr = group.SyncIO.Read(_config.DataSource, handles.Length, handles, out statesPtr, out errorsPtr);
            if (hr < 0)
            {
                throw new COMException("IOPCSyncIO.Read failed", hr);
            }

            try
            {
                int stateSize = Marshal.SizeOf(typeof(OPCITEMSTATE));
                for (int i = 0; i < successful.Count; i++)
                {
                    int itemError = Marshal.ReadInt32(AddPtr(errorsPtr, i * 4));
                    IntPtr statePtr = AddPtr(statesPtr, i * stateSize);
                    OPCITEMSTATE state = (OPCITEMSTATE)Marshal.PtrToStructure(statePtr, typeof(OPCITEMSTATE));
                    try
                    {
                        ushort quality = (ushort)state.wQuality;
                        bool qualityGood = (quality & 0xC0) == 0xC0;
                        if (itemError >= 0 && (_config.IncludeBadQuality || qualityGood))
                        {
                            Dictionary<string, object> point = new Dictionary<string, object>();
                            point["device_id"] = group.Device.DeviceId;
                            point["opc_device"] = group.Device.OpcDevice;
                            point["group"] = group.Group.Name;
                            point["tag"] = successful[i].Point.Tag;
                            point["address"] = successful[i].Point.Address;
                            point["data_type"] = successful[i].Point.DataType;
                            point["item_id"] = successful[i].ItemId;
                            point["value"] = ConvertJsonValue(state.vDataValue);
                            point["quality"] = quality;
                            point["quality_hex"] = "0x" + quality.ToString("X4", CultureInfo.InvariantCulture);
                            point["quality_good"] = qualityGood;
                            point["opc_timestamp"] = FileTimeToIsoString(state.ftTimeStamp);
                            point["timestamp"] = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
                            points.Add(point);
                        }
                    }
                    finally
                    {
                        Marshal.DestroyStructure(statePtr, typeof(OPCITEMSTATE));
                    }
                }
            }
            finally
            {
                if (statesPtr != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(statesPtr);
                }
                if (errorsPtr != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(errorsPtr);
                }
            }
            return points;
        }

        public void Dispose()
        {
            for (int i = 0; i < _groups.Count; i++)
            {
                GroupBinding group = _groups[i];
                try
                {
                    if (group.AsyncIO2 != null)
                    {
                        group.AsyncIO2.SetEnable(false);
                    }
                }
                catch
                {
                    // Best-effort cleanup.
                }
                try
                {
                    if (group.ConnectionPoint != null && group.ConnectionCookie != 0)
                    {
                        group.ConnectionPoint.Unadvise(group.ConnectionCookie);
                        group.ConnectionCookie = 0;
                    }
                }
                catch
                {
                    // Best-effort cleanup.
                }
                try
                {
                    if (_server != null)
                    {
                        _server.RemoveGroup(group.ServerGroupHandle, true);
                    }
                }
                catch
                {
                    // Best-effort cleanup.
                }
                ReleaseComObject(group.GroupObject);
                ReleaseComObject(group.ConnectionPoint);
            }
            _groups.Clear();
            ReleaseComObject(_serverObject);
            _serverObject = null;
            _server = null;
        }

        private static string FormatItemId(AppConfig config, DeviceSpec device, GroupSpec group, PointSpec point)
        {
            return config.ItemFormat
                .Replace("{server}", config.ServerProgId)
                .Replace("{channel}", config.Channel)
                .Replace("{device}", device.OpcDevice)
                .Replace("{device_id}", device.DeviceId)
                .Replace("{opc_device}", device.OpcDevice)
                .Replace("{group}", group.Name)
                .Replace("{tag}", point.Tag)
                .Replace("{address}", point.Address);
        }

        private static object ConvertJsonValue(object value)
        {
            if (value == null)
            {
                return null;
            }
            Array arr = value as Array;
            if (arr != null)
            {
                List<object> values = new List<object>();
                foreach (object item in arr)
                {
                    values.Add(ConvertJsonValue(item));
                }
                return values;
            }
            if (value is bool || value is string || value is int || value is long || value is short || value is byte || value is double || value is float || value is decimal)
            {
                return value;
            }
            IFormattable formattable = value as IFormattable;
            if (formattable != null)
            {
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            }
            return value.ToString();
        }

        private static string FileTimeToIsoString(ComFileTime ft)
        {
            DateTime value = FileTimeToLocalDateTime(ft);
            return value == DateTime.MinValue ? "" : value.ToString("o", CultureInfo.InvariantCulture);
        }

        internal static DateTime FileTimeToLocalDateTime(ComFileTime ft)
        {
            long high = ((long)ft.dwHighDateTime) << 32;
            long low = (uint)ft.dwLowDateTime;
            long fileTime = high + low;
            if (fileTime <= 0)
            {
                return DateTime.MinValue;
            }
            try
            {
                return DateTime.FromFileTimeUtc(fileTime).ToLocalTime();
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        private static IntPtr AddPtr(IntPtr ptr, int offset)
        {
            return new IntPtr(ptr.ToInt64() + offset);
        }

        private static void CheckHResult(int hr, string operation)
        {
            if (hr < 0)
            {
                throw new COMException(operation + " failed", hr);
            }
        }

        private static void ReleaseComObject(object value)
        {
            if (value != null && Marshal.IsComObject(value))
            {
                Marshal.FinalReleaseComObject(value);
            }
        }
    }

    internal sealed class GroupBinding
    {
        public readonly object SyncRoot = new object();
        public DeviceSpec Device;
        public GroupSpec Group;
        public int ClientGroupHandle;
        public int ServerGroupHandle;
        public int RevisedUpdateRateMs;
        public object GroupObject;
        public IOPCItemMgt ItemMgt;
        public IOPCSyncIO SyncIO;
        public IOPCAsyncIO2 AsyncIO2;
        public IConnectionPoint ConnectionPoint;
        public int ConnectionCookie;
        public OpcDataCallbackSink CallbackSink;
        public List<ItemBinding> Items;
        public Dictionary<int, ItemBinding> ClientHandleMap = new Dictionary<int, ItemBinding>();
        public Dictionary<int, CachedPoint> Cache = new Dictionary<int, CachedPoint>();
    }

    internal sealed class ItemBinding
    {
        public PointSpec Point;
        public string ItemId;
        public int ClientHandle;
        public int ServerHandle;
        public int AddError;
        public bool AddSucceeded;
    }

    internal sealed class CachedPoint
    {
        public int ClientHandle;
        public object Value;
        public ushort Quality;
        public bool QualityGood;
        public DateTime OpcTimestamp;
        public DateTime ReceivedAt;
        public int Error;
        public bool Dirty;
    }

    internal sealed class SubscriptionStats
    {
        private readonly object _sync = new object();
        private long _totalCallbacks;
        private long _totalChanged;
        private long _windowCallbacks;
        private long _windowChanged;
        private long _windowGood;
        private long _windowBad;
        private long _windowErrors;
        private long _windowMaxEventAgeMs = -1;
        private DateTime _lastCallbackAt = DateTime.MinValue;

        public void RecordCallback(DateTime receivedAt, int changed, int good, int bad, int errors, long maxEventAgeMs)
        {
            lock (_sync)
            {
                _totalCallbacks++;
                _totalChanged += changed;
                _windowCallbacks++;
                _windowChanged += changed;
                _windowGood += good;
                _windowBad += bad;
                _windowErrors += errors;
                _lastCallbackAt = receivedAt;
                if (maxEventAgeMs >= 0 && maxEventAgeMs > _windowMaxEventAgeMs)
                {
                    _windowMaxEventAgeMs = maxEventAgeMs;
                }
            }
        }

        public SubscriptionStatsSnapshot SnapshotAndResetWindow(int cachedCount)
        {
            lock (_sync)
            {
                DateTime now = DateTime.Now;
                SubscriptionStatsSnapshot snapshot = new SubscriptionStatsSnapshot();
                snapshot.Callbacks = _windowCallbacks;
                snapshot.Changed = _windowChanged;
                snapshot.Good = _windowGood;
                snapshot.Bad = _windowBad;
                snapshot.Errors = _windowErrors;
                snapshot.CachedCount = cachedCount;
                snapshot.TotalCallbacks = _totalCallbacks;
                snapshot.TotalChanged = _totalChanged;
                snapshot.LastCallbackAgeMs = _lastCallbackAt == DateTime.MinValue ? -1 : Math.Max(0, (int)(now - _lastCallbackAt).TotalMilliseconds);
                snapshot.MaxEventAgeMs = _windowMaxEventAgeMs;

                _windowCallbacks = 0;
                _windowChanged = 0;
                _windowGood = 0;
                _windowBad = 0;
                _windowErrors = 0;
                _windowMaxEventAgeMs = -1;
                return snapshot;
            }
        }
    }

    internal sealed class SubscriptionStatsSnapshot
    {
        public long Callbacks;
        public long Changed;
        public long Good;
        public long Bad;
        public long Errors;
        public int CachedCount;
        public long TotalCallbacks;
        public long TotalChanged;
        public int LastCallbackAgeMs;
        public long MaxEventAgeMs;
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class OpcDataCallbackSink : IOPCDataCallback
    {
        private readonly GroupBinding _group;
        private readonly SubscriptionStats _stats;

        internal OpcDataCallbackSink(GroupBinding group, SubscriptionStats stats)
        {
            _group = group;
            _stats = stats;
        }

        public int OnDataChange(
            int dwTransid,
            int hGroup,
            int hrMasterquality,
            int hrMastererror,
            int dwCount,
            int[] phClientItems,
            object[] pvValues,
            short[] pwQualities,
            ComFileTime[] pftTimeStamps,
            int[] pErrors)
        {
            return ProcessValues(dwCount, phClientItems, pvValues, pwQualities, pftTimeStamps, pErrors);
        }

        public int OnReadComplete(
            int dwTransid,
            int hGroup,
            int hrMasterquality,
            int hrMastererror,
            int dwCount,
            int[] phClientItems,
            object[] pvValues,
            short[] pwQualities,
            ComFileTime[] pftTimeStamps,
            int[] pErrors)
        {
            return ProcessValues(dwCount, phClientItems, pvValues, pwQualities, pftTimeStamps, pErrors);
        }

        public int OnWriteComplete(int dwTransid, int hGroup, int hrMastererr, int dwCount, int[] pClienthandles, int[] pErrors)
        {
            return 0;
        }

        public int OnCancelComplete(int dwTransid, int hGroup)
        {
            return 0;
        }

        private int ProcessValues(
            int count,
            int[] clientHandles,
            object[] values,
            short[] qualities,
            ComFileTime[] timestamps,
            int[] errors)
        {
            DateTime now = DateTime.Now;
            int changed = 0;
            int good = 0;
            int bad = 0;
            int failed = 0;
            long maxEventAgeMs = -1;

            lock (_group.SyncRoot)
            {
                for (int i = 0; i < count; i++)
                {
                    int clientHandle = GetInt(clientHandles, i, 0);
                    ItemBinding item;
                    if (!_group.ClientHandleMap.TryGetValue(clientHandle, out item))
                    {
                        failed++;
                        continue;
                    }

                    int itemError = GetInt(errors, i, 0);
                    if (itemError < 0)
                    {
                        failed++;
                        continue;
                    }

                    ushort quality = (ushort)GetShort(qualities, i, 0);
                    bool qualityGood = (quality & 0xC0) == 0xC0;
                    if (qualityGood)
                    {
                        good++;
                    }
                    else
                    {
                        bad++;
                    }

                    DateTime opcTimestamp = DateTime.MinValue;
                    if (timestamps != null && i < timestamps.Length)
                    {
                        opcTimestamp = OpcDaClient.FileTimeToLocalDateTime(timestamps[i]);
                        if (opcTimestamp != DateTime.MinValue)
                        {
                            long eventAgeMs = Math.Max(0, (long)(now - opcTimestamp).TotalMilliseconds);
                            if (eventAgeMs > maxEventAgeMs)
                            {
                                maxEventAgeMs = eventAgeMs;
                            }
                        }
                    }

                    CachedPoint cached;
                    if (!_group.Cache.TryGetValue(clientHandle, out cached))
                    {
                        cached = new CachedPoint();
                        cached.ClientHandle = clientHandle;
                        _group.Cache[clientHandle] = cached;
                    }
                    cached.Value = values != null && i < values.Length ? values[i] : null;
                    cached.Quality = quality;
                    cached.QualityGood = qualityGood;
                    cached.OpcTimestamp = opcTimestamp;
                    cached.ReceivedAt = now;
                    cached.Error = itemError;
                    cached.Dirty = true;
                    changed++;
                }
            }

            _stats.RecordCallback(now, changed, good, bad, failed, maxEventAgeMs);
            return 0;
        }

        private static int GetInt(int[] values, int index, int fallback)
        {
            return values != null && index < values.Length ? values[index] : fallback;
        }

        private static short GetShort(short[] values, int index, short fallback)
        {
            return values != null && index < values.Length ? values[index] : fallback;
        }
    }

    internal static class PushClient
    {
        public static bool PostPoints(AppConfig config, List<Dictionary<string, object>> points)
        {
            int sent = 0;
            while (sent < points.Count && !Program.IsStopping)
            {
                int take = Math.Min(config.BatchSize, points.Count - sent);
                List<Dictionary<string, object>> batch = points.GetRange(sent, take);
                string batchId = Guid.NewGuid().ToString("N");
                if (!PostBatchWithRetry(config, batch, batchId))
                {
                    return false; // 停止或失败，退出；失败点不再伪装成已发送
                }
                sent += take;
            }
            return sent == points.Count;
        }

        private static bool PostBatchWithRetry(
            AppConfig config,
            List<Dictionary<string, object>> batch,
            string batchId)
        {
            List<Dictionary<string, object>> pending = new List<Dictionary<string, object>>(batch);
            int attempt = 0;
            while (pending.Count > 0 && !Program.IsStopping)
            {
                try
                {
                    List<int> failedIndexes = PostBatch(config, pending, batchId);
                    if (failedIndexes.Count == 0)
                    {
                        return true;
                    }

                    List<Dictionary<string, object>> failed = new List<Dictionary<string, object>>();
                    for (int i = 0; i < failedIndexes.Count; i++)
                    {
                        failed.Add(pending[failedIndexes[i]]);
                    }
                    pending = failed;
                    Program.LogWarn(
                        "Push batch " + batchId + " partially rejected; retrying " +
                        pending.Count.ToString(CultureInfo.InvariantCulture) + " point(s).");
                    if (attempt >= config.MaxPostRetries)
                    {
                        Program.LogError(
                            "Push batch " + batchId + " exhausted retries with " +
                            pending.Count.ToString(CultureInfo.InvariantCulture) +
                            " point(s) remaining.");
                        return false;
                    }
                    attempt++;
                    SleepBeforeRetry(config, attempt);
                }
                catch (Exception ex)
                {
                    if (attempt >= config.MaxPostRetries || Program.IsStopping)
                    {
                        Program.LogError(
                            "Push batch " + batchId + " failed with " +
                            pending.Count.ToString(CultureInfo.InvariantCulture) +
                            " point(s) remaining, error=" + ex.Message);
                        return false;
                    }
                    attempt++;
                    SleepBeforeRetry(config, attempt);
                }
            }
            return false;
        }

        private static void SleepBeforeRetry(AppConfig config, int attempt)
        {
            for (int i = 0; i < config.RetryDelayMs * attempt && !Program.IsStopping; i += 100)
            {
                Thread.Sleep(100);
            }
        }

        private static List<int> PostBatch(
            AppConfig config,
            List<Dictionary<string, object>> batch,
            string batchId)
        {
            Dictionary<string, object> payload = new Dictionary<string, object>();
            payload["source"] = "opcda_sidecar";
            payload["schema"] = "opcda_tag_batch_v1";
            payload["batch_id"] = batchId;
            payload["server"] = config.ServerProgId;
            payload["com_host"] = string.IsNullOrWhiteSpace(config.ComHost) ? Environment.MachineName : config.ComHost;
            payload["channel"] = config.Channel;
            payload["sent_at"] = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
            payload["points"] = batch;

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            byte[] body = Encoding.UTF8.GetBytes(serializer.Serialize(payload));

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(config.CollectorUrl);
            request.Method = "POST";
            request.ContentType = "application/json; charset=utf-8";
            request.Timeout = config.PostTimeoutMs;
            request.ReadWriteTimeout = config.PostTimeoutMs;
            if (!string.IsNullOrWhiteSpace(config.ApiKey))
            {
                request.Headers["X-API-Key"] = config.ApiKey;
            }
            request.ContentLength = body.Length;
            using (Stream stream = request.GetRequestStream())
            {
                stream.Write(body, 0, body.Length);
            }

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream responseStream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(responseStream, Encoding.UTF8))
            {
                string responseBody = reader.ReadToEnd();
                int status = (int)response.StatusCode;
                if (status < 200 || status >= 300)
                {
                    throw new InvalidOperationException(
                        "HTTP status " + status.ToString(CultureInfo.InvariantCulture) +
                        ", response: " + (responseBody.Length > 200 ? responseBody.Substring(0, 200) : responseBody));
                }
                return ParseResponse(responseBody, batch.Count, batchId);
            }
        }

        private static List<int> ParseResponse(string responseBody, int batchCount, string batchId)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            object decoded = serializer.DeserializeObject(responseBody);
            Dictionary<string, object> result = decoded as Dictionary<string, object>;
            if (result == null)
            {
                throw new InvalidOperationException("Collector response is not a JSON object.");
            }

            object responseBatchId;
            if (result.TryGetValue("batch_id", out responseBatchId) &&
                !string.Equals(
                    Convert.ToString(responseBatchId, CultureInfo.InvariantCulture),
                    batchId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Collector response batch_id does not match request.");
            }

            int accepted = ReadNonNegativeCount(result, "accepted");
            int rejected = ReadNonNegativeCount(result, "rejected");
            List<string> errors = ReadErrors(result);
            List<int> failedIndexes = ReadRejectedIndexes(result, batchCount);

            if (accepted == batchCount && rejected == 0 && errors.Count == 0 && failedIndexes.Count == 0)
            {
                return failedIndexes;
            }

            if (failedIndexes.Count == 0)
            {
                // 老版本 collector 或不完整响应没有逐点索引时，保守地重试当前剩余批次，
                // 绝不能把 rejected/errors 当作整体成功。
                for (int i = 0; i < batchCount; i++)
                {
                    failedIndexes.Add(i);
                }
            }
            Program.LogWarn(
                "Collector batch " + batchId + " response accepted=" +
                accepted.ToString(CultureInfo.InvariantCulture) + " rejected=" +
                rejected.ToString(CultureInfo.InvariantCulture) + " errors=" +
                errors.Count.ToString(CultureInfo.InvariantCulture));
            return failedIndexes;
        }

        private static int ReadNonNegativeCount(Dictionary<string, object> result, string key)
        {
            object raw;
            if (!result.TryGetValue(key, out raw) || raw == null)
            {
                return 0;
            }
            int value;
            try
            {
                value = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Collector response field " + key + " is invalid.", ex);
            }
            if (value < 0)
            {
                throw new InvalidOperationException("Collector response field " + key + " is negative.");
            }
            return value;
        }

        private static List<string> ReadErrors(Dictionary<string, object> result)
        {
            List<string> errors = new List<string>();
            object raw;
            if (!result.TryGetValue("errors", out raw) || raw == null)
            {
                return errors;
            }
            object[] values = raw as object[];
            if (values != null)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    errors.Add(Convert.ToString(values[i], CultureInfo.InvariantCulture));
                }
                return errors;
            }
            errors.Add(Convert.ToString(raw, CultureInfo.InvariantCulture));
            return errors;
        }

        private static List<int> ReadRejectedIndexes(Dictionary<string, object> result, int batchCount)
        {
            List<int> indexes = new List<int>();
            object raw;
            if (!result.TryGetValue("rejected_indices", out raw) || raw == null)
            {
                return indexes;
            }
            object[] values = raw as object[];
            if (values == null)
            {
                throw new InvalidOperationException("Collector response rejected_indices is invalid.");
            }
            for (int i = 0; i < values.Length; i++)
            {
                int index;
                try
                {
                    index = Convert.ToInt32(values[i], CultureInfo.InvariantCulture);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Collector response rejected index is invalid.", ex);
                }
                if (index < 0 || index >= batchCount || indexes.Contains(index))
                {
                    throw new InvalidOperationException("Collector response rejected index is out of range.");
                }
                indexes.Add(index);
            }
            return indexes;
        }
    }

    internal enum OPCDATASOURCE
    {
        OPC_DS_CACHE = 1,
        OPC_DS_DEVICE = 2
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct OPCITEMDEF
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string szAccessPath;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string szItemID;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bActive;
        public int hClient;
        public int dwBlobSize;
        public IntPtr pBlob;
        public short vtRequestedDataType;
        public short wReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct OPCITEMRESULT
    {
        public int hServer;
        public short vtCanonicalDataType;
        public short wReserved;
        public int dwAccessRights;
        public int dwBlobSize;
        public IntPtr pBlob;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct OPCITEMSTATE
    {
        public int hClient;
        public ComFileTime ftTimeStamp;
        public short wQuality;
        public short wReserved;
        [MarshalAs(UnmanagedType.Struct)]
        public object vDataValue;
    }

    [ComImport]
    [Guid("39C13A4D-011E-11D0-9675-0020AFD8ADB3")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IOPCServer
    {
        [PreserveSig]
        int AddGroup(
            [MarshalAs(UnmanagedType.LPWStr)] string szName,
            [MarshalAs(UnmanagedType.Bool)] bool bActive,
            int dwRequestedUpdateRate,
            int hClientGroup,
            IntPtr pTimeBias,
            IntPtr pPercentDeadband,
            int dwLCID,
            out int phServerGroup,
            out int pRevisedUpdateRate,
            ref Guid riid,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppUnk);

        [PreserveSig]
        int GetErrorString(int dwError, int dwLocale, [MarshalAs(UnmanagedType.LPWStr)] out string ppString);

        [PreserveSig]
        int GetGroupByName(
            [MarshalAs(UnmanagedType.LPWStr)] string szName,
            ref Guid riid,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppUnk);

        [PreserveSig]
        int GetStatus(out IntPtr ppServerStatus);

        [PreserveSig]
        int RemoveGroup(int hServerGroup, [MarshalAs(UnmanagedType.Bool)] bool bForce);

        [PreserveSig]
        int CreateGroupEnumerator(int dwScope, ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppUnk);
    }

    [ComImport]
    [Guid("39C13A54-011E-11D0-9675-0020AFD8ADB3")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IOPCItemMgt
    {
        [PreserveSig]
        int AddItems(
            int dwCount,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] OPCITEMDEF[] pItemArray,
            out IntPtr ppAddResults,
            out IntPtr ppErrors);

        [PreserveSig]
        int ValidateItems(
            int dwCount,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] OPCITEMDEF[] pItemArray,
            [MarshalAs(UnmanagedType.Bool)] bool bBlobUpdate,
            out IntPtr ppValidationResults,
            out IntPtr ppErrors);

        [PreserveSig]
        int RemoveItems(
            int dwCount,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] int[] phServer,
            out IntPtr ppErrors);

        [PreserveSig]
        int SetActiveState(
            int dwCount,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] int[] phServer,
            [MarshalAs(UnmanagedType.Bool)] bool bActive,
            out IntPtr ppErrors);

        [PreserveSig]
        int SetClientHandles(
            int dwCount,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] int[] phServer,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] int[] phClient,
            out IntPtr ppErrors);

        [PreserveSig]
        int SetDatatypes(
            int dwCount,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] int[] phServer,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] short[] pRequestedDatatypes,
            out IntPtr ppErrors);

        [PreserveSig]
        int CreateEnumerator(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppUnk);
    }

    [ComImport]
    [Guid("39C13A52-011E-11D0-9675-0020AFD8ADB3")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IOPCSyncIO
    {
        [PreserveSig]
        int Read(
            OPCDATASOURCE dwSource,
            int dwCount,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] int[] phServer,
            out IntPtr ppItemValues,
            out IntPtr ppErrors);

        [PreserveSig]
        int Write(
            int dwCount,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] int[] phServer,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] object[] pItemValues,
            out IntPtr ppErrors);
    }

    [ComImport]
    [Guid("39C13A71-011E-11D0-9675-0020AFD8ADB3")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IOPCAsyncIO2
    {
        [PreserveSig]
        int Read(
            int dwCount,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] int[] phServer,
            int dwTransactionID,
            out int pdwCancelID,
            out IntPtr ppErrors);

        [PreserveSig]
        int Write(
            int dwCount,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] int[] phServer,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] object[] pItemValues,
            int dwTransactionID,
            out int pdwCancelID,
            out IntPtr ppErrors);

        [PreserveSig]
        int Refresh2(OPCDATASOURCE dwSource, int dwTransactionID, out int pdwCancelID);

        [PreserveSig]
        int Cancel2(int dwCancelID);

        [PreserveSig]
        int SetEnable([MarshalAs(UnmanagedType.Bool)] bool bEnable);

        [PreserveSig]
        int GetEnable([MarshalAs(UnmanagedType.Bool)] out bool pbEnable);
    }

    [ComVisible(true)]
    [Guid("39C13A70-011E-11D0-9675-0020AFD8ADB3")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IOPCDataCallback
    {
        [PreserveSig]
        int OnDataChange(
            int dwTransid,
            int hGroup,
            int hrMasterquality,
            int hrMastererror,
            int dwCount,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] int[] phClientItems,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] object[] pvValues,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] short[] pwQualities,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] ComFileTime[] pftTimeStamps,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] int[] pErrors);

        [PreserveSig]
        int OnReadComplete(
            int dwTransid,
            int hGroup,
            int hrMasterquality,
            int hrMastererror,
            int dwCount,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] int[] phClientItems,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] object[] pvValues,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] short[] pwQualities,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] ComFileTime[] pftTimeStamps,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] int[] pErrors);

        [PreserveSig]
        int OnWriteComplete(
            int dwTransid,
            int hGroup,
            int hrMastererr,
            int dwCount,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] int[] pClienthandles,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] int[] pErrors);

        [PreserveSig]
        int OnCancelComplete(int dwTransid, int hGroup);
    }
}
