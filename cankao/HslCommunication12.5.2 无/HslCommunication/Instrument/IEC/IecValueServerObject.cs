using System;
using System.Collections.Generic;
using System.IO;

namespace HslCommunication.Instrument.IEC
{
	/// <summary>
	/// 服务器端的IEC数据对象信息，可以实例化不连续地址，或是连续地址信息
	/// </summary>
	/// <typeparam name="T">类型信息</typeparam>
	public class IecValueServerObject<T>
	{
		private Func<IecValueObject<T>, byte[]> _trans;

		private byte _typeID;

		/// <summary>
		/// 地址是否连续
		/// </summary>
		public bool IsAddressContinuous { get; private set; }

		/// <summary>
		/// 类型ID信息
		/// </summary>
		public byte TypeID => _typeID;

		/// <summary>
		/// IEC数据对象值
		/// </summary>
		public List<IecValueObject<T>> Values { get; private set; }

		/// <summary>
		/// 当值发生变化的时候
		/// </summary>
		public Action<IecValueServerObject<T>, IecValueObject<T>> OnIecValueChanged { get; set; }

		/// <summary>
		/// 实例化一个不连续地址的数据对象
		/// </summary>
		/// <param name="value">列表值</param>
		/// <param name="typeID">数据标识</param>
		/// <param name="trans">转换方法</param>
		public IecValueServerObject(List<IecValueObject<T>> value, byte typeID, Func<IecValueObject<T>, byte[]> trans)
		{
			_typeID = typeID;
			_trans = trans;
			SetValues(value);
		}

		/// <summary>
		/// 实例化一个连续地址的数据对象
		/// </summary>
		/// <param name="address">起始地址</param>
		/// <param name="length">长度</param>
		/// <param name="typeID">数据标识</param>
		/// <param name="trans">原始字节转换关系</param>
		public IecValueServerObject(ushort address, int length, byte typeID, Func<IecValueObject<T>, byte[]> trans)
		{
			_typeID = typeID;
			_trans = trans;
			SetValues(address, length);
		}

		/// <summary>
		/// 设置不连续的地址数据信息
		/// </summary>
		/// <param name="values">iec的值对象</param>
		public void SetValues(List<IecValueObject<T>> values)
		{
			Values = values;
			IsAddressContinuous = false;
			values.ForEach(delegate(IecValueObject<T> m)
			{
				m.OnValueChanged = Iec104ValueChanged;
			});
		}

		/// <summary>
		/// 设置连续的地址数据信息
		/// </summary>
		/// <param name="address">起始地址</param>
		/// <param name="length">数据长度</param>
		public void SetValues(ushort address, int length)
		{
			Values = new List<IecValueObject<T>>();
			for (int i = 0; i < length; i++)
			{
				IecValueObject<T> iecValueObject = new IecValueObject<T>();
				iecValueObject.Address = address + i;
				iecValueObject.OnValueChanged = Iec104ValueChanged;
				Values.Add(iecValueObject);
			}
			IsAddressContinuous = true;
		}

		private List<byte[]> GetAsduCommandByList(List<IecValueObject<T>> iecValues, bool isAddressContinuous, byte reason, byte station)
		{
			List<byte[]> list = new List<byte[]>();
			int i = 0;
			int num = _trans(iecValues[0]).Length + ((!isAddressContinuous) ? 3 : 0);
			if (_typeID != 1 && _typeID != 3)
			{
				num++;
			}
			if (isAddressContinuous)
			{
				int num3;
				for (; i < iecValues.Count; i += num3)
				{
					MemoryStream memoryStream = new MemoryStream();
					memoryStream.WriteByte(_typeID);
					int num2 = 128;
					num3 = Math.Min(val2: 240 / num, val1: iecValues.Count - i);
					if (num3 > 127)
					{
						num3 = 127;
					}
					memoryStream.WriteByte((byte)(num2 | num3));
					memoryStream.WriteByte(reason);
					memoryStream.WriteByte(0);
					memoryStream.WriteByte(station);
					memoryStream.WriteByte(0);
					memoryStream.Write(BitConverter.GetBytes((ushort)iecValues[i].Address));
					memoryStream.WriteByte(0);
					for (int j = 0; j < num3; j++)
					{
						memoryStream.Write(_trans(iecValues[j + i]));
						if (_typeID != 1 && _typeID != 3)
						{
							memoryStream.WriteByte(iecValues[j + i].Quality);
						}
					}
					list.Add(memoryStream.ToArray());
				}
			}
			else
			{
				int num5;
				for (; i < iecValues.Count; i += num5)
				{
					MemoryStream memoryStream2 = new MemoryStream();
					memoryStream2.WriteByte(_typeID);
					int num4 = 0;
					num5 = Math.Min(val2: 243 / num, val1: iecValues.Count - i);
					if (num5 > 127)
					{
						num5 = 127;
					}
					memoryStream2.WriteByte((byte)(num4 | num5));
					memoryStream2.WriteByte(reason);
					memoryStream2.WriteByte(0);
					memoryStream2.WriteByte(station);
					memoryStream2.WriteByte(0);
					for (int k = 0; k < num5; k++)
					{
						memoryStream2.Write(BitConverter.GetBytes((ushort)iecValues[k + i].Address));
						memoryStream2.WriteByte(0);
						memoryStream2.Write(_trans(iecValues[k + i]));
						if (_typeID != 1 && _typeID != 3)
						{
							memoryStream2.WriteByte(iecValues[k + i].Quality);
						}
					}
					list.Add(memoryStream2.ToArray());
				}
			}
			return list;
		}

		/// <summary>
		/// 获取发送的Asdu报文信息
		/// </summary>
		/// <param name="reason">传送原因</param>
		/// <param name="station">公共地址</param>
		/// <returns></returns>
		public List<byte[]> GetAsduCommand(byte reason, byte station)
		{
			return GetAsduCommandByList(Values, IsAddressContinuous, reason, station);
		}

		/// <summary>
		/// 获取一个突发的数据变化的报文信息
		/// </summary>
		/// <param name="iecValue">数据变化值</param>
		/// <param name="station">公共地址</param>
		/// <returns>发送客户端的报文</returns>
		public byte[] GetAsduBreakOut(IecValueObject<T> iecValue, byte station)
		{
			return GetAsduCommandByList(new List<IecValueObject<T>> { iecValue }, false, 3, station)[0];
		}

		private void Iec104ValueChanged(IecValueObject<T> iecValue)
		{
			OnIecValueChanged?.Invoke(this, iecValue);
		}
	}
}
