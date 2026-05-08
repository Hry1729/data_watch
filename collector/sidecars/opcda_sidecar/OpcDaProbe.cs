using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using ComFileTime = System.Runtime.InteropServices.ComTypes.FILETIME;

namespace OpcDaSidecar
{
    internal static class Program
    {
        private static readonly Guid IidOpcItemMgt = new Guid("39C13A54-011E-11D0-9675-0020AFD8ADB3");

        [STAThread]
        private static int Main(string[] args)
        {
            Options options = Options.Parse(args);
            if (options.ShowHelp)
            {
                Options.PrintHelp();
                return 0;
            }

            try
            {
                List<TagRow> rows = LoadCsv(options.CsvPath, options.Skip, options.Limit);
                if (rows.Count == 0)
                {
                    Console.Error.WriteLine("No rows loaded from CSV: " + options.CsvPath);
                    return 2;
                }

                Console.WriteLine("OPC DA probe starting");
                Console.WriteLine("  server : " + options.ServerProgId);
                Console.WriteLine("  channel: " + options.Channel);
                Console.WriteLine("  device : " + options.Device);
                Console.WriteLine("  group  : " + options.Group);
                Console.WriteLine("  csv    : " + options.CsvPath);
                Console.WriteLine("  rows   : " + rows.Count.ToString(CultureInfo.InvariantCulture));

                object serverObject = CreateComServer(options.ServerProgId);
                IOPCServer server = (IOPCServer)serverObject;
                int serverGroupHandle;
                object groupObject = AddGroup(server, options, out serverGroupHandle);

                try
                {
                    IOPCItemMgt itemMgt = (IOPCItemMgt)groupObject;
                    IOPCSyncIO syncIO = (IOPCSyncIO)groupObject;

                    string selectedFormat = options.ItemFormat;
                    if (string.IsNullOrEmpty(selectedFormat))
                    {
                        selectedFormat = DetectBestFormat(itemMgt, rows, options);
                    }

                    if (string.IsNullOrEmpty(selectedFormat))
                    {
                        Console.Error.WriteLine("Could not find a valid OPC ItemID format.");
                        return 3;
                    }

                    Console.WriteLine("Selected ItemID format: " + selectedFormat);
                    List<ItemBinding> bindings = AddItems(itemMgt, rows, options, selectedFormat, true);
                    int successCount = 0;
                    for (int i = 0; i < bindings.Count; i++)
                    {
                        if (bindings[i].AddSucceeded)
                        {
                            successCount++;
                        }
                    }

                    Console.WriteLine("AddItems success: " + successCount + "/" + bindings.Count);
                    if (successCount == 0)
                    {
                        return 4;
                    }

                    if (options.ReadDelayMs > 0)
                    {
                        Console.WriteLine("Waiting before read: " + options.ReadDelayMs + " ms");
                        System.Threading.Thread.Sleep(options.ReadDelayMs);
                    }

                    int readCount = ReadItems(syncIO, bindings, options);
                    Console.WriteLine("Read success: " + readCount + "/" + successCount);
                    return readCount > 0 ? 0 : 5;
                }
                finally
                {
                    try
                    {
                        server.RemoveGroup(serverGroupHandle, true);
                    }
                    catch
                    {
                        // Best-effort cleanup only.
                    }
                    ReleaseComObject(groupObject);
                    ReleaseComObject(serverObject);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("OPC DA probe failed: " + ex.GetType().FullName + ": " + ex.Message);
                Console.Error.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        private static object CreateComServer(string progId)
        {
            Type type = Type.GetTypeFromProgID(progId, true);
            object instance = Activator.CreateInstance(type);
            if (instance == null)
            {
                throw new InvalidOperationException("Could not create COM server: " + progId);
            }
            return instance;
        }

        private static object AddGroup(IOPCServer server, Options options, out int serverGroupHandle)
        {
            int revisedRate;
            object groupObject;
            Guid iid = IidOpcItemMgt;
            string groupName = "CodexOpcDaProbe_" + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            int hr = server.AddGroup(
                groupName,
                true,
                options.UpdateRateMs,
                1,
                IntPtr.Zero,
                IntPtr.Zero,
                0,
                out serverGroupHandle,
                out revisedRate,
                ref iid,
                out groupObject);
            CheckHResult(hr, "IOPCServer.AddGroup");
            Console.WriteLine("Group added: handle=" + serverGroupHandle + ", revisedRateMs=" + revisedRate);
            return groupObject;
        }

        private static string DetectBestFormat(IOPCItemMgt itemMgt, List<TagRow> rows, Options options)
        {
            string[] formats = new string[]
            {
                "{channel}.{device}.{group}.{tag}",
                "{channel}.{device}.{group}.{address}",
                "{channel}.{device}.{tag}",
                "{channel}.{device}.{address}",
                "{device}.{group}.{tag}",
                "{device}.{group}.{address}",
                "{device}.{tag}",
                "{device}.{address}",
                "{group}.{tag}",
                "{group}.{address}",
                "{tag}",
                "{address}"
            };

            int probeCount = Math.Min(options.ProbeCount, rows.Count);
            string bestFormat = null;
            int bestSuccess = -1;

            for (int i = 0; i < formats.Length; i++)
            {
                string format = formats[i];
                List<TagRow> sample = rows.GetRange(0, probeCount);
                List<ItemBinding> bindings = AddItems(itemMgt, sample, options, format, false);
                int success = 0;
                for (int j = 0; j < bindings.Count; j++)
                {
                    if (bindings[j].AddSucceeded)
                    {
                        success++;
                    }
                }
                Console.WriteLine("Probe format: " + format + " => " + success + "/" + probeCount);
                if (success > bestSuccess)
                {
                    bestSuccess = success;
                    bestFormat = format;
                }
                if (success == probeCount && probeCount > 0)
                {
                    return format;
                }
            }

            return bestSuccess > 0 ? bestFormat : null;
        }

        private static List<ItemBinding> AddItems(
            IOPCItemMgt itemMgt,
            List<TagRow> rows,
            Options options,
            string itemFormat,
            bool verbose)
        {
            OPCITEMDEF[] defs = new OPCITEMDEF[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                string itemId = FormatItemId(itemFormat, rows[i], options);
                defs[i] = new OPCITEMDEF();
                defs[i].szAccessPath = "";
                defs[i].szItemID = itemId;
                defs[i].bActive = true;
                defs[i].hClient = i + 1;
                defs[i].dwBlobSize = 0;
                defs[i].pBlob = IntPtr.Zero;
                defs[i].vtRequestedDataType = 0; // VT_EMPTY: let the server choose canonical type.
                defs[i].wReserved = 0;
            }

            IntPtr resultsPtr = IntPtr.Zero;
            IntPtr errorsPtr = IntPtr.Zero;
            int hr = itemMgt.AddItems(rows.Count, defs, out resultsPtr, out errorsPtr);
            if (hr < 0)
            {
                throw new COMException("IOPCItemMgt.AddItems failed", hr);
            }

            List<ItemBinding> bindings = new List<ItemBinding>();
            try
            {
                int resultSize = Marshal.SizeOf(typeof(OPCITEMRESULT));
                for (int i = 0; i < rows.Count; i++)
                {
                    int itemError = Marshal.ReadInt32(AddPtr(errorsPtr, i * 4));
                    OPCITEMRESULT result = new OPCITEMRESULT();
                    if (resultsPtr != IntPtr.Zero)
                    {
                        result = (OPCITEMRESULT)Marshal.PtrToStructure(AddPtr(resultsPtr, i * resultSize), typeof(OPCITEMRESULT));
                    }

                    ItemBinding binding = new ItemBinding();
                    binding.Row = rows[i];
                    binding.ItemId = defs[i].szItemID;
                    binding.ClientHandle = i + 1;
                    binding.ServerHandle = result.hServer;
                    binding.CanonicalDataType = result.vtCanonicalDataType;
                    binding.AccessRights = result.dwAccessRights;
                    binding.BlobSize = result.dwBlobSize;
                    binding.AddError = itemError;
                    binding.AddSucceeded = itemError >= 0;
                    bindings.Add(binding);

                    if (verbose)
                    {
                        Console.WriteLine(
                            "AddItem " + (binding.AddSucceeded ? "OK" : "FAIL") +
                            " itemId=" + binding.ItemId +
                            " hClient=" + binding.ClientHandle +
                            " hServer=" + binding.ServerHandle +
                            " canonicalVt=" + binding.CanonicalDataType +
                            " accessRights=0x" + binding.AccessRights.ToString("X8", CultureInfo.InvariantCulture) +
                            " blobSize=" + binding.BlobSize +
                            " err=0x" + itemError.ToString("X8", CultureInfo.InvariantCulture));
                    }

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

        private static int ReadItems(IOPCSyncIO syncIO, List<ItemBinding> bindings, Options options)
        {
            List<ItemBinding> successful = new List<ItemBinding>();
            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i].AddSucceeded)
                {
                    successful.Add(bindings[i]);
                }
            }

            int[] handles = new int[successful.Count];
            for (int i = 0; i < successful.Count; i++)
            {
                handles[i] = successful[i].ServerHandle;
            }

            IntPtr statesPtr = IntPtr.Zero;
            IntPtr errorsPtr = IntPtr.Zero;
            int hr = syncIO.Read(options.Source, handles.Length, handles, out statesPtr, out errorsPtr);
            if (hr < 0)
            {
                throw new COMException("IOPCSyncIO.Read failed", hr);
            }

            int okCount = 0;
            try
            {
                int stateSize = Marshal.SizeOf(typeof(OPCITEMSTATE));
                for (int i = 0; i < successful.Count; i++)
                {
                    int itemError = Marshal.ReadInt32(AddPtr(errorsPtr, i * 4));
                    IntPtr statePtr = AddPtr(statesPtr, i * stateSize);
                    OPCITEMSTATE state = (OPCITEMSTATE)Marshal.PtrToStructure(statePtr, typeof(OPCITEMSTATE));
                    if (itemError >= 0)
                    {
                        okCount++;
                    }

                    ushort quality = (ushort)state.wQuality;
                    Console.WriteLine(
                        "Read " + (itemError >= 0 ? "OK" : "FAIL") +
                        " itemId=" + successful[i].ItemId +
                        " tag=" + successful[i].Row.Tag +
                        " address=" + successful[i].Row.Address +
                        " hClient=" + state.hClient +
                        " hServer=" + successful[i].ServerHandle +
                        " value=" + ValueToString(state.vDataValue) +
                        " valueType=" + ValueTypeToString(state.vDataValue) +
                        " quality=" + quality.ToString(CultureInfo.InvariantCulture) +
                        " qualityHex=0x" + quality.ToString("X4", CultureInfo.InvariantCulture) +
                        " qualityGood=" + IsQualityGood(quality).ToString(CultureInfo.InvariantCulture) +
                        " opcTime=" + FileTimeToString(state.ftTimeStamp) +
                        " err=0x" + itemError.ToString("X8", CultureInfo.InvariantCulture));

                    Marshal.DestroyStructure(statePtr, typeof(OPCITEMSTATE));
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

            return okCount;
        }

        private static string FormatItemId(string format, TagRow row, Options options)
        {
            return format
                .Replace("{server}", options.ServerProgId)
                .Replace("{channel}", options.Channel)
                .Replace("{device}", options.Device)
                .Replace("{group}", options.Group)
                .Replace("{tag}", row.Tag)
                .Replace("{address}", row.Address);
        }

        private static List<TagRow> LoadCsv(string path, int skip, int limit)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("CSV file not found", path);
            }

            List<TagRow> rows = new List<TagRow>();
            string[] lines = File.ReadAllLines(path, Encoding.Default);
            int seen = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }
                List<string> cols = SplitCsvLine(lines[i]);
                if (cols.Count < 3)
                {
                    continue;
                }
                seen++;
                if (skip > 0 && seen <= skip)
                {
                    continue;
                }
                TagRow row = new TagRow();
                row.Tag = cols[0].Trim();
                row.Address = cols[1].Trim();
                row.DataType = cols[2].Trim();
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

        private static string ValueToString(object value)
        {
            if (value == null)
            {
                return "<null>";
            }
            Array arr = value as Array;
            if (arr != null)
            {
                List<string> parts = new List<string>();
                foreach (object item in arr)
                {
                    parts.Add(item == null ? "<null>" : Convert.ToString(item, CultureInfo.InvariantCulture));
                }
                return "[" + string.Join(",", parts.ToArray()) + "]";
            }
            IFormattable formattable = value as IFormattable;
            if (formattable != null)
            {
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            }
            return value.ToString();
        }

        private static string ValueTypeToString(object value)
        {
            if (value == null)
            {
                return "<null>";
            }
            return value.GetType().FullName;
        }

        private static bool IsQualityGood(ushort quality)
        {
            return (quality & 0x00C0) == 0x00C0;
        }

        private static string FileTimeToString(ComFileTime ft)
        {
            long high = ((long)ft.dwHighDateTime) << 32;
            long low = (uint)ft.dwLowDateTime;
            long fileTime = high + low;
            if (fileTime <= 0)
            {
                return "";
            }
            try
            {
                return DateTime.FromFileTimeUtc(fileTime).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            }
            catch
            {
                return "";
            }
        }
    }

    internal sealed class Options
    {
        public string ServerProgId = "ZPMC.OPCServer";
        public string Channel = "OPD_Server";
        public string Device = "AGV801";
        public string Group = "ViewMain";
        public string CsvPath = @"..\..\data\AGV_OPC\agv\ViewMain.csv";
        public string ItemFormat = "";
        public int Skip = 0;
        public int Limit = 20;
        public int ProbeCount = 10;
        public int UpdateRateMs = 1000;
        public int ReadDelayMs = 1000;
        public OPCDATASOURCE Source = OPCDATASOURCE.OPC_DS_DEVICE;
        public bool ShowHelp = false;

        public static Options Parse(string[] args)
        {
            Options options = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg == "--help" || arg == "-h" || arg == "/?")
                {
                    options.ShowHelp = true;
                }
                else if (arg == "--server" && i + 1 < args.Length)
                {
                    options.ServerProgId = args[++i];
                }
                else if (arg == "--channel" && i + 1 < args.Length)
                {
                    options.Channel = args[++i];
                }
                else if (arg == "--device" && i + 1 < args.Length)
                {
                    options.Device = args[++i];
                }
                else if (arg == "--group" && i + 1 < args.Length)
                {
                    options.Group = args[++i];
                }
                else if (arg == "--csv" && i + 1 < args.Length)
                {
                    options.CsvPath = args[++i];
                }
                else if (arg == "--item-format" && i + 1 < args.Length)
                {
                    options.ItemFormat = args[++i];
                }
                else if (arg == "--limit" && i + 1 < args.Length)
                {
                    options.Limit = int.Parse(args[++i], CultureInfo.InvariantCulture);
                }
                else if (arg == "--skip" && i + 1 < args.Length)
                {
                    options.Skip = int.Parse(args[++i], CultureInfo.InvariantCulture);
                }
                else if (arg == "--probe-count" && i + 1 < args.Length)
                {
                    options.ProbeCount = int.Parse(args[++i], CultureInfo.InvariantCulture);
                }
                else if (arg == "--source" && i + 1 < args.Length)
                {
                    string source = args[++i].ToLowerInvariant();
                    options.Source = source == "cache" ? OPCDATASOURCE.OPC_DS_CACHE : OPCDATASOURCE.OPC_DS_DEVICE;
                }
                else if (arg == "--read-delay-ms" && i + 1 < args.Length)
                {
                    options.ReadDelayMs = int.Parse(args[++i], CultureInfo.InvariantCulture);
                }
            }
            return options;
        }

        public static void PrintHelp()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  OpcDaProbe.exe --server ZPMC.OPCServer --channel OPD_Server --device AGV801 --group ViewMain --csv <ViewMain.csv>");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --server       OPC DA COM ProgID. Default: ZPMC.OPCServer");
            Console.WriteLine("  --channel      OPC item channel/driver name. Default: OPD_Server");
            Console.WriteLine("  --device       OPC item device name. Default: AGV801");
            Console.WriteLine("  --group        OPC item group name. Default: ViewMain");
            Console.WriteLine("  --csv          CSV path. Default: ..\\..\\data\\AGV_OPC\\agv\\ViewMain.csv");
            Console.WriteLine("  --item-format  Override format, e.g. {channel}.{device}.{group}.{tag}");
            Console.WriteLine("  --limit        Number of CSV rows to add/read. Default: 20");
            Console.WriteLine("  --skip         Number of CSV rows to skip before loading. Default: 0");
            Console.WriteLine("  --probe-count  Rows used for automatic ItemID format detection. Default: 10");
            Console.WriteLine("  --source       device or cache. Default: device");
            Console.WriteLine("  --read-delay-ms Wait before SyncIO.Read after AddItems. Default: 1000");
        }
    }

    internal sealed class TagRow
    {
        public string Tag;
        public string Address;
        public string DataType;
    }

    internal sealed class ItemBinding
    {
        public TagRow Row;
        public string ItemId;
        public int ClientHandle;
        public int ServerHandle;
        public short CanonicalDataType;
        public int AccessRights;
        public int BlobSize;
        public int AddError;
        public bool AddSucceeded;
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
}
