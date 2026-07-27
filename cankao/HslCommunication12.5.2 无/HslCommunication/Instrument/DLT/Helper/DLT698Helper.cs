using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HslCommunication.BasicFramework;
using HslCommunication.Core;

namespace HslCommunication.Instrument.DLT.Helper
{
	/// <summary>
	/// 698 协议的帮助类
	/// </summary>
	public class DLT698Helper
	{
		/// <summary>
		/// 预连接请求
		/// </summary>
		public const byte LinkRequest = 1;

		/// <summary>
		/// 建立应用连接请求
		/// </summary>
		public const byte ConnectRequest = 2;

		/// <summary>
		/// 断开应用连接请求
		/// </summary>
		public const byte ReleaseRequest = 3;

		/// <summary>
		/// 读取请求
		/// </summary>
		public const byte GetRequest = 5;

		/// <summary>
		/// 设置请求
		/// </summary>
		public const byte SetRequest = 6;

		/// <summary>
		/// 操作请求 
		/// </summary>
		public const byte ActionRequest = 7;

		/// <summary>
		/// 操作请求
		/// </summary>
		public const byte ReportRequest = 8;

		/// <summary>
		/// 代理请求
		/// </summary>
		public const byte ReportResponse = 9;

		/// <summary>
		/// 安全请求
		/// </summary>
		public const byte SecurityResquest = 16;

		/// <summary>
		/// 预连接响应
		/// </summary>
		public const byte LinkResponse = 129;

		/// <summary>
		/// 建立应用连接响应
		/// </summary>
		public const byte ConnectResponse = 130;

		/// <summary>
		/// 断开应用连接响应 
		/// </summary>
		public const byte ReleaseResponse = 131;

		/// <summary>
		/// 断开应用连接通知
		/// </summary>
		public const byte ReleaseNotification = 132;

		/// <summary>
		/// 读取响应
		/// </summary>
		public const byte GetResponse = 133;

		/// <summary>
		/// 设置响应
		/// </summary>
		public const byte SetResponse = 134;

		/// <summary>
		/// 操作响应
		/// </summary>
		public const byte ActionResponse = 135;

		/// <summary>
		/// 上报通知
		/// </summary>
		public const byte ReportNotification = 136;

		/// <summary>
		/// 代理响应
		/// </summary>
		public const byte ProxyResponse = 137;

		/// <summary>
		/// 安全响应
		/// </summary>
		public const byte SecurityResponse = 144;

		/// <inheritdoc cref="M:HslCommunication.Core.Net.BinaryCommunication.PackCommandWithHeader(System.Byte[])" />
		public static byte[] PackCommandWithHeader(IDlt698 dlt, byte[] command)
		{
			if (dlt.EnableCodeFE)
			{
				return SoftBasic.SpliceArray<byte>(new byte[4] { 254, 254, 254, 254 }, command);
			}
			return command;
		}

		/// <summary>
		/// 根据地址类型，逻辑地址，实际的地址信息构建出真实的地址报文
		/// </summary>
		/// <param name="addressType">地址类型，0：单地址, 1：通配地址，2：组地址，3：广播地址</param>
		/// <param name="logicAddress">逻辑地址</param>
		/// <param name="address">地址信息</param>
		/// <param name="ca">客户机地址</param>
		/// <returns>原始字节信息</returns>
		private static byte[] CalculateAddressArea(int addressType, int logicAddress, string address, byte ca)
		{
			if (address.Length % 2 == 1)
			{
				address += "F";
			}
			if (logicAddress > 3)
			{
				logicAddress = 3;
			}
			byte[] array = new byte[2 + address.Length / 2];
			array[0] = (byte)((addressType << 6) | (logicAddress << 4) | (address.Length / 2 - 1));
			address.ToHexBytes().Reverse().ToArray()
				.CopyTo(array, 1);
			array[array.Length - 1] = ca;
			return array;
		}

		internal static byte[] CreateStringValueBuffer(string value)
		{
			if (value.Length % 2 == 1)
			{
				value += "F";
			}
			byte[] array = value.ToHexBytes();
			byte[] array2 = new byte[array.Length + 2];
			array2[0] = 9;
			array2[1] = (byte)array.Length;
			array.CopyTo(array2, 2);
			return array2;
		}

		internal static byte[] CreateDateTimeValue(DateTime time)
		{
			return new byte[8]
			{
				28,
				BitConverter.GetBytes(time.Year)[1],
				BitConverter.GetBytes(time.Year)[0],
				(byte)time.Month,
				(byte)time.Day,
				(byte)time.Hour,
				(byte)time.Minute,
				(byte)time.Second
			};
		}

