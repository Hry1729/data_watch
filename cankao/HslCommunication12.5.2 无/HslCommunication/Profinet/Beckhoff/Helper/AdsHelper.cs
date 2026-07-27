using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HslCommunication.BasicFramework;
using HslCommunication.Core;

namespace HslCommunication.Profinet.Beckhoff.Helper
{
	internal class AdsHelper
	{
		/// <summary>
		/// 根据命令码ID，消息ID，数据信息组成AMS的命令码
		/// </summary>
		/// <param name="commandId">命令码ID</param>
		/// <param name="data">数据内容</param>
		/// <returns>打包之后的数据信息，没有填写AMSNetId的Target和Source内容</returns>
		public static byte[] BuildAmsHeaderCommand(ushort commandId, byte[] data)
		{
			if (data == null)
			{
				data = new byte[0];
			}
			byte[] array = new byte[32 + data.Length];
			array[16] = BitConverter.GetBytes(commandId)[0];
			array[17] = BitConverter.GetBytes(commandId)[1];
			array[18] = 4;
			array[19] = 0;
			array[20] = BitConverter.GetBytes(data.Length)[0];
			array[21] = BitConverter.GetBytes(data.Length)[1];
			array[22] = BitConverter.GetBytes(data.Length)[2];
			array[23] = BitConverter.GetBytes(data.Length)[3];
			array[24] = 0;
			array[25] = 0;
			array[26] = 0;
			array[27] = 0;
			data.CopyTo(array, 32);
			return PackAmsTcpHelper(AmsTcpHeaderFlags.Command, array);
		}

		/// <summary>
		/// 构建读取设备信息的命令报文
		/// </summary>
		/// <returns>报文信息</returns>
		public static OperateResult<byte[]> BuildReadDeviceInfoCommand()
		{
			return OperateResult.CreateSuccessResult(BuildAmsHeaderCommand(1, null));
		}

		/// <summary>
		/// 构建读取状态的命令报文
		/// </summary>
		/// <returns>报文信息</returns>
		public static OperateResult<byte[]> BuildReadStateCommand()
		{
			return OperateResult.CreateSuccessResult(BuildAmsHeaderCommand(4, null));
		}

		/// <summary>
		/// 构建写入状态的命令报文
		/// </summary>
		/// <param name="state">Ads state</param>
		/// <param name="deviceState">Device state</param>
		/// <param name="data">Data</param>
		/// <returns>报文信息</returns>
		public static OperateResult<byte[]> BuildWriteControlCommand(short state, short deviceState, byte[] data)
		{
			if (data == null)
			{
				data = new byte[0];
			}
			byte[] array = new byte[8 + data.Length];
			return OperateResult.CreateSuccessResult(BuildAmsHeaderCommand(5, SoftBasic.SpliceArray<byte>(BitConverter.GetBytes(state), BitConverter.GetBytes(deviceState), BitConverter.GetBytes(data.Length), data)));
		}

		/// <summary>
		/// 构建写入的指令信息
		/// </summary>
		/// <param name="address">地址信息</param>
		/// <param name="length">数据长度</param>
		/// <param name="isBit">是否是位信息</param>
		/// <returns>结果内容</returns>
		public static OperateResult<byte[]> BuildReadCommand(string address, int length, bool isBit)
		{
			OperateResult<uint, uint> operateResult = AnalysisAddress(address, isBit);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<byte[]>(operateResult);
			}
			byte[] array = new byte[12];
			BitConverter.GetBytes(operateResult.Content1).CopyTo(array, 0);
			BitConverter.GetBytes(operateResult.Content2).CopyTo(array, 4);
			BitConverter.GetBytes(length).CopyTo(array, 8);
			return OperateResult.CreateSuccessResult(BuildAmsHeaderCommand(2, array));
		}

