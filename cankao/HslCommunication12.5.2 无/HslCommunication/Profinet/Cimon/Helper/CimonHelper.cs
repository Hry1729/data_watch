using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HslCommunication.BasicFramework;
using HslCommunication.Core;

namespace HslCommunication.Profinet.Cimon.Helper
{
	/// <summary>
	/// 辅助帮助类对象
	/// </summary>
	public class CimonHelper
	{
		/// <summary>
		/// 所有支持的地址信息
		/// </summary>
		public const string AddressTypes = "YMLKFTCDSX";

		/// <summary>
		/// 根据错误码获取错误文本信息
		/// </summary>
		/// <param name="error">错误信息</param>
		/// <returns>错误消息</returns>
		public static string GetErrorText(int error)
		{
			switch (error)
			{
			case 0:
				return "No Error";
			case 1:
				return "Error in system (No link with CPU)";
			case 2:
				return "Invalid Device Prefix";
			case 3:
				return "Invalid Device Address";
			case 4:
				return "UDP_ERR_READ_DATASIZE (Error in requested data size)";
			case 5:
				return "UDP_ERR_BLOCK_SIZE (Over 16 requested blocks)";
			case 6:
				return "The case that buffer memory send an error in data and size";
			case 7:
				return "Over receiving buffer capacity";
			case 8:
				return "Over sending time";
			case 9:
				return "UDP_ERR_INVALID_HEADER (Error in header)";
			case 10:
				return "Error in Check-Sum (Check-Sum of received data)";
			case 11:
				return "Error in the information on Frame Length (Total received frame size)";
			case 12:
				return "UDP_ERR_WRITE_DATASIZE (Error in the size to write)";
			case 13:
				return "Unknown Bit Value (Error in Bit Write Data)";
			case 14:
				return "Unknown Command";
			case 15:
				return "Disabling state from writing";
			case 16:
				return "Error in CPU process";
			default:
				return StringResources.Language.UnknownError;
			}
		}

		/// <summary>
		/// 打包错误信息的报文
		/// </summary>
		/// <param name="frame"></param>
		/// <param name="err"></param>
		/// <returns></returns>
		public static byte[] PackErrorResponse(byte frame, int err)
		{
			return PackEntireCommand(true, frame, 65, new byte[4]
			{
				0,
				0,
				BitConverter.GetBytes(err)[1],
				BitConverter.GetBytes(err)[0]
			});
		}

		/// <summary>
		/// 打包完整的命令信息
		/// </summary>
		/// <param name="response">是否响应的报文</param>
		/// <param name="frame">站号信息</param>
		/// <param name="cmd">命令码</param>
		/// <param name="data">返回的数据信息</param>
		/// <returns>完成的报文</returns>
		public static byte[] PackEntireCommand(bool response, byte frame, byte cmd, byte[] data)
		{
			MemoryStream memoryStream = new MemoryStream();
			if (response)
			{
				memoryStream.Write(Encoding.ASCII.GetBytes("KDT_PLC_S"));
			}
			else
			{
				memoryStream.Write(Encoding.ASCII.GetBytes("KDT_PLC_M"));
			}
			if (response)
			{
				memoryStream.WriteByte((byte)(frame + 128));
			}
			else
			{
				memoryStream.WriteByte(frame);
			}
			memoryStream.WriteByte(cmd);
			memoryStream.WriteByte(0);
			if (cmd == 82 || cmd == 114)
			{
				if (response)
				{
					memoryStream.WriteByte(BitConverter.GetBytes(data.Length)[1]);
					memoryStream.WriteByte(BitConverter.GetBytes(data.Length)[0]);
				}
				else
				{
					memoryStream.WriteByte(BitConverter.GetBytes(data.Length)[0]);
				}
			}
			else
			{
				memoryStream.WriteByte(BitConverter.GetBytes(data.Length)[1]);
				memoryStream.WriteByte(BitConverter.GetBytes(data.Length)[0]);
			}
			if (data.Length != 0)
			{
				memoryStream.Write(data);
			}
			memoryStream.WriteByte(0);
			memoryStream.WriteByte(0);
			byte[] array = memoryStream.ToArray();
			int num = 0;
			for (int i = 0; i < array.Length - 2; i++)
			{
				num += array[i];
			}
			array[array.Length - 2] = BitConverter.GetBytes(num)[1];
			array[array.Length - 1] = BitConverter.GetBytes(num)[0];
			return array;
		}

