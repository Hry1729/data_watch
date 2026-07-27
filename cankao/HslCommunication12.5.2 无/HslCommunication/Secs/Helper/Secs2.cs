using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HslCommunication.Core;
using HslCommunication.Secs.Types;

namespace HslCommunication.Secs.Helper
{
	/// <summary>
	/// Secs2相关的规则
	/// </summary>
	public class Secs2
	{
		/// <summary>
		/// 列表的类型信息
		/// </summary>
		public const int TypeList = 0;

		/// <summary>
		/// ASCII字符串的信息
		/// </summary>
		public const int TypeASCII = 64;

		/// <summary>
		/// 有符号的1个字节长度的整型
		/// </summary>
		public const int TypeSByte = 100;

		/// <summary>
		/// 无符号的1个字节长度的整型
		/// </summary>
		public const int TypeByte = 164;

		/// <summary>
		/// 有符号的2个字节长度的整型
		/// </summary>
		public const int TypeInt16 = 104;

		/// <summary>
		/// 无符号的2个字节长度的整型
		/// </summary>
		public const int TypeUInt16 = 168;

		/// <summary>
		/// 有符号的4个字节长度的整型
		/// </summary>
		public const int TypeInt32 = 112;

		/// <summary>
		/// 无符号的4个字节长度的整型
		/// </summary>
		public const int TypeUInt32 = 176;

		/// <summary>
		/// 有符号的8个字节长度的整型
		/// </summary>
		public const int TypeInt64 = 96;

		/// <summary>
		/// 无符号的8个字节长度的整型
		/// </summary>
		public const int TypeUInt64 = 160;

		/// <summary>
		/// 单浮点精度的类型
		/// </summary>
		public const int TypeSingle = 144;

		/// <summary>
		/// 双浮点精度的类型
		/// </summary>
		public const int TypeDouble = 128;

		/// <summary>
		/// Bool值信息
		/// </summary>
		public const int TypeBool = 36;

		/// <summary>
		/// 二进制的数据信息
		/// </summary>
		public const int TypeBinary = 32;

		/// <summary>
		/// JIS8类型的数据
		/// </summary>
		public const int TypeJIS8 = 68;

		/// <summary>
		/// SECS的字节顺序信息
		/// </summary>
		public static IByteTransform SecsTransform = new ReverseBytesTransform();

		private static int GetMaxLength(byte[] buffer, int index, int length)
		{
			if (index + length <= buffer.Length)
			{
				return length;
			}
			return buffer.Length - index;
		}

