using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using HslCommunication.BasicFramework;
using HslCommunication.Core;

namespace HslCommunication.Profinet.Omron.Helper
{
	/// <summary>
	/// 欧姆龙的OmronHostLinkCMode的辅助类方法
	/// </summary>
	public class OmronHostLinkCModeHelper
	{
		/// <inheritdoc cref="M:HslCommunication.Profinet.Omron.OmronFinsNet.Read(System.String,System.UInt16)" />
		/// <remarks>
		/// 地址里可以额外指定单元号信息，例如 s=2;D100
		/// </remarks>
		public static OperateResult<byte[]> Read(IReadWriteDevice omron, byte unitNumber, string address, ushort length)
		{
			byte unitNumber2 = (byte)HslHelper.ExtractParameter(ref address, "s", unitNumber);
			OperateResult<List<byte[]>> operateResult = BuildReadCommand(address, length, false);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<byte[]>(operateResult);
			}
			List<byte> list = new List<byte>();
			for (int i = 0; i < operateResult.Content.Count; i++)
			{
				OperateResult<byte[]> operateResult2 = omron.ReadFromCoreServer(PackCommand(operateResult.Content[i], unitNumber2));
				if (!operateResult2.IsSuccess)
				{
					return OperateResult.CreateFailedResult<byte[]>(operateResult2);
				}
				OperateResult<byte[]> operateResult3 = ResponseValidAnalysis(operateResult2.Content, true);
				if (!operateResult3.IsSuccess)
				{
					return OperateResult.CreateFailedResult<byte[]>(operateResult3);
				}
				list.AddRange(operateResult3.Content);
			}
			return OperateResult.CreateSuccessResult(list.ToArray());
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.Omron.Helper.OmronHostLinkCModeHelper.Read(HslCommunication.Core.IReadWriteDevice,System.Byte,System.String,System.UInt16)" />
		public static async Task<OperateResult<byte[]>> ReadAsync(IReadWriteDevice omron, byte unitNumber, string address, ushort length)
		{
			byte station = (byte)HslHelper.ExtractParameter(ref address, "s", unitNumber);
			OperateResult<List<byte[]>> command = BuildReadCommand(address, length, false);
			if (!command.IsSuccess)
			{
				return OperateResult.CreateFailedResult<byte[]>(command);
			}
			List<byte> array = new List<byte>();
			for (int i = 0; i < command.Content.Count; i++)
			{
				OperateResult<byte[]> read = await omron.ReadFromCoreServerAsync(PackCommand(command.Content[i], station));
				if (!read.IsSuccess)
				{
					return OperateResult.CreateFailedResult<byte[]>(read);
				}
				OperateResult<byte[]> valid = ResponseValidAnalysis(read.Content, true);
				if (!valid.IsSuccess)
				{
					return OperateResult.CreateFailedResult<byte[]>(valid);
				}
				array.AddRange(valid.Content);
			}
			return OperateResult.CreateSuccessResult(array.ToArray());
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.Omron.OmronFinsNet.Write(System.String,System.Byte[])" />
		/// <remarks>
		/// 地址里可以额外指定单元号信息，例如 s=2;D100
		/// </remarks>
		public static OperateResult Write(IReadWriteDevice omron, byte unitNumber, string address, byte[] value)
		{
			byte unitNumber2 = (byte)HslHelper.ExtractParameter(ref address, "s", unitNumber);
			OperateResult<List<byte[]>> operateResult = BuildWriteWordCommand(address, value);
			if (!operateResult.IsSuccess)
			{
				return operateResult;
			}
			for (int i = 0; i < operateResult.Content.Count; i++)
			{
				OperateResult<byte[]> operateResult2 = omron.ReadFromCoreServer(PackCommand(operateResult.Content[i], unitNumber2));
				if (!operateResult2.IsSuccess)
				{
					return operateResult2;
				}
				OperateResult<byte[]> operateResult3 = ResponseValidAnalysis(operateResult2.Content, false);
				if (!operateResult3.IsSuccess)
				{
					return operateResult3;
				}
			}
			return OperateResult.CreateSuccessResult();
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.Omron.Helper.OmronHostLinkCModeHelper.Write(HslCommunication.Core.IReadWriteDevice,System.Byte,System.String,System.Byte[])" />
		public static async Task<OperateResult> WriteAsync(IReadWriteDevice omron, byte unitNumber, string address, byte[] value)
		{
			byte station = (byte)HslHelper.ExtractParameter(ref address, "s", unitNumber);
			OperateResult<List<byte[]>> command = BuildWriteWordCommand(address, value);
			if (!command.IsSuccess)
			{
				return command;
			}
			for (int i = 0; i < command.Content.Count; i++)
			{
				OperateResult<byte[]> read = await omron.ReadFromCoreServerAsync(PackCommand(command.Content[i], station));
				if (!read.IsSuccess)
				{
					return read;
				}
				OperateResult<byte[]> valid = ResponseValidAnalysis(read.Content, false);
				if (!valid.IsSuccess)
				{
					return valid;
				}
			}
			return OperateResult.CreateSuccessResult();
		}

		/// <summary>
		/// <b>[商业授权]</b> 读取PLC的当前的型号信息<br />
		/// <b>[Authorization]</b> Read the current model information of the PLC
		/// </summary>
		/// <param name="omron">PLC连接对象</param>
		/// <param name="unitNumber">站号信息</param>
		/// <returns>型号</returns>
		public static OperateResult<string> ReadPlcType(IReadWriteDevice omron, byte unitNumber)
		{
			if(false)
			{
				return new OperateResult<string>(StringResources.Language.InsufficientPrivileges);
			}
			OperateResult<byte[]> operateResult = omron.ReadFromCoreServer(PackCommand(Encoding.ASCII.GetBytes("MM"), unitNumber));
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<string>(operateResult);
			}
			int num = Convert.ToInt32(Encoding.ASCII.GetString(operateResult.Content, 5, 2), 16);
			if (num > 0)
			{
				return new OperateResult<string>(num, "Unknown Error");
			}
			return GetModelText(Encoding.ASCII.GetString(operateResult.Content, 7, 2));
		}

		/// <summary>
		/// <b>[商业授权]</b> 读取PLC当前的操作模式，0: 编程模式  1: 运行模式  2: 监视模式<br />
		/// <b>[Authorization]</b> Reads the Operation mode of the CPU Unit. 0: PROGRAM mode  1: RUN mode  2: MONITOR mode
		/// </summary>
		/// <param name="omron">PLC连接对象</param>
		/// <param name="unitNumber">站号信息</param>
		/// <returns>0: 编程模式  1: 运行模式  2: 监视模式</returns>
		public static OperateResult<int> ReadPlcMode(IReadWriteDevice omron, byte unitNumber)
		{
			if(false)
			{
				return new OperateResult<int>(StringResources.Language.InsufficientPrivileges);
			}
			OperateResult<byte[]> operateResult = omron.ReadFromCoreServer(PackCommand(Encoding.ASCII.GetBytes("MS"), unitNumber));
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<int>(operateResult);
			}
			int num = Convert.ToInt32(Encoding.ASCII.GetString(operateResult.Content, 5, 2), 16);
			if (num > 0)
			{
				return new OperateResult<int>(num, "Unknown Error");
			}
			return OperateResult.CreateSuccessResult(Encoding.ASCII.GetString(operateResult.Content, 7, 4).ToHexBytes()[0] & 3);
		}

