using System;
using HslCommunication.Core.Device;
using HslCommunication.Core.IMessage;

namespace HslCommunication.Profinet.Special
{
	/// <summary>
	/// FFU 用EC 风机风机通信协议，使用串口通讯，支持风机的转速控制，风机的地址设置，风机的状态读取等功能<br />
	/// </summary>
	public class EcFanMachine : DeviceSerialPort
	{
		/// <summary>
		/// 获取或设置风机的地址，范围是1-30
		/// </summary>
		public byte Station { get; set; } = 1;

		/// <summary>
		/// 实例化一个默认的对象
		/// </summary>
		public EcFanMachine()
		{
		}

		/// <inheritdoc />
		protected override INetMessage GetNewNetMessage()
		{
			return new EuFunMessage();
		}

		/// <inheritdoc />
		public override OperateResult<byte[]> UnpackResponseContent(byte[] send, byte[] response)
		{
			if (response == null || response.Length < 8)
			{
				return new OperateResult<byte[]>("Command is too short: " + response.ToHexString(' '));
			}
			if (response.Length >= 9 && response[0] == byte.MaxValue && response[8] == 13)
			{
				response = response.RemoveBegin(1);
			}
			byte[] array = CalculateCheck(response);
			if (CheckCrc(response))
			{
				return OperateResult.CreateSuccessResult(response);
			}
			byte[] array2 = response.CopyArray();
			if (array2[0] != 126)
			{
				array2[0] = 126;
				if (CheckCrc(array2))
				{
					return OperateResult.CreateSuccessResult(response);
				}
			}
			return new OperateResult<byte[]>("Check response crc error: " + response.ToHexString(' '));
		}

		/// <summary>
		/// 下发速度的命令，返回风机的状态数据<br />
		/// </summary>
		/// <param name="run">运行状态</param>
		/// <param name="emergency">应急模式设置位，True表示切换应急模式，False表示不操作</param>
		/// <param name="speed">转速</param>
		/// <returns></returns>
		public OperateResult<EcFanData> ControlSpeed(bool run, bool emergency, int speed)
		{
			OperateResult<byte[]> operateResult = ReadFromCoreServer(BuildCommand5A(Station, run, emergency, speed));
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<EcFanData>(operateResult);
			}
			return ParseResult5A(operateResult.Content);
		}

		/// <summary>
		/// 设置风机的地址，范围是1-30<br />
		/// </summary>
		/// <param name="id">新的地址</param>
		/// <returns>是否设置成功</returns>
		public OperateResult SetStation(byte id)
		{
			OperateResult<byte[]> operateResult = ReadFromCoreServer(BuildCommandA2(Station, id));
			if (!operateResult.IsSuccess)
			{
				return operateResult;
			}
			Station = id;
			return OperateResult.CreateSuccessResult();
		}

