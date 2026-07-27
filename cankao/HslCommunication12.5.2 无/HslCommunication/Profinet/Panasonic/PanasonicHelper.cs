using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HslCommunication.BasicFramework;
using HslCommunication.Core;

namespace HslCommunication.Profinet.Panasonic
{
	/// <summary>
	/// 松下PLC的辅助类，提供了基本的辅助方法，用于解析地址，计算校验和，创建报文<br />
	/// The auxiliary class of Panasonic PLC provides basic auxiliary methods for parsing addresses, calculating checksums, and creating messages
	/// </summary>
	public class PanasonicHelper
	{
		private static string CalculateCrc(StringBuilder sb)
		{
			byte b = 0;
			b = (byte)sb[0];
			for (int i = 1; i < sb.Length; i++)
			{
				b ^= (byte)sb[i];
			}
			return SoftBasic.ByteToHexString(new byte[1] { b });
		}

		/// <summary>
		/// 位地址转换方法，101等同于10.1等同于10*16+1=161<br />
		/// Bit address conversion method, 101 is equivalent to 10.1 is equivalent to 10 * 16 + 1 = 161
		/// </summary>
		/// <param name="address">地址信息</param>
		/// <param name="fromBase">倍率信息</param>
		/// <returns>实际的位地址信息</returns>
		public static int CalculateComplexAddress(string address, int fromBase = 16)
		{
			int num = 0;
			if (address.IndexOf(".") < 0)
			{
				if (address.Length == 1)
				{
					return Convert.ToInt32(address, fromBase);
				}
				return Convert.ToInt32(address.Substring(0, address.Length - 1)) * fromBase + Convert.ToInt32(address.Substring(address.Length - 1), fromBase);
			}
			return Convert.ToInt32(address.Substring(0, address.IndexOf("."))) * fromBase + HslHelper.CalculateBitStartIndex(address.Substring(address.IndexOf(".") + 1));
		}

		/// <summary>
		/// 解析数据地址，解析出地址类型，起始地址<br />
		/// Parse the data address, resolve the address type, start address
		/// </summary>
		/// <param name="address">数据地址</param>
		/// <returns>解析出地址类型，起始地址</returns>
		public static OperateResult<string, int> AnalysisAddress(string address)
		{
			OperateResult<string, int> operateResult = new OperateResult<string, int>();
			try
			{
				operateResult.Content2 = 0;
				if (address.StartsWith("IX", StringComparison.OrdinalIgnoreCase))
				{
					operateResult.Content1 = "IX";
					operateResult.Content2 = int.Parse(address.Substring(2));
				}
				else if (address.StartsWith("IY") || address.StartsWith("iy"))
				{
					operateResult.Content1 = "IY";
					operateResult.Content2 = int.Parse(address.Substring(2));
				}
				else if (address.StartsWith("ID") || address.StartsWith("id"))
				{
					operateResult.Content1 = "ID";
					operateResult.Content2 = int.Parse(address.Substring(2));
				}
				else if (address.StartsWith("SR") || address.StartsWith("sr"))
				{
					operateResult.Content1 = "SR";
					operateResult.Content2 = CalculateComplexAddress(address.Substring(2));
				}
				else if (address.StartsWith("LD") || address.StartsWith("ld"))
				{
					operateResult.Content1 = "LD";
					operateResult.Content2 = int.Parse(address.Substring(2));
				}
				else if (address.StartsWithAndNumber("DT"))
				{
					operateResult.Content1 = "D";
					operateResult.Content2 = int.Parse(address.Substring(2));
				}
				else if (address[0] == 'X' || address[0] == 'x')
				{
					operateResult.Content1 = "X";
					operateResult.Content2 = CalculateComplexAddress(address.Substring(1));
				}
				else if (address[0] == 'Y' || address[0] == 'y')
				{
					operateResult.Content1 = "Y";
					operateResult.Content2 = CalculateComplexAddress(address.Substring(1));
				}
				else if (address[0] == 'R' || address[0] == 'r')
				{
					operateResult.Content1 = "R";
					operateResult.Content2 = CalculateComplexAddress(address.Substring(1));
				}
				else if (address[0] == 'T' || address[0] == 't')
				{
					operateResult.Content1 = "T";
					operateResult.Content2 = int.Parse(address.Substring(1));
				}
				else if (address[0] == 'C' || address[0] == 'c')
				{
					operateResult.Content1 = "C";
					operateResult.Content2 = int.Parse(address.Substring(1));
				}
				else if (address[0] == 'L' || address[0] == 'l')
				{
					operateResult.Content1 = "L";
					operateResult.Content2 = CalculateComplexAddress(address.Substring(1));
				}
				else if (address[0] == 'D' || address[0] == 'd')
				{
					operateResult.Content1 = "D";
					operateResult.Content2 = int.Parse(address.Substring(1));
				}
				else if (address[0] == 'F' || address[0] == 'f')
				{
					operateResult.Content1 = "F";
					operateResult.Content2 = int.Parse(address.Substring(1));
				}
				else if (address[0] == 'S' || address[0] == 's')
				{
					operateResult.Content1 = "S";
					operateResult.Content2 = int.Parse(address.Substring(1));
				}
				else
				{
					if (address[0] != 'K' && address[0] != 'k')
					{
						throw new Exception(StringResources.Language.NotSupportedDataType);
					}
					operateResult.Content1 = "K";
					operateResult.Content2 = int.Parse(address.Substring(1));
				}
			}
			catch (Exception ex)
			{
				operateResult.Message = ex.Message;
				return operateResult;
			}
			operateResult.IsSuccess = true;
			return operateResult;
		}

