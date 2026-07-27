using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HslCommunication.Core;
using HslCommunication.Core.Device;
using HslCommunication.Core.IMessage;
using HslCommunication.Reflection;

namespace HslCommunication.Profinet.Keyence
{
	/// <summary>
	/// 老型号的基恩士类，适用于 Kv-10xx, 16xx, 24xx, 40xx, kv-80, kv-300，其中 xx 表示 AR/AT/DR/DT<br />
	/// Old Keenes class, suitable for Kv-10xx, 16xx, 24xx, 40xx, kv-80, kv-300, xx means AR/AT/DR/DT<br />
	/// </summary>
	public class KeyenceKvOld : DeviceTcpNet
	{
		/// <summary>
		/// 实例化一个默认的对象<br />
		/// Instantiate a default object
		/// </summary>
		public KeyenceKvOld()
		{
			base.WordLength = 1;
			base.ByteTransform = new RegularByteTransform();
			base.ByteTransform.IsStringReverseByteWord = true;
			LogMsgFormatBinary = false;
		}

		/// <summary>
		/// 使用指定的ip地址和端口号来初始化对象<br />
		/// Initialize the object with the specified IP address and port number
		/// </summary>
		/// <param name="ipAddress">Ip地址数据</param>
		/// <param name="port">端口号</param>
		public KeyenceKvOld(string ipAddress, int port = 8501)
			: this()
		{
			IpAddress = ipAddress;
			Port = port;
		}

		/// <inheritdoc />
		protected override INetMessage GetNewNetMessage()
		{
			return new SpecifiedCharacterMessage(13, 10);
		}

		/// <inheritdoc />
		protected override OperateResult InitializationOnConnect()
		{
			OperateResult<byte[]> operateResult = ReadFromCoreServer(CommunicationPipe, KeyenceNanoHelper.GetConnectCmd(0, false), true, true);
			if (!operateResult.IsSuccess)
			{
				return operateResult;
			}
			return OperateResult.CreateSuccessResult();
		}

		/// <inheritdoc />
		protected override OperateResult ExtraOnDisconnect()
		{
			OperateResult<byte[]> operateResult = ReadFromCoreServer(CommunicationPipe, KeyenceNanoHelper.GetDisConnectCmd(0, false), true, true);
			if (!operateResult.IsSuccess)
			{
				return operateResult;
			}
			return OperateResult.CreateSuccessResult();
		}

		/// <inheritdoc />
		protected override async Task<OperateResult> InitializationOnConnectAsync()
		{
			OperateResult<byte[]> result = await ReadFromCoreServerAsync(CommunicationPipe, KeyenceNanoHelper.GetConnectCmd(0, false), true, true).ConfigureAwait(false);
			if (!result.IsSuccess)
			{
				return result;
			}
			return OperateResult.CreateSuccessResult();
		}

		/// <inheritdoc />
		protected override async Task<OperateResult> ExtraOnDisconnectAsync()
		{
			OperateResult<byte[]> result = await ReadFromCoreServerAsync(CommunicationPipe, KeyenceNanoHelper.GetDisConnectCmd(0, false), true, true).ConfigureAwait(false);
			if (!result.IsSuccess)
			{
				return result;
			}
			return OperateResult.CreateSuccessResult();
		}

		/// <inheritdoc />
		[HslMqttApi("ReadByteArray", "")]
		public override OperateResult<byte[]> Read(string address, ushort length)
		{
			OperateResult<byte[]> operateResult = BuildReadWordCommand(address, false);
			if (!operateResult.IsSuccess)
			{
				return operateResult;
			}
			OperateResult<byte[]> operateResult2 = ReadFromCoreServer(operateResult.Content);
			if (!operateResult2.IsSuccess)
			{
				return operateResult2;
			}
			return ExtraResponseContent(address, operateResult2.Content, true);
		}