		private static OperateResult<byte[]> PackReadWriteCommand(byte frameNo, byte control, string address, int length, byte[] data)
		{
			if (string.IsNullOrEmpty(address))
			{
				return new OperateResult<byte[]>("Address is null");
			}
			string text = address.Substring(0, 1);
			if (!"YMLKFTCDSX".Contains(text))
			{
				return new OperateResult<byte[]>("Address type error: " + text);
			}
			List<byte> list = new List<byte>();
			list.AddRange(Encoding.ASCII.GetBytes(text.ToUpper()));
			list.Add(0);
			list.AddRange(Encoding.ASCII.GetBytes(address.Substring(1).PadLeft(6, '0')));
			list.Add(BitConverter.GetBytes(length)[1]);
			list.Add(BitConverter.GetBytes(length)[0]);
			if (data != null && data.Length != 0)
			{
				list.AddRange(data);
			}
			return OperateResult.CreateSuccessResult(PackEntireCommand(false, frameNo, control, list.ToArray()));
		}

		/// <summary>
		/// 构建读取的报文信息
		/// </summary>
		/// <param name="frameNo">站号信息</param>
		/// <param name="address">PLC的地址</param>
		/// <param name="length">读取的长度</param>
		/// <returns>读取指定地址的报文</returns>
		public static OperateResult<byte[]> BuildReadByteCommand(byte frameNo, string address, int length)
		{
			frameNo = (byte)HslHelper.ExtractParameter(ref address, "f", frameNo);
			return PackReadWriteCommand(frameNo, 82, address, length, null);
		}

		/// <summary>
		/// 构建一个读取位的报文信息
		/// </summary>
		/// <param name="frameNo">站号信息</param>
		/// <param name="address"></param>
		/// <param name="length"></param>
		/// <returns></returns>
		public static OperateResult<byte[]> BuildReadBitCommand(byte frameNo, string address, int length)
		{
			frameNo = (byte)HslHelper.ExtractParameter(ref address, "f", frameNo);
			return PackReadWriteCommand(frameNo, 114, address, length, null);
		}

		/// <summary>
		/// 构建写入的报文信息
		/// </summary>
		/// <param name="frameNo">站号信息</param>
		/// <param name="address">PLC的地址</param>
		/// <param name="data">写入的数据</param>
		/// <returns>写入数据到地址的报文</returns>
		public static OperateResult<byte[]> BuildWriteByteCommand(byte frameNo, string address, byte[] data)
		{
			frameNo = (byte)HslHelper.ExtractParameter(ref address, "f", frameNo);
			if (data == null)
			{
				data = new byte[0];
			}
			return PackReadWriteCommand(frameNo, 87, address, data.Length, data);
		}

		/// <summary>
		/// 构建写入bool数组的报文信息
		/// </summary>
		/// <param name="frameNo">站号信息</param>
		/// <param name="address">写入的PLC地址</param>
		/// <param name="data">Bool数组值</param>
		/// <returns>写入数据的报文信息</returns>
		public static OperateResult<byte[]> BuildWriteBitCommand(byte frameNo, string address, bool[] data)
		{
			frameNo = (byte)HslHelper.ExtractParameter(ref address, "f", frameNo);
			if (data == null)
			{
				data = new bool[0];
			}
			byte[] array = new byte[data.Length];
			for (int i = 0; i < array.Length; i++)
			{
				if (data[i])
				{
					array[i] = 1;
				}
			}
			return PackReadWriteCommand(frameNo, 119, address, data.Length, array);
		}

		/// <summary>
		/// 解析出实际的数据信息
		/// </summary>
		/// <param name="response">PLC返回的报文数据</param>
		/// <returns>提炼的真实的数据</returns>
		public static OperateResult<byte[]> ExtractActualData(byte[] response)
		{
			if (response.Length < 20)
			{
				return new OperateResult<byte[]>("Length is less than 20:" + SoftBasic.ByteToHexString(response));
			}
			try
			{
				if (response[10] == 65)
				{
					int num = response[14] * 256 + response[15];
					return new OperateResult<byte[]>(num, GetErrorText(num) ?? "");
				}
				if (response[10] == 87 || response[10] == 119)
				{
					return OperateResult.CreateSuccessResult(new byte[0]);
				}
				if (response[10] == 82)
				{
					return OperateResult.CreateSuccessResult(response.RemoveDouble(23, 2));
				}
				if (response[10] == 114)
				{
					return OperateResult.CreateSuccessResult(response.SelectMiddle(23, response[12] * 256 + response[13] - 9));
				}
				return new OperateResult<byte[]>(StringResources.Language.NotSupportedFunction);
			}
			catch (Exception ex)
			{
				return new OperateResult<byte[]>("ExtractActualData failed: " + ex.Message + " Souce: " + response.ToHexString(' '));
			}
		}
	}
}
