using System;
using System.Collections.Generic;
using HslCommunication.Core.Device;
using HslCommunication.Core.IMessage;
using HslCommunication.Core.Net;

namespace HslCommunication.Enthernet.Tcp
{
	/// <summary>
	/// 用于调试的远程服务器
	/// </summary>
	public class DebugRemoteServer : DeviceServer
	{
		private Dictionary<int, BinaryCommunication> binaryCommunication;

		/// <inheritdoc cref="M:HslCommunication.Core.Net.BinaryCommunication.#ctor" />
		public DebugRemoteServer()
		{
			binaryCommunication = new Dictionary<int, BinaryCommunication>();
		}

		/// <summary>
		/// 设置当前的服务器关联的通信设备对象，默认的key为0，如果已经存在，则自动覆盖
		/// </summary>
		/// <param name="communication">设备通信对象</param>
		public void SetDeviceCommunication(BinaryCommunication communication)
		{
			if (binaryCommunication.ContainsKey(0))
			{
				binaryCommunication[0] = communication;
			}
			else
			{
				binaryCommunication.Add(0, communication);
			}
		}

		/// <summary>
		/// 新增加当前的服务器关联的通信设备对象，需要指定唯一标识，如果已经存在，则引发异常
		/// </summary>
		/// <param name="key">设备的唯一标识</param>
		/// <param name="communication">设备通信对象</param>
		public void AddDeviceCommunication(ushort key, BinaryCommunication communication)
		{
			binaryCommunication.Add(key, communication);
		}

		/// <summary>
		/// 移除一个设备的通信对象，如果存在的话，返回是否移除成功
		/// </summary>
		/// <param name="key">设备的唯一标识</param>
		public bool RemoveDeviceCommunication(ushort key)
		{
			if (binaryCommunication.ContainsKey(key))
			{
				return binaryCommunication.Remove(key);
			}
			return false;
		}

		/// <inheritdoc />
		protected override INetMessage GetNewNetMessage()
		{
			return new AdsNetMessage();
		}

		/// <inheritdoc />
		protected override OperateResult<byte[]> ReadFromCoreServer(PipeSession session, byte[] receive)
		{
			if (receive == null || receive.Length < 6)
			{
				return OperateResult.CreateSuccessResult(new byte[0]);
			}
			if (binaryCommunication == null)
			{
				base.LogNet?.WriteError(ToString(), "BinaryCommunication is null");
				return OperateResult.CreateSuccessResult(new byte[0]);
			}
			int num = BitConverter.ToUInt16(receive, 0);
			byte[] send = receive.RemoveBegin(6);
			if (!binaryCommunication.ContainsKey(num))
			{
				base.LogNet?.WriteError(ToString(), $"{session.Communication} Communication Key[{num}] is not exist. ");
				return OperateResult.CreateSuccessResult(new byte[0]);
			}
			OperateResult<byte[]> operateResult = binaryCommunication[num].ReadFromCoreServer(send, true, false);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<byte[]>(operateResult);
			}
			return OperateResult.CreateSuccessResult(operateResult.Content);
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return $"DebugRemoteServer[{base.Port}]";
		}
	}
}