		/// <summary>
		/// <b>[商业授权]</b> 将当前PLC的模式变更为指定的模式，0: 编程模式  1: 运行模式  2: 监视模式<br />
		/// <b>[Authorization]</b> Change the current PLC mode to the specified mode, 0: programming mode 1: running mode 2: monitoring mode
		/// </summary>
		/// <param name="omron">PLC连接对象</param>
		/// <param name="unitNumber">站号信息</param>
		/// <param name="mode">0: 编程模式  1: 运行模式  2: 监视模式</param>
		/// <returns>是否变更成功</returns>
		public static OperateResult ChangePlcMode(IReadWriteDevice omron, byte unitNumber, byte mode)
		{
			if(false)
			{
				return new OperateResult<int>(StringResources.Language.InsufficientPrivileges);
			}
			OperateResult<byte[]> operateResult = omron.ReadFromCoreServer(PackCommand(Encoding.ASCII.GetBytes("SC" + mode.ToString("X2")), unitNumber));
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<int>(operateResult);
			}
			int num = Convert.ToInt32(Encoding.ASCII.GetString(operateResult.Content, 5, 2), 16);
			if (num > 0)
			{
				return new OperateResult<int>(num, "Unknown Error");
			}
			return OperateResult.CreateSuccessResult();
		}