		internal static byte[] CreatePreLogin(byte[] time)
		{
			MemoryStream memoryStream = new MemoryStream();
			memoryStream.WriteByte(129);
			memoryStream.WriteByte(0);
			memoryStream.WriteByte(128);
			memoryStream.Write(time);
			memoryStream.Write(CreateDateTimeWithMs(DateTime.Now));
			memoryStream.Write(CreateDateTimeWithMs(DateTime.Now));
			return memoryStream.ToArray();
		}

		/// <summary>
		/// 将指定的地址信息，控制码信息，数据域信息打包成完整的报文命令
		/// </summary>
		/// <param name="control">控制码信息</param>
		/// <param name="sa">服务器的地址</param>
		/// <param name="ca">客户机地址</param>
		/// <param name="apdu">链路用户数据</param>
		/// <returns>返回是否报文创建成功</returns>
		public static OperateResult<byte[]> BuildEntireCommand(byte control, string sa, byte ca, byte[] apdu)
		{
			int addressType = 0;
			if (sa == "AA")
			{
				addressType = 3;
			}
			else if (sa.Contains("A"))
			{
				addressType = 1;
			}
			byte[] array = CalculateAddressArea(addressType, 0, sa, ca);
			int num = 0;
			byte[] array2 = new byte[4 + array.Length + 2 + apdu.Length + 2 + 1];
			array2[num++] = 104;
			array2[num++] = BitConverter.GetBytes(array2.Length - 2)[0];
			array2[num++] = BitConverter.GetBytes(array2.Length - 2)[1];
			array2[num++] = control;
			array.CopyTo(array2, num);
			num += array.Length;
			DLT698FcsHelper.CalculateFcs16(array2, 1, num - 1).CopyTo(array2, num);
			num += 2;
			apdu.CopyTo(array2, num);
			num += apdu.Length;
			DLT698FcsHelper.CalculateFcs16(array2, 1, num - 1).CopyTo(array2, num);
			array2[num + 2] = 22;
			return OperateResult.CreateSuccessResult(array2);
		}

		private static byte[] CreateApduBySecurity(byte[] apdu, bool useSecurity)
		{
			if (useSecurity)
			{
				byte[] array = new byte[21 + apdu.Length];
				array[0] = 16;
				array[1] = BitConverter.GetBytes(apdu.Length)[1];
				array[2] = BitConverter.GetBytes(apdu.Length)[0];
				array[apdu.Length + 3] = 1;
				array[apdu.Length + 4] = 16;
				array[apdu.Length + 5] = 17;
				array[apdu.Length + 6] = 34;
				array[apdu.Length + 7] = 51;
				array[apdu.Length + 8] = 68;
				array[apdu.Length + 9] = 85;
				array[apdu.Length + 10] = 102;
				array[apdu.Length + 11] = 119;
				array[apdu.Length + 12] = 136;
				array[apdu.Length + 13] = 153;
				array[apdu.Length + 14] = 0;
				array[apdu.Length + 15] = 170;
				array[apdu.Length + 16] = 187;
				array[apdu.Length + 17] = 204;
				array[apdu.Length + 18] = 221;
				array[apdu.Length + 19] = 238;
				array[apdu.Length + 20] = byte.MaxValue;
				apdu.CopyTo(array, 3);
				return array;
			}
			return apdu;
		}

		/// <summary>
		/// 构建读取单个对象的报文数据
		/// </summary>
		/// <param name="address">数据地址信息</param>
		/// <param name="station">特殊指定的站号信息</param>
		/// <param name="dlt">通信的DLT对象</param>
		/// <returns>单次读取的报文信息</returns>
		public static OperateResult<byte[]> BuildReadSingleObject(string address, string station, IDlt698 dlt)
		{
			bool useSecurityResquest = dlt.UseSecurityResquest;
			if (address.IndexOf(';') > 0)
			{
				string[] array = address.Split(new char[1] { ';' }, StringSplitOptions.RemoveEmptyEntries);
				if (array[0].StartsWith("s="))
				{
					station = array[0].Substring(2);
				}
				address = array[1];
			}
			byte[] array2 = new byte[8] { 5, 1, 1, 0, 0, 2, 0, 0 };
			address.ToHexBytes().CopyTo(array2, 3);
			return BuildEntireCommand(67, station, dlt.CA, CreateApduBySecurity(array2, useSecurityResquest));
		}