		internal static SecsValue ExtraToSecsItemValue(IByteTransform byteTransform, byte[] buffer, ref int index, Encoding encoding)
		{
			if (index >= buffer.Length)
			{
				return new SecsValue();
			}
			int num = buffer[index] & 3;
			int num2 = buffer[index] & 0xFC;
			int num3 = 0;
			switch (num)
			{
			case 1:
				num3 = buffer[index + 1];
				index += 2;
				break;
			case 2:
				num3 = buffer[index + 1] * 256 + buffer[index + 2];
				index += 3;
				break;
			case 3:
				num3 = buffer[index + 1] * 65536 + buffer[index + 2] * 256 + buffer[index + 3];
				index += 4;
				break;
			default:
				index++;
				break;
			}
			if (num2 == 0)
			{
				SecsValue[] array = new SecsValue[num3];
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = ExtraToSecsItemValue(byteTransform, buffer, ref index, encoding);
				}
				return new SecsValue(SecsItemType.List, array);
			}
			int num4 = index;
			index += num3;
			switch (num2)
			{
			case 64:
				return new SecsValue(SecsItemType.ASCII, encoding.GetString(buffer, num4, GetMaxLength(buffer, num4, num3)), num3);
			case 100:
				return new SecsValue(SecsItemType.SByte, (num3 == 1) ? ((object)(sbyte)buffer[num4]) : (from m in buffer.SelectMiddle(num4, num3)
					select (sbyte)m).ToArray());
			case 164:
				return new SecsValue(SecsItemType.Byte, (num3 == 1) ? ((object)buffer[num4]) : buffer.SelectMiddle(num4, num3));
			case 104:
				return new SecsValue(SecsItemType.Int16, (num3 == 2) ? ((object)byteTransform.TransInt16(buffer, num4)) : byteTransform.TransInt16(buffer, num4, num3 / 2));
			case 168:
				return new SecsValue(SecsItemType.UInt16, (num3 == 2) ? ((object)byteTransform.TransUInt16(buffer, num4)) : byteTransform.TransUInt16(buffer, num4, num3 / 2));
			case 112:
				return new SecsValue(SecsItemType.Int32, (num3 == 4) ? ((object)byteTransform.TransInt32(buffer, num4)) : byteTransform.TransInt32(buffer, num4, num3 / 4));
			case 176:
				return new SecsValue(SecsItemType.UInt32, (num3 == 4) ? ((object)byteTransform.TransUInt32(buffer, num4)) : byteTransform.TransUInt32(buffer, num4, num3 / 4));
			case 96:
				return new SecsValue(SecsItemType.Int64, (num3 == 8) ? ((object)byteTransform.TransInt64(buffer, num4)) : byteTransform.TransInt64(buffer, num4, num3 / 8));
			case 160:
				return new SecsValue(SecsItemType.UInt64, (num3 == 8) ? ((object)byteTransform.TransUInt64(buffer, num4)) : byteTransform.TransUInt64(buffer, num4, num3 / 8));
			case 144:
				return new SecsValue(SecsItemType.Single, (num3 == 4) ? ((object)byteTransform.TransSingle(buffer, num4)) : byteTransform.TransSingle(buffer, num4, num3 / 4));
			case 128:
				return new SecsValue(SecsItemType.Double, (num3 == 8) ? ((object)byteTransform.TransDouble(buffer, num4)) : byteTransform.TransDouble(buffer, num4, num3 / 8));
			case 36:
				return new SecsValue(SecsItemType.Bool, (num3 == 1) ? ((object)(buffer[num4] != 0)) : (from m in buffer.SelectMiddle(num4, num3)
					select m != 0).ToArray());
			case 32:
				return new SecsValue(SecsItemType.Binary, buffer.SelectMiddle(num4, num3));
			case 68:
				return new SecsValue(SecsItemType.JIS8, buffer.SelectMiddle(num4, num3));
			default:
				return null;
			}
		}

		internal static int GetTypeCodeFrom(SecsItemType type)
		{
			switch (type)
			{
			case SecsItemType.ASCII:
				return 64;
			case SecsItemType.SByte:
				return 100;
			case SecsItemType.Byte:
				return 164;
			case SecsItemType.Int16:
				return 104;
			case SecsItemType.UInt16:
				return 168;
			case SecsItemType.Int32:
				return 112;
			case SecsItemType.UInt32:
				return 176;
			case SecsItemType.Int64:
				return 96;
			case SecsItemType.UInt64:
				return 160;
			case SecsItemType.Single:
				return 144;
			case SecsItemType.Double:
				return 128;
			case SecsItemType.Bool:
				return 36;
			case SecsItemType.Binary:
				return 32;
			case SecsItemType.JIS8:
				return 68;
			default:
				return 0;
			}
		}

		internal static void AddCodeSource(List<byte> bytes, SecsItemType type, int length)
		{
			int typeCodeFrom = GetTypeCodeFrom(type);
			if (length < 256)
			{
				bytes.Add((byte)(typeCodeFrom | 1));
				bytes.Add((byte)length);
			}
			else if (length < 65536)
			{
				byte[] bytes2 = BitConverter.GetBytes(length);
				bytes.Add((byte)(typeCodeFrom | 2));
				bytes.Add(bytes2[1]);
				bytes.Add(bytes2[0]);
			}
			else
			{
				byte[] bytes3 = BitConverter.GetBytes(length);
				bytes.Add((byte)(typeCodeFrom | 3));
				bytes.Add(bytes3[2]);
				bytes.Add(bytes3[1]);
				bytes.Add(bytes3[0]);
			}
		}

