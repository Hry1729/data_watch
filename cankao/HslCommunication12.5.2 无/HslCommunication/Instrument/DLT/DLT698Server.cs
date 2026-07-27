using System;
using System.IO;
using System.Linq;
using System.Text;
using HslCommunication.BasicFramework;
using HslCommunication.Core;
using HslCommunication.Core.IMessage;
using HslCommunication.Instrument.DLT.Helper;
using HslCommunication.Reflection;

namespace HslCommunication.Instrument.DLT
{
	/// <summary>
	/// DLT698的虚拟服务器实现
	/// </summary>
	public class DLT698Server : DLT645Server
	{
		/// <inheritdoc />
		public override string Station
		{
			get
			{
				return base.Station;
			}
			set
			{
				base.Station = value;
				value.PadLeft(12, '0').ToHexBytes().CopyTo(allDatas["40010200"].Buffer, 2);
			}
		}

		/// <summary>
		/// 实例化一个默认的对象
		/// </summary>
		public DLT698Server()
		{
			base.ByteTransform = new ReverseBytesTransform();
			CreateAddressTags2();
		}

		/// <inheritdoc />
		protected override void CreateAddressTags()
		{
		}

		private void CreateAddressTags2()
		{
			AddDltTag("00000200", CreateDltBuffer(new int[5] { 1100, 2200, 3300, 4400, -5500 }), 2);
			AddDltTag("00100200", CreateDltBuffer(new uint[5] { 110u, 120u, 130u, 140u, 150u }), 2);
			AddDltTag("00200200", CreateDltBuffer(new uint[5] { 210u, 220u, 230u, 240u, 250u }), 2);
			AddDltTag("00300200", CreateDltBuffer(new uint[5] { 310u, 320u, 330u, 340u, 350u }), 2);
			AddDltTag("00400200", CreateDltBuffer(new uint[5] { 410u, 420u, 430u, 440u, 450u }), 2);
			AddDltTag("10000200", CreateDltBuffer(new uint[5] { 410000u, 420000u, 430000u, 440000u, 450000u }), 4);
			AddDltTag("10100200", CreateDltBuffer(new uint[5] { 510000u, 520000u, 530000u, 540000u, 550000u }), 4);
			AddDltTag("10200200", CreateDltBuffer(new uint[5] { 610000u, 620000u, 630000u, 640000u, 650000u }), 4);
			AddDltTag("10300200", CreateDltBuffer(new uint[5] { 710000u, 720000u, 730000u, 740000u, 750000u }), 4);
			AddDltTag("10400200", CreateDltBuffer(new uint[5] { 810000u, 820000u, 830000u, 840000u, 850000u }), 4);
			AddDltTag("20000200", CreateDltBuffer(new ushort[3] { 2231, 2331, 2431 }), 1);
			AddDltTag("20010200", CreateDltBuffer(new int[3] { 11000, 12000, 13000 }), 3);
			AddDltTag("20020200", CreateDltBuffer(new ushort[3] { 234, 244, 254 }), 1);
			AddDltTag("20030200", CreateDltBuffer(new ushort[3] { 334, 344, 354 }), 1);
			AddDltTag("20040200", CreateDltBuffer(1234), 1);
			AddDltTag("20050200", CreateDltBuffer(1334), 1);
			AddDltTag("20060200", CreateDltBuffer(1434), 1);
			AddDltTag("200A0200", CreateDltBuffer((short)12345), 3);
			AddDltTag("200F0200", CreateDltBuffer((ushort)5012), 2);
			AddDltTag("20100200", CreateDltBuffer((short)234), 1);
			AddDltTag("20110200", CreateDltBuffer((ushort)567), 2);
			AddDltTag("20120200", CreateDltBuffer((ushort)577), 2);
			AddDltTag("20170200", CreateDltBuffer(23456), 4);
			AddDltTag("20180200", CreateDltBuffer(33456), 4);
			AddDltTag("20190200", CreateDltBuffer(43456), 4);
			AddDltTag("201A0200", CreateDltBuffer(567u), 4);
			AddDltTag("20260200", CreateDltBuffer((ushort)567), 2);
			AddDltTag("20270200", CreateDltBuffer((ushort)667), 2);
			AddDltTag("20280200", CreateDltBuffer((ushort)767), 2);
			AddDltTag("40000200", "1C07E00114101B0B".ToHexBytes(), -1);
			allDatas["40000200"].Buffer = DLT698Helper.CreateDateTimeValue(DateTime.Now);
			AddDltTag("40010200", "0906000000000001".ToHexBytes(), -1);
			AddDltTag("40020200", "0906123456789001".ToHexBytes(), -1);
			AddDltTag("40030200", "0906111122223333".ToHexBytes(), -1);
			AddDltTag("41000200", CreateDltBuffer((short)1234), 0);
			AddDltTag("41010200", CreateDltBuffer((short)2234), 0);
			AddDltTag("41020200", CreateDltBuffer((short)100), 0);
			AddDltTag("41030200", CreateDltBuffer("1".PadLeft(32, '2')), 0);
			AddDltTag("41040200", CreateDltBuffer("5.0000"), 0);
			AddDltTag("41050200", CreateDltBuffer("5.1200"), 0);
			AddDltTag("41060200", CreateDltBuffer("10.200"), 0);
			AddDltTag("41070200", CreateDltBuffer("1230"), 0);
			AddDltTag("41080200", CreateDltBuffer("1230"), 0);
			AddDltTag("41090200", CreateDltBuffer(1234567u), 0);
			AddDltTag("410A0200", CreateDltBuffer(2234567u), 0);
			AddDltTag("410B0200", CreateDltBuffer("HslCommunication".PadRight(32, ' ')), 0);
		}