		/// <summary>
		/// 构建一个读取多个地址数据的报文信息
		/// </summary>
		/// <param name="address">地址数组</param>
		/// <param name="station">站号信息</param>
		/// <param name="dlt">DLT通信对象</param>
		/// <returns>结果报文内容</returns>
		public static OperateResult<byte[]> BuildReadMultiObject(string[] address, string station, IDlt698 dlt)
		{
			bool useSecurityResquest = dlt.UseSecurityResquest;
			MemoryStream memoryStream = new MemoryStream();
			memoryStream.WriteByte(5);
			memoryStream.WriteByte(2);
			memoryStream.WriteByte(2);
			memoryStream.WriteByte((byte)address.Length);
			for (int i = 0; i < address.Length; i++)
			{
				string text = address[i];
				if (text.IndexOf(';') > 0)
				{
					string[] array = text.Split(new char[1] { ';' }, StringSplitOptions.RemoveEmptyEntries);
					if (array[0].StartsWith("s="))
					{
						station = array[0].Substring(2);
					}
					text = array[1];
				}
				memoryStream.Write(text.ToHexBytes());
			}
			memoryStream.WriteByte(0);
			return BuildEntireCommand(67, station, dlt.CA, CreateApduBySecurity(memoryStream.ToArray(), useSecurityResquest));
		}

		/// <summary>
		/// 构建单个写得对象的数据操作
		/// </summary>
		/// <param name="address">数据地址信息</param>
		/// <param name="station">站号信息</param>
		/// <param name="data">数据信息</param>
		/// <param name="dlt">通信的DLT对象</param>
		/// <returns>最终报文</returns>
		public static OperateResult<byte[]> BuildWriteSingleObject(string address, string station, byte[] data, IDlt698 dlt)
		{
			bool useSecurityResquest = dlt.UseSecurityResquest;
			if (address.IndexOf(';') > 0)
			{
				string[] array = address.Split(new char[1] { ';' }, StringSplitOptions.RemoveEmptyEntries);
				if (array[0].StartsWith("s="))
				{
					station = array[0].Substring(2);
				}
				address = array[1];
			}
			byte[] array2 = new byte[8 + data.Length];
			array2[0] = 6;
			array2[1] = 1;
			array2[2] = 2;
			array2[3] = 0;
			array2[4] = 0;
			array2[5] = 2;
			array2[6] = 0;
			data.CopyTo(array2, 7);
			array2[7 + data.Length] = 0;
			address.ToHexBytes().CopyTo(array2, 3);
			return BuildEntireCommand(67, station, dlt.CA, CreateApduBySecurity(array2, useSecurityResquest));
		}

		private static byte GetDayOfWeek(DayOfWeek dayOfWeek)
		{
			switch (dayOfWeek)
			{
			case DayOfWeek.Monday:
				return 1;
			case DayOfWeek.Tuesday:
				return 2;
			case DayOfWeek.Wednesday:
				return 3;
			case DayOfWeek.Thursday:
				return 4;
			case DayOfWeek.Friday:
				return 5;
			case DayOfWeek.Saturday:
				return 6;
			default:
				return 7;
			}
		}

		/// <summary>
		/// 设置一个事件时间到DLT的报文中去
		/// </summary>
		/// <param name="apdu"></param>
		/// <param name="index"></param>
		/// <param name="time"></param>
		public static void SetDltDataTime(byte[] apdu, int index, DateTime time)
		{
			apdu[index] = BitConverter.GetBytes(time.Year)[1];
			apdu[index + 1] = BitConverter.GetBytes(time.Year)[0];
			apdu[index + 2] = BitConverter.GetBytes(time.Month)[0];
			apdu[index + 3] = BitConverter.GetBytes(time.Day)[0];
			apdu[index + 4] = GetDayOfWeek(time.DayOfWeek);
			apdu[index + 5] = (byte)time.Hour;
			apdu[index + 6] = (byte)time.Minute;
			apdu[index + 7] = (byte)time.Second;
			apdu[index + 8] = BitConverter.GetBytes(time.Millisecond)[1];
			apdu[index + 9] = BitConverter.GetBytes(time.Millisecond)[0];
		}

		/// <summary>
		/// 根据给定的时间信息创建一个DLT的时间报文
		/// </summary>
		/// <param name="time">时间信息</param>
		/// <returns>时间报文</returns>
		public static byte[] CreateDateTimeWithMs(DateTime time)
		{
			byte[] array = new byte[10];
			SetDltDataTime(array, 0, time);
			return array;
		}