		/// <summary>
		/// 将松下的命令打包成带有%开头，CRC校验，CR结尾的完整的命令报文。如果参数 <c>useExpandedHeader</c> 设置为 <c>Ture</c>，则命令头使用 &lt; 开头
		/// </summary>
		/// <param name="station">站号信息</param>
		/// <param name="cmd">松下的命令。例如 RCSR100F</param>
		/// <param name="useExpandedHeader">设置是否使用扩展的命令头消息</param>
		/// <returns>原始的字节数组的命令</returns>
		public static OperateResult<byte[]> PackPanasonicCommand(byte station, string cmd, bool useExpandedHeader)
		{
			StringBuilder stringBuilder = new StringBuilder(useExpandedHeader ? "<" : "%");
			stringBuilder.Append(station.ToString("X2"));
			stringBuilder.Append(cmd);
			stringBuilder.Append(CalculateCrc(stringBuilder));
			stringBuilder.Append('\r');
			return OperateResult.CreateSuccessResult(Encoding.ASCII.GetBytes(stringBuilder.ToString()));
		}

		private static OperateResult AppendCoil(StringBuilder sb, string address)
		{
			OperateResult<string, int> operateResult = AnalysisAddress(address);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<byte[]>(operateResult);
			}
			sb.Append(operateResult.Content1);
			if (operateResult.Content1 == "X" || operateResult.Content1 == "Y" || operateResult.Content1 == "R" || operateResult.Content1 == "L")
			{
				sb.Append((operateResult.Content2 / 16).ToString("D3"));
				sb.Append((operateResult.Content2 % 16).ToString("X1"));
			}
			else
			{
				if (!(operateResult.Content1 == "T") && !(operateResult.Content1 == "C"))
				{
					return new OperateResult<byte[]>(StringResources.Language.NotSupportedDataType);
				}
				sb.Append("0");
				sb.Append(operateResult.Content2.ToString("D3"));
			}
			return OperateResult.CreateSuccessResult();
		}

