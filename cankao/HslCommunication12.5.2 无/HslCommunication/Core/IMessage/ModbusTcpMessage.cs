using System;

namespace HslCommunication.Core.IMessage
{
	/// <summary>
	/// Modbus-Tcp协议支持的消息解析类
	/// </summary>
	public class ModbusTcpMessage : NetMessageBase, INetMessage
	{
		/// <inheritdoc cref="P:HslCommunication.Core.IMessage.INetMessage.ProtocolHeadBytesLength" />
		public int ProtocolHeadBytesLength => 8;

		/// <summary>
		/// 获取或设置是否进行检查返回的消息ID和发送的消息ID是否一致，默认为true，也就是检查<br />
		/// Get or set whether to check whether the returned message ID is consistent with the sent message ID, the default is true, that is, check
		/// </summary>
		public bool IsCheckMessageId { get; set; } = true;

		/// <inheritdoc cref="P:HslCommunication.ModBus.ModbusRtu.StationCheckMatch" />
		public bool StationCheckMatch { get; set; } = true;

		/// <inheritdoc cref="M:HslCommunication.Core.IMessage.INetMessage.GetContentLengthByHeadBytes" />
		public int GetContentLengthByHeadBytes()
		{
			if (base.HeadBytes?.Length >= ProtocolHeadBytesLength)
			{
				int num = base.HeadBytes[4] * 256 + base.HeadBytes[5];
				int num2 = 0;
				if (num == 0)
				{
					base.HeadBytes = base.HeadBytes.RemoveBegin(1);
					num2 = base.HeadBytes[4] * 256 + base.HeadBytes[5] - 1;
				}
				else
				{
					num2 = Math.Min(num - 2, 300);
				}
				if (base.SendBytes != null && base.SendBytes.Length == 12)
				{
					if (base.SendBytes[7] == 3 || base.SendBytes[7] == 4)
					{
						int num3 = base.SendBytes[10] * 256 + base.SendBytes[11];
						if (num3 > 0 && num3 <= 127)
						{
							int num4 = num3 * 2 + 1;
							if (num4 != num2 && num == 6)
							{
								num2 = num4;
							}
						}
					}
					else if (base.SendBytes[7] == 1 || base.SendBytes[7] == 2)
					{
						int num5 = base.SendBytes[10] * 256 + base.SendBytes[11];
						if (num5 > 0 && num5 <= 2040)
						{
							int num6 = (num5 - 1) / 8 + 1 + 1;
							if (num6 != num2 && num == 6)
							{
								num2 = num6;
							}
						}
					}
				}
				return num2;
			}
			return 0;
		}

		/// <inheritdoc />
		public override int CheckMessageMatch(byte[] send, byte[] receive)
		{
			if (send == null)
			{
				return 1;
			}
			if (receive == null)
			{
				return 1;
			}
			if (send.Length < 8 || receive.Length < 8)
			{
				return 1;
			}
			if (IsCheckMessageId && (send[0] != receive[0] || send[1] != receive[1]))
			{
				return -1;
			}
			if (StationCheckMatch && send[6] != receive[6])
			{
				return -1;
			}
			return 1;
		}

		/// <inheritdoc cref="M:HslCommunication.Core.IMessage.INetMessage.GetHeadBytesIdentity" />
		public override int GetHeadBytesIdentity()
		{
			return base.HeadBytes[0] * 256 + base.HeadBytes[1];
		}
	}
}