		/// <summary>
		/// 检查当前的反馈数据信息是否正确
		/// </summary>
		/// <param name="response">从仪表反馈的数据信息</param>
		/// <returns>是否校验成功</returns>
		public static OperateResult<byte[]> CheckResponse(byte[] response)
		{
			try
			{
				if (response.Length < 9)
				{
					return new OperateResult<byte[]>(StringResources.Language.ReceiveDataLengthTooShort);
				}
				if (BitConverter.ToUInt16(response, 1) != response.Length - 2)
				{
					return new OperateResult<byte[]>("Receive length check faild, source: " + response.ToHexString(' '));
				}
				if (!DLT698FcsHelper.CheckFcs16(response, 1, response.Length - 4))
				{
					return new OperateResult<byte[]>("fcs 16 check failed: " + response.ToHexString(' '));
				}
				int num = 5 + (response[4] + 1) + 1 + 2;
				byte[] array = null;
				if (response[num] == 144)
				{
					num++;
					array = response.SelectMiddle(num + 2, response[num] * 256 + response[num + 1]);
				}
				else
				{
					if (response[num] == 238)
					{
						return new OperateResult<byte[]>(response[num + 2], "Current device not support request type");
					}
					array = response.SelectMiddle(num, response.Length - num - 3);
				}
				if (array[0] == 134)
				{
					if (array.Length >= 9 && array[1] == 1 && array[7] != 0)
					{
						return new OperateResult<byte[]>(array[8], GetErrorText(array[8]));
					}
					if (array.Length >= 9 && array[1] == 2 && array[8] != 0)
					{
						return new OperateResult<byte[]>(array[8], GetErrorText(array[8]));
					}
				}
				else if (array[0] == 133 && array[1] == 2)
				{
					if (array.Length >= 10 && array[8] == 0)
					{
						return new OperateResult<byte[]>(array[9], GetErrorText(array[9]));
					}
				}
				else if (array.Length >= 9 && array[7] == 0)
				{
					return new OperateResult<byte[]>(array[8], GetErrorText(array[8]));
				}
				return OperateResult.CreateSuccessResult(array);
			}
			catch (Exception ex)
			{
				return new OperateResult<byte[]>("CheckResponse failed: " + ex.Message + Environment.NewLine + "Source: " + response.ToHexString(' '));
			}
		}

		private static string ExtraData(byte[] content, IByteTransform byteTransform, ref int index, byte oi1, byte oi2, byte attr)
		{
			byte b = content[index++];
			switch (b)
			{
			case 3:
				return (content[index++] != 0).ToString();
			case 4:
			{
				byte b14 = content[index++];
				int num7 = (b14 + 7) / 8;
				byte[] inBytes = content.SelectMiddle(index, num7);
				index += num7;
				return inBytes.ToBoolArray().SelectBegin(b14).ToArrayString();
			}
			case 5:
			{
				int value8 = byteTransform.TransInt32(content, index);
				index += 4;
				return GetScale(value8, oi1, oi2, attr);
			}
			case 6:
			{
				uint value7 = byteTransform.TransUInt32(content, index);
				index += 4;
				return GetScale(value7, oi1, oi2, attr);
			}
			case 9:
			{
				int num6 = content[index++];
				string result = content.SelectMiddle(index, num6).ToHexString();
				index += num6;
				return result;
			}
			case 10:
			{
				int num5 = content[index++];
				string string2 = Encoding.ASCII.GetString(content, index, num5);
				index += num5;
				return string2;
			}
			default:
				switch (b)
				{
				case 10:
				{
					int num4 = content[index++];
					string @string = Encoding.UTF8.GetString(content, index, num4);
					index += num4;
					return @string;
				}
				case 15:
					return GetScale((sbyte)content[index++], oi1, oi2, attr);
				case 16:
				{
					short value6 = byteTransform.TransInt16(content, index);
					index += 2;
					return GetScale(value6, oi1, oi2, attr);
				}
				case 17:
					return GetScale(content[index++], oi1, oi2, attr);
				case 18:
				{
					ushort value5 = byteTransform.TransUInt16(content, index);
					index += 2;
					return GetScale(value5, oi1, oi2, attr);
				}
				case 20:
				{
					long value4 = byteTransform.TransInt64(content, index);
					index += 8;
					return GetScale(value4, oi1, oi2, attr);
				}
				case 21:
				{
					ulong value3 = byteTransform.TransUInt64(content, index);
					index += 8;
					return GetScale(value3, oi1, oi2, attr);
				}
				case 22:
					return content[index++].ToString();
				case 23:
				{
					float value2 = byteTransform.TransSingle(content, index);
					index += 4;
					return GetScale(value2, oi1, oi2, attr);
				}
				case 24:
				{
					double value = byteTransform.TransDouble(content, index);
					index += 8;
					return GetScale(value, oi1, oi2, attr);
				}
				case 25:
				{
					ushort num3 = byteTransform.TransUInt16(content, index);
					index += 2;
					byte b9 = content[index++];
					byte b10 = content[index++];
					index++;
					byte b11 = content[index++];
					byte b12 = content[index++];
					byte b13 = content[index++];
					ushort millisecond = byteTransform.TransUInt16(content, index);
					index += 2;
					try
					{
						return new DateTime(num3, b9, b10, b11, b12, b13, millisecond).ToString();
					}
					catch
					{
						return $"{num3}-{b9}-{b10} {b11}:{b12}:{b13}";
					}
				}
				case 28:
				{
					ushort num2 = byteTransform.TransUInt16(content, index);
					index += 2;
					byte b4 = content[index++];
					byte b5 = content[index++];
					byte b6 = content[index++];
					byte b7 = content[index++];
					byte b8 = content[index++];
					try
					{
						return new DateTime(num2, b4, b5, b6, b7, b8).ToString();
					}
					catch
					{
						return $"{num2}-{b4}-{b5} {b6}:{b7}:{b8}";
					}
				}
				case 26:
				{
					ushort num = byteTransform.TransUInt16(content, index);
					index += 2;
					byte b2 = content[index++];
					byte b3 = content[index++];
					index++;
					return $"{num}-{b2}-{b3}";
				}
				case 27:
					return new TimeSpan(content[index++], content[index++], content[index++]).ToString();
				default:
					return null;
				}
			}
		}