		internal static void AddCodeAndValueSource(List<byte> bytes, SecsValue value, Encoding encoding)
		{
			if (value == null)
			{
				return;
			}
			if (value.ItemType == SecsItemType.List)
			{
				AddCodeSource(length: (value.Value is IEnumerable<SecsValue> source) ? source.Count() : 0, bytes: bytes, type: value.ItemType);
				return;
			}
			byte[] array = null;
			switch (value.ItemType)
			{
			case SecsItemType.ASCII:
				array = encoding.GetBytes(value.Value.ToString());
				break;
			case SecsItemType.SByte:
				array = ((!(value.Value.GetType() == typeof(sbyte))) ? ((sbyte[])value.Value).Select((sbyte m) => (byte)m).ToArray() : new byte[1] { (byte)value.Value });
				break;
			case SecsItemType.Byte:
				array = ((!(value.Value.GetType() == typeof(byte))) ? ((byte[])value.Value) : new byte[1] { (byte)value.Value });
				break;
			case SecsItemType.Int16:
				array = ((value.Value.GetType() == typeof(short)) ? SecsTransform.TransByte((short)value.Value) : SecsTransform.TransByte((short[])value.Value));
				break;
			case SecsItemType.UInt16:
				array = ((value.Value.GetType() == typeof(ushort)) ? SecsTransform.TransByte((ushort)value.Value) : SecsTransform.TransByte((ushort[])value.Value));
				break;
			case SecsItemType.Int32:
				array = ((value.Value.GetType() == typeof(int)) ? SecsTransform.TransByte((int)value.Value) : SecsTransform.TransByte((int[])value.Value));
				break;
			case SecsItemType.UInt32:
				array = ((value.Value.GetType() == typeof(uint)) ? SecsTransform.TransByte((uint)value.Value) : SecsTransform.TransByte((uint[])value.Value));
				break;
			case SecsItemType.Int64:
				array = ((value.Value.GetType() == typeof(long)) ? SecsTransform.TransByte((long)value.Value) : SecsTransform.TransByte((long[])value.Value));
				break;
			case SecsItemType.UInt64:
				array = ((value.Value.GetType() == typeof(ulong)) ? SecsTransform.TransByte((ulong)value.Value) : SecsTransform.TransByte((ulong[])value.Value));
				break;
			case SecsItemType.Single:
				array = ((value.Value.GetType() == typeof(float)) ? SecsTransform.TransByte((float)value.Value) : SecsTransform.TransByte((float[])value.Value));
				break;
			case SecsItemType.Double:
				array = ((value.Value.GetType() == typeof(double)) ? SecsTransform.TransByte((double)value.Value) : SecsTransform.TransByte((double[])value.Value));
				break;
			case SecsItemType.Bool:
				array = ((!(value.Value.GetType() == typeof(bool))) ? ((bool[])value.Value).Select((bool m) => (byte)(m ? byte.MaxValue : 0)).ToArray() : ((!(bool)value.Value) ? new byte[1] : new byte[1] { 255 }));
				break;
			case SecsItemType.Binary:
				array = (byte[])value.Value;
				break;
			case SecsItemType.JIS8:
				array = (byte[])value.Value;
				break;
			case SecsItemType.None:
				array = new byte[0];
				break;
			default:
				array = (byte[])value.Value;
				break;
			}
			AddCodeSource(bytes, value.ItemType, array.Length);
			bytes.AddRange(array);
		}

		/// <summary>
		/// 将返回的数据内容解析为实际的字符串信息，根据secsⅡ 协议定义的规则解析出实际的数据信息
		/// </summary>
		/// <param name="buffer">原始的字节数据内容</param>
		/// <param name="encoding">字符串对象的编码信息</param>
		/// <returns>字符串消息</returns>
		public static SecsValue ExtraToSecsItemValue(byte[] buffer, Encoding encoding)
		{
			if (buffer == null)
			{
				return null;
			}
			int index = 0;
			return ExtraToSecsItemValue(SecsTransform, buffer, ref index, encoding);
		}
	}
}
