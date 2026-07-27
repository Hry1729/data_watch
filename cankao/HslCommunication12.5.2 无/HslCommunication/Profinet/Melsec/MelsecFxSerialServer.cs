using System;
using System.Text;
using System.Threading;
using HslCommunication.BasicFramework;
using HslCommunication.Core;
using HslCommunication.Core.Device;
using HslCommunication.Core.IMessage;
using HslCommunication.Core.Net;
using HslCommunication.Core.Pipe;
using HslCommunication.Profinet.Melsec.Helper;
using HslCommunication.Reflection;

namespace HslCommunication.Profinet.Melsec
{
	/// <summary>
	/// 三菱编程口协议的虚拟服务器，属性<see cref="P:HslCommunication.Profinet.Melsec.MelsecFxSerialServer.IsNewVersion" />的配置和客户端保持一致才能数据一致<br />
	/// Mitsubishi Programming interface protocol virtual server, attribute <see cref="P:HslCommunication.Profinet.Melsec.MelsecFxSerialServer.IsNewVersion" /> and the client configuration must be consistent data consistency
	/// </summary>
	public class MelsecFxSerialServer : DeviceServer
	{
		private SoftBuffer softBuffer;

		private const int DataPoolLength = 65536;

		/// <inheritdoc cref="P:HslCommunication.Profinet.Melsec.MelsecFxSerialOverTcp.IsNewVersion" />
		public bool IsNewVersion { get; set; }

		/// <summary>
		/// 实例化一个虚拟的FxLinks服务器<br />
		/// Instantiate a virtual FxLinks server
		/// </summary>
		public MelsecFxSerialServer()
		{
			base.WordLength = 1;
			base.ByteTransform = new RegularByteTransform();
			LogMsgFormatBinary = false;
			softBuffer = new SoftBuffer(131072);
		}

		/// <inheritdoc />
		[HslMqttApi("ReadByteArray", "")]
		public override OperateResult<byte[]> Read(string address, ushort length)
		{
			OperateResult<ushort> operateResult = MelsecFxSerialHelper.FxCalculateWordStartAddress(address, IsNewVersion);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<byte[]>(operateResult);
			}
			return OperateResult.CreateSuccessResult(softBuffer.GetBytes(operateResult.Content, length * 2));
		}