		/// <summary>
		/// 构建批量读取的指令信息，不能传入读取符号数据，只能传入读取M,I,Q,i=0x0001信息
		/// </summary>
		/// <param name="address">地址信息</param>
		/// <param name="length">数据长度</param>
		/// <returns>结果内容</returns>
		public static OperateResult<byte[]> BuildReadCommand(string[] address, ushort[] length)
		{
			byte[] array = new byte[12 * address.Length];
			int num = 0;
			for (int i = 0; i < address.Length; i++)
			{
				OperateResult<uint, uint> operateResult = AnalysisAddress(address[i], false);
				if (!operateResult.IsSuccess)
				{
					return OperateResult.CreateFailedResult<byte[]>(operateResult);
				}
				BitConverter.GetBytes(operateResult.Content1).CopyTo(array, 12 * i);
				BitConverter.GetBytes(operateResult.Content2).CopyTo(array, 12 * i + 4);
				BitConverter.GetBytes((int)length[i]).CopyTo(array, 12 * i + 8);
				num += length[i];
			}
			return BuildReadWriteCommand("ig=0xF080;0", num, false, array);
		}

		/// <summary>
		/// 构建写入的指令信息
		/// </summary>
		/// <param name="address">地址信息</param>
		/// <param name="length">数据长度</param>
		/// <param name="isBit">是否是位信息</param>
		/// <param name="value">写入的数值</param>
		/// <returns>结果内容</returns>
		public static OperateResult<byte[]> BuildReadWriteCommand(string address, int length, bool isBit, byte[] value)
		{
			OperateResult<uint, uint> operateResult = AnalysisAddress(address, isBit);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<byte[]>(operateResult);
			}
			byte[] array = new byte[16 + value.Length];
			BitConverter.GetBytes(operateResult.Content1).CopyTo(array, 0);
			BitConverter.GetBytes(operateResult.Content2).CopyTo(array, 4);
			BitConverter.GetBytes(length).CopyTo(array, 8);
			BitConverter.GetBytes(value.Length).CopyTo(array, 12);
			value.CopyTo(array, 16);
			return OperateResult.CreateSuccessResult(BuildAmsHeaderCommand(9, array));
		}

		/// <summary>
		/// 构建批量写入的指令代码，不能传入读取符号数据，只能传入读取M,I,Q,i=0x0001信息
		/// </summary>
		/// <remarks>
		/// 实际没有调试通
		/// </remarks>
		/// <param name="address">地址列表信息</param>
		/// <param name="value">写入的数据值信息</param>
		/// <returns>命令报文</returns>
		public static OperateResult<byte[]> BuildWriteCommand(string[] address, List<byte[]> value)
		{
			MemoryStream memoryStream = new MemoryStream();
			int num = 0;
			for (int i = 0; i < address.Length; i++)
			{
				OperateResult<uint, uint> operateResult = AnalysisAddress(address[i], false);
				if (!operateResult.IsSuccess)
				{
					return OperateResult.CreateFailedResult<byte[]>(operateResult);
				}
				memoryStream.Write(BitConverter.GetBytes(operateResult.Content1));
				memoryStream.Write(BitConverter.GetBytes(operateResult.Content2));
				memoryStream.Write(BitConverter.GetBytes(value[i].Length));
				memoryStream.Write(value[i]);
				num += value[i].Length;
			}
			return BuildReadWriteCommand("ig=0xF081;0", num, false, memoryStream.ToArray());
		}

		/// <summary>
		/// 构建写入的指令信息
		/// </summary>
		/// <param name="address">地址信息</param>
		/// <param name="value">数据</param>
		/// <param name="isBit">是否是位信息</param>
		/// <returns>结果内容</returns>
		public static OperateResult<byte[]> BuildWriteCommand(string address, byte[] value, bool isBit)
		{
			OperateResult<uint, uint> operateResult = AnalysisAddress(address, isBit);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<byte[]>(operateResult);
			}
			byte[] array = new byte[12 + value.Length];
			BitConverter.GetBytes(operateResult.Content1).CopyTo(array, 0);
			BitConverter.GetBytes(operateResult.Content2).CopyTo(array, 4);
			BitConverter.GetBytes(value.Length).CopyTo(array, 8);
			value.CopyTo(array, 12);
			return OperateResult.CreateSuccessResult(BuildAmsHeaderCommand(3, array));
		}

		/// <summary>
		/// 构建写入的指令信息
		/// </summary>
		/// <param name="address">地址信息</param>
		/// <param name="value">数据</param>
		/// <param name="isBit">是否是位信息</param>
		/// <returns>结果内容</returns>
		public static OperateResult<byte[]> BuildWriteCommand(string address, bool[] value, bool isBit)
		{
			OperateResult<uint, uint> operateResult = AnalysisAddress(address, isBit);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<byte[]>(operateResult);
			}
			byte[] array = value.Select((bool m) => (byte)(m ? 1 : 0)).ToArray();
			byte[] array2 = new byte[12 + array.Length];
			BitConverter.GetBytes(operateResult.Content1).CopyTo(array2, 0);
			BitConverter.GetBytes(operateResult.Content2).CopyTo(array2, 4);
			BitConverter.GetBytes(array.Length).CopyTo(array2, 8);
			array.CopyTo(array2, 12);
			return OperateResult.CreateSuccessResult(BuildAmsHeaderCommand(3, array2));
		}

		/// <summary>
		/// 构建释放句柄的报文信息，当获取了变量的句柄后，这个句柄就被释放
		/// </summary>
		/// <param name="handle">句柄信息</param>
		/// <returns>报文的结果内容</returns>
		public static OperateResult<byte[]> BuildReleaseSystemHandle(uint handle)
		{
			byte[] array = new byte[16];
			BitConverter.GetBytes(61446).CopyTo(array, 0);
			BitConverter.GetBytes(4).CopyTo(array, 8);
			BitConverter.GetBytes(handle).CopyTo(array, 12);
			return OperateResult.CreateSuccessResult(BuildAmsHeaderCommand(3, array));
		}

		/// <summary>
		/// 检查从PLC的反馈的数据报文是否正确
		/// </summary>
		/// <param name="response">反馈报文</param>
		/// <returns>检查结果</returns>
		public static OperateResult<int> CheckResponse(byte[] response)
		{
			try
			{
				int num = BitConverter.ToInt32(response, 30);
				if (num > 0)
				{
					return new OperateResult<int>(num, GetErrorCodeText(num) + Environment.NewLine + "Source:" + response.ToHexString(' '));
				}
				if (response.Length >= 42)
				{
					int num2 = BitConverter.ToInt32(response, 38);
					if (num2 != 0)
					{
						return new OperateResult<int>(num2, GetErrorCodeText(num2) + Environment.NewLine + "Source:" + response.ToHexString(' '));
					}
				}
			}
			catch (Exception ex)
			{
				return new OperateResult<int>(ex.Message + " Source:" + response.ToHexString(' '));
			}
			return OperateResult.CreateSuccessResult(0);
		}

		/// <summary>
		/// 将实际的包含AMS头报文和数据报文的命令，打包成实际可发送的命令
		/// </summary>
		/// <param name="headerFlags">命令头信息</param>
		/// <param name="command">命令信息</param>
		/// <returns>结果信息</returns>
		public static byte[] PackAmsTcpHelper(AmsTcpHeaderFlags headerFlags, byte[] command)
		{
			byte[] array = new byte[6 + command.Length];
			BitConverter.GetBytes((ushort)headerFlags).CopyTo(array, 0);
			BitConverter.GetBytes(command.Length).CopyTo(array, 2);
			command.CopyTo(array, 6);
			return array;
		}

		private static int CalculateAddressStarted(string address)
		{
			if (address.IndexOf('.') < 0)
			{
				return Convert.ToInt32(address);
			}
			string[] array = address.Split('.');
			return Convert.ToInt32(array[0]) * 8 + HslHelper.CalculateBitStartIndex(array[1]);
		}

		/// <summary>
		/// 分析当前的地址信息，根据结果信息进行解析出真实的偏移地址
		/// </summary>
		/// <param name="address">地址</param>
		/// <param name="isBit">是否位访问</param>
		/// <returns>结果内容</returns>
		public static OperateResult<uint, uint> AnalysisAddress(string address, bool isBit)
		{
			OperateResult<uint, uint> operateResult = new OperateResult<uint, uint>();
			try
			{
				if (address.StartsWith("i=") || address.StartsWith("I="))
				{
					operateResult.Content1 = 61445u;
					operateResult.Content2 = uint.Parse(address.Substring(2));
				}
				else if (address.StartsWith("s=") || address.StartsWith("S="))
				{
					operateResult.Content1 = 61443u;
					operateResult.Content2 = 0u;
				}
				else if (address.StartsWith("ig=") || address.StartsWith("IG="))
				{
					address = address.ToUpper();
					operateResult.Content1 = (uint)HslHelper.ExtractParameter(ref address, "IG", 0);
					operateResult.Content2 = uint.Parse(address);
				}
				else
				{
					switch (address[0])
					{
					case 'M':
					case 'm':
						if (isBit)
						{
							operateResult.Content1 = 16417u;
							operateResult.Content2 = (uint)CalculateAddressStarted(address.Substring(1));
						}
						else
						{
							operateResult.Content1 = 16416u;
							operateResult.Content2 = uint.Parse(address.Substring(1));
						}
						break;
					case 'I':
					case 'i':
						if (isBit)
						{
							operateResult.Content1 = 61473u;
							operateResult.Content2 = (uint)(CalculateAddressStarted(address.Substring(1)) + 1024000);
						}
						else
						{
							operateResult.Content1 = 61472u;
							operateResult.Content2 = uint.Parse(address.Substring(1)) + 128000;
						}
						break;
					case 'Q':
					case 'q':
						if (isBit)
						{
							operateResult.Content1 = 61489u;
							operateResult.Content2 = (uint)(CalculateAddressStarted(address.Substring(1)) + 2048000);
						}
						else
						{
							operateResult.Content1 = 61488u;
							operateResult.Content2 = uint.Parse(address.Substring(1)) + 256000;
						}
						break;
					default:
						throw new Exception(StringResources.Language.NotSupportedDataType);
					}
				}
			}
			catch (Exception ex)
			{
				operateResult.Message = ex.Message;
				return operateResult;
			}
			operateResult.IsSuccess = true;
			operateResult.Message = StringResources.Language.SuccessText;
			return operateResult;
		}

		/// <summary>
		/// 将字符串名称转变为ADS协议可识别的字节数组
		/// </summary>
		/// <param name="value">值</param>
		/// <returns>字节数组</returns>
		public static byte[] StrToAdsBytes(string value)
		{
			return SoftBasic.SpliceArray<byte>(Encoding.ASCII.GetBytes(value), new byte[1]);
		}

		/// <summary>
		/// 将字符串的信息转换为AMS目标的地址
		/// </summary>
		/// <param name="amsNetId">目标信息</param>
		/// <returns>字节数组</returns>
		public static byte[] StrToAMSNetId(string amsNetId)
		{
			string text = amsNetId;
			byte[] array;
			if (amsNetId.IndexOf(':') > 0)
			{
				array = new byte[8];
				string[] array2 = amsNetId.Split(new char[1] { ':' }, StringSplitOptions.RemoveEmptyEntries);
				text = array2[0];
				array[6] = BitConverter.GetBytes(int.Parse(array2[1]))[0];
				array[7] = BitConverter.GetBytes(int.Parse(array2[1]))[1];
			}
			else
			{
				array = new byte[6];
			}
			string[] array3 = text.Split(new char[1] { '.' }, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < array3.Length; i++)
			{
				array[i] = byte.Parse(array3[i]);
			}
			return array;
		}

		/// <summary>
		/// 根据byte数组信息提取出字符串格式的AMSNetId数据信息，方便日志查看
		/// </summary>
		/// <param name="data">原始的报文数据信息</param>
		/// <param name="index">起始的节点信息</param>
		/// <returns>Ams节点号信息</returns>
		public static string GetAmsNetIdString(byte[] data, int index)
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(data[index]);
			stringBuilder.Append(".");
			stringBuilder.Append(data[index + 1]);
			stringBuilder.Append(".");
			stringBuilder.Append(data[index + 2]);
			stringBuilder.Append(".");
			stringBuilder.Append(data[index + 3]);
			stringBuilder.Append(".");
			stringBuilder.Append(data[index + 4]);
			stringBuilder.Append(".");
			stringBuilder.Append(data[index + 5]);
			stringBuilder.Append(":");
			stringBuilder.Append(BitConverter.ToUInt16(data, index + 6));
			return stringBuilder.ToString();
		}

		/// <summary>
		/// 根据AMS的错误号，获取到错误信息，错误信息来源于 wirshake 源代码文件 "..\wireshark\plugins\epan\ethercat\packet-ams.c"
		/// </summary>
		/// <param name="error">错误号</param>
		/// <returns>错误的描述信息</returns>
		public static string GetErrorCodeText(int error)
		{
			switch (error)
			{
			case 0:
				return "NO ERROR";
			case 1:
				return "InternalError";
			case 2:
				return "NO RTIME";
			case 3:
				return "Allocation locked – memory error.";
			case 4:
				return "Mailbox full – the ADS message could not be sent. Reducing the number of ADS messages per cycle will help.";
			case 5:
				return "WRONG RECEIVEH MSG";
			case 6:
				return "Target port not found – ADS server is not started or is not reachable.";
			case 7:
				return "Target computer not found – AMS route was not found.";
			case 8:
				return "Unknown command ID.";
			case 9:
				return "Invalid task ID.";
			case 10:
				return "No IO.";
			case 11:
				return "Unknown AMS command.";
			case 12:
				return "Win32 error.";
			case 13:
				return "Port not connected.";
			case 14:
				return "Invalid AMS length.";
			case 15:
				return "Invalid AMS Net ID.";
			case 16:
				return "Installation level is too low –TwinCAT 2 license error.";
			case 17:
				return "No debugging available.";
			case 18:
				return "Port disabled – TwinCAT system service not started.";
			case 19:
				return "Port already connected.";
			case 20:
				return "AMS Sync Win32 error.";
			case 21:
				return "AMS Sync Timeout.";
			case 22:
				return "AMS Sync error.";
			case 23:
				return "No index map for AMS Sync available.";
			case 24:
				return "Invalid AMS port.";
			case 25:
				return "No memory.";
			case 26:
				return "TCP send error.";
			case 27:
				return "Host unreachable.";
			case 28:
				return "Invalid AMS fragment.";
			case 29:
				return "TLS send error – secure ADS connection failed.";
			case 30:
				return "Access denied – secure ADS access denied.";
			case 1280:
				return "Locked memory cannot be allocated.";
			case 1281:
				return "The router memory size could not be changed.";
			case 1282:
				return "The mailbox has reached the maximum number of possible messages.";
			case 1283:
				return "The Debug mailbox has reached the maximum number of possible messages.";
			case 1284:
				return "The port type is unknown.";
			case 1285:
				return "The router is not initialized.";
			case 1286:
				return "The port number is already assigned.";
			case 1287:
				return "The port is not registered.";
			case 1288:
				return "The maximum number of ports has been reached.";
			case 1289:
				return "The port is invalid.";
			case 1290:
				return "The router is not active.";
			case 1291:
				return "The mailbox has reached the maximum number for fragmented messages.";
			case 1292:
				return "A fragment timeout has occurred.";
			case 1293:
				return "The port is removed.";
			case 1792:
				return "General device error.";
			case 1793:
				return "Service is not supported by the server.";
			case 1794:
				return "Invalid index group.";
			case 1795:
				return "Invalid index offset.";
			case 1796:
				return "Reading or writing not permitted.";
			case 1797:
				return "Parameter size not correct. Commonly found in batch processing, check the calculation command length";
			case 1798:
				return "Invalid data values.";
			case 1799:
				return "Device is not ready to operate. It is possible that the TSM configuration is incorrect, reactivate the configuration";
			case 1800:
				return "Device Busy";
			case 1801:
				return "Invalid operating system context. This can result from use of ADS blocks in different tasks. It may be possible to resolve this through multitasking synchronization in the PLC.";
			case 1802:
				return "Insufficient memory.";
			case 1803:
				return "Invalid parameter values.";
			case 1804:
				return "Device Not Found";
			case 1805:
				return "Device Syntax Error";
			case 1806:
				return "Objects do not match.";
			case 1807:
				return "Object already exists.";
			case 1808:
				return "Symbol not found. Check whether the variable name is correct, Note: the global variables in some PLC equipment are: .[Variable Name]";
			case 1809:
				return "Invalid symbol version. This can occur due to an online change. Create a new handle.";
			case 1810:
				return "Device (server) is in invalid state.";
			case 1811:
				return "AdsTransMode not supported.";
			case 1812:
				return "Device Notify Handle Invalid";
			case 1813:
				return "Notification client not registered.";
			case 1814:
				return "Device No More Handles";
			case 1815:
				return "Device Invalid Watch size";
			case 1816:
				return "Device Not Initialized";
			case 1817:
				return "Device TimeOut";
			case 1818:
				return "Device No Interface";
			case 1819:
				return "Device Invalid Interface";
			case 1820:
				return "Device Invalid CLSID";
			case 1821:
				return "Device Invalid Object ID";
			case 1822:
				return "Device Request Is Pending";
			case 1823:
				return "Device Request Is Aborted";
			case 1824:
				return "Device Signal Warning";
			case 1825:
				return "Device Invalid Array Index";
			case 1826:
				return "Device Symbol Not Active";
			case 1827:
				return "Device Access Denied";
			case 1828:
				return "Device Missing License";
			case 1829:
				return "Device License Expired";
			case 1830:
				return "Device License Exceeded";
			case 1831:
				return "Device License Invalid";
			case 1832:
				return "Device License System Id";
			case 1833:
				return "Device License No Time Limit";
			case 1834:
				return "Device License Future Issue";
			case 1835:
				return "Device License Time To Long";
			case 1836:
				return "Device Exception During Startup";
			case 1837:
				return "Device License Duplicated";
			case 1838:
				return "Device Signature Invalid";
			case 1839:
				return "Device Certificate Invalid";
			case 1840:
				return "Device License Oem Not Found";
			case 1841:
				return "Device License Restricted";
			case 1842:
				return "Device License Demo Denied";
			case 1843:
				return "Device Invalid Function Id";
			case 1844:
				return "Device Out Of Range";
			case 1845:
				return "Device Invalid Alignment";
			case 1846:
				return "Device License Platform";
			case 1847:
				return "Device Context Forward Passive Level";
			case 1848:
				return "Device Context Forward Dispatch Level";
			case 1849:
				return "Device Context Forward RealTime";
			case 1850:
				return "Device Certificate Entrust";
			case 1856:
				return "ClientError";
			case 1857:
				return "Client Invalid Parameter";
			case 1858:
				return "Client List Empty";
			case 1859:
				return "Client Variable In Use";
			case 1860:
				return "Client Duplicate InvokeID";
			case 1861:
				return "Timeout has occurred – the remote terminal is not responding in the specified ADS timeout. The route setting of the remote terminal may be configured incorrectly.";
			case 1862:
				return "ClientW32OR";
			case 1863:
				return "Client Timeout Invalid";
			case 1864:
				return "Client Port Not Open";
			case 1865:
				return "Client No Ams Addr";
			case 1872:
				return "Client Sync Internal";
			case 1873:
				return "Client Add Hash";
			case 1874:
				return "Client Remove Hash";
			case 1875:
				return "Client No More Symbols";
			case 1876:
				return "Client Response Invalid";
			case 1877:
				return "Client Port Locked";
			case 32768:
				return "ClientQueueFull";
			case 10060:
				return "A connection timeout has occurred - error while establishing the connection, because the remote terminal did not respond properly after a certain period of time";
			case 10061:
				return "WSA_ConnRefused";
			case 10065:
				return "No route to host - a socket operation referred to an unavailable host.";
			default:
				return StringResources.Language.UnknownError;
			}
		}
	}
}