		internal static int GetScale(byte oi1, byte oi2, byte attr)
		{
			attr &= 0xF;
			int result = 0;
			if ((oi1 & 0xF0) == 0)
			{
				result = ((attr != 4) ? (-2) : (-4));
			}
			else if ((oi1 & 0xF0) == 16)
			{
				result = -4;
			}
			else
			{
				switch (oi1)
				{
				case 32:
					if (oi2 == 0)
					{
						result = -1;
					}
					else if (oi2 == 1)
					{
						result = -3;
					}
					else if (oi2 < 10)
					{
						result = -1;
					}
					else if (oi2 == 10)
					{
						result = -3;
					}
					else if (oi2 < 16)
					{
						result = -2;
					}
					else if (oi2 == 16)
					{
						result = -1;
					}
					else if (oi2 < 19)
					{
						result = -2;
					}
					else if (oi2 < 23)
					{
						result = 0;
					}
					else if (oi2 < 30)
					{
						result = -4;
					}
					else if (oi2 < 38)
					{
						result = 0;
					}
					else if (oi2 < 42)
					{
						result = -2;
					}
					else if (oi2 == 49 || oi2 == 50)
					{
						result = -2;
					}
					else if (oi2 == 128 || oi2 == 144)
					{
						result = -4;
					}
					break;
				case 37:
					if (oi2 < 2)
					{
						result = -4;
					}
					else if (oi2 < 4)
					{
						result = -2;
					}
					break;
				case 64:
					if (oi2 == 48)
					{
						result = -1;
					}
					break;
				case 65:
					if (oi2 == 12 || oi2 == 13 || oi2 == 14 || oi2 == 15)
					{
						result = -3;
					}
					break;
				}
			}
			return result;
		}

		private static string GetScale<T>(T value, byte oi1, byte oi2, byte attr)
		{
			int scale = GetScale(oi1, oi2, attr);
			if (scale == 0)
			{
				return value.ToString();
			}
			return (Convert.ToDouble(value) * Math.Pow(10.0, scale)).ToString();
		}

		internal static string[] ExtraStringsValues(IByteTransform byteTransform, byte[] response, ref int index)
		{
			List<string> list = new List<string>();
			if (response[index] == 1 || response[index] == 2)
			{
				index++;
				int num = response[index++];
				for (int i = 0; i < num; i++)
				{
					list.AddRange(ExtraStringsValues(byteTransform, response, ref index));
				}
				return list.ToArray();
			}
			if (response[index] == 0)
			{
				return list.ToArray();
			}
			list.Add(ExtraData(response, byteTransform, ref index, response[3], response[4], response[5]));
			return list.ToArray();
		}