		/// <inheritdoc />
		[HslMqttApi("WriteByteArray", "")]
		public override OperateResult Write(string address, byte[] value)
		{
			OperateResult<ushort> operateResult = MelsecFxSerialHelper.FxCalculateWordStartAddress(address, IsNewVersion);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<byte[]>(operateResult);
			}
			softBuffer.SetBytes(value, operateResult.Content);
			return OperateResult.CreateSuccessResult();
		}

		/// <inheritdoc />
		[HslMqttApi("ReadBoolArray", "")]
		public override OperateResult<bool[]> ReadBool(string address, ushort length)
		{
			OperateResult<ushort, ushort, ushort> operateResult = MelsecFxSerialHelper.FxCalculateBoolStartAddress(address, IsNewVersion);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<bool[]>(operateResult);
			}
			return OperateResult.CreateSuccessResult(softBuffer.GetBool(operateResult.Content1 * 8 + operateResult.Content3, length));
		}

		/// <inheritdoc />
		[HslMqttApi("WriteBoolArray", "")]
		public override OperateResult Write(string address, bool[] value)
		{
			OperateResult<ushort, ushort, ushort> operateResult = MelsecFxSerialHelper.FxCalculateBoolStartAddress(address, IsNewVersion);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<bool[]>(operateResult);
			}
			softBuffer.SetBool(value, operateResult.Content1 * 8 + operateResult.Content3);
			return OperateResult.CreateSuccessResult();
		}

		/// <inheritdoc />
		protected override INetMessage GetNewNetMessage()
		{
			return null;
		}

		/// <inheritdoc />
		protected override OperateResult<byte[]> ReadFromCoreServer(PipeSession session, byte[] receive)
		{
			if (receive.Length == 1 && receive[0] == 5)
			{
				return OperateResult.CreateSuccessResult(new byte[1] { 6 });
			}
			if (receive[1] == 48)
			{
				return ReadByCommand(receive, 2);
			}
			if (receive[1] == 49)
			{
				return WriteByCommand(receive, 2);
			}
			if (receive[1] == 55)
			{
				return WriteBitByCommand(receive, 2, true);
			}
			if (receive[1] == 56)
			{
				return WriteBitByCommand(receive, 2, false);
			}
			if (receive[1] == 69)
			{
				if (receive[2] == 48)
				{
					return ReadByCommand(receive, 4);
				}
				if (receive[2] == 49)
				{
					return WriteByCommand(receive, 4);
				}
			}
			else if (receive[1] == 65)
			{
				session.Communication.Send(new byte[1] { 6 });
				if (session.Communication is PipeSerialPort pipeSerialPort)
				{
					Thread.Sleep(20);
					if (receive[2] == 49)
					{
						pipeSerialPort.GetPipe().BaudRate = 19200;
					}
					else if (receive[2] == 50)
					{
						pipeSerialPort.GetPipe().BaudRate = 38400;
					}
					else if (receive[2] == 51)
					{
						pipeSerialPort.GetPipe().BaudRate = 57600;
					}
					else if (receive[2] == 52)
					{
						pipeSerialPort.GetPipe().BaudRate = 115200;
					}
				}
			}
			return OperateResult.CreateSuccessResult(new byte[0]);
		}

		private OperateResult<byte[]> ReadByCommand(byte[] cmd, int offset)
		{
			int index = Convert.ToInt32(Encoding.ASCII.GetString(cmd, offset, 4), 16);
			int length = Convert.ToInt32(Encoding.ASCII.GetString(cmd, offset + 4, 2), 16);
			return OperateResult.CreateSuccessResult(BuildReadResponse(softBuffer.GetBytes(index, length)));
		}

		private OperateResult<byte[]> WriteByCommand(byte[] cmd, int offset)
		{
			int destIndex = Convert.ToInt32(Encoding.ASCII.GetString(cmd, offset, 4), 16);
			int num = Convert.ToInt32(Encoding.ASCII.GetString(cmd, offset + 4, 2), 16);
			if (num * 2 == cmd.Length - offset - 9)
			{
				byte[] array = new byte[num];
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = (byte)Convert.ToInt32(Encoding.ASCII.GetString(cmd, 6 + offset + i * 2, 2), 16);
				}
				softBuffer.SetBytes(array, destIndex);
				return OperateResult.CreateSuccessResult(new byte[2] { 6, 3 });
			}
			return OperateResult.CreateSuccessResult(new byte[0]);
		}

		private OperateResult<byte[]> WriteBitByCommand(byte[] cmd, int offset, bool value)
		{
			byte[] bytes = new byte[4]
			{
				cmd[offset + 2],
				cmd[offset + 3],
				cmd[offset],
				cmd[offset + 1]
			};
			int num = Convert.ToInt32(Encoding.ASCII.GetString(bytes), 16);
			if (IsNewVersion)
			{
				if (num >= 2048 && num < 10048)
				{
					num = 278528 + (num - 2048);
				}
				else if (num >= 1024 && num < 1280)
				{
					num = 288000 + (num - 1024);
				}
				else if (num >= 1280 && num < 1536)
				{
					num = 286208 + (num - 1280);
				}
				else if (num >= 0 && num < 1024)
				{
					num = 288512 + num;
				}
			}
			softBuffer.SetBool(value, num);
			return OperateResult.CreateSuccessResult(new byte[2] { 6, 3 });
		}

		private byte[] BuildReadResponse(byte[] data)
		{
			byte[] array = new byte[4 + data.Length * 2];
			array[0] = 2;
			for (int i = 0; i < data.Length; i++)
			{
				array[1 + 2 * i] = SoftBasic.BuildAsciiBytesFrom(data[i])[0];
				array[2 + 2 * i] = SoftBasic.BuildAsciiBytesFrom(data[i])[1];
			}
			array[array.Length - 3] = 3;
			MelsecHelper.FxCalculateCRC(array).CopyTo(array, array.Length - 2);
			return array;
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return $"MelsecFxSerialServer[{base.Port}]";
		}
	}
}