		/// <summary>
		/// 创建读取离散触点的报文指令<br />
		/// Create message instructions for reading discrete contacts
		/// </summary>
		/// <param name="station">站号信息</param>
		/// <param name="address">地址信息</param>
		/// <returns>包含是否成功的结果对象</returns>
		public static OperateResult<byte[]> BuildReadOneCoil(byte station, string address)
		{
			if (address == null)
			{
				return new OperateResult<byte[]>("address is not allowed null");
			}
			if (address.Length < 1 || address.Length > 8)
			{
				return new OperateResult<byte[]>("length must be 1-8");
			}
			StringBuilder stringBuilder = new StringBuilder("#RCS");
			OperateResult operateResult = AppendCoil(stringBuilder, address);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<byte[]>(operateResult);
			}
			return PackPanasonicCommand(station, stringBuilder.ToString(), false);
		}

		/// <summary>
		/// 创建读取多个bool值得报文命令
		/// </summary>
		/// <param name="station">站号信息</param>
		/// <param name="address">等待读取的地址数组</param>
		/// <returns>包含是否成功的结果对象</returns>
		public static OperateResult<List<byte[]>> BuildReadCoils(byte station, string[] address)
		{
			List<byte[]> list = new List<byte[]>();
			List<string[]> list2 = SoftBasic.ArraySplitByLength(address, 8);
			for (int i = 0; i < list2.Count; i++)
			{
				StringBuilder stringBuilder = new StringBuilder("#RCP");
				stringBuilder.Append(list2[i].Length.ToString());
				for (int j = 0; j < list2[i].Length; j++)
				{
					OperateResult operateResult = AppendCoil(stringBuilder, list2[i][j]);
					if (!operateResult.IsSuccess)
					{
						return OperateResult.CreateFailedResult<List<byte[]>>(operateResult);
					}
				}
				list.Add(PackPanasonicCommand(station, stringBuilder.ToString(), false).Content);
			}
			return OperateResult.CreateSuccessResult(list);
		}

		/// <summary>
		/// 创建写入离散触点的报文指令<br />
		/// Create message instructions to write discrete contacts
		/// </summary>
		/// <param name="station">站号信息</param>
		/// <param name="address">地址信息</param>
		/// <param name="value">bool值数组</param>
		/// <returns>包含是否成功的结果对象</returns>
		public static OperateResult<byte[]> BuildWriteOneCoil(byte station, string address, bool value)
		{
			StringBuilder stringBuilder = new StringBuilder("#WCS");
			OperateResult operateResult = AppendCoil(stringBuilder, address);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<byte[]>(operateResult);
			}
			stringBuilder.Append(value ? '1' : '0');
			return PackPanasonicCommand(station, stringBuilder.ToString(), false);
		}

		/// <summary>
		/// 创建写入多个离散触点的报文指令
		/// </summary>
		/// <param name="station">站号信息</param>
		/// <param name="address">等待写入的地址列表</param>
		/// <param name="value">等待写入的值列表，长度应和地址长度一致</param>
		/// <returns>所有写入命令的报文列表</returns>
		public static OperateResult<List<byte[]>> BuildWriteCoils(byte station, string[] address, bool[] value)
		{
			if (address == null)
			{
				return new OperateResult<List<byte[]>>("Parameter address can't be null");
			}
			if (value == null)
			{
				return new OperateResult<List<byte[]>>("Parameter value can't be null");
			}
			if (address.Length != value.Length)
			{
				return new OperateResult<List<byte[]>>("Parameter address and parameter value, length is not same!");
			}
			List<byte[]> list = new List<byte[]>();
			List<string[]> list2 = SoftBasic.ArraySplitByLength(address, 8);
			List<bool[]> list3 = SoftBasic.ArraySplitByLength(value, 8);
			for (int i = 0; i < list2.Count; i++)
			{
				StringBuilder stringBuilder = new StringBuilder("#WCP");
				stringBuilder.Append(list2[i].Length.ToString());
				for (int j = 0; j < list2[i].Length; j++)
				{
					OperateResult operateResult = AppendCoil(stringBuilder, list2[i][j]);
					if (!operateResult.IsSuccess)
					{
						return OperateResult.CreateFailedResult<List<byte[]>>(operateResult);
					}
					stringBuilder.Append(list3[i][j] ? '1' : '0');
				}
				list.Add(PackPanasonicCommand(station, stringBuilder.ToString(), false).Content);
			}
			return OperateResult.CreateSuccessResult(list);
		}

		/// <summary>
		/// 创建批量读取触点的报文指令<br />
		/// Create message instructions for batch reading contacts
		/// </summary>
		/// <param name="station">站号信息</param>
		/// <param name="address">地址信息</param>
		/// <param name="length">数据长度</param>
		/// <param name="isBit">是否进行位为单位</param>
		/// <returns>包含是否成功的结果对象</returns>
		public static OperateResult<List<byte[]>> BuildReadCommand(byte station, string address, ushort length, bool isBit)
		{
			if (address == null)
			{
				return new OperateResult<List<byte[]>>(StringResources.Language.PanasonicAddressParameterCannotBeNull);
			}
			OperateResult<string, int> operateResult = AnalysisAddress(address);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<List<byte[]>>(operateResult);
			}
			List<byte[]> list = new List<byte[]>();
			if (isBit)
			{
				length += (ushort)(operateResult.Content2 % 16);
				operateResult.Content2 -= operateResult.Content2 % 16;
				int[] array = SoftBasic.SplitIntegerToArray(length, 400);
				foreach (int num in array)
				{
					StringBuilder stringBuilder = new StringBuilder("#");
					if (operateResult.Content1 == "X" || operateResult.Content1 == "Y" || operateResult.Content1 == "R" || operateResult.Content1 == "L")
					{
						stringBuilder.Append("RCC");
						stringBuilder.Append(operateResult.Content1);
						int num2 = operateResult.Content2 / 16;
						int num3 = (operateResult.Content2 + num - 1) / 16;
						stringBuilder.Append(num2.ToString("D4"));
						stringBuilder.Append(num3.ToString("D4"));
						operateResult.Content2 += num;
						list.Add(PackPanasonicCommand(station, stringBuilder.ToString(), false).Content);
						continue;
					}
					return new OperateResult<List<byte[]>>("Bit read only support X,Y,R,L");
				}
				return OperateResult.CreateSuccessResult(list);
			}
			int[] array2 = SoftBasic.SplitIntegerToArray(length, 500);
			foreach (int num4 in array2)
			{
				StringBuilder stringBuilder2 = new StringBuilder("#");
				if (operateResult.Content1 == "X" || operateResult.Content1 == "Y" || operateResult.Content1 == "R" || operateResult.Content1 == "L")
				{
					stringBuilder2.Append("RCC");
					stringBuilder2.Append(operateResult.Content1);
					int num5 = operateResult.Content2 / 16;
					int num6 = (operateResult.Content2 + (num4 - 1) * 16) / 16;
					stringBuilder2.Append(num5.ToString("D4"));
					stringBuilder2.Append(num6.ToString("D4"));
					operateResult.Content2 += num4 * 16;
				}
				else if (operateResult.Content1 == "D" || operateResult.Content1 == "LD" || operateResult.Content1 == "F")
				{
					stringBuilder2.Append("RD");
					stringBuilder2.Append(operateResult.Content1.Substring(0, 1));
					stringBuilder2.Append(operateResult.Content2.ToString("D5"));
					stringBuilder2.Append((operateResult.Content2 + num4 - 1).ToString("D5"));
					operateResult.Content2 += num4;
				}
				else if (operateResult.Content1 == "IX" || operateResult.Content1 == "IY" || operateResult.Content1 == "ID")
				{
					stringBuilder2.Append("RD");
					stringBuilder2.Append(operateResult.Content1);
					stringBuilder2.Append("000000000");
					operateResult.Content2 += num4;
				}
				else if (operateResult.Content1 == "C" || operateResult.Content1 == "T")
				{
					stringBuilder2.Append("RS");
					stringBuilder2.Append(operateResult.Content2.ToString("D4"));
					stringBuilder2.Append((operateResult.Content2 + num4 - 1).ToString("D4"));
					operateResult.Content2 += num4;
				}
				else
				{
					if (!(operateResult.Content1 == "K") && !(operateResult.Content1 == "S"))
					{
						return new OperateResult<List<byte[]>>(StringResources.Language.NotSupportedDataType);
					}
					stringBuilder2.Append("R");
					stringBuilder2.Append(operateResult.Content1);
					stringBuilder2.Append(operateResult.Content2.ToString("D4"));
					stringBuilder2.Append((operateResult.Content2 + num4 - 1).ToString("D4"));
					operateResult.Content2 += num4;
				}
				list.Add(PackPanasonicCommand(station, stringBuilder2.ToString(), num4 > 27).Content);
			}
			return OperateResult.CreateSuccessResult(list);
		}

		/// <summary>
		/// 创建批量读取触点的报文指令<br />
		/// Create message instructions for batch reading contacts
		/// </summary>
		/// <param name="station">设备站号</param>
		/// <param name="address">地址信息</param>
		/// <param name="values">数据值</param>
		/// <returns>包含是否成功的结果对象</returns>
		public static OperateResult<byte[]> BuildWriteCommand(byte station, string address, byte[] values)
		{
			if (address == null)
			{
				return new OperateResult<byte[]>(StringResources.Language.PanasonicAddressParameterCannotBeNull);
			}
			OperateResult<string, int> operateResult = AnalysisAddress(address);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<byte[]>(operateResult);
			}
			values = SoftBasic.ArrayExpandToLengthEven(values);
			short num = (short)(values.Length / 2);
			StringBuilder stringBuilder = new StringBuilder("#");
			if (operateResult.Content1 == "X" || operateResult.Content1 == "Y" || operateResult.Content1 == "R" || operateResult.Content1 == "L")
			{
				stringBuilder.Append("WCC");
				stringBuilder.Append(operateResult.Content1);
				int num2 = operateResult.Content2 / 16;
				int num3 = num2 + num - 1;
				stringBuilder.Append(num2.ToString("D4"));
				stringBuilder.Append(num3.ToString("D4"));
			}
			else if (operateResult.Content1 == "D" || operateResult.Content1 == "LD" || operateResult.Content1 == "F")
			{
				stringBuilder.Append("WD");
				stringBuilder.Append(operateResult.Content1.Substring(0, 1));
				stringBuilder.Append(operateResult.Content2.ToString("D5"));
				stringBuilder.Append((operateResult.Content2 + num - 1).ToString("D5"));
			}
			else if (operateResult.Content1 == "IX" || operateResult.Content1 == "IY" || operateResult.Content1 == "ID")
			{
				stringBuilder.Append("WD");
				stringBuilder.Append(operateResult.Content1);
				stringBuilder.Append(operateResult.Content2.ToString("D9"));
				stringBuilder.Append((operateResult.Content2 + num - 1).ToString("D9"));
			}
			else if (operateResult.Content1 == "C" || operateResult.Content1 == "T")
			{
				stringBuilder.Append("WS");
				stringBuilder.Append(operateResult.Content2.ToString("D4"));
				stringBuilder.Append((operateResult.Content2 + num - 1).ToString("D4"));
			}
			else if (operateResult.Content1 == "K" || operateResult.Content1 == "S")
			{
				stringBuilder.Append("W");
				stringBuilder.Append(operateResult.Content1);
				stringBuilder.Append(operateResult.Content2.ToString("D4"));
				stringBuilder.Append((operateResult.Content2 + num - 1).ToString("D4"));
			}
			stringBuilder.Append(SoftBasic.ByteToHexString(values));
			return PackPanasonicCommand(station, stringBuilder.ToString(), stringBuilder.Length > 112);
		}

		/// <summary>
		/// 构建获取PLC型号的报文命令
		/// </summary>
		/// <param name="station">站号信息</param>
		/// <returns>读取PLC型号的命令报文信息</returns>
		public static OperateResult<byte[]> BuildReadPlcModel(byte station)
		{
			StringBuilder stringBuilder = new StringBuilder("#");
			stringBuilder.Append("RT");
			return PackPanasonicCommand(station, stringBuilder.ToString(), stringBuilder.Length > 112);
		}

		/// <summary>
		/// 检查从PLC反馈的数据，并返回正确的数据内容<br />
		/// Check the data feedback from the PLC and return the correct data content
		/// </summary>
		/// <param name="response">反馈信号</param>
		/// <param name="parseData">是否解析数据内容部分</param>
		/// <returns>是否成功的结果信息</returns>
		public static OperateResult<byte[]> ExtraActualData(byte[] response, bool parseData = true)
		{
			if (response.Length < 9)
			{
				return new OperateResult<byte[]>(StringResources.Language.PanasonicReceiveLengthMustLargerThan9);
			}
			try
			{
				if (response[3] == 36)
				{
					byte[] array = new byte[response.Length - 9];
					if (array.Length != 0)
					{
						Array.Copy(response, 6, array, 0, array.Length);
						if (parseData)
						{
							array = SoftBasic.HexStringToBytes(Encoding.ASCII.GetString(array));
						}
					}
					return OperateResult.CreateSuccessResult(array);
				}
				if (response[3] == 33)
				{
					int err = int.Parse(Encoding.ASCII.GetString(response, 4, 2));
					return new OperateResult<byte[]>(err, GetErrorDescription(err));
				}
				return new OperateResult<byte[]>(StringResources.Language.UnknownError + " Source Data: " + SoftBasic.GetAsciiStringRender(response));
			}
			catch (Exception ex)
			{
				return new OperateResult<byte[]>("ExtraActualData failed: " + ex.Message + Environment.NewLine + "Source: " + SoftBasic.GetAsciiStringRender(response));
			}
		}

		/// <summary>
		/// 检查从PLC反馈的数据，并返回正确的数据内容<br />
		/// Check the data feedback from the PLC and return the correct data content
		/// </summary>
		/// <param name="response">反馈信号</param>
		/// <returns>是否成功的结果信息</returns>
		public static OperateResult<bool[]> ExtraActualBool(byte[] response)
		{
			if (response.Length < 9)
			{
				return new OperateResult<bool[]>(StringResources.Language.PanasonicReceiveLengthMustLargerThan9 + " Source: " + SoftBasic.GetAsciiStringRender(response));
			}
			if (response[3] == 36)
			{
				return OperateResult.CreateSuccessResult((from m in response.SelectMiddle(6, response.Length - 9)
					select m == 49).ToArray());
			}
			if (response[3] == 33)
			{
				int err = int.Parse(Encoding.ASCII.GetString(response, 4, 2));
				return new OperateResult<bool[]>(err, GetErrorDescription(err));
			}
			return new OperateResult<bool[]>(StringResources.Language.UnknownError + " Source: " + SoftBasic.GetAsciiStringRender(response));
		}

		/// <summary>
		/// 根据错误码获取到错误描述文本<br />
		/// Get the error description text according to the error code
		/// </summary>
		/// <param name="err">错误代码</param>
		/// <returns>字符信息</returns>
		public static string GetErrorDescription(int err)
		{
			switch (err)
			{
			case 20:
				return StringResources.Language.PanasonicMewStatus20;
			case 21:
				return StringResources.Language.PanasonicMewStatus21;
			case 22:
				return StringResources.Language.PanasonicMewStatus22;
			case 23:
				return StringResources.Language.PanasonicMewStatus23;
			case 24:
				return StringResources.Language.PanasonicMewStatus24;
			case 25:
				return StringResources.Language.PanasonicMewStatus25;
			case 26:
				return StringResources.Language.PanasonicMewStatus26;
			case 27:
				return StringResources.Language.PanasonicMewStatus27;
			case 28:
				return StringResources.Language.PanasonicMewStatus28;
			case 29:
				return StringResources.Language.PanasonicMewStatus29;
			case 30:
				return StringResources.Language.PanasonicMewStatus30;
			case 40:
				return StringResources.Language.PanasonicMewStatus40;
			case 41:
				return StringResources.Language.PanasonicMewStatus41;
			case 42:
				return StringResources.Language.PanasonicMewStatus42;
			case 43:
				return StringResources.Language.PanasonicMewStatus43;
			case 50:
				return StringResources.Language.PanasonicMewStatus50;
			case 51:
				return StringResources.Language.PanasonicMewStatus51;
			case 52:
				return StringResources.Language.PanasonicMewStatus52;
			case 53:
				return StringResources.Language.PanasonicMewStatus53;
			case 60:
				return StringResources.Language.PanasonicMewStatus60;
			case 61:
				return StringResources.Language.PanasonicMewStatus61;
			case 62:
				return StringResources.Language.PanasonicMewStatus62;
			case 63:
				return StringResources.Language.PanasonicMewStatus63;
			case 64:
				return StringResources.Language.PanasonicMewStatus64;
			case 65:
				return StringResources.Language.PanasonicMewStatus65;
			case 66:
				return StringResources.Language.PanasonicMewStatus66;
			case 67:
				return StringResources.Language.PanasonicMewStatus67;
			case 68:
				return StringResources.Language.PanasonicMewStatus68;
			case 71:
				return StringResources.Language.PanasonicMewStatus71;
			case 78:
				return StringResources.Language.PanasonicMewStatus78;
			case 80:
				return StringResources.Language.PanasonicMewStatus80;
			case 81:
				return StringResources.Language.PanasonicMewStatus81;
			case 90:
				return StringResources.Language.PanasonicMewStatus90;
			case 92:
				return StringResources.Language.PanasonicMewStatus92;
			default:
				return StringResources.Language.UnknownError;
			}
		}

		/// <summary>
		/// 根据MC的错误码去查找对象描述信息
		/// </summary>
		/// <param name="code">错误码</param>
		/// <returns>描述信息</returns>
		public static string GetMcErrorDescription(int code)
		{
			switch (code)
			{
			case 16433:
				return StringResources.Language.PanasonicMc4031;
			case 49233:
				return StringResources.Language.PanasonicMcC051;
			case 49238:
				return StringResources.Language.PanasonicMcC056;
			case 49241:
				return StringResources.Language.PanasonicMcC059;
			case 49243:
				return StringResources.Language.PanasonicMcC05B;
			case 49244:
				return StringResources.Language.PanasonicMcC05C;
			case 49247:
				return StringResources.Language.PanasonicMcC05F;
			case 49248:
				return StringResources.Language.PanasonicMcC060;
			case 49249:
				return StringResources.Language.PanasonicMcC061;
			default:
				return StringResources.Language.MelsecPleaseReferToManualDocument;
			}
		}
	}
}