		/// <summary>
		/// 根据错误代码返回详细的错误文本消息
		/// </summary>
		/// <param name="err">错误代码</param>
		/// <returns>错误文本消息</returns>
		public static string GetErrorText(byte err)
		{
			switch (err)
			{
			case 1:
				return StringResources.Language.DLT698Error01;
			case 2:
				return StringResources.Language.DLT698Error02;
			case 3:
				return StringResources.Language.DLT698Error03;
			case 4:
				return StringResources.Language.DLT698Error04;
			case 5:
				return StringResources.Language.DLT698Error05;
			case 6:
				return StringResources.Language.DLT698Error06;
			case 7:
				return StringResources.Language.DLT698Error07;
			case 8:
				return StringResources.Language.DLT698Error08;
			case 9:
				return StringResources.Language.DLT698Error09;
			case 10:
				return StringResources.Language.DLT698Error10;
			case 11:
				return StringResources.Language.DLT698Error11;
			case 12:
				return StringResources.Language.DLT698Error12;
			case 13:
				return StringResources.Language.DLT698Error13;
			case 14:
				return StringResources.Language.DLT698Error14;
			case 15:
				return StringResources.Language.DLT698Error15;
			case 16:
				return StringResources.Language.DLT698Error16;
			case 17:
				return StringResources.Language.DLT698Error17;
			case 18:
				return StringResources.Language.DLT698Error18;
			case 19:
				return StringResources.Language.DLT698Error19;
			case 20:
				return StringResources.Language.DLT698Error20;
			case 21:
				return StringResources.Language.DLT698Error21;
			case 22:
				return StringResources.Language.DLT698Error22;
			case 23:
				return StringResources.Language.DLT698Error23;
			case 24:
				return StringResources.Language.DLT698Error24;
			case 25:
				return StringResources.Language.DLT698Error25;
			case 26:
				return StringResources.Language.DLT698Error26;
			case 27:
				return StringResources.Language.DLT698Error27;
			case 28:
				return StringResources.Language.DLT698Error28;
			case 29:
				return StringResources.Language.DLT698Error29;
			case 30:
				return StringResources.Language.DLT698Error30;
			case 31:
				return StringResources.Language.DLT698Error31;
			case 32:
				return StringResources.Language.DLT698Error32;
			case 33:
				return StringResources.Language.DLT698Error33;
			case 34:
				return StringResources.Language.DLT698Error34;
			case 35:
				return StringResources.Language.DLT698Error35;
			default:
				return StringResources.Language.UnknownError;
			}
		}

		/// <summary>
		/// 根据传入的APDU的命令读取原始的字节数据返回，并检查返回的字节数据是否合法<br />
		/// Read the original byte data return according to the incoming APDU command, and check whether the returned byte data is valid
		/// </summary>
		/// <param name="dlt">通信的DLT对象</param>
		/// <param name="apdu">apdu报文信息</param>
		/// <returns>原始字节数据信息</returns>
		public static OperateResult<byte[]> ReadByApdu(IDlt698 dlt, byte[] apdu)
		{
			OperateResult<byte[]> operateResult = BuildEntireCommand(67, dlt.Station, dlt.CA, apdu);
			if (!operateResult.IsSuccess)
			{
				return operateResult;
			}
			OperateResult<byte[]> operateResult2 = dlt.ReadFromCoreServer(operateResult.Content);
			if (!operateResult2.IsSuccess)
			{
				return operateResult2;
			}
			return CheckResponse(operateResult2.Content);
		}

		/// <inheritdoc cref="M:HslCommunication.Instrument.DLT.Helper.DLT698Helper.ReadByApdu(HslCommunication.Instrument.DLT.Helper.IDlt698,System.Byte[])" />
		public static async Task<OperateResult<byte[]>> ReadByApduAsync(IDlt698 dlt, byte[] apdu)
		{
			OperateResult<byte[]> command = BuildEntireCommand(67, dlt.Station, dlt.CA, apdu);
			if (!command.IsSuccess)
			{
				return command;
			}
			OperateResult<byte[]> read = await dlt.ReadFromCoreServerAsync(command.Content);
			if (!read.IsSuccess)
			{
				return read;
			}
			return CheckResponse(read.Content);
		}

		/// <summary>
		/// 激活设备的命令，只发送数据到设备，不等待设备数据返回<br />
		/// The command to activate the device, only send data to the device, do not wait for the device data to return
		/// </summary>
		/// <returns>是否发送成功</returns>
		public static OperateResult ActiveDeveice(IDlt698 dlt)
		{
			return dlt.ReadFromCoreServer(new byte[4] { 254, 254, 254, 254 }, false, false);
		}