		private byte[] CreateDltBufferHelper<T>(T[] values, byte typeCode, Func<T, byte[]> func)
		{
			MemoryStream memoryStream = new MemoryStream();
			memoryStream.WriteByte(1);
			memoryStream.WriteByte((byte)values.Length);
			for (int i = 0; i < values.Length; i++)
			{
				memoryStream.WriteByte(typeCode);
				memoryStream.Write(func(values[i]));
			}
			return memoryStream.ToArray();
		}

		private byte[] CreateDltBuffer(short value)
		{
			byte[] array = new byte[3] { 16, 0, 0 };
			base.ByteTransform.TransByte(value).CopyTo(array, 1);
			return array;
		}

		private byte[] CreateDltBuffer(ushort value)
		{
			byte[] array = new byte[3] { 18, 0, 0 };
			base.ByteTransform.TransByte(value).CopyTo(array, 1);
			return array;
		}

		private byte[] CreateDltBuffer(int value)
		{
			byte[] array = new byte[5] { 5, 0, 0, 0, 0 };
			base.ByteTransform.TransByte(value).CopyTo(array, 1);
			return array;
		}

		private byte[] CreateDltBuffer(uint value)
		{
			byte[] array = new byte[5] { 6, 0, 0, 0, 0 };
			base.ByteTransform.TransByte(value).CopyTo(array, 1);
			return array;
		}

		private byte[] CreateDltBuffer(string value)
		{
			byte[] array = (string.IsNullOrEmpty(value) ? new byte[0] : Encoding.ASCII.GetBytes(value));
			byte[] array2 = new byte[array.Length + 2];
			array2[0] = 10;
			array2[1] = (byte)array.Length;
			array.CopyTo(array2, 2);
			return array2;
		}

		private byte[] CreateDltBuffer(int[] values)
		{
			return CreateDltBufferHelper(values, 5, base.ByteTransform.TransByte);
		}

		private byte[] CreateDltBuffer(uint[] values)
		{
			return CreateDltBufferHelper(values, 6, base.ByteTransform.TransByte);
		}

		private byte[] CreateDltBuffer(ushort[] values)
		{
			return CreateDltBufferHelper(values, 18, base.ByteTransform.TransByte);
		}