		/// <summary>
		/// 读取应急模式的转速
		/// </summary>
		/// <returns>转速信息</returns>
		public OperateResult<int> ReadSpeedEmergency()
		{
			OperateResult<byte[]> operateResult = ReadFromCoreServer(BuildReadSpeed(173, Station));
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<int>(operateResult);
			}
			return OperateResult.CreateSuccessResult((int)BitConverter.ToUInt16(operateResult.Content, 3));
		}

		/// <summary>
		/// 读取风机的最低转速
		/// </summary>
		/// <returns>转速信息</returns>
		public OperateResult<int> ReadSpeedMin()
		{
			OperateResult<byte[]> operateResult = ReadFromCoreServer(BuildReadSpeed(174, Station));
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<int>(operateResult);
			}
			return OperateResult.CreateSuccessResult((int)BitConverter.ToUInt16(operateResult.Content, 3));
		}

		/// <summary>
		/// 读取风机的最高转速
		/// </summary>
		/// <returns>转速信息</returns>
		public OperateResult<int> ReadSpeedMax()
		{
			OperateResult<byte[]> operateResult = ReadFromCoreServer(BuildReadSpeed(175, Station));
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<int>(operateResult);
			}
			return OperateResult.CreateSuccessResult((int)BitConverter.ToUInt16(operateResult.Content, 3));
		}

		/// <summary>
		/// 构建命令5A的命令数据<br />
		/// </summary>
		/// <param name="stat">站号</param>
		/// <param name="run">运行状态</param>
		/// <param name="emergency">应急模式</param>
		/// <param name="speed">速度</param>
		/// <returns>完整的命令</returns>
		public static byte[] BuildCommand5A(byte stat, bool run, bool emergency, int speed)
		{
			byte[] array = new byte[4] { 90, stat, 0, 0 };
			if (run)
			{
				array[2] |= 128;
			}
			if (emergency)
			{
				array[2] |= 64;
			}
			byte[] bytes = BitConverter.GetBytes(speed);
			array[2] |= (byte)(bytes[1] & 0xF);
			array[3] = bytes[0];
			return BuildEntireCommand(array);
		}

		internal static OperateResult<EcFanData> ParseResult5A(byte[] command)
		{
			try
			{
				EcFanData ecFanData = new EcFanData();
				ecFanData.EmergencyState = (command[2] & 0x40) == 64;
				ecFanData.RunState = (command[3] & 0x80) == 128;
				ecFanData.LockState = (command[3] & 0x40) == 64;
				ecFanData.OverHotState = (command[3] & 0x20) == 32;
				ecFanData.LostSpeedState = (command[3] & 0x10) == 16;
				ecFanData.Speed = ((command[3] & 0xF) << 8) | command[4];
				return OperateResult.CreateSuccessResult(ecFanData);
			}
			catch (Exception ex)
			{
				return new OperateResult<EcFanData>("Parse result error: " + ex.Message + Environment.NewLine + "Source Code: " + command.ToHexString(' '));
			}
		}

		internal static byte[] BuildCommandA2(byte stat, byte id)
		{
			return BuildEntireCommand(new byte[4] { 162, stat, id, 255 });
		}

		internal static byte[] BuildReadSpeed(byte code, byte stat)
		{
			return BuildEntireCommand(new byte[4] { code, stat, 44, 1 });
		}

		/// <summary>
		/// 传入命令，返回完整的命令数据
		/// </summary>
		/// <param name="command">读写数据的命令</param>
		/// <returns></returns>
		public static byte[] BuildEntireCommand(byte[] command)
		{
			byte[] array = new byte[command.Length + 4];
			array[0] = 126;
			command.CopyTo(array, 1);
			byte[] array2 = CalculateCheck(array);
			array[command.Length + 1] = array2[0];
			array[command.Length + 2] = array2[1];
			array[command.Length + 3] = 13;
			return array;
		}

		/// <summary>
		/// 计算校验和，返回2个字节的校验和<br />
		/// </summary>
		/// <param name="command">命令报文</param>
		/// <returns>检查结果值信息</returns>
		internal static byte[] CalculateCheck(byte[] command)
		{
			int num = 0;
			for (int i = 0; i < command.Length - 3 && i < 5; i++)
			{
				num += command[i];
			}
			num = ~num + 1;
			return new byte[2]
			{
				(byte)(num >> 8),
				(byte)(num & 0xFF)
			};
		}

		internal static bool CheckCrc(byte[] command)
		{
			if (command == null || command.Length < 8)
			{
				return false;
			}
			byte[] array = CalculateCheck(command);
			return command[5] == array[0] && command[6] == array[1];
		}

		/// <inheritdoc cref="M:HslCommunication.Core.IMessage.NetMessageBase.CheckReceiveDataComplete(System.Byte[],System.IO.MemoryStream)" />
		public static bool CheckReceiveDataComplete(byte[] send, byte[] response)
		{
			if (response == null)
			{
				return false;
			}
			if (response.Length >= 8 && response[7] == 13)
			{
				return true;
			}
			if (response.Length >= 9 && response[0] == byte.MaxValue && response[8] == 13)
			{
				return true;
			}
			return false;
		}
	}
}