		private static OperateResult<string, int> GetEMAddress(string address, int start, bool isRead)
		{
			string[] array = address.SplitDot();
			int num = Convert.ToInt32(array[0].Substring(start), 16);
			return OperateResult.CreateSuccessResult((isRead ? "RE" : "WE") + Encoding.ASCII.GetString(SoftBasic.BuildAsciiBytesFrom((byte)num)), (int)ushort.Parse(array[1]));
		}

		/// <summary>
		/// 解析欧姆龙的数据地址，参考来源是Omron手册第188页，比如D100， E1.100<br />
		/// Analyze Omron's data address, the reference source is page 188 of the Omron manual, such as D100, E1.100
		/// </summary>
		/// <param name="address">数据地址</param>
		/// <param name="isBit">是否是位地址</param>
		/// <param name="isRead">是否读取</param>
		/// <returns>解析后的结果地址对象</returns>
		public static OperateResult<string, int> AnalysisAddress(string address, bool isBit, bool isRead)
		{
			try
			{
				if (address.StartsWith("DM", StringComparison.OrdinalIgnoreCase))
				{
					return OperateResult.CreateSuccessResult(isRead ? "RD" : "WD", (int)ushort.Parse(address.Substring(2)));
				}
				if (address.StartsWith("LR", StringComparison.OrdinalIgnoreCase))
				{
					return OperateResult.CreateSuccessResult(isRead ? "RL" : "WL", (int)ushort.Parse(address.Substring(2)));
				}
				if (address.StartsWith("HR", StringComparison.OrdinalIgnoreCase))
				{
					return OperateResult.CreateSuccessResult(isRead ? "RH" : "WH", (int)ushort.Parse(address.Substring(2)));
				}
				if (address.StartsWith("AR", StringComparison.OrdinalIgnoreCase))
				{
					return OperateResult.CreateSuccessResult(isRead ? "RJ" : "WJ", (int)ushort.Parse(address.Substring(2)));
				}
				if (address.StartsWith("CIO", StringComparison.OrdinalIgnoreCase))
				{
					return OperateResult.CreateSuccessResult(isRead ? "RR" : "WR", (int)ushort.Parse(address.Substring(3)));
				}
				if (address.StartsWith("TIM", StringComparison.OrdinalIgnoreCase))
				{
					return OperateResult.CreateSuccessResult(isRead ? "RC" : "WC", (int)ushort.Parse(address.Substring(3)));
				}
				if (address.StartsWith("CNT", StringComparison.OrdinalIgnoreCase))
				{
					return OperateResult.CreateSuccessResult(isRead ? "RC" : "WC", ushort.Parse(address.Substring(3)) + 2048);
				}
				if (address.StartsWith("EM", StringComparison.OrdinalIgnoreCase))
				{
					return GetEMAddress(address, 2, isRead);
				}
				switch (address[0])
				{
				case 'D':
				case 'd':
					return OperateResult.CreateSuccessResult(isRead ? "RD" : "WD", (int)ushort.Parse(address.Substring(1)));
				case 'C':
				case 'c':
					return OperateResult.CreateSuccessResult(isRead ? "RR" : "WR", (int)ushort.Parse(address.Substring(1)));
				case 'H':
				case 'h':
					return OperateResult.CreateSuccessResult(isRead ? "RH" : "WH", (int)ushort.Parse(address.Substring(1)));
				case 'A':
				case 'a':
					return OperateResult.CreateSuccessResult(isRead ? "RJ" : "WJ", (int)ushort.Parse(address.Substring(1)));
				case 'E':
				case 'e':
					return GetEMAddress(address, 1, isRead);
				default:
					throw new Exception(StringResources.Language.NotSupportedDataType);
				}
			}
			catch (Exception ex)
			{
				return new OperateResult<string, int>(ex.Message);
			}
		}

