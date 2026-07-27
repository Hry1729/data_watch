using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace HslCommunication.BasicFramework
{
	/// <summary>
	/// 软件授权类，可以获取本机的唯一机器码信息，将根据cpu号，主板号，
	/// </summary>
	public class SoftAuthorize : SoftFileSaveBase
	{
		private class HWID
		{
			public static string BIOS => GetWMIIdent("Win32_BIOS", "Manufacturer", "SerialNumber", "SMBIOSBIOSVersion", "IdentificationCode");

			public static string CPU => GetWMIIdent("Win32_Processor", "ProcessorId", "UniqueId", "Name");

			public static string HDD => GetWMIIdent("Win32_DiskDrive", "Model", "TotalHeads");

			public static string GPU => GetWMIIdent("Win32_VideoController", "DriverVersion", "Name");

			public static string MAC => GetWMIIdent("Win32_NetworkAdapterConfiguration", "MACAddress");

			public static string OS => GetWMIIdent("Win32_OperatingSystem", "SerialNumber", "Name");

			public static string SCSI => GetWMIIdent("Win32_SCSIController", "DeviceID", "Name");

			public static string BaseBoard => GetWMIIdent("Win32_BaseBoard", "SerialNumber", "PartNumber");

			public static bool IsServer => HDD.Contains("SCSI");
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		internal struct SendCmdInParams
		{
			public uint cBufferSize;

			public IdeRegs irDriveRegs;

			public byte bDriveNumber;

			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
			public byte[] bReserved;

			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
			public uint[] dwReserved;

			public byte bBuffer;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		internal struct DriverStatus
		{
			public byte bDriverError;

			public byte bIDEStatus;

			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
			public byte[] bReserved;

			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
			public uint[] dwReserved;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		internal struct SendCmdOutParams
		{
			public uint cBufferSize;

			public DriverStatus DriverStatus;

			public IdSector bBuffer;
		}

		/// <summary>
		/// 硬盘信息
		/// </summary>
		[Serializable]
		public struct HardDiskInfo
		{
			/// <summary>
			/// 型号
			/// </summary>
			public string ModuleNumber;

			/// <summary>
			/// 固件版本
			/// </summary>
			public string Firmware;

			/// <summary>
			/// 序列号
			/// </summary>
			public string SerialNumber;

			/// <summary>
			/// 容量，以M为单位
			/// </summary>
			public uint Capacity;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		internal struct GetVersionOutParams
		{
			public byte bVersion;

			public byte bRevision;

			public byte bReserved;

			public byte bIDEDeviceMap;

			public uint fCapabilities;

			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
			public uint[] dwReserved;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		internal struct IdeRegs
		{
			public byte bFeaturesReg;

			public byte bSectorCountReg;

			public byte bSectorNumberReg;

			public byte bCylLowReg;

			public byte bCylHighReg;

			public byte bDriveHeadReg;

			public byte bCommandReg;

			public byte bReserved;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 512)]
		internal struct IdSector
		{
			public ushort wGenConfig;

			public ushort wNumCyls;

			public ushort wReserved;

			public ushort wNumHeads;

			public ushort wBytesPerTrack;

			public ushort wBytesPerSector;

			public ushort wSectorsPerTrack;

			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
			public ushort[] wVendorUnique;

			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
			public byte[] sSerialNumber;

			public ushort wBufferType;

			public ushort wBufferSize;

			public ushort wECCSize;

			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
			public byte[] sFirmwareRev;

			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 40)]
			public byte[] sModelNumber;

			public ushort wMoreVendorUnique;

			public ushort wDoubleWordIO;

			public ushort wCapabilities;

			public ushort wReserved1;

			public ushort wPIOTiming;

			public ushort wDMATiming;

			public ushort wBS;

			public ushort wNumCurrentCyls;

			public ushort wNumCurrentHeads;

			public ushort wNumCurrentSectorsPerTrack;

			public uint ulCurrentSectorCapacity;

			public ushort wMultSectorStuff;

			public uint ulTotalAddressableSectors;

			public ushort wSingleWordDMA;

			public ushort wMultiWordDMA;

			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
			public byte[] bReserved;
		}

		/// <summary>
		/// 注册码描述文本
		/// </summary>
		public static readonly string TextCode = "Code";

		private string machine_code = "";

		private const uint DFP_GET_VERSION = 475264u;

		private const uint DFP_SEND_DRIVE_COMMAND = 508036u;

		private const uint DFP_RECEIVE_DRIVE_DATA = 508040u;

		private const uint GENERIC_READ = 2147483648u;

		private const uint GENERIC_WRITE = 1073741824u;

		private const uint FILE_SHARE_READ = 1u;

		private const uint FILE_SHARE_WRITE = 2u;

		private const uint CREATE_NEW = 1u;

		private const uint OPEN_EXISTING = 3u;

		/// <summary>
		/// 最终的注册秘钥信息，注意是只读的。
		/// </summary>
		/// <remarks>
		/// 时间：2018年9月1日 23:01:54，来自 洛阳-LYG 的建议，公开了本属性信息，只读。
		/// </remarks>
		public string FinalCode { get; private set; } = "";

		/// <summary>
		/// 是否正式发行版，是的话就取消授权
		/// </summary>
		public bool IsReleaseVersion { get; set; } = false;

		/// <summary>
		/// 获取或设置当前计算机器码的时候，会否报文硬盘的基本信息
		/// </summary>
		public bool ContainsHardDiskInformation { get; set; } = true;

		/// <summary>
		/// 指示是否加载过文件信息
		/// </summary>
		private bool HasLoadByFile { get; set; } = false;

		/// <summary>
		/// 指示系统是否处于试用运行
		/// </summary>
		public bool IsSoftTrial { get; set; } = false;

		/// <summary>
		/// 实例化一个软件授权类
		/// </summary>
		/// <param name="UseAdmin">是否使用管理员模式</param>
		/// <param name="useHDD">是否使用硬盘信息</param>
		public SoftAuthorize(bool UseAdmin = false, bool useHDD = true)
		{
			machine_code = GetInfo(UseAdmin, useHDD);
			base.LogHeaderText = "SoftAuthorize";
		}

		/// <summary>
		/// 获取本机的机器码
		/// </summary>
		/// <returns>机器码字符串</returns>
		public string GetMachineCodeString()
		{
			return machine_code;
		}

		/// <summary>
		/// 获取需要保存的数据内容
		/// </summary>
		/// <returns>实际保存的内容</returns>
		public override string ToSaveString()
		{
			return new JObject { 
			{
				TextCode,
				new JValue(FinalCode)
			} }.ToString();
		}

		/// <summary>
		/// 从字符串加载数据
		/// </summary>
		/// <param name="content">文件存储的数据</param>
		public override void LoadByString(string content)
		{
			FinalCode = SoftBasic.GetValueFromJsonObject(JObject.Parse(content), TextCode, FinalCode);
			HasLoadByFile = true;
		}

		/// <summary>
		/// 使用特殊加密算法加密数据
		/// </summary>
		public override void SaveToFile()
		{
			SaveToFile((string m) => SoftSecurity.MD5Encrypt(m));
		}

		/// <summary>
		/// 使用特殊解密算法解密数据
		/// </summary>
		public override void LoadByFile()
		{
			LoadByFile((string m) => SoftSecurity.MD5Decrypt(m));
		}

		/// <summary>
		/// 检查该注册码是否是正确的注册码
		/// </summary>
		/// <param name="code">注册码信息</param>
		/// <param name="encrypt">数据加密的方法，必须用户指定</param>
		/// <returns>是否注册成功</returns>
		public bool CheckAuthorize(string code, Func<string, string> encrypt)
		{
			if (code != encrypt(GetMachineCodeString()))
			{
				return false;
			}
			FinalCode = code;
			SaveToFile();
			return true;
		}

		/// <summary>
		/// 检测授权是否成功
		/// </summary>
		/// <param name="encrypt">数据加密的方法，必须用户指定</param>
		/// <returns>是否成功授权</returns>
		public bool IsAuthorizeSuccess(Func<string, string> encrypt)
		{
			if (IsReleaseVersion)
			{
				return true;
			}
			if (encrypt(GetMachineCodeString()) == FinalCode)
			{
				return true;
			}
			FinalCode = "";
			SaveToFile();
			return false;
		}

		/// <summary>
		/// 获取本地计算机唯一的机器码，将由bios编码，cpu编码，hdd编码结合出来的
		/// </summary>
		/// <param name="UseAdmin">是否使用了管理员模式</param>
		/// <param name="useHDD">是否使用硬盘，在部分的情况下，使用硬盘会受U盘插拔影响</param>
		/// <returns>字符串形式的机器码</returns>
		public static string GetInfo(bool UseAdmin, bool useHDD = true)
		{
			string text = string.Concat("" + HWID.BIOS + "|", HWID.CPU, "|");
			if (useHDD)
			{
				text = text + HWID.HDD + "|";
			}
			text = text + HWID.BaseBoard + "|";
			if (HWID.IsServer)
			{
				text = text + HWID.SCSI + "|";
			}
			ManagementObject managementObject = new ManagementObject(string.Concat("win32_logicaldisk.deviceid=", "\"" + Environment.GetEnvironmentVariable("systemdrive") + "\""));
			managementObject.Get();
			text = string.Concat(text + managementObject["VolumeSerialNumber"].ToString(), "|");
			using (ManagementObjectCollection.ManagementObjectEnumerator managementObjectEnumerator = new ManagementClass("Win32_ComputerSystemProduct").GetInstances().GetEnumerator())
			{
				if (managementObjectEnumerator.MoveNext())
				{
					text += (string)((ManagementObject)managementObjectEnumerator.Current).Properties["UUID"].Value;
				}
			}
			text += "|";
			if (UseAdmin)
			{
				if (new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
				{
					text += GetHddInfo(0).SerialNumber;
				}
				else
				{
					ProcessStartInfo processStartInfo = new ProcessStartInfo();
					processStartInfo.FileName = Application.ExecutablePath;
					processStartInfo.Verb = "runas";
					Process.Start(processStartInfo);
					Application.Exit();
				}
			}
			return SoftBasic.ByteToHexString(new SHA1CryptoServiceProvider().ComputeHash(Encoding.Unicode.GetBytes(text))).Substring(0, 25);
		}

		private static string GetWMIIdent(string Class, string Property)
		{
			string result = "";
			using (ManagementObjectCollection.ManagementObjectEnumerator managementObjectEnumerator = new ManagementClass(Class).GetInstances().GetEnumerator())
			{
				while (managementObjectEnumerator.MoveNext() && !((result = managementObjectEnumerator.Current.GetPropertyValue(Property) as string) != ""))
				{
				}
			}
			return result;
		}

		private static string GetWMIIdent(string Class, params string[] Propertys)
		{
			string ident = "";
			Array.ForEach(Propertys, delegate(string prop)
			{
				ident = ident + GetWMIIdent(Class, prop) + " ";
			});
			return ident;
		}

		/// <summary>
		/// 获得硬盘信息
		/// </summary>
		/// <param name="driveIndex">硬盘序号</param>
		/// <returns>硬盘信息</returns>
		/// <remarks>
		/// by sunmast for everyone
		/// thanks lu0 for his great works
		/// 在Windows Array8/ME中，S.M.A.R.T并不缺省安装，请将SMARTVSD.VXD拷贝到%SYSTEM%＼IOSUBSYS目录下。
		/// 在Windows 2000/2003下，需要Administrators组的权限。
		/// </remarks>
		/// <example>
		/// AtapiDevice.GetHddInfo()
		/// </example>
		public static HardDiskInfo GetHddInfo(byte driveIndex = 0)
		{
			switch (Environment.OSVersion.Platform)
			{
			case PlatformID.Win32Windows:
				return GetHddInfoArrayx(driveIndex);
			case PlatformID.Win32NT:
				return GetHddInfoNT(driveIndex);
			case PlatformID.Win32S:
				throw new NotSupportedException("Win32s is not supported.");
			case PlatformID.WinCE:
				throw new NotSupportedException("WinCE is not supported.");
			default:
				throw new NotSupportedException("Unknown Platform.");
			}
		}

		private static HardDiskInfo GetHardDiskInfo(IdSector phdinfo)
		{
			HardDiskInfo result = default(HardDiskInfo);
			ChangeByteOrder(phdinfo.sModelNumber);
			result.ModuleNumber = Encoding.ASCII.GetString(phdinfo.sModelNumber).Trim();
			ChangeByteOrder(phdinfo.sFirmwareRev);
			result.Firmware = Encoding.ASCII.GetString(phdinfo.sFirmwareRev).Trim();
			ChangeByteOrder(phdinfo.sSerialNumber);
			result.SerialNumber = Encoding.ASCII.GetString(phdinfo.sSerialNumber).Trim();
			result.Capacity = phdinfo.ulTotalAddressableSectors / 2 / 1024;
			return result;
		}

		private static HardDiskInfo GetHddInfoArrayx(byte driveIndex)
		{
			GetVersionOutParams lpOutBuffer = default(GetVersionOutParams);
			SendCmdInParams lpInBuffer = default(SendCmdInParams);
			SendCmdOutParams lpOutBuffer2 = default(SendCmdOutParams);
			uint lpBytesReturned = 0u;
			IntPtr intPtr = CreateFile("＼＼.＼Smartvsd", FileAccess.ReadWrite, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, FileAttributes.Normal, IntPtr.Zero);
			if (intPtr == IntPtr.Zero)
			{
				throw new Exception("Open smartvsd.vxd failed.");
			}
			if (DeviceIoControl(intPtr, 475264u, IntPtr.Zero, 0u, ref lpOutBuffer, (uint)Marshal.SizeOf(lpOutBuffer), ref lpBytesReturned, IntPtr.Zero) == 0)
			{
				CloseHandle(intPtr);
				throw new Exception("DeviceIoControl failed:DFP_GET_VERSION");
			}
			if ((lpOutBuffer.fCapabilities & 1) == 0)
			{
				CloseHandle(intPtr);
				throw new Exception("Error: IDE identify command not supported.");
			}
			if ((driveIndex & 1) != 0)
			{
				lpInBuffer.irDriveRegs.bDriveHeadReg = 176;
			}
			else
			{
				lpInBuffer.irDriveRegs.bDriveHeadReg = 160;
			}
			if ((lpOutBuffer.fCapabilities & (16 >> (int)driveIndex)) != 0)
			{
				CloseHandle(intPtr);
				throw new Exception($"Drive {driveIndex + 1} is a ATAPI device, we don’t detect it");
			}
			lpInBuffer.irDriveRegs.bCommandReg = 236;
			lpInBuffer.bDriveNumber = driveIndex;
			lpInBuffer.irDriveRegs.bSectorCountReg = 1;
			lpInBuffer.irDriveRegs.bSectorNumberReg = 1;
			lpInBuffer.cBufferSize = 512u;
			if (DeviceIoControl(intPtr, 508040u, ref lpInBuffer, (uint)Marshal.SizeOf(lpInBuffer), ref lpOutBuffer2, (uint)Marshal.SizeOf(lpOutBuffer2), ref lpBytesReturned, IntPtr.Zero) == 0)
			{
				CloseHandle(intPtr);
				throw new Exception("DeviceIoControl failed: DFP_RECEIVE_DRIVE_DATA");
			}
			CloseHandle(intPtr);
			return GetHardDiskInfo(lpOutBuffer2.bBuffer);
		}

		private static HardDiskInfo GetHddInfoNT(byte driveIndex)
		{
			GetVersionOutParams lpOutBuffer = default(GetVersionOutParams);
			SendCmdInParams lpInBuffer = default(SendCmdInParams);
			SendCmdOutParams lpOutBuffer2 = default(SendCmdOutParams);
			uint lpBytesReturned = 0u;
			IntPtr intPtr = CreateFile("\\\\.\\PhysicalDrive0", FileAccess.ReadWrite, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, FileAttributes.Normal, IntPtr.Zero);
			if (intPtr == IntPtr.Zero)
			{
				throw new Exception("CreateFile faild.");
			}
			if (DeviceIoControl(intPtr, 475264u, IntPtr.Zero, 0u, ref lpOutBuffer, (uint)Marshal.SizeOf(lpOutBuffer), ref lpBytesReturned, IntPtr.Zero) == 0)
			{
				CloseHandle(intPtr);
				throw new Exception($"Drive {driveIndex + 1} may not exists.");
			}
			if ((lpOutBuffer.fCapabilities & 1) == 0)
			{
				CloseHandle(intPtr);
				throw new Exception("Error: IDE identify command not supported.");
			}
			if ((driveIndex & 1) != 0)
			{
				lpInBuffer.irDriveRegs.bDriveHeadReg = 176;
			}
			else
			{
				lpInBuffer.irDriveRegs.bDriveHeadReg = 160;
			}
			if ((lpOutBuffer.fCapabilities & (16 >> (int)driveIndex)) != 0)
			{
				CloseHandle(intPtr);
				throw new Exception($"Drive {driveIndex + 1} is a ATAPI device, we don’t detect it.");
			}
			lpInBuffer.irDriveRegs.bCommandReg = 236;
			lpInBuffer.bDriveNumber = driveIndex;
			lpInBuffer.irDriveRegs.bSectorCountReg = 1;
			lpInBuffer.irDriveRegs.bSectorNumberReg = 1;
			lpInBuffer.cBufferSize = 512u;
			if (DeviceIoControl(intPtr, 508040u, ref lpInBuffer, (uint)Marshal.SizeOf(lpInBuffer), ref lpOutBuffer2, (uint)Marshal.SizeOf(lpOutBuffer2), ref lpBytesReturned, IntPtr.Zero) == 0)
			{
				CloseHandle(intPtr);
				throw new Exception("DeviceIoControl failed: DFP_RECEIVE_DRIVE_DATA");
			}
			CloseHandle(intPtr);
			return GetHardDiskInfo(lpOutBuffer2.bBuffer);
		}

		private static void ChangeByteOrder(byte[] charArray)
		{
			for (int i = 0; i < charArray.Length; i += 2)
			{
				byte b = charArray[i];
				charArray[i] = charArray[i + 1];
				charArray[i + 1] = b;
			}
		}

		/// <summary>
		/// 执行打开/建立资源的功能。
		/// </summary>
		/// <param name="lpFileName">指定要打开的设备或文件的名称。</param>
		/// <param name="dwDesiredAccess">
		/// <para>Win32 常量，用于控制对设备的读访问、写访问或读/写访问的常数。内容如下表：
		/// <p><list type="table">
		/// <listheader>
		/// <term>名称</term>
		/// <description>说明</description>
		/// </listheader>
		/// <item>
		/// <term>GENERIC_READ</term><description>指定对设备进行读取访问。</description>
		/// </item>
		/// <item>
		/// <term>GENERIC_WRITE</term><description>指定对设备进行写访问。</description>
		/// </item>
		/// <item><term><b>0</b></term><description>如果值为零，则表示只允许获取与一个设备有关的信息。</description></item>
		/// </list></p>
		/// </para>
		/// </param>
		/// <param name="dwShareMode">指定打开设备时的文件共享模式</param>
		/// <param name="lpSecurityAttributes"></param>
		/// <param name="dwCreationDisposition">Win32 常量，指定操作系统打开文件的方式。内容如下表：
		/// <para><p>
		/// <list type="table">
		/// <listheader><term>名称</term><description>说明</description></listheader>
		/// <item>
		/// <term>CREATE_NEW</term>
		/// <description>指定操作系统应创建新文件。如果文件存在，则抛出 <see cref="T:System.IO.IOException" /> 异常。</description>
		/// </item>
		/// <item><term>CREATE_ALWAYS</term><description>指定操作系统应创建新文件。如果文件已存在，它将被改写。</description></item>
		/// </list>
		/// </p></para>
		/// </param>
		/// <param name="dwFlagsAndAttributes"></param>
		/// <param name="hTemplateFile"></param>
		/// <returns>使用函数打开的设备的句柄。</returns>
		/// <remarks>
		/// 本函数可以执行打开或建立文件、文件流、目录/文件夹、物理磁盘、卷、系统控制的缓冲区、磁带设备、
		/// 通信资源、邮件系统和命名管道。
		/// </remarks>
		[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		private static extern IntPtr CreateFile(string lpFileName, [MarshalAs(UnmanagedType.U4)] FileAccess dwDesiredAccess, [MarshalAs(UnmanagedType.U4)] FileShare dwShareMode, IntPtr lpSecurityAttributes, [MarshalAs(UnmanagedType.U4)] FileMode dwCreationDisposition, [MarshalAs(UnmanagedType.U4)] FileAttributes dwFlagsAndAttributes, IntPtr hTemplateFile);

		/// <summary>
		/// 关闭一个指定的指针对象指向的设备。。
		/// </summary>
		/// <param name="hObject">要关闭的句柄 <see cref="T:System.IntPtr" /> 对象。</param>
		/// <returns>成功返回 <b>0</b> ，不成功返回非零值。</returns>
		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern int CloseHandle(IntPtr hObject);

		/// <summary>
		/// 对设备执行指定的操作。
		/// </summary>
		/// <param name="hDevice">要执行操作的设备句柄。</param>
		/// <param name="dwIoControlCode">Win32 API 常数，输入的是以 <b>FSCTL_</b> 为前缀的常数，定义在
		/// <b>WinIoCtl.h</b> 文件内，执行此重载方法必须输入 <b>SMART_GET_VERSION</b> 。</param>
		/// <param name="lpInBuffer">当参数为指针时，默认的输入值是 <b>0</b> 。</param>
		/// <param name="nInBufferSize">输入缓冲区的字节数量。</param>
		/// <param name="lpOutBuffer">一个 <b>GetVersionOutParams</b> ，表示执行函数后输出的设备检查。</param>
		/// <param name="nOutBufferSize">输出缓冲区的字节数量。</param>
		/// <param name="lpBytesReturned">实际装载到输出缓冲区的字节数量。</param>
		/// <param name="lpOverlapped">同步操作控制，一般不使用，默认值为 <b>0</b> 。</param>
		/// <returns>非零表示成功，零表示失败。</returns>
		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern int DeviceIoControl(IntPtr hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, ref GetVersionOutParams lpOutBuffer, uint nOutBufferSize, ref uint lpBytesReturned, [Out] IntPtr lpOverlapped);

		/// <summary>
		/// 对设备执行指定的操作。
		/// </summary>
		/// <param name="hDevice">要执行操作的设备句柄。</param>
		/// <param name="dwIoControlCode">Win32 API 常数，输入的是以 <b>FSCTL_</b> 为前缀的常数，定义在
		/// <b>WinIoCtl.h</b> 文件内，执行此重载方法必须输入 <b>SMART_SEND_DRIVE_COMMAND</b> 或 <b>SMART_RCV_DRIVE_DATA</b> 。</param>
		/// <param name="lpInBuffer">一个 <b>SendCmdInParams</b> 结构，它保存向系统发送的查询要求具体命令的数据结构。</param>
		/// <param name="nInBufferSize">输入缓冲区的字节数量。</param>
		/// <param name="lpOutBuffer">一个 <b>SendCmdOutParams</b> 结构，它保存系统根据命令返回的设备相信信息二进制数据。</param>
		/// <param name="nOutBufferSize">输出缓冲区的字节数量。</param>
		/// <param name="lpBytesReturned">实际装载到输出缓冲区的字节数量。</param>
		/// <param name="lpOverlapped">同步操作控制，一般不使用，默认值为 <b>0</b> 。</param>
		/// <returns>非零表示成功，零表示失败。</returns>
		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern int DeviceIoControl(IntPtr hDevice, uint dwIoControlCode, ref SendCmdInParams lpInBuffer, uint nInBufferSize, ref SendCmdOutParams lpOutBuffer, uint nOutBufferSize, ref uint lpBytesReturned, [Out] IntPtr lpOverlapped);
	}
}
