using System;
using HslCommunication.Instrument.IEC.Helper;

namespace HslCommunication.Instrument.IEC
{
	/// <summary>
	/// IEC104的消息事件
	/// </summary>
	public class IEC104MessageEventArgs : EventArgs
	{
		/// <summary>
		/// 获取或设置当前的asdu信息
		/// </summary>
		public byte[] ASDU { get; }

		/// <summary>
		/// 类型标识
		/// </summary>
		public byte TypeID { get; set; }

		/// <summary>
		/// 地址是否连续
		/// </summary>
		public bool IsAddressContinuous { get; set; }

		/// <summary>
		/// 信息对象个数
		/// </summary>
		public int InfoObjectCount { get; set; }

		/// <summary>
		/// 传送原因
		/// </summary>
		public int TransmissionReason { get; set; }

		/// <summary>
		/// 站地址
		/// </summary>
		public int StationAddress { get; set; }

		/// <summary>
		/// 信息体
		/// </summary>
		public byte[] Body { get; set; }

		/// <summary>
		/// 指定asdu消息进行实例化一个对象
		/// </summary>
		/// <param name="asdu">asdu报文</param>
		public IEC104MessageEventArgs(byte[] asdu)
		{
			ASDU = asdu;
			if (asdu != null)
			{
				TypeID = asdu[0];
				IsAddressContinuous = (asdu[1] & 0x80) == 128;
				InfoObjectCount = asdu[1] & 0x7F;
				TransmissionReason = asdu[2] & 0x3F;
				StationAddress = BitConverter.ToUInt16(asdu, 4);
				Body = asdu.RemoveBegin(6);
			}
		}

		/// <inheritdoc cref="M:HslCommunication.Instrument.IEC.Helper.IECHelper.GetTransmissionReasonText(System.Int32)" />
		public string GetTransmissionReasonText()
		{
			return IECHelper.GetTransmissionReasonText(TransmissionReason);
		}

		/// <inheritdoc cref="M:HslCommunication.Instrument.IEC.Helper.IECHelper.GetTypeIDText(System.Int32)" />
		public string GetTypeIDText()
		{
			return IECHelper.GetTypeIDText(TypeID);
		}

		/// <summary>
		/// 获取当前的消息是否带时标信息
		/// </summary>
		/// <returns>是否携带时间标度信息</returns>
		public bool WithTimeInfo()
		{
			if (TypeID < 30)
			{
				return false;
			}
			if (TypeID < 45)
			{
				return true;
			}
			if (TypeID < 58)
			{
				return false;
			}
			if (TypeID < 70)
			{
				return true;
			}
			return false;
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return string.Format("{0} [{1}:{2}] Reason[{3}] Pub-Address[{4}]", GetTypeIDText(), IsAddressContinuous ? "Continuous" : "Uncontinuous", InfoObjectCount, GetTransmissionReasonText(), StationAddress);
		}
	}
}
