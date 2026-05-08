using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Threading.Tasks;
using ComFileTime = System.Runtime.InteropServices.ComTypes.FILETIME;

namespace OpcDaProxyService
{
    /// <summary>
    /// OPC DA 连接管理器
    /// 管理远程 OPC DA 服务器的连接和读取操作。
    /// 复用 OPC DA COM 操作代码，支持远程 DCOM 连接。
    /// </summary>
    internal class OpcConnectionManager : IDisposable
    {
        private readonly ProxyConfig _config;
        private readonly Dictionary<string, OpcServerConnection> _connections = new Dictionary<string, OpcServerConnection>();
        private readonly object _lock = new object();

        public OpcConnectionManager(ProxyConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// 连接到 OPC DA 服务器
        /// </summary>
        public bool Connect(string opcServer, string vmHost, int updateRateMs)
        {
            string key = GetConnectionKey(opcServer, vmHost);

            lock (_lock)
            {
                if (_connections.ContainsKey(key))
                {
                    return true; // Already connected
                }

                try
                {
                    OpcServerConnection connection = new OpcServerConnection(_config, opcServer, vmHost, updateRateMs);
                    connection.Connect();
                    _connections[key] = connection;
                    Program.Log("Connected to OPC server: " + opcServer + "@" + (vmHost ?? "localhost"));
                    return true;
                }
                catch (Exception ex)
                {
                    Program.LogError("Failed to connect to OPC server: " + opcServer + "@" + (vmHost ?? "localhost") + " - " + ex.Message);
                    return false;
                }
            }
        }

        /// <summary>
        /// 断开所有连接
        /// </summary>
        public void Disconnect()
        {
            lock (_lock)
            {
                foreach (var connection in _connections.Values)
                {
                    try
                    {
                        connection.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Program.LogError("Error disconnecting: " + ex.Message);
                    }
                }
                _connections.Clear();
            }
        }

        /// <summary>
        /// 读取点位数据
        /// </summary>
        public List<Dictionary<string, object>> ReadItems(List<string> itemIds, string opcServer, string vmHost)
        {
            // 如果指定了服务器但未连接，先连接
            if (!string.IsNullOrEmpty(opcServer))
            {
                string key = GetConnectionKey(opcServer, vmHost);
                lock (_lock)
                {
                    if (!_connections.ContainsKey(key))
                    {
                        Connect(opcServer, vmHost, _config.UpdateRateMs);
                    }
                }
            }

            // 找到可用的连接
            OpcServerConnection connection = null;
            lock (_lock)
            {
                foreach (var conn in _connections.Values)
                {
                    connection = conn;
                    break;
                }
            }

            if (connection == null)
            {
                Program.LogError("No OPC connection available");
                return new List<Dictionary<string, object>>();
            }

            return connection.ReadItems(itemIds);
        }

        private static string GetConnectionKey(string opcServer, string vmHost)
        {
            return (opcServer ?? "") + "@" + (vmHost ?? "localhost");
        }

        public void Dispose()
        {
            Disconnect();
        }
    }

    /// <summary>
    /// 单个 OPC DA 服务器连接
    /// </summary>
    internal class OpcServerConnection : IDisposable
    {
        private readonly ProxyConfig _config;
        private readonly string _opcServer;
        private readonly string _vmHost;
        private readonly int _updateRateMs;
        private object _serverObject;
        private IOPCServer _server;
        private readonly List<GroupContext> _groups = new List<GroupContext>();
        private int _groupCounter;

        public OpcServerConnection(ProxyConfig config, string opcServer, string vmHost, int updateRateMs)
        {
            _config = config;
            _opcServer = opcServer;
            _vmHost = vmHost;
            _updateRateMs = updateRateMs;
        }

        public void Connect()
        {
            int timeoutMs = _config.ConnectionTimeoutMs;
            if (timeoutMs <= 0)
            {
                timeoutMs = 10000; // 默认 10 秒
            }

            // 使用 Task 实现超时控制
            CancellationTokenSource cts = new CancellationTokenSource(timeoutMs);
            Task connectTask = Task.Run(() => ConnectInternal(), cts.Token);

            try
            {
                // 等待连接完成或超时
                connectTask.Wait(timeoutMs);

                if (connectTask.IsFaulted)
                {
                    throw connectTask.Exception != null && connectTask.Exception.InnerException != null
                        ? connectTask.Exception.InnerException
                        : new Exception("Connection failed");
                }

                if (!connectTask.IsCompleted)
                {
                    throw new TimeoutException("OPC DA connection timeout after " + timeoutMs + "ms");
                }
            }
            catch (AggregateException ex)
            {
                // 解包聚合异常
                throw ex.InnerException ?? ex;
            }
        }