		/// <inheritdoc cref="M:HslCommunication.Core.IReadWriteNet.Write(System.String,System.Double)" />
		[HslMqttApi("WriteDouble", "")]
		public override OperateResult Write(string address, double value)
		{
			return Write(address, new double[1] { value });
		}

		/// <inheritdoc />
		[HslMqttApi("WriteDoubleArray", "")]
		public override OperateResult Write(string address, double[] values)
		{
			byte[] array = address.ToHexBytes();
			address = array.ToHexString();
			int num = DLT698Helper.GetScale(array[0], array[1], array[2]);
			if (num < 0)
			{
				num = -num;
			}
			if (allDatas.ContainsKey(address))
			{
				SetDoubleValue(allDatas[address].Buffer, 0, values, num);
			}
			return OperateResult.CreateSuccessResult();
		}

		private void SetDoubleValue(byte[] buffer, int index, double[] values, int scale)
		{
			if (index >= buffer.Length || buffer[index] == 0)
			{
				return;
			}
			if (buffer[index] == 1)
			{
				int num = buffer[index + 1];
				int num2 = 2;
				for (int i = 0; i < num; i++)
				{
					if (i < values.Length)
					{
						int num3 = SetDoubleValue(buffer, index + num2, values[i], scale);
						num2 += num3;
						if (num3 == 0)
						{
							break;
						}
					}
				}
			}
			else
			{
				SetDoubleValue(buffer, index, values[0], scale);
			}
		}

		private int SetDoubleValue(byte[] buffer, int index, double value, int scale)
		{
			if (buffer.Length == 0)
			{
				return 0;
			}
			if (index >= buffer.Length)
			{
				return 0;
			}
			if (buffer[index] == 18)
			{
				ushort value2 = (ushort)(value * Math.Pow(10.0, scale));
				base.ByteTransform.TransByte(value2).CopyTo(buffer, index + 1);
				return 3;
			}
			if (buffer[index] == 5)
			{
				int value3 = (int)(value * Math.Pow(10.0, scale));
				base.ByteTransform.TransByte(value3).CopyTo(buffer, index + 1);
				return 5;
			}
			if (buffer[index] == 6)
			{
				uint value4 = (uint)(value * Math.Pow(10.0, scale));
				base.ByteTransform.TransByte(value4).CopyTo(buffer, index + 1);
				return 5;
			}
			if (buffer[index] == 15)
			{
				buffer[index + 1] = (byte)(sbyte)(value * Math.Pow(10.0, scale));
				return 2;
			}
			if (buffer[index] == 16)
			{
				short value5 = (short)(value * Math.Pow(10.0, scale));
				base.ByteTransform.TransByte(value5).CopyTo(buffer, index + 1);
				return 3;
			}
			if (buffer[index] == 17)
			{
				buffer[index + 1] = (byte)(value * Math.Pow(10.0, scale));
				return 2;
			}
			if (buffer[index] == 20)
			{
				long value6 = (long)(value * Math.Pow(10.0, scale));
				base.ByteTransform.TransByte(value6).CopyTo(buffer, index + 1);
				return 9;
			}
			if (buffer[index] == 21)
			{
				ulong value7 = (ulong)(value * Math.Pow(10.0, scale));
				base.ByteTransform.TransByte(value7).CopyTo(buffer, index + 1);
				return 9;
			}
			if (buffer[index] == 23)
			{
				float value8 = (float)(value * Math.Pow(10.0, scale));
				base.ByteTransform.TransByte(value8).CopyTo(buffer, index + 1);
				return 5;
			}
			if (buffer[index] == 24)
			{
				double value9 = value * Math.Pow(10.0, scale);
				base.ByteTransform.TransByte(value9).CopyTo(buffer, index + 1);
				return 9;
			}
			return 0;
		}

		/// <inheritdoc />
		public override OperateResult<string[]> ReadStringArray(string address)
		{
			byte[] array = address.ToHexBytes();
			address = array.ToHexString();
			if (allDatas.ContainsKey(address))
			{
				byte[] array2 = SoftBasic.SpliceArray<byte>(new byte[8], allDatas[address].Buffer);
				array.CopyTo(array2, 3);
				int index = 8;
				return OperateResult.CreateSuccessResult(DLT698Helper.ExtraStringsValues(base.ByteTransform, array2, ref index));
			}
			return new OperateResult<string[]>(StringResources.Language.NotSupportedDataType);
		}

