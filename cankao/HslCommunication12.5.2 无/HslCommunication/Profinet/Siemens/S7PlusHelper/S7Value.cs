using System.IO;
using HslCommunication.Core;

namespace HslCommunication.Profinet.Siemens.S7PlusHelper
{
	/// <summary>
	/// S7对象的类，表示一个对象信息
	/// </summary>
	public class S7Value
	{
		/// <summary>
		/// 类型代号
		/// </summary>
		public byte TypeCode { get; set; }

		/// <summary>
		/// 内存数据
		/// </summary>
		public byte[] Buffer { get; set; }

		/// <summary>
		/// 值对象
		/// </summary>
		public object Value { get; set; }

		/// <summary>
		/// 标记信息
		/// </summary>
		public byte Flag { get; set; }

		/// <summary>
		/// 关联的结构体的标识信息
		/// </summary>
		public uint StructID { get; set; }

		internal static byte[] GetBufferBool(IByteTransform byteTransform, bool value)
		{
			byte[] array = new byte[3] { 0, 1, 0 };
			if (value)
			{
				array[2] = 1;
			}
			else
			{
				array[2] = 0;
			}
			return array;
		}

		internal static byte[] GetBufferInt8(IByteTransform byteTransform, sbyte value)
		{
			return new byte[3]
			{
				0,
				6,
				(byte)value
			};
		}

		internal static byte[] GetBufferUInt8(IByteTransform byteTransform, byte value)
		{
			return new byte[3] { 0, 10, value };
		}

		internal static byte[] GetBufferUInt8(IByteTransform byteTransform, byte[] value)
		{
			MemoryStream memoryStream = new MemoryStream();
			memoryStream.WriteByte(16);
			memoryStream.WriteByte(10);
			S7Object.WriteUint32(memoryStream, (uint)value.Length);
			memoryStream.Write(value);
			return memoryStream.ToArray();
		}

		internal static byte[] GetBufferInt16(IByteTransform byteTransform, short value)
		{
			byte[] array = new byte[4] { 0, 7, 0, 0 };
			byteTransform.TransByte(value).CopyTo(array, 2);
			return array;
		}

		internal static byte[] GetBufferInt16(IByteTransform byteTransform, short[] value)
		{
			MemoryStream memoryStream = new MemoryStream();
			memoryStream.WriteByte(16);
			memoryStream.WriteByte(7);
			S7Object.WriteUint32(memoryStream, (uint)value.Length);
			memoryStream.Write(byteTransform.TransByte(value));
			return memoryStream.ToArray();
		}

		internal static byte[] GetBufferUInt16(IByteTransform byteTransform, ushort value)
		{
			byte[] array = new byte[4] { 0, 11, 0, 0 };
			byteTransform.TransByte(value).CopyTo(array, 2);
			return array;
		}

		internal static byte[] GetBufferUInt16(IByteTransform byteTransform, ushort[] value)
		{
			MemoryStream memoryStream = new MemoryStream();
			memoryStream.WriteByte(16);
			memoryStream.WriteByte(11);
			S7Object.WriteUint32(memoryStream, (uint)value.Length);
			memoryStream.Write(byteTransform.TransByte(value));
			return memoryStream.ToArray();
		}

		internal static byte[] GetBufferInt32(IByteTransform byteTransform, int value)
		{
			MemoryStream memoryStream = new MemoryStream();
			memoryStream.WriteByte(0);
			memoryStream.WriteByte(8);
			S7Object.WriteUint32(memoryStream, (uint)value);
			return memoryStream.ToArray();
		}

		internal static byte[] GetBufferInt32(IByteTransform byteTransform, int[] value)
		{
			MemoryStream memoryStream = new MemoryStream();
			memoryStream.WriteByte(16);
			memoryStream.WriteByte(8);
			S7Object.WriteUint32(memoryStream, (uint)value.Length);
			for (int i = 0; i < value.Length; i++)
			{
				S7Object.WriteUint32(memoryStream, (uint)value[i]);
			}
			return memoryStream.ToArray();
		}

		internal static byte[] GetBufferUInt32(IByteTransform byteTransform, uint value)
		{
			byte[] array = new byte[6] { 0, 12, 0, 0, 0, 0 };
			byteTransform.TransByte(value).CopyTo(array, 2);
			return array;
		}

		internal static byte[] GetBufferUInt32(IByteTransform byteTransform, uint[] value)
		{
			MemoryStream memoryStream = new MemoryStream();
			memoryStream.WriteByte(16);
			memoryStream.WriteByte(12);
			S7Object.WriteUint32(memoryStream, (uint)value.Length);
			memoryStream.Write(byteTransform.TransByte(value));
			return memoryStream.ToArray();
		}

		internal static byte[] GetBufferInt64(IByteTransform byteTransform, long value)
		{
			MemoryStream memoryStream = new MemoryStream();
			memoryStream.WriteByte(0);
			memoryStream.WriteByte(9);
			S7Object.WriteUint64(memoryStream, (ulong)value);
			return memoryStream.ToArray();
		}

		internal static byte[] GetBufferInt64(IByteTransform byteTransform, long[] value)
		{
			MemoryStream memoryStream = new MemoryStream();
			memoryStream.WriteByte(16);
			memoryStream.WriteByte(9);
			S7Object.WriteUint32(memoryStream, (uint)value.Length);
			for (int i = 0; i < value.Length; i++)
			{
				S7Object.WriteUint64(memoryStream, (ulong)value[i]);
			}
			return memoryStream.ToArray();
		}

		internal static byte[] GetBufferUInt64(IByteTransform byteTransform, ulong value)
		{
			byte[] array = new byte[10] { 0, 13, 0, 0, 0, 0, 0, 0, 0, 0 };
			byteTransform.TransByte(value).CopyTo(array, 2);
			return array;
		}

		internal static byte[] GetBufferUInt64(IByteTransform byteTransform, ulong[] value)
		{
			MemoryStream memoryStream = new MemoryStream();
			memoryStream.WriteByte(16);
			memoryStream.WriteByte(13);
			S7Object.WriteUint32(memoryStream, (uint)value.Length);
			memoryStream.Write(byteTransform.TransByte(value));
			return memoryStream.ToArray();
		}

		internal static byte[] GetBufferFloat(IByteTransform byteTransform, float value)
		{
			byte[] array = new byte[6] { 0, 14, 0, 0, 0, 0 };
			byteTransform.TransByte(value).CopyTo(array, 2);
			return array;
		}

		internal static byte[] GetBufferFloat(IByteTransform byteTransform, float[] value)
		{
			MemoryStream memoryStream = new MemoryStream();
			memoryStream.WriteByte(16);
			memoryStream.WriteByte(14);
			S7Object.WriteUint32(memoryStream, (uint)value.Length);
			memoryStream.Write(byteTransform.TransByte(value));
			return memoryStream.ToArray();
		}

		internal static byte[] GetBufferDouble(IByteTransform byteTransform, double value)
		{
			byte[] array = new byte[10] { 0, 15, 0, 0, 0, 0, 0, 0, 0, 0 };
			byteTransform.TransByte(value).CopyTo(array, 2);
			return array;
		}

		internal static byte[] GetBufferDouble(IByteTransform byteTransform, double[] value)
		{
			MemoryStream memoryStream = new MemoryStream();
			memoryStream.WriteByte(16);
			memoryStream.WriteByte(15);
			S7Object.WriteUint32(memoryStream, (uint)value.Length);
			memoryStream.Write(byteTransform.TransByte(value));
			return memoryStream.ToArray();
		}
	}
}
