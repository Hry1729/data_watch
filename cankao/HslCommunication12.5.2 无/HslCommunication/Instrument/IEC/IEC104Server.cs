using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HslCommunication.Core.Device;
using HslCommunication.Core.IMessage;
using HslCommunication.Core.Net;
using HslCommunication.Instrument.IEC.Helper;

namespace HslCommunication.Instrument.IEC
{
	/// <summary>
	/// IEC104的虚拟服务器，支持总召唤操作，数据区包含单点遥信，双点遥信，归一化遥测值，标度化遥测值，短浮点遥测值，32位比特串，默认地址数量100，也可以手动设置更大的地址范围<br />
	/// The virtual server of IEC104 supports total call operation. The data area includes single-point remote signaling, dual-point remote signaling, 
	/// normalized telemetry values, scaled telemetry values, short floating-point telemetry values, 32-bit bit strings, and the default address range is 100. A larger address range can also be manually set
	/// </summary>
	/// <remarks>
	/// 如果需要手动设置更大的地址范围，例如短浮点遥测1000个地址，可以使用代码 server.YaoCeC.SetValues( 0, 1000 );<br />
	/// If you need to manually set a larger address range, such as 1000 short floating-point telemetry addresses, you can use the code server.YaoCeC.SetValues(0, 1000);
	/// </remarks>
	public class IEC104Server : DeviceServer
	{
		private IecValueServerObject<byte> list_单点遥信 = new IecValueServerObject<byte>(0, 100, 1, (IecValueObject<byte> m) => new byte[1] { (byte)(m.Quality | m.Value) });

		private IecValueServerObject<byte> list_双点遥信 = new IecValueServerObject<byte>(0, 100, 3, (IecValueObject<byte> m) => new byte[1] { (byte)(m.Quality | m.Value) });

		private IecValueServerObject<short> list_归一化遥测值 = new IecValueServerObject<short>(0, 100, 9, (IecValueObject<short> m) => BitConverter.GetBytes(m.Value));

		private IecValueServerObject<short> list_标度化遥测值 = new IecValueServerObject<short>(0, 100, 11, (IecValueObject<short> m) => BitConverter.GetBytes(m.Value));

		private IecValueServerObject<float> list_短浮点遥测值 = new IecValueServerObject<float>(0, 100, 13, (IecValueObject<float> m) => BitConverter.GetBytes(m.Value));

		private IecValueServerObject<uint> list_比特串 = new IecValueServerObject<uint>(0, 100, 7, (IecValueObject<uint> m) => BitConverter.GetBytes(m.Value));

		/// <summary>
		/// 获取或设置默认的数据单元公共地址，默认值为 1
		/// </summary>
		public byte PubAddress { get; set; } = 1;

		/// <summary>
		/// 单点遥信，类型ID为: M_SP_NA_1
		/// </summary>
		public IecValueServerObject<byte> SingleYaoXin => list_单点遥信;

		/// <summary>
		/// 双点遥信，类型ID为: M_DP_NA_1
		/// </summary>
		public IecValueServerObject<byte> DoubleYaoXin => list_双点遥信;

		/// <summary>
		/// 归一化遥测值，类型ID: M_ME_NA_1
		/// </summary>
		public IecValueServerObject<short> YaoCeA => list_归一化遥测值;

		/// <summary>
		/// 标度化遥测值，类型ID: M_ME_NB_1
		/// </summary>
		public IecValueServerObject<short> YaoCeB => list_标度化遥测值;

		/// <summary>
		/// 短浮点遥测值，类型ID: M_ME_NC_1
		/// </summary>
		public IecValueServerObject<float> YaoCeC => list_短浮点遥测值;

		/// <summary>
		/// 32位比特串，类型ID: M_BO_NA_1
		/// </summary>
		public IecValueServerObject<uint> BitArray => list_比特串;

		/// <summary>
		/// 实例化一个默认的对象
		/// </summary>
		public IEC104Server()
		{
			base.Port = 2404;
			list_单点遥信.OnIecValueChanged = IecValueChangedHelper;
			list_双点遥信.OnIecValueChanged = IecValueChangedHelper;
			list_归一化遥测值.OnIecValueChanged = IecValueChangedHelper;
			list_标度化遥测值.OnIecValueChanged = IecValueChangedHelper;
			list_短浮点遥测值.OnIecValueChanged = IecValueChangedHelper;
			list_比特串.OnIecValueChanged = IecValueChangedHelper;
		}

		/// <inheritdoc />
		protected override INetMessage GetNewNetMessage()
		{
			return new IEC104Message();
		}

		private void IecValueChangedHelper<T>(IecValueServerObject<T> iecValueServer, IecValueObject<T> iecValue)
		{
			PipeSession[] pipeSessions = GetCommunicationServer().GetPipeSessions();
			foreach (PipeSession pipeSession in pipeSessions)
			{
				SendAsdu(pipeSession, pipeSession.Tag as IECSessionInfo, iecValueServer.GetAsduBreakOut(iecValue, PubAddress));
			}
		}