		/// <summary>
		/// 根据指定的数据标识来读取相关的原始数据信息，地址标识根据手册来，从高位到地位，例如 00-00-00-00，分割符可以任意特殊字符或是没有分隔符。<br />
		/// Read the relevant original data information according to the specified data identifier. The address identifier is based on the manual, 
		/// from high to position, such as 00-00-00-00. The separator can be any special character or no separator.
		/// </summary>
		/// <remarks>
		/// 地址可以携带地址域信息，例如 "s=2;00-00-00-00" 或是 "s=100000;00-00-02-00"，关于数据域信息，需要查找手册，例如:00-01-00-00 表示： (当前)正向有功总电能
		/// </remarks>
		/// <param name="dlt">通信的DLT对象</param>
		/// <param name="address">数据标识，具体需要查找手册来对应</param>
		/// <param name="length">数据长度信息</param>
		/// <returns>结果信息</returns>
		public static OperateResult<byte[]> Read(IDlt698 dlt, string address, ushort length)
		{
			OperateResult<byte[]> operateResult = BuildReadSingleObject(address, dlt.Station, dlt);
			if (!operateResult.IsSuccess)
			{
				return operateResult;
			}
			OperateResult<byte[]> operateResult2 = dlt.ReadFromCoreServer(operateResult.Content);
			if (!operateResult2.IsSuccess)
			{
				return operateResult2;
			}
			return CheckResponse(operateResult2.Content);
		}

		/// <summary>
		/// 读取指定地址的所有的字符串数据信息，一般来说，一个地址只有一个数据，当属性为数组或是结构体的时候，存在多个数据，具体几个数据，需要根据
		/// </summary>
		/// <remarks>
		/// 地址可以携带地址域信息，例如 "s=2;20-00-02-00" 或是 "s=100000;20-00-02-00"，
		/// </remarks>
		/// <param name="dlt">通信的DLT对象</param>
		/// <param name="address">数据标识，具体需要查找手册来对应</param>
		/// <returns>字符串数组信息</returns>
		public static OperateResult<string[]> ReadStringArray(IDlt698 dlt, string address)
		{
			OperateResult<byte[]> operateResult = Read(dlt, address, 1);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<string[]>(operateResult);
			}
			int index = 8;
			return OperateResult.CreateSuccessResult(ExtraStringsValues(dlt.ByteTransform, operateResult.Content, ref index));
		}