        private void ConnectInternal()
        {
            Type serverType;

            if (string.IsNullOrWhiteSpace(_vmHost))
            {
                // 本地连接
                serverType = Type.GetTypeFromProgID(_opcServer, true);
            }
            else
            {
                // 远程 DCOM 连接
                serverType = Type.GetTypeFromProgID(_opcServer, _vmHost, true);
            }

            _serverObject = Activator.CreateInstance(serverType);
            if (_serverObject == null)
            {
                throw new InvalidOperationException("Could not create COM server: " + _opcServer);
            }

            _server = (IOPCServer)_serverObject;
            Program.Log("OPC DA server connected: " + _opcServer);
        }

        public List<Dictionary<string, object>> ReadItems(List<string> itemIds)
        {
            List<Dictionary<string, object>> results = new List<Dictionary<string, object>>();

            if (_server == null)
            {
                Program.LogError("Server not connected");
                return results;
            }

            // 创建临时组读取数据
            GroupContext group = CreateTempGroup(itemIds);
            if (group == null)
            {
                return results;
            }

            try
            {
                DateTime now = DateTime.Now;
                List<ItemBinding> readableBindings = new List<ItemBinding>();
                for (int i = 0; i < group.ItemBindings.Count; i++)
                {
                    if (group.ItemBindings[i].AddSucceeded)
                    {
                        readableBindings.Add(group.ItemBindings[i]);
                    }
                    else
                    {
                        Dictionary<string, object> failedResult = new Dictionary<string, object>();
                        failedResult["item_id"] = group.ItemBindings[i].ItemId;
                        failedResult["value"] = null;
                        failedResult["quality"] = 0;
                        failedResult["timestamp"] = now.ToString("o", CultureInfo.InvariantCulture);
                        failedResult["error"] = "AddItems failed: 0x" + group.ItemBindings[i].AddError.ToString("X8");
                        results.Add(failedResult);
                    }
                }

                if (readableBindings.Count == 0)
                {
                    return results;
                }

                // 同步读取
                int[] serverHandles = new int[readableBindings.Count];
                for (int i = 0; i < readableBindings.Count; i++)
                {
                    serverHandles[i] = readableBindings[i].ServerHandle;
                }

                IntPtr statesPtr = IntPtr.Zero;
                IntPtr errorsPtr = IntPtr.Zero;

                try
                {
                    int hr = group.SyncIO.Read(OPCDATASOURCE.OPC_DS_CACHE, serverHandles.Length, serverHandles, out statesPtr, out errorsPtr);
                    if (hr < 0)
                    {
                        throw new COMException("IOPCSyncIO.Read failed", hr);
                    }

                    int stateSize = Marshal.SizeOf(typeof(OPCITEMSTATE));
                    for (int i = 0; i < readableBindings.Count; i++)
                    {
                        int itemError = Marshal.ReadInt32(AddPtr(errorsPtr, i * 4));

                        Dictionary<string, object> result = new Dictionary<string, object>();
                        result["item_id"] = readableBindings[i].ItemId;

                        if (itemError >= 0)
                        {
                            IntPtr statePtr = AddPtr(statesPtr, i * stateSize);
                            OPCITEMSTATE state = (OPCITEMSTATE)Marshal.PtrToStructure(statePtr, typeof(OPCITEMSTATE));

                            ushort quality = (ushort)state.wQuality;
                            result["value"] = ConvertJsonValue(state.vDataValue);
                            result["quality"] = quality;
                            result["timestamp"] = FileTimeToIsoString(state.ftTimeStamp, now);

                            Marshal.DestroyStructure(statePtr, typeof(OPCITEMSTATE));
                        }
                        else
                        {
                            result["value"] = null;
                            result["quality"] = 0;
                            result["timestamp"] = now.ToString("o", CultureInfo.InvariantCulture);
                        }

                        results.Add(result);
                    }
                }
                finally
                {
                    if (statesPtr != IntPtr.Zero) Marshal.FreeCoTaskMem(statesPtr);
                    if (errorsPtr != IntPtr.Zero) Marshal.FreeCoTaskMem(errorsPtr);
                }
            }
            finally
            {
                RemoveGroup(group);
            }

            return results;
        }

        private GroupContext CreateTempGroup(List<string> itemIds)
        {
            _groupCounter++;
            string groupName = "ProxyTemp_" + _groupCounter + "_" + DateTime.Now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);

            int serverGroupHandle;
            int revisedRate;
            object groupObject;
            Guid iid = new Guid("39C13A54-011E-11D0-9675-0020AFD8ADB3"); // IID_IOPCItemMgt

            int hr = _server.AddGroup(
                groupName,
                true,
                _updateRateMs,
                _groupCounter,
                IntPtr.Zero,
                IntPtr.Zero,
                0,
                out serverGroupHandle,
                out revisedRate,
                ref iid,
                out groupObject);