		/// <inheritdoc />
		protected override OperateResult<byte[]> ReadFromCoreServer(PipeSession session, byte[] receive)
		{
			if (receive == null || receive.Length < 6)
			{
				return OperateResult.CreateSuccessResult(new byte[0]);
			}
			if (receive[0] != 104)
			{
				return OperateResult.CreateSuccessResult(new byte[0]);
			}
			if (session.Tag == null)
			{
				session.Tag = new IECSessionInfo();
			}
			if (!receive[2].GetBoolByIndex(0) && !receive[4].GetBoolByIndex(0))
			{
				IECSessionInfo iECSessionInfo = session.Tag as IECSessionInfo;
				iECSessionInfo.IncrRecvMessageID();
				StringBuilder stringBuilder = new StringBuilder(IECHelper.GetTypeIDText(receive[6]) + " 传送原因[" + IECHelper.GetTransmissionReasonText(receive[8]) + "] ");
				if (receive[6] == 100)
				{
					if (receive[8] == 6)
					{
						SendAsdu(session, iECSessionInfo, "64 01 07 00 01 00 00 00 00 14");
						SendAdsu(session, iECSessionInfo, list_单点遥信, 20, PubAddress);
						SendAdsu(session, iECSessionInfo, list_双点遥信, 20, PubAddress);
						SendAdsu(session, iECSessionInfo, list_归一化遥测值, 20, PubAddress);
						SendAdsu(session, iECSessionInfo, list_标度化遥测值, 20, PubAddress);
						SendAdsu(session, iECSessionInfo, list_短浮点遥测值, 20, PubAddress);
						SendAdsu(session, iECSessionInfo, list_比特串, 20, PubAddress);
						SendAsdu(session, iECSessionInfo, "64 01 0A 00 01 00 00 00 00 14");
					}
				}
				else if (receive[6] == 48)
				{
					SetReceiveValue(list_归一化遥测值, receive, BitConverter.ToInt16, stringBuilder);
				}
				else if (receive[6] == 49)
				{
					SetReceiveValue(list_标度化遥测值, receive, BitConverter.ToInt16, stringBuilder);
				}
				else if (receive[6] == 50)
				{
					SetReceiveValue(list_短浮点遥测值, receive, BitConverter.ToSingle, stringBuilder);
				}
				else if (receive[6] == 51)
				{
					SetReceiveValue(list_比特串, receive, BitConverter.ToUInt32, stringBuilder);
				}
				else if (receive[6] == 45)
				{
					SetReceiveValue(list_单点遥信, receive, (byte[] m, int index) => m[index], stringBuilder);
				}
				else if (receive[6] == 46)
				{
					SetReceiveValue(list_双点遥信, receive, (byte[] m, int index) => m[index], stringBuilder);
				}
				base.LogNet?.WriteInfo(ToString(), stringBuilder.ToString());
				return OperateResult.CreateSuccessResult(new byte[0]);
			}
			if (receive[2].GetBoolByIndex(0) && !receive[4].GetBoolByIndex(0))
			{
				if (receive[2].GetBoolByIndex(1))
				{
					base.LogNet?.WriteInfo(ToString(), "U帧报文-" + ((receive[2] == 7) ? "启动" : ((receive[2] == 19) ? "停止" : "测试")));
					if (receive[2] == 7)
					{
						return OperateResult.CreateSuccessResult(IECHelper.PackIEC104Message(11, 0, 0, 0, null));
					}
					if (receive[2] == 19)
					{
						return OperateResult.CreateSuccessResult(IECHelper.PackIEC104Message(35, 0, 0, 0, null));
					}
					if (receive[2] == 67)
					{
						return OperateResult.CreateSuccessResult(IECHelper.PackIEC104Message(131, 0, 0, 0, null));
					}
					return OperateResult.CreateSuccessResult(new byte[0]);
				}
				base.LogNet?.WriteInfo(ToString(), "S帧报文");
				return OperateResult.CreateSuccessResult(new byte[0]);
			}
			return base.ReadFromCoreServer(session, receive);
		}

		private void SetReceiveValue<T>(IecValueServerObject<T> serverObject, byte[] receive, Func<byte[], int, T> trans, StringBuilder stringBuilder)
		{
			int add = BitConverter.ToUInt16(receive, 12);
			T val = trans(receive, 15);
			IecValueObject<T> iecValueObject = serverObject.Values.FirstOrDefault((IecValueObject<T> m) => m.Address == add);
			if (iecValueObject != null)
			{
				iecValueObject.Value = val;
			}
			stringBuilder.Append($"Address[{add}] = {val} ");
		}

		private void SendAdsu<T>(PipeSession session, IECSessionInfo sessionInfo, IecValueServerObject<T> value, byte reason, byte station)
		{
			List<byte[]> asduCommand = value.GetAsduCommand(reason, station);
			for (int i = 0; i < asduCommand.Count; i++)
			{
				SendAsdu(session, sessionInfo, asduCommand[i]);
			}
		}

		private void SendAsdu(PipeSession session, IECSessionInfo sessionInfo, string asdu)
		{
			SendAsdu(session, sessionInfo, asdu.ToHexBytes());
		}

		private void SendAsdu(PipeSession session, IECSessionInfo sessionInfo, byte[] asdu)
		{
			byte[] array = IECHelper.PackIEC104Message((ushort)(sessionInfo.GetSendMessageID() * 2), (ushort)(sessionInfo.RecvMessageID * 2), asdu);
			session.Communication.Send(array);
			LogSendMessage(array, session);
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return $"IEC104Server[{base.Port}]";
		}
	}
}