		/// <summary>
		/// 批量读取多个地址的数据信息，返回所有的数据对象信息<br />
		/// </summary>
		/// <param name="dlt">DLT通信对象</param>
		/// <param name="address">地址数组信息</param>
		/// <returns>字符串结果数据</returns>
		public static OperateResult<string[]> ReadStringArray(IDlt698 dlt, string[] address)
		{
			OperateResult<byte[]> operateResult = BuildReadMultiObject(address, dlt.Station, dlt);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<string[]>(operateResult);
			}
			OperateResult<byte[]> operateResult2 = dlt.ReadFromCoreServer(operateResult.Content);
			if (!operateResult2.IsSuccess)
			{
				return OperateResult.CreateFailedResult<string[]>(operateResult2);
			}
			OperateResult<byte[]> operateResult3 = CheckResponse(operateResult2.Content);
			if (!operateResult3.IsSuccess)
			{
				return OperateResult.CreateFailedResult<string[]>(operateResult3);
			}
			int num = operateResult3.Content[3];
			List<string> list = new List<string>();
			int index = 9;
			for (int i = 0; i < num; i++)
			{
				list.AddRange(ExtraStringsValues(dlt.ByteTransform, operateResult3.Content, ref index));
				index += 5;
			}
			return OperateResult.CreateSuccessResult(list.ToArray());
		}

		internal static OperateResult<T[]> ReadDataAndParse<T>(OperateResult<string[]> read, ushort length, Func<string, T> trans)
		{
			if (!read.IsSuccess)
			{
				return OperateResult.CreateFailedResult<T[]>(read);
			}
			try
			{
				return OperateResult.CreateSuccessResult((from m in read.Content.Take(length)
					select trans(m)).ToArray());
			}
			catch (Exception ex)
			{
				return new OperateResult<T[]>(typeof(T).Name + ".Parse failed: " + ex.Message + Environment.NewLine + "Source: " + read.Content.ToArrayString());
			}
		}

		internal static OperateResult<bool[]> ReadBool(OperateResult<string[]> read, ushort length)
		{
			if (!read.IsSuccess)
			{
				return OperateResult.CreateFailedResult<bool[]>(read);
			}
			try
			{
				List<bool> list = new List<bool>();
				for (int i = 0; i < read.Content.Length; i++)
				{
					list.AddRange(read.Content[i].ToStringArray<bool>());
				}
				return OperateResult.CreateSuccessResult(list.ToArray());
			}
			catch (Exception ex)
			{
				return new OperateResult<bool[]>("bool.Parse failed: " + ex.Message + Environment.NewLine + "Source: " + read.Content.ToArrayString());
			}
		}

		/// <summary>
		/// 根据指定的数据标识来写入相关的原始数据信息，地址标识根据手册来，从高位到地位，例如 00-00-00-00，分割符可以任意特殊字符或是没有分隔符。<br />
		/// Read the relevant original data information according to the specified data identifier. The address identifier is based on the manual, 
		/// from high to position, such as 00-00-00-00. The separator can be any special character or no separator.
		/// </summary>
		/// <remarks>
		/// 写入数据的时候，需要使用类型+值信息，例如写入地址 40-00-02-00  值为 1C 07 E0 01 14 10 1B 0B  表示 时间：2016-01-20 16：27：11
		/// </remarks>
		/// <param name="dlt">通信的DLT对象</param>
		/// <param name="address">地址信息</param>
		/// <param name="value">写入的数据值</param>
		/// <returns>是否写入成功</returns>
		public static OperateResult Write(IDlt698 dlt, string address, byte[] value)
		{
			OperateResult<byte[]> operateResult = BuildWriteSingleObject(address, dlt.Station, value, dlt);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<string>(operateResult);
			}
			OperateResult<byte[]> operateResult2 = dlt.ReadFromCoreServer(operateResult.Content);
			if (!operateResult2.IsSuccess)
			{
				return OperateResult.CreateFailedResult<string>(operateResult2);
			}
			return CheckResponse(operateResult2.Content);
		}

		/// <summary>
		/// 读取设备的通信地址，仅支持点对点通讯的情况，返回地址域数据，例如：149100007290<br />
		/// Read the communication address of the device, only support point-to-point communication, and return the address field data, for example: 149100007290
		/// </summary>
		/// <param name="dlt">通信的DLT对象</param>
		/// <returns>设备的通信地址</returns>
		public static OperateResult<string> ReadAddress(IDlt698 dlt)
		{
			OperateResult<byte[]> operateResult = BuildReadSingleObject("40-01-02-00", "AAAAAAAAAAAA", dlt);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<string>(operateResult);
			}
			OperateResult<byte[]> operateResult2 = dlt.ReadFromCoreServer(operateResult.Content);
			if (!operateResult2.IsSuccess)
			{
				return OperateResult.CreateFailedResult<string>(operateResult2);
			}
			OperateResult<byte[]> operateResult3 = CheckResponse(operateResult2.Content);
			if (!operateResult3.IsSuccess)
			{
				return OperateResult.CreateFailedResult<string>(operateResult3);
			}
			dlt.Station = operateResult3.Content.SelectMiddle(10, operateResult3.Content[9]).ToHexString();
			return OperateResult.CreateSuccessResult(dlt.Station);
		}

		/// <summary>
		/// 写入设备的地址域信息，仅支持点对点通讯的情况，需要指定地址域信息，例如：149100007290<br />
		/// Write the address domain information of the device, only support point-to-point communication, 
		/// you need to specify the address domain information, for example: 149100007290
		/// </summary>
		/// <param name="dlt">通信的DLT对象</param>
		/// <param name="address">等待写入的地址域</param>
		/// <returns>是否写入成功</returns>
		public static OperateResult WriteAddress(IDlt698 dlt, string address)
		{
			OperateResult<byte[]> operateResult = BuildWriteSingleObject("40-01-02-00", "AAAAAAAAAAAA", CreateStringValueBuffer(address), dlt);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<string>(operateResult);
			}
			OperateResult<byte[]> operateResult2 = dlt.ReadFromCoreServer(operateResult.Content);
			if (!operateResult2.IsSuccess)
			{
				return OperateResult.CreateFailedResult<string>(operateResult2);
			}
			return CheckResponse(operateResult2.Content);
		}

		/// <summary>
		/// 写入设备的时间信息到指定的地址，返回是否成功，使用的时间类型为 0x1C, 有效数据为 年月日时分秒。<br />
		/// Write the time information of the device to the specified address, return whether it is successful, the time type used is 0x1C, and the valid data is year, month, day, hour, minute, and second.
		/// </summary>
		/// <param name="dlt">通信的DLT对象</param>
		/// <param name="address">写入的地址的信息</param>
		/// <param name="time">时间数据</param>
		/// <returns>是否写入成功</returns>
		public static OperateResult WriteDateTime(IDlt698 dlt, string address, DateTime time)
		{
			OperateResult<byte[]> operateResult = BuildWriteSingleObject(address, dlt.Station, CreateDateTimeValue(time), dlt);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<string>(operateResult);
			}
			OperateResult<byte[]> operateResult2 = dlt.ReadFromCoreServer(operateResult.Content);
			if (!operateResult2.IsSuccess)
			{
				return OperateResult.CreateFailedResult<string>(operateResult2);
			}
			return CheckResponse(operateResult2.Content);
		}
	}
}