		/// <summary>
		/// 根据读取的地址，长度，是否位读取创建Fins协议的核心报文<br />
		/// According to the read address, length, whether to read the core message that creates the Fins protocol
		/// </summary>
		/// <param name="address">地址，具体格式请参照示例说明</param>
		/// <param name="length">读取的数据长度</param>
		/// <param name="isBit">是否使用位读取</param>
		/// <returns>带有成功标识的Fins核心报文</returns>
		public static OperateResult<List<byte[]>> BuildReadCommand(string address, ushort length, bool isBit)
		{
			OperateResult<string, int> operateResult = AnalysisAddress(address, isBit, true);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<List<byte[]>>(operateResult);
			}
			int[] array = SoftBasic.SplitIntegerToArray(length, 30);
			List<byte[]> list = new List<byte[]>();
			for (int i = 0; i < array.Length; i++)
			{
				StringBuilder stringBuilder = new StringBuilder();
				stringBuilder.Append(operateResult.Content1);
				stringBuilder.Append(operateResult.Content2.ToString("D4"));
				stringBuilder.Append(array[i].ToString("D4"));
				list.Add(Encoding.ASCII.GetBytes(stringBuilder.ToString()));
				operateResult.Content2 += array[i];
			}
			return OperateResult.CreateSuccessResult(list);
		}

		/// <summary>
		/// 根据读取的地址，长度，是否位读取创建Fins协议的核心报文<br />
		/// According to the read address, length, whether to read the core message that creates the Fins protocol
		/// </summary>
		/// <param name="address">地址，具体格式请参照示例说明</param>
		/// <param name="value">等待写入的数据</param>
		/// <returns>带有成功标识的Fins核心报文</returns>
		public static OperateResult<List<byte[]>> BuildWriteWordCommand(string address, byte[] value)
		{
			OperateResult<string, int> operateResult = AnalysisAddress(address, false, false);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<List<byte[]>>(operateResult);
			}
			List<byte[]> list = SoftBasic.ArraySplitByLength(value, 60);
			List<byte[]> list2 = new List<byte[]>();
			for (int i = 0; i < list.Count; i++)
			{
				StringBuilder stringBuilder = new StringBuilder();
				stringBuilder.Append(operateResult.Content1);
				stringBuilder.Append(operateResult.Content2.ToString("D4"));
				if (list[i].Length != 0)
				{
					stringBuilder.Append(list[i].ToHexString());
				}
				list2.Add(Encoding.ASCII.GetBytes(stringBuilder.ToString()));
				operateResult.Content2 += list[i].Length / 2;
			}
			return OperateResult.CreateSuccessResult(list2);
		}

		/// <summary>
		/// 验证欧姆龙的Fins-TCP返回的数据是否正确的数据，如果正确的话，并返回所有的数据内容
		/// </summary>
		/// <param name="response">来自欧姆龙返回的数据内容</param>
		/// <param name="isRead">是否读取</param>
		/// <returns>带有是否成功的结果对象</returns>
		public static OperateResult<byte[]> ResponseValidAnalysis(byte[] response, bool isRead)
		{
			if (response.Length >= 11)
			{
				try
				{
					int num = Convert.ToInt32(Encoding.ASCII.GetString(response, 5, 2), 16);
					byte[] array = null;
					if (response.Length > 11)
					{
						array = Encoding.ASCII.GetString(response, 7, response.Length - 11).ToHexBytes();
					}
					if (num > 0)
					{
						return new OperateResult<byte[]>
						{
							ErrorCode = num,
							Message = GetErrorMessage(num),
							Content = array
						};
					}
					return OperateResult.CreateSuccessResult(array);
				}
				catch (Exception ex)
				{
					return new OperateResult<byte[]>("ResponseValidAnalysis failed: " + ex.Message + " Source: " + response.ToHexString(' '));
				}
			}
			return new OperateResult<byte[]>(StringResources.Language.OmronReceiveDataError);
		}

		/// <summary>
		/// 将普通的指令打包成完整的指令
		/// </summary>
		/// <param name="cmd">fins指令</param>
		/// <param name="unitNumber">站号信息</param>
		/// <returns>完整的质量</returns>
		public static byte[] PackCommand(byte[] cmd, byte unitNumber)
		{
			byte[] array = new byte[7 + cmd.Length];
			array[0] = 64;
			array[1] = SoftBasic.BuildAsciiBytesFrom(unitNumber)[0];
			array[2] = SoftBasic.BuildAsciiBytesFrom(unitNumber)[1];
			array[array.Length - 2] = 42;
			array[array.Length - 1] = 13;
			cmd.CopyTo(array, 3);
			int num = array[0];
			for (int i = 1; i < array.Length - 4; i++)
			{
				num ^= array[i];
			}
			array[array.Length - 4] = SoftBasic.BuildAsciiBytesFrom((byte)num)[0];
			array[array.Length - 3] = SoftBasic.BuildAsciiBytesFrom((byte)num)[1];
			return array;
		}

		/// <summary>
		/// 获取model的字符串描述信息
		/// </summary>
		/// <param name="model">型号代码</param>
		/// <returns>是否解析成功</returns>
		public static OperateResult<string> GetModelText(string model)
		{
			switch (model)
			{
			case "30":
				return OperateResult.CreateSuccessResult("CS/CJ");
			case "01":
				return OperateResult.CreateSuccessResult("C250");
			case "02":
				return OperateResult.CreateSuccessResult("C500");
			case "03":
				return OperateResult.CreateSuccessResult("C120/C50");
			case "09":
				return OperateResult.CreateSuccessResult("C250F");
			case "0A":
				return OperateResult.CreateSuccessResult("C500F");
			case "0B":
				return OperateResult.CreateSuccessResult("C120F");
			case "0E":
				return OperateResult.CreateSuccessResult("C2000");
			case "10":
				return OperateResult.CreateSuccessResult("C1000H");
			case "11":
				return OperateResult.CreateSuccessResult("C2000H/CQM1/CPM1");
			case "12":
				return OperateResult.CreateSuccessResult("C20H/C28H/C40H, C200H, C200HS, C200HX/HG/HE (-ZE)");
			case "20":
				return OperateResult.CreateSuccessResult("CV500");
			case "21":
				return OperateResult.CreateSuccessResult("CV1000");
			case "22":
				return OperateResult.CreateSuccessResult("CV2000");
			case "40":
				return OperateResult.CreateSuccessResult("CVM1-CPU01-E");
			case "41":
				return OperateResult.CreateSuccessResult("CVM1-CPU11-E");
			case "42":
				return OperateResult.CreateSuccessResult("CVM1-CPU21-E");
			default:
				return new OperateResult<string>("Unknown model, model code:" + model);
			}
		}

		/// <summary>
		/// 根据错误码的信息，返回错误的具体描述的文本<br />
		/// According to the information of the error code, return the text of the specific description of the error
		/// </summary>
		/// <param name="err">错误码</param>
		/// <returns>错误的描述文本</returns>
		public static string GetErrorMessage(int err)
		{
			switch (err)
			{
			case 1:
				return "Not executable in RUN mode";
			case 2:
				return "Not executable in MONITOR mode";
			case 3:
				return "UM write-protected";
			case 4:
				return "Address over: The program address setting in an read or write command is above the highest program address.";
			case 11:
				return "Not executable in PROGRAM mode";
			case 19:
				return "The FCS is wrong.";
			case 20:
				return "The command format is wrong, or a command that cannot be divided has been divided, or the frame length is smaller than the minimum length for the applicable command.";
			case 21:
				return "1. The data is outside of the specified range or too long. 2.Hexadecimal data has not been specified.";
			case 22:
				return "Command not supported: The operand specified in an SV Read or SV Change command does not exist in the program.";
			case 24:
				return "Frame length error: The maximum frame length of 131 bytes was exceeded.";
			case 25:
				return "Not executable: The read SV exceeded 9,999, or an I/O memory batch read was executed when items to read were not registered for composite command, or access right was not obtained.";
			case 32:
				return "Could not create I/O table";
			case 33:
				return "Not executable due to CPU Unit CPU error( See note.)";
			case 35:
				return "User memory protected, The UM is read-protected or writeprotected.";
			case 163:
				return "Aborted due to FCS error in transmission data";
			case 164:
				return "Aborted due to format error in transmission data";
			case 165:
				return "Aborted due to entry number data error in transmission data";
			case 168:
				return "Aborted due to frame length error in transmission data";
			default:
				return StringResources.Language.UnknownError;
			}
		}
	}
}