		/// <inheritdoc />
		public override OperateResult Write(string address, string value, Encoding encoding)
		{
			byte[] array = address.ToHexBytes();
			address = array.ToHexString();
			byte b = 0;
			if (allDatas.ContainsKey(address))
			{
				if (allDatas[address].Buffer[0] == 28)
				{
					b = WriteBufferToAddress(array, DLT698Helper.CreateDateTimeValue(DateTime.Parse(value)));
					if (b != 0)
					{
						return new OperateResult(b, DLT698Helper.GetErrorText(b));
					}
				}
				else if (allDatas[address].Buffer[0] == 9)
				{
					b = WriteBufferToAddress(array, DLT698Helper.CreateStringValueBuffer(value));
					if (b != 0)
					{
						return new OperateResult(b, DLT698Helper.GetErrorText(b));
					}
				}
				else if (allDatas[address].Buffer[0] == 10)
				{
					byte[] array2 = encoding.GetBytes(value);
					if (array2.Length > allDatas[address].Buffer.Length - 2)
					{
						array2 = array2.SelectBegin(allDatas[address].Buffer.Length - 2);
					}
					array2.CopyTo(allDatas[address].Buffer, 2);
				}
			}
			else
			{
				b = 6;
			}
			if (b == 0)
			{
				return OperateResult.CreateSuccessResult();
			}
			return new OperateResult(b, DLT698Helper.GetErrorText(b));
		}

		/// <inheritdoc />
		protected override INetMessage GetNewNetMessage()
		{
			return new DLT698Message();
		}

		/// <inheritdoc />
		protected override OperateResult<byte[]> ReadFromDLTCore(byte[] receive)
		{
			string text = receive.SelectMiddle(5, (receive[4] & 0xF) + 1).Reverse().ToArray()
				.ToHexString();
			int num = 5 + (receive[4] & 0xF) + 1 + 3;
			byte ca = 16;
			if (receive[3] == 129)
			{
				if (receive[num] == 1)
				{
					if (receive[num + 2] == 0 || receive[num + 2] == 1)
					{
						return DLT698Helper.BuildEntireCommand(1, Station, ca, DLT698Helper.CreatePreLogin(receive.SelectMiddle(num + 5, 10)));
					}
				}
				else if (receive[num] == 2)
				{
					return DLT698Helper.BuildEntireCommand(1, Station, ca, "82 00 54 4F 50 53 30 31 30 32 31 36 30 37 33 31 30 31 30 32 31 36 30 37 33 31 00 00 00 00 00 00 00 00 00 10 FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF 04 00 04 00 01 04 00 00 00 00 64 00 00 00 00".ToHexBytes());
				}
			}
			else if (receive[3] == 67)
			{
				byte[] array = receive.RemoveDouble(num, 2);
				bool security = false;
				if (array[0] == 16)
				{
					security = true;
					array = array.SelectMiddle(3, array[2]);
				}
				if (array[0] == 5)
				{
					if (array[1] == 1)
					{
						byte[] array2 = array.SelectMiddle(3, 4);
						string key = array2.ToHexString();
						if (allDatas.ContainsKey(key))
						{
							return DLT698Helper.BuildEntireCommand(3, Station, ca, CreateReadOneObject(array2, array[2], allDatas[key].Buffer, security));
						}
						return DLT698Helper.BuildEntireCommand(3, Station, ca, CreateErrorResponse(133, array2, array[2], 6, security));
					}
					if (array[1] == 2)
					{
						int num2 = array[3];
						MemoryStream memoryStream = new MemoryStream();
						for (int i = 0; i < num2; i++)
						{
							byte[] array3 = array.SelectMiddle(4 + i * 4, 4);
							string key2 = array3.ToHexString();
							if (allDatas.ContainsKey(key2))
							{
								memoryStream.Write(array3);
								memoryStream.WriteByte(1);
								memoryStream.Write(allDatas[key2].Buffer);
								continue;
							}
							return DLT698Helper.BuildEntireCommand(3, Station, ca, CreateErrorResponse(133, array3, array[2], 6, security));
						}
						return DLT698Helper.BuildEntireCommand(3, Station, ca, CreateReadMultiObject(array[2], num2, memoryStream.ToArray(), security));
					}
				}
				else if (array[0] == 6)
				{
					if (array[1] == 1)
					{
						byte[] array4 = array.SelectMiddle(3, 4);
						string key3 = array4.ToHexString();
						if (allDatas.ContainsKey(key3))
						{
							int index = 7;
							string[] array5 = DLT698Helper.ExtraStringsValues(base.ByteTransform, array, ref index);
							byte b = WriteBufferToAddress(array4, array.SelectMiddle(7, index - 7));
							return DLT698Helper.BuildEntireCommand(3, Station, ca, CreateErrorResponse(134, array4, array[2], 0, security));
						}
					}
				}
				else if (array[0] != 136)
				{
				}
			}
			return new OperateResult<byte[]>("");
		}