		/// <inheritdoc />
		[HslMqttApi("WriteByteArray", "")]
		public override OperateResult Write(string address, byte[] value)
		{
			OperateResult<byte[]> operateResult = BuildWriteWordCommand(address, value);
			if (!operateResult.IsSuccess)
			{
				return operateResult;
			}
			OperateResult<byte[]> operateResult2 = ReadFromCoreServer(operateResult.Content);
			if (!operateResult2.IsSuccess)
			{
				return operateResult2;
			}
			return ExtraResponseContent(address, operateResult2.Content, false);
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.Keyence.KeyenceKvOld.Read(System.String,System.UInt16)" />
		public override async Task<OperateResult<byte[]>> ReadAsync(string address, ushort length)
		{
			OperateResult<byte[]> build = BuildReadWordCommand(address, false);
			if (!build.IsSuccess)
			{
				return build;
			}
			OperateResult<byte[]> read = await ReadFromCoreServerAsync(build.Content).ConfigureAwait(false);
			if (!read.IsSuccess)
			{
				return read;
			}
			return ExtraResponseContent(address, read.Content, true);
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.Keyence.KeyenceKvOld.Write(System.String,System.Byte[])" />
		public override async Task<OperateResult> WriteAsync(string address, byte[] value)
		{
			OperateResult<byte[]> build = BuildWriteWordCommand(address, value);
			if (!build.IsSuccess)
			{
				return build;
			}
			OperateResult<byte[]> read = await ReadFromCoreServerAsync(build.Content).ConfigureAwait(false);
			if (!read.IsSuccess)
			{
				return read;
			}
			return ExtraResponseContent(address, read.Content, false);
		}

		private static bool CheckBoolOnWordAddress(string address)
		{
			return Regex.IsMatch(address, "^(DM|TM)[0-9]+\\.[0-9]+$", RegexOptions.IgnoreCase);
		}

		/// <inheritdoc />
		[HslMqttApi("ReadBool", "")]
		public override OperateResult<bool> ReadBool(string address)
		{
			if (CheckBoolOnWordAddress(address))
			{
				return ByteTransformHelper.GetResultFromArray(HslHelper.ReadBool(this, address, 1));
			}
			OperateResult<byte[]> operateResult = BuildReadWordCommand(address, true);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<bool>(operateResult);
			}
			OperateResult<byte[]> operateResult2 = ReadFromCoreServer(operateResult.Content);
			if (!operateResult2.IsSuccess)
			{
				return OperateResult.CreateFailedResult<bool>(operateResult2);
			}
			return ExtraBoolContent(address, operateResult2.Content, true);
		}

		/// <inheritdoc />
		[HslMqttApi("WriteBool", "")]
		public override OperateResult Write(string address, bool value)
		{
			if (CheckBoolOnWordAddress(address))
			{
				return HslHelper.WriteBool(this, address, new bool[1] { value });
			}
			OperateResult<byte[]> operateResult = BuildWriteBoolCommand(address, value);
			if (!operateResult.IsSuccess)
			{
				return operateResult;
			}
			OperateResult<byte[]> operateResult2 = ReadFromCoreServer(operateResult.Content);
			if (!operateResult2.IsSuccess)
			{
				return operateResult2;
			}
			return ExtraBoolContent(address, operateResult2.Content, false);
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.Keyence.KeyenceKvOld.ReadBool(System.String)" />
		public override async Task<OperateResult<bool>> ReadBoolAsync(string address)
		{
			if (CheckBoolOnWordAddress(address))
			{
				return ByteTransformHelper.GetResultFromArray(await HslHelper.ReadBoolAsync(this, address, 1).ConfigureAwait(false));
			}
			OperateResult<byte[]> build = BuildReadWordCommand(address, true);
			if (!build.IsSuccess)
			{
				return OperateResult.CreateFailedResult<bool>(build);
			}
			OperateResult<byte[]> read = await ReadFromCoreServerAsync(build.Content);
			if (!read.IsSuccess)
			{
				return OperateResult.CreateFailedResult<bool>(read);
			}
			return ExtraBoolContent(address, read.Content, true);
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.Keyence.KeyenceKvOld.Write(System.String,System.Boolean)" />
		public override async Task<OperateResult> WriteAsync(string address, bool value)
		{
			if (CheckBoolOnWordAddress(address))
			{
				return await HslHelper.WriteBoolAsync(this, address, new bool[1] { value }).ConfigureAwait(false);
			}
			OperateResult<byte[]> build = BuildWriteBoolCommand(address, value);
			if (!build.IsSuccess)
			{
				return build;
			}
			OperateResult<byte[]> read = await ReadFromCoreServerAsync(build.Content);
			if (!read.IsSuccess)
			{
				return read;
			}
			return ExtraBoolContent(address, read.Content, false);
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return $"KeyenceKvOld<{CommunicationPipe}>";
		}

		private static OperateResult<string> AnalysisAddress(string address, bool isBit)
		{
			if (isBit)
			{
				if (address.StartsWithAndNumber("CTH"))
				{
					return OperateResult.CreateSuccessResult(address);
				}
				if (address.StartsWithAndNumber("CTC"))
				{
					return OperateResult.CreateSuccessResult(address);
				}
				if (address.StartsWithAndNumber("C"))
				{
					return OperateResult.CreateSuccessResult(address);
				}
				if (address.StartsWithAndNumber("T"))
				{
					return OperateResult.CreateSuccessResult(address);
				}
				if (address.StartsWithAndNumber("R"))
				{
					address = address.Substring(1);
					int num = address.IndexOf(".");
					if (num > 0 && num < address.Length - 1)
					{
						return OperateResult.CreateSuccessResult(address.Substring(0, num) + address.Substring(num + 1).PadLeft(2, '0'));
					}
					return OperateResult.CreateSuccessResult(address);
				}
				if (Regex.IsMatch(address, "^[0-9]+$"))
				{
					return OperateResult.CreateSuccessResult(address);
				}
				return new OperateResult<string>(StringResources.Language.NotSupportedDataType);
			}
			if (address.StartsWithAndNumber("DM"))
			{
				return OperateResult.CreateSuccessResult(address);
			}
			if (address.StartsWithAndNumber("CC"))
			{
				return OperateResult.CreateSuccessResult("C" + address.Substring(2));
			}
			if (address.StartsWithAndNumber("CS"))
			{
				return OperateResult.CreateSuccessResult("C" + address.Substring(2));
			}
			if (address.StartsWithAndNumber("CTH"))
			{
				return OperateResult.CreateSuccessResult(address);
			}
			if (address.StartsWithAndNumber("TC"))
			{
				return OperateResult.CreateSuccessResult("T" + address.Substring(2));
			}
			if (address.StartsWithAndNumber("TS"))
			{
				return OperateResult.CreateSuccessResult("T" + address.Substring(2));
			}
			if (address.StartsWithAndNumber("AT"))
			{
				return OperateResult.CreateSuccessResult(address);
			}
			if (address.StartsWithAndNumber("TM"))
			{
				return OperateResult.CreateSuccessResult(address);
			}
			return new OperateResult<string>(StringResources.Language.NotSupportedDataType);
		}

		private static OperateResult<byte[]> BuildReadWordCommand(string address, bool isBit)
		{
			OperateResult<string> operateResult = AnalysisAddress(address, isBit);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<byte[]>(operateResult);
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("RD");
			stringBuilder.Append(" ");
			stringBuilder.Append(operateResult.Content);
			stringBuilder.Append("\r");
			return OperateResult.CreateSuccessResult(Encoding.ASCII.GetBytes(stringBuilder.ToString()));
		}

		private static OperateResult<byte[]> BuildWriteWordCommand(string address, byte[] value)
		{
			OperateResult<string> operateResult = AnalysisAddress(address, false);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<byte[]>(operateResult);
			}
			StringBuilder stringBuilder = new StringBuilder();
			if (address.StartsWithAndNumber("CC"))
			{
				stringBuilder.Append("WR");
			}
			else if (address.StartsWithAndNumber("CS"))
			{
				stringBuilder.Append("WS");
			}
			else if (address.StartsWithAndNumber("CTH"))
			{
				stringBuilder.Append("WR");
			}
			else if (address.StartsWithAndNumber("TC"))
			{
				stringBuilder.Append("WR");
			}
			else if (address.StartsWithAndNumber("TS"))
			{
				stringBuilder.Append("WS");
			}
			else
			{
				stringBuilder.Append("WR");
			}
			stringBuilder.Append(" ");
			stringBuilder.Append(operateResult.Content);
			stringBuilder.Append(" ");
			stringBuilder.Append(BitConverter.ToUInt16(value, 0));
			stringBuilder.Append("\r");
			return OperateResult.CreateSuccessResult(Encoding.ASCII.GetBytes(stringBuilder.ToString()));
		}

		private static OperateResult<byte[]> BuildWriteBoolCommand(string address, bool value)
		{
			OperateResult<string> operateResult = AnalysisAddress(address, true);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<byte[]>(operateResult);
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(value ? "ST" : "RS");
			stringBuilder.Append(" ");
			stringBuilder.Append(operateResult.Content);
			stringBuilder.Append("\r");
			return OperateResult.CreateSuccessResult(Encoding.ASCII.GetBytes(stringBuilder.ToString()));
		}

		private static OperateResult<bool> ExtraBoolContent(string address, byte[] ack, bool isRead)
		{
			if (ack.Length == 0)
			{
				return new OperateResult<bool>(StringResources.Language.MelsecFxReceiveZero);
			}
			if (ack[0] == 69)
			{
				return new OperateResult<bool>(KeyenceNanoHelper.GetErrorText(Encoding.ASCII.GetString(ack)));
			}
			if (!isRead)
			{
				string @string = Encoding.ASCII.GetString(ack);
				if (@string.StartsWith("OK", StringComparison.OrdinalIgnoreCase))
				{
					return OperateResult.CreateSuccessResult(true);
				}
				return new OperateResult<bool>(@string);
			}
			try
			{
				return OperateResult.CreateSuccessResult(ushort.Parse(Encoding.ASCII.GetString(ack).TrimEnd('\r', '\n').Split(',')[0]) != 0);
			}
			catch (Exception ex)
			{
				return new OperateResult<bool>(ex.Message + " Ack: " + ack.ToHexString(' '));
			}
		}

		private static OperateResult<byte[]> ExtraResponseContent(string address, byte[] ack, bool isRead)
		{
			if (ack.Length == 0)
			{
				return new OperateResult<byte[]>(StringResources.Language.MelsecFxReceiveZero);
			}
			if (ack[0] == 69)
			{
				return new OperateResult<byte[]>(KeyenceNanoHelper.GetErrorText(Encoding.ASCII.GetString(ack)));
			}
			if (!isRead)
			{
				string @string = Encoding.ASCII.GetString(ack);
				if (@string.StartsWith("OK", StringComparison.OrdinalIgnoreCase))
				{
					return OperateResult.CreateSuccessResult(new byte[0]);
				}
				return new OperateResult<byte[]>(@string);
			}
			try
			{
				string text = Encoding.ASCII.GetString(ack).TrimEnd('\r', '\n');
				if (address.StartsWithAndNumber("CC"))
				{
					return OperateResult.CreateSuccessResult(BitConverter.GetBytes(ushort.Parse(text.Split(',')[1])));
				}
				if (address.StartsWithAndNumber("CS"))
				{
					return OperateResult.CreateSuccessResult(BitConverter.GetBytes(ushort.Parse(text.Split(',')[2])));
				}
				if (address.StartsWithAndNumber("CTH"))
				{
					return OperateResult.CreateSuccessResult(BitConverter.GetBytes(ushort.Parse(text.Split(',')[1])));
				}
				if (address.StartsWithAndNumber("TC"))
				{
					return OperateResult.CreateSuccessResult(BitConverter.GetBytes(ushort.Parse(text.Split(',')[1])));
				}
				if (address.StartsWithAndNumber("TS"))
				{
					return OperateResult.CreateSuccessResult(BitConverter.GetBytes(ushort.Parse(text.Split(',')[2])));
				}
				if (address.StartsWithAndNumber("AT"))
				{
					return OperateResult.CreateSuccessResult(BitConverter.GetBytes(ushort.Parse(text.Split(',')[1])));
				}
				return OperateResult.CreateSuccessResult(BitConverter.GetBytes(ushort.Parse(text)));
			}
			catch (Exception ex)
			{
				return new OperateResult<byte[]>(ex.Message + " Ack: " + ack.ToHexString(' '));
			}
		}
	}
}