            if (hr < 0)
            {
                Program.LogError("AddGroup failed: 0x" + hr.ToString("X8"));
                return null;
            }

            GroupContext group = new GroupContext();
            group.ServerGroupHandle = serverGroupHandle;
            group.GroupObject = groupObject;
            group.ItemMgt = (IOPCItemMgt)groupObject;
            group.SyncIO = (IOPCSyncIO)groupObject;

            // 添加 items
            OPCITEMDEF[] defs = new OPCITEMDEF[itemIds.Count];
            for (int i = 0; i < itemIds.Count; i++)
            {
                defs[i] = new OPCITEMDEF();
                defs[i].szAccessPath = "";
                defs[i].szItemID = itemIds[i];
                defs[i].bActive = true;
                defs[i].hClient = i + 1;
                defs[i].dwBlobSize = 0;
                defs[i].pBlob = IntPtr.Zero;
                defs[i].vtRequestedDataType = 0;
                defs[i].wReserved = 0;
            }

            IntPtr resultsPtr = IntPtr.Zero;
            IntPtr errorsPtr = IntPtr.Zero;

            try
            {
                hr = group.ItemMgt.AddItems(defs.Length, defs, out resultsPtr, out errorsPtr);
                if (hr < 0)
                {
                    Program.LogError("AddItems failed: 0x" + hr.ToString("X8"));
                    _server.RemoveGroup(serverGroupHandle, true);
                    return null;
                }

                int resultSize = Marshal.SizeOf(typeof(OPCITEMRESULT));
                for (int i = 0; i < defs.Length; i++)
                {
                    int itemError = Marshal.ReadInt32(AddPtr(errorsPtr, i * 4));
                    OPCITEMRESULT result = (OPCITEMRESULT)Marshal.PtrToStructure(AddPtr(resultsPtr, i * resultSize), typeof(OPCITEMRESULT));

                    ItemBinding binding = new ItemBinding();
                    binding.ItemId = itemIds[i];
                    binding.ServerHandle = result.hServer;
                    binding.AddError = itemError;
                    binding.AddSucceeded = itemError >= 0;
                    group.ItemBindings.Add(binding);

                    if (result.pBlob != IntPtr.Zero)
                    {
                        Marshal.FreeCoTaskMem(result.pBlob);
                    }
                }
            }
            finally
            {
                if (resultsPtr != IntPtr.Zero) Marshal.FreeCoTaskMem(resultsPtr);
                if (errorsPtr != IntPtr.Zero) Marshal.FreeCoTaskMem(errorsPtr);
            }

            _groups.Add(group);
            return group;
        }

        private void RemoveGroup(GroupContext group)
        {
            try
            {
                if (_server != null)
                {
                    _server.RemoveGroup(group.ServerGroupHandle, true);
                }
            }
            catch { }

            _groups.Remove(group);
            ReleaseComObject(group.GroupObject);
        }

        private static object ConvertJsonValue(object value)
        {
            if (value == null) return null;

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

            if (value is bool || value is string || value is int || value is long ||
                value is short || value is byte || value is double || value is float || value is decimal)
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

        private static string FileTimeToIsoString(ComFileTime ft, DateTime fallback)
        {
            long high = ((long)ft.dwHighDateTime) << 32;
            long low = (uint)ft.dwLowDateTime;
            long fileTime = high + low;

            if (fileTime <= 0)
            {
                return fallback.ToString("o", CultureInfo.InvariantCulture);
            }

            try
            {
                return DateTime.FromFileTimeUtc(fileTime).ToLocalTime().ToString("o", CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback.ToString("o", CultureInfo.InvariantCulture);
            }
        }

        private static IntPtr AddPtr(IntPtr ptr, int offset)
        {
            return new IntPtr(ptr.ToInt64() + offset);
        }

        private static void ReleaseComObject(object value)
        {
            if (value != null && Marshal.IsComObject(value))
            {
                Marshal.FinalReleaseComObject(value);
            }
        }

        public void Dispose()
        {
            foreach (var group in _groups)
            {
                try
                {
                    if (_server != null)
                    {
                        _server.RemoveGroup(group.ServerGroupHandle, true);
                    }
                }
                catch { }
                ReleaseComObject(group.GroupObject);
            }
            _groups.Clear();

            ReleaseComObject(_serverObject);
            _serverObject = null;
            _server = null;
        }
    }

    internal class GroupContext
    {
        public int ServerGroupHandle;
        public object GroupObject;
        public IOPCItemMgt ItemMgt;
        public IOPCSyncIO SyncIO;
        public List<ItemBinding> ItemBindings = new List<ItemBinding>();
    }

    internal class ItemBinding
    {
        public string ItemId;
        public int ServerHandle;
        public int AddError;
        public bool AddSucceeded;
    }

    #region OPC DA COM Interfaces

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

    #endregion
}