		private byte WriteBufferToAddress(byte[] addressBytes, byte[] buffer)
		{
			string key = addressBytes.ToHexString();
			if (allDatas.ContainsKey(key))
			{
				if (buffer == null || buffer.Length == 0)
				{
					return 9;
				}
				if (allDatas[key].Buffer[0] != buffer[0])
				{
					return 7;
				}
				if (allDatas[key].Buffer.Length != buffer.Length)
				{
					return 9;
				}
				buffer.CopyTo(allDatas[key].Buffer, 0);
				return 0;
			}
			return 6;
		}

		private byte[] CreateErrorResponse(byte code, byte[] address, byte piid, byte error, bool security)
		{
			byte[] array = new byte[(error != 0) ? 11 : 10];
			array[0] = code;
			array[1] = 1;
			array[2] = piid;
			address.CopyTo(array, 3);
			array[7] = 0;
			if (error != 0)
			{
				array[8] = error;
			}
			return security ? CreateSecurityResponse(array) : array;
		}

		private byte[] CreateSecurityResponse(byte[] data)
		{
			MemoryStream memoryStream = new MemoryStream();
			memoryStream.WriteByte(144);
			memoryStream.WriteByte(0);
			memoryStream.WriteByte((byte)data.Length);
			memoryStream.Write(data);
			memoryStream.WriteByte(1);
			memoryStream.WriteByte(0);
			memoryStream.WriteByte(4);
			memoryStream.WriteByte(18);
			memoryStream.WriteByte(52);
			memoryStream.WriteByte(86);
			memoryStream.WriteByte(120);
			return memoryStream.ToArray();
		}

		private byte[] CreateReadOneObject(byte[] address, byte piid, byte[] data, bool security)
		{
			byte[] array = new byte[10 + data.Length];
			array[0] = 133;
			array[1] = 1;
			array[2] = piid;
			address.CopyTo(array, 3);
			array[7] = 1;
			data.CopyTo(array, 8);
			return security ? CreateSecurityResponse(array) : array;
		}

		private byte[] CreateReadMultiObject(byte piid, int count, byte[] data, bool security)
		{
			byte[] array = new byte[6 + data.Length];
			array[0] = 133;
			array[1] = 2;
			array[2] = piid;
			array[3] = (byte)count;
			data.CopyTo(array, 4);
			return security ? CreateSecurityResponse(array) : array;
		}

		/// <inheritdoc />
		protected override bool CheckSerialReceiveDataComplete(byte[] buffer, int receivedLength)
		{
			MemoryStream ms = new MemoryStream();
			ms.Write(buffer.SelectBegin(receivedLength));
			return DLT645Helper.CheckReceiveDataComplete(ms);
		}

		/// <inheritdoc />
		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
			}
			base.Dispose(disposing);
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return $"DLT698Server[{base.Port}]";
		}
	}
}
