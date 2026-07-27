using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HslCommunication.BasicFramework;
using HslCommunication.Core;
using HslCommunication.Core.Device;
using HslCommunication.Core.IMessage;
using HslCommunication.Core.Pipe;
using HslCommunication.Profinet.Siemens.S7PlusHelper;

namespace HslCommunication.Profinet.Siemens
{
	/// <summary>
	/// 测试的基于S7 PLUS的协议，功能还未完成，无法调用
	/// </summary>
	public class SiemensS7Plus : DeviceTcpNet
	{
		private byte[] iso_head = new byte[20]
		{
			3, 0, 0, 36, 31, 224, 0, 0, 0, 1,
			0, 192, 1, 10, 193, 2, 1, 0, 194, 16
		};

		private byte[] destTSAP = Encoding.ASCII.GetBytes("SIMATIC-ROOT-HMI");

		private int pdu_length = 240;

		private SoftIncrementCount incrementCount;

		private SoftIncrementCount integrityIdCount;

		private byte protocolVersion;

		private bool useSSL = false;

		private OpenSslConnect sslConnect;

		private MemoryStream memoryStream = new MemoryStream();

		private AutoResetEvent autoResetEventPdu;

		private byte protoVersionBuffer = 2;

		private byte[] recieveBuffer;

		/// <summary>
		/// 西门子相关的本地TSAP参数信息<br />
		/// A parameter information related to Siemens
		/// </summary>
		public ushort LocalTSAP { get; set; } = 1536;

		/// <summary>
		/// 西门子相关的远程TSAP参数信息<br />
		/// A parameter information related to Siemens
		/// </summary>
		public byte[] DestTSAP
		{
			get
			{
				return destTSAP;
			}
			set
			{
				destTSAP = value;
			}
		}

		/// <summary>
		/// 最近一次的PDU类型
		/// </summary>
		public byte LastPDUType { get; set; }

		/// <summary>
		/// 获取当前西门子的PDU的长度信息，不同型号PLC的值会不一样。<br />
		/// Get the length information of the current Siemens PDU, the value of different types of PLC will be different.
		/// </summary>
		public int PDULength => pdu_length;

		/// <summary>
		/// 获取当前的通信会话ID信息
		/// </summary>
		public uint SessionID { get; private set; }

		/// <summary>
		/// 获取当前的PLC的订货号
		/// </summary>
		public string OrderNumber { get; private set; }

		/// <summary>
		/// 实例化一个西门子的S7协议的通讯对象 <br />
		/// Instantiate a communication object for a Siemens S7 protocol
		/// </summary>
		public SiemensS7Plus()
		{
			incrementCount = new SoftIncrementCount(65535L, 1L);
			integrityIdCount = new SoftIncrementCount(4294967295L, 0L);
			base.WordLength = 2;
			base.ByteTransform = new ReverseBytesTransform();
			autoResetEventPdu = new AutoResetEvent(false);
			base.ReceiveTimeOut = 30000;
		}

		/// <summary>
		/// 实例化一个西门子的S7协议的通讯对象并指定Ip地址 <br />
		/// Instantiate a communication object for a Siemens S7 protocol and specify an IP address
		/// </summary>
		/// <param name="ipAddress">Ip地址</param>
		/// <param name="port">端口号信息</param>
		public SiemensS7Plus(string ipAddress, int port = 102)
			: this()
		{
			IpAddress = ipAddress;
			Port = port;
		}

		/// <inheritdoc />
		protected override INetMessage GetNewNetMessage()
		{
			return new S7Message();
		}

		private OperateResult<byte[]> ReadFromCoreServer(CommunicationPipe pipe, List<byte[]> sends, bool hasResponseData = true, bool usePackHeader = true)
		{
			INetMessage newNetMessage = GetNewNetMessage();
			if (newNetMessage != null)
			{
				newNetMessage.SendBytes = ((sends.Count > 0) ? sends[0] : null);
			}
			OperateResult operateResult = sslConnect.Write(sends);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<byte[]>(operateResult);
			}
			if (base.ReceiveTimeOut < 0)
			{
				return OperateResult.CreateSuccessResult(new byte[0]);
			}
			if (!hasResponseData)
			{
				return OperateResult.CreateSuccessResult(new byte[0]);
			}
			if (base.SleepTime > 0)
			{
				HslHelper.ThreadSleep(base.SleepTime);
			}
			OperateResult<byte[]> operateResult2 = null;
			if (autoResetEventPdu.WaitOne(base.ReceiveTimeOut))
			{
				newNetMessage.HeadBytes = recieveBuffer;
				operateResult2 = OperateResult.CreateSuccessResult(recieveBuffer);
				if (!operateResult2.IsSuccess)
				{
					return operateResult2;
				}
				return OperateResult.CreateSuccessResult(operateResult2.Content);
			}
			pipe.RaisePipeError();
			pipe.CloseCommunication();
			sslConnect?.Dispose();
			return new OperateResult<byte[]>(-10000, StringResources.Language.ReceiveDataTimeout + base.ReceiveTimeOut);
		}

		private byte[] PackSendBuffer(byte[] buffer)
		{
			base.ByteTransform.TransByte(SessionID).CopyTo(buffer, 9);
			MemoryStream memoryStream = new MemoryStream();
			memoryStream.Write(buffer);
			memoryStream.Write("00 00 04 E8 89 69 00 12 00 00 00 00 89 6A 00 13 00 89 6B 00 04 00 00".ToHexBytes());
			S7Object.WriteUint32(memoryStream, (uint)integrityIdCount.GetCurrentValue());
			memoryStream.Write("00 00 00 00".ToHexBytes());
			buffer = memoryStream.ToArray();
			int value = (int)incrementCount.GetCurrentValue();
			buffer[7] = BitConverter.GetBytes(value)[1];
			buffer[8] = BitConverter.GetBytes(value)[0];
			return buffer;
		}

		/// <inheritdoc />
		public override OperateResult<byte[]> ReadFromCoreServer(CommunicationPipe pipe, byte[] send, bool hasResponseData = true, bool usePackAndUnpack = true)
		{
			if (!useSSL)
			{
				OperateResult<byte[]> operateResult;
				byte[] content;
				do
				{
					operateResult = base.ReadFromCoreServer(pipe, send, hasResponseData, usePackAndUnpack);
					if (!operateResult.IsSuccess)
					{
						return operateResult;
					}
					content = operateResult.Content;
				}
				while (content == null || content.Length < 4 || operateResult.Content[2] * 256 + operateResult.Content[3] == 7);
				LastPDUType = operateResult.Content[5];
				return operateResult;
			}
			if (send[0] != 114)
			{
				if (usePackAndUnpack)
				{
					send = PackSendBuffer(send);
				}
				return ReadFromCoreServer(pipe, BuildS7PlusPdu(send, send.Length, 2), hasResponseData, usePackAndUnpack);
			}
			return ReadFromCoreServer(pipe, new List<byte[]> { send }, hasResponseData, usePackAndUnpack);
		}

		/// <inheritdoc />
		protected override OperateResult InitializationOnConnect()
		{
			useSSL = false;
			CommunicationPipe.UseServerActivePush = false;
			incrementCount = new SoftIncrementCount(65535L, 1L);
			integrityIdCount = new SoftIncrementCount(4294967295L, 0L);
			SessionID = 0u;
			CommunicationPipe.ResetConnectErrorCount();
			OperateResult<byte[]> operateResult = ReadFromCoreServer(CommunicationPipe, GetISOTelegrams(), true, false);
			if (!operateResult.IsSuccess)
			{
				return operateResult;
			}
			if (operateResult.Content.Length == 36 && LastPDUType != 208)
			{
				return new OperateResult(65536, "errIsoConnect");
			}
			OperateResult<byte[]> operateResult2 = ReadFromCoreServer(CommunicationPipe, BuildInitSslCommand(), true, false);
			if (!operateResult2.IsSuccess)
			{
				return operateResult2;
			}
			if (base.ByteTransform.TransUInt16(operateResult2.Content, 14) != 1459)
			{
				CommunicationPipe.CloseCommunication();
				return new OperateResult("InitSsl failed: need 0x05b3 but actual 0x" + base.ByteTransform.TransUInt16(operateResult2.Content, 14).ToString("X"));
			}
			protocolVersion = operateResult2.Content[8];
			try
			{
				sslConnect?.Dispose();
				useSSL = true;
				sslConnect = new OpenSslConnect(SendS7Pdu, ReceiveS7Pdu);
				sslConnect.LogNet = base.LogNet;
			}
			catch (Exception ex)
			{
				return new OperateResult("Create OpenSslConnect failed: " + ex.Message);
			}
			CommunicationPipe.UseServerActivePush = true;
			base.InitializationOnConnect();
			try
			{
				sslConnect.SSLConnect();
				sslConnect.SSLInit();
			}
			catch (Exception ex2)
			{
				return new OperateResult(ex2.Message);
			}
			HslHelper.ThreadSleep(100);
			OperateResult<byte[]> operateResult3 = ReadFromCoreServer(CommunicationPipe, BuildCreateObjectRequest(), true, false);
			if (!operateResult3.IsSuccess)
			{
				return operateResult3;
			}
			int index = 10;
			S7Object.GetValueUint64(operateResult3.Content, ref index);
			index++;
			SessionID = S7Object.GetValueUint32(operateResult3.Content, ref index);
			S7Object.GetValueUint32(operateResult3.Content, ref index);
			int num = index;
			OrderNumber = Encoding.ASCII.GetString(ExtraResponseData(operateResult3.Content, 319u, ref index).Content);
			index = num;
			OperateResult<byte[]> operateResult4 = ReadFromCoreServer(send: BuildMultiSetRequest(ExtraResponseData(operateResult3.Content, 306u, ref index).Content), pipe: CommunicationPipe, hasResponseData: true, usePackAndUnpack: false);
			if (!operateResult4.IsSuccess)
			{
				return operateResult4;
			}
			return OperateResult.CreateSuccessResult();
		}

		/// <inheritdoc />
		protected override bool DecideWhetherQAMessage(CommunicationPipe pipe, OperateResult<byte[]> receive)
		{
			byte[] content = receive.Content;
			if (content != null && content.Length >= 4)
			{
				if (receive.Content[2] * 256 + receive.Content[3] == 7)
				{
					return false;
				}
				if (receive.Content.Length > 7)
				{
					sslConnect.ReadCompleted(receive.Content.RemoveBegin(7));
					if (receive.Content[7] == 22)
					{
						sslConnect.SSLInit();
					}
				}
			}
			return false;
		}

		/// <inheritdoc />
		protected override OperateResult ExtraOnDisconnect()
		{
			sslConnect?.Dispose();
			return base.ExtraOnDisconnect();
		}

		/// <inheritdoc />
		protected override async Task<OperateResult> InitializationOnConnectAsync()
		{
			useSSL = false;
			CommunicationPipe.UseServerActivePush = false;
			incrementCount = new SoftIncrementCount(65535L, 1L);
			integrityIdCount = new SoftIncrementCount(4294967295L, 0L);
			SessionID = 0u;
			CommunicationPipe.ResetConnectErrorCount();
			OperateResult<byte[]> read1 = await ReadFromCoreServerAsync(CommunicationPipe, GetISOTelegrams(), true, false);
			if (!read1.IsSuccess)
			{
				return read1;
			}
			if (read1.Content.Length == 36 && LastPDUType != 208)
			{
				return new OperateResult(65536, "errIsoConnect");
			}
			OperateResult<byte[]> read2 = await ReadFromCoreServerAsync(CommunicationPipe, BuildInitSslCommand(), true, false);
			if (!read2.IsSuccess)
			{
				return read2;
			}
			if (base.ByteTransform.TransUInt16(read2.Content, 14) != 1459)
			{
				await CommunicationPipe.CloseCommunicationAsync();
				return new OperateResult("InitSsl failed: need 0x05b3 but actual 0x" + base.ByteTransform.TransUInt16(read2.Content, 14).ToString("X"));
			}
			protocolVersion = read2.Content[8];
			try
			{
				sslConnect?.Dispose();
				useSSL = true;
				sslConnect = new OpenSslConnect(SendS7Pdu, ReceiveS7Pdu);
				sslConnect.LogNet = base.LogNet;
			}
			catch (Exception ex)
			{
				return new OperateResult("Create OpenSslConnect failed: " + ex.Message);
			}
			CommunicationPipe.UseServerActivePush = true;
			base.InitializationOnConnect();
			try
			{
				sslConnect.SSLConnect();
				sslConnect.SSLInit();
			}
			catch (Exception ex2)
			{
				return new OperateResult(ex2.Message);
			}
			HslHelper.ThreadSleep(100);
			OperateResult<byte[]> sessionRead = await ReadFromCoreServerAsync(CommunicationPipe, BuildCreateObjectRequest(), true, false);
			if (!sessionRead.IsSuccess)
			{
				return sessionRead;
			}
			int index = 10;
			S7Object.GetValueUint64(sessionRead.Content, ref index);
			index++;
			SessionID = S7Object.GetValueUint32(sessionRead.Content, ref index);
			S7Object.GetValueUint32(sessionRead.Content, ref index);
			int indexTmp = index;
			OrderNumber = Encoding.ASCII.GetString(ExtraResponseData(sessionRead.Content, 319u, ref index).Content);
			index = indexTmp;
			OperateResult<byte[]> multiSet = await ReadFromCoreServerAsync(send: BuildMultiSetRequest(ExtraResponseData(sessionRead.Content, 306u, ref index).Content), pipe: CommunicationPipe, hasResponseData: true, usePackAndUnpack: false);
			if (!multiSet.IsSuccess)
			{
				return multiSet;
			}
			return OperateResult.CreateSuccessResult();
		}

		/// <inheritdoc />
		public override async Task<OperateResult<byte[]>> ReadFromCoreServerAsync(CommunicationPipe pipe, byte[] send, bool hasResponseData = true, bool usePackAndUnpack = true)
		{
			if (!useSSL)
			{
				OperateResult<byte[]> read;
				byte[] content;
				do
				{
					read = await base.ReadFromCoreServerAsync(pipe, send, hasResponseData, usePackAndUnpack);
					if (!read.IsSuccess)
					{
						return read;
					}
					content = read.Content;
				}
				while (content == null || content.Length < 4 || read.Content[2] * 256 + read.Content[3] == 7);
				LastPDUType = read.Content[5];
				return read;
			}
			if (send[0] != 114)
			{
				if (usePackAndUnpack)
				{
					send = PackSendBuffer(send);
				}
				return await Task.Run(() => ReadFromCoreServer(pipe, BuildS7PlusPdu(send, send.Length, 2), hasResponseData, usePackAndUnpack));
			}
			return await Task.Run(() => ReadFromCoreServer(pipe, new List<byte[]> { send }, hasResponseData, usePackAndUnpack));
		}

		private S7Tag[] CreateTagsFromAddress(string address, ushort length)
		{
			return CreateTagsFromAddress(address.Split(new char[1] { ';' }, StringSplitOptions.RemoveEmptyEntries));
		}

		private S7Tag[] CreateTagsFromAddress(string[] address)
		{
			List<S7Tag> list = new List<S7Tag>();
			for (int i = 0; i < address.Length; i++)
			{
				string[] source = address[i].Split(new char[1] { '.' }, StringSplitOptions.RemoveEmptyEntries);
				S7Tag s7Tag = new S7Tag();
				s7Tag.LID = source.Select((string m) => Convert.ToUInt32(m, 16)).ToList();
				list.Add(s7Tag);
			}
			return list.ToArray();
		}

		/// <inheritdoc />
		public override OperateResult<byte[]> Read(string address, ushort length)
		{
			OperateResult<List<S7Value>> operateResult = ReadTags(CreateTagsFromAddress(address, length));
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<byte[]>(operateResult);
			}
			List<S7Value> content = operateResult.Content;
			if (content != null && content.Count > 0)
			{
				if (operateResult.Content.Count == 1)
				{
					return OperateResult.CreateSuccessResult(operateResult.Content[0].Buffer);
				}
				return OperateResult.CreateSuccessResult(SoftBasic.SpliceArray(operateResult.Content.Select((S7Value m) => m.Buffer).ToArray()));
			}
			return new OperateResult<byte[]>("S7 Value null, address not exist!");
		}

		/// <inheritdoc />
		/// <remarks>
		/// 此处写入的数据需要带标志位，类型，数据信息，所以灵活的支持任意的数据写入，如果需要写入 uint8[] 数据本身，使用方法 <see cref="M:HslCommunication.Profinet.Siemens.SiemensS7Plus.WriteByte(System.String,System.Byte[])" />
		/// </remarks>
		public override OperateResult Write(string address, byte[] value)
		{
			return WriteTag(address, value);
		}

		/// <inheritdoc />
		public override OperateResult<bool[]> ReadBool(string address, ushort length)
		{
			OperateResult<List<S7Value>> operateResult = ReadTags(CreateTagsFromAddress(address, length));
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<bool[]>(operateResult);
			}
			List<S7Value> content = operateResult.Content;
			if (content != null && content.Count > 0)
			{
				return OperateResult.CreateSuccessResult(operateResult.Content[0].Buffer.ToBoolArray().SelectBegin(length));
			}
			return new OperateResult<bool[]>("S7 Value null, address not exist!");
		}

		/// <inheritdoc />
		public override OperateResult Write(string address, bool value)
		{
			return WriteTag(address, S7Value.GetBufferBool(base.ByteTransform, value));
		}

		/// <summary>
		/// 使用类型代号为 0x0a 写入到指定的地址里，返回是否写入成功的结果对象
		/// </summary>
		/// <param name="address">地址信息</param>
		/// <param name="value">二进制的值</param>
		/// <returns>是否写入成功</returns>
		public OperateResult WriteByte(string address, byte[] value)
		{
			return WriteTag(address, S7Value.GetBufferUInt8(base.ByteTransform, value));
		}

		/// <inheritdoc />
		public override async Task<OperateResult<byte[]>> ReadAsync(string address, ushort length)
		{
			OperateResult<List<S7Value>> read = await ReadTagsAsync(CreateTagsFromAddress(address, length));
			if (!read.IsSuccess)
			{
				return OperateResult.CreateFailedResult<byte[]>(read);
			}
			List<S7Value> content = read.Content;
			if (content != null && content.Count > 0)
			{
				if (read.Content.Count == 1)
				{
					return OperateResult.CreateSuccessResult(read.Content[0].Buffer);
				}
				return OperateResult.CreateSuccessResult(SoftBasic.SpliceArray(read.Content.Select((S7Value m) => m.Buffer).ToArray()));
			}
			return new OperateResult<byte[]>("S7 Value null, address not exist!");
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.Siemens.SiemensS7Plus.Write(System.String,System.Byte[])" />
		public override async Task<OperateResult> WriteAsync(string address, byte[] value)
		{
			return await WriteTagAsync(address, value);
		}

		/// <inheritdoc />
		public override async Task<OperateResult<bool[]>> ReadBoolAsync(string address, ushort length)
		{
			OperateResult<List<S7Value>> read = await ReadTagsAsync(CreateTagsFromAddress(address, length));
			if (!read.IsSuccess)
			{
				return OperateResult.CreateFailedResult<bool[]>(read);
			}
			List<S7Value> content = read.Content;
			if (content != null && content.Count > 0)
			{
				return OperateResult.CreateSuccessResult(read.Content[0].Buffer.ToBoolArray().SelectBegin(length));
			}
			return new OperateResult<bool[]>("S7 Value null, address not exist!");
		}

		/// <inheritdoc />
		public override async Task<OperateResult> WriteAsync(string address, bool value)
		{
			return await WriteTagAsync(address, S7Value.GetBufferBool(base.ByteTransform, value));
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.Siemens.SiemensS7Plus.WriteByte(System.String,System.Byte[])" />
		public async Task<OperateResult> WriteByteAsync(string address, byte[] value)
		{
			return await WriteTagAsync(address, S7Value.GetBufferUInt8(base.ByteTransform, value));
		}

		/// <inheritdoc />
		public override OperateResult<string> ReadString(string address, ushort length, Encoding encoding)
		{
			OperateResult<byte[]> operateResult = Read(address, length);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<string>(operateResult);
			}
			return OperateResult.CreateSuccessResult(encoding.GetString(operateResult.Content, 2, operateResult.Content[1]));
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.Siemens.SiemensS7Net.Write(System.String,System.Byte)" />
		public OperateResult Write(string address, byte value)
		{
			return WriteTag(address, S7Value.GetBufferUInt8(base.ByteTransform, value));
		}

		/// <inheritdoc />
		public override OperateResult Write(string address, short value)
		{
			return WriteTag(address, S7Value.GetBufferInt16(base.ByteTransform, value));
		}

		/// <inheritdoc />
		public override OperateResult Write(string address, ushort value)
		{
			return WriteTag(address, S7Value.GetBufferUInt16(base.ByteTransform, value));
		}

		/// <inheritdoc />
		public override OperateResult Write(string address, int value)
		{
			return WriteTag(address, S7Value.GetBufferInt32(base.ByteTransform, value));
		}

		/// <inheritdoc />
		public override OperateResult Write(string address, int[] values)
		{
			return WriteTag(address, S7Value.GetBufferInt32(base.ByteTransform, values));
		}

		/// <inheritdoc />
		public override OperateResult Write(string address, uint value)
		{
			return WriteTag(address, S7Value.GetBufferUInt32(base.ByteTransform, value));
		}

		/// <inheritdoc />
		public override OperateResult Write(string address, uint[] values)
		{
			return WriteTag(address, S7Value.GetBufferUInt32(base.ByteTransform, values));
		}

		/// <inheritdoc />
		public override OperateResult Write(string address, float value)
		{
			return WriteTag(address, S7Value.GetBufferFloat(base.ByteTransform, value));
		}

		/// <inheritdoc />
		public override OperateResult Write(string address, float[] values)
		{
			return WriteTag(address, S7Value.GetBufferFloat(base.ByteTransform, values));
		}

		/// <inheritdoc />
		public override OperateResult Write(string address, double value)
		{
			return WriteTag(address, S7Value.GetBufferDouble(base.ByteTransform, value));
		}

		/// <inheritdoc />
		public override OperateResult Write(string address, double[] values)
		{
			return WriteTag(address, S7Value.GetBufferDouble(base.ByteTransform, values));
		}

		/// <inheritdoc />
		public override OperateResult Write(string address, string value)
		{
			return Write(address, value, Encoding.Default);
		}

		/// <inheritdoc />
		public override OperateResult Write(string address, string value, Encoding encoding)
		{
			OperateResult<byte[]> operateResult = Read(address, 1);
			if (!operateResult.IsSuccess)
			{
				return operateResult;
			}
			byte[] bytes = encoding.GetBytes(value);
			if (bytes.Length > operateResult.Content[0])
			{
				return new OperateResult("string length is too big, must less than :" + operateResult.Content[0]);
			}
			bytes.CopyTo(operateResult.Content, 2);
			operateResult.Content[1] = (byte)bytes.Length;
			return OperateResult.CreateSuccessResult(encoding.GetString(operateResult.Content, 2, operateResult.Content[1]));
		}

		/// <inheritdoc />
		public override async Task<OperateResult<string>> ReadStringAsync(string address, ushort length, Encoding encoding)
		{
			OperateResult<byte[]> read = await ReadAsync(address, length);
			if (!read.IsSuccess)
			{
				return OperateResult.CreateFailedResult<string>(read);
			}
			return OperateResult.CreateSuccessResult(encoding.GetString(read.Content, 2, read.Content[1]));
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.Siemens.SiemensS7Net.Write(System.String,System.Byte)" />
		public async Task<OperateResult> WriteAsync(string address, byte value)
		{
			return await WriteTagAsync(address, S7Value.GetBufferUInt8(base.ByteTransform, value));
		}

		/// <inheritdoc />
		public override async Task<OperateResult> WriteAsync(string address, short value)
		{
			return await WriteTagAsync(address, S7Value.GetBufferInt16(base.ByteTransform, value));
		}

		/// <inheritdoc />
		public override async Task<OperateResult> WriteAsync(string address, ushort value)
		{
			return await WriteTagAsync(address, S7Value.GetBufferUInt16(base.ByteTransform, value));
		}

		/// <inheritdoc />
		public override async Task<OperateResult> WriteAsync(string address, int value)
		{
			return await WriteTagAsync(address, S7Value.GetBufferInt32(base.ByteTransform, value));
		}

		/// <inheritdoc />
		public override async Task<OperateResult> WriteAsync(string address, int[] values)
		{
			return await WriteTagAsync(address, S7Value.GetBufferInt32(base.ByteTransform, values));
		}

		/// <inheritdoc />
		public override async Task<OperateResult> WriteAsync(string address, uint value)
		{
			return await WriteTagAsync(address, S7Value.GetBufferUInt32(base.ByteTransform, value));
		}

		/// <inheritdoc />
		public override async Task<OperateResult> WriteAsync(string address, uint[] values)
		{
			return await WriteTagAsync(address, S7Value.GetBufferUInt32(base.ByteTransform, values));
		}

		/// <inheritdoc />
		public override async Task<OperateResult> WriteAsync(string address, float value)
		{
			return await WriteTagAsync(address, S7Value.GetBufferFloat(base.ByteTransform, value));
		}

		/// <inheritdoc />
		public override async Task<OperateResult> WriteAsync(string address, float[] values)
		{
			return await WriteTagAsync(address, S7Value.GetBufferFloat(base.ByteTransform, values));
		}

		/// <inheritdoc />
		public override async Task<OperateResult> WriteAsync(string address, double value)
		{
			return await WriteTagAsync(address, S7Value.GetBufferDouble(base.ByteTransform, value));
		}

		/// <inheritdoc />
		public override async Task<OperateResult> WriteAsync(string address, double[] values)
		{
			return await WriteTagAsync(address, S7Value.GetBufferDouble(base.ByteTransform, values));
		}

		/// <inheritdoc />
		public override async Task<OperateResult> WriteAsync(string address, string value)
		{
			return await WriteAsync(address, value, Encoding.Default);
		}

		/// <summary>
		/// 浏览当前PLC的DB块列表，并且拿到用户自定义的DB块的关联ID信息<br />
		/// Browse the DB block list of the current PLC and get the associated ID information of the user-defined DB block
		/// </summary>
		/// <returns>DB块列表信息</returns>
		public OperateResult<List<S7Object>> BrowerDB()
		{
			OperateResult<byte[]> operateResult = ReadFromCoreServer(BuildBrowseRequest(3u, new uint[3] { 233u, 2521u, 4288u })[0]);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<List<S7Object>>(operateResult);
			}
			List<S7Object> list = ExploreResponse(operateResult.Content);
			for (int i = 0; i < list.Count; i++)
			{
				S7Object[] array = list[i].SubObjects.Where((S7Object m) => m.RelationId >> 16 == 35342).ToArray();
				OperateResult<byte[]> operateResult2 = ReadFromCoreServer(BuildMultiGetRequest(array));
				if (!operateResult2.IsSuccess)
				{
					return OperateResult.CreateFailedResult<List<S7Object>>(operateResult2);
				}
				for (int j = 0; j < array.Length; j++)
				{
					array[j].RelationId2 = base.ByteTransform.TransUInt32(operateResult2.Content, 7 * j + 3 + 11);
				}
			}
			return OperateResult.CreateSuccessResult(list);
		}

		/// <summary>
		/// 浏览PLC的点位信息
		/// </summary>
		/// <returns>点位结果列表</returns>
		public OperateResult<List<S7Object>> BrowseTags()
		{
			OperateResult<byte[]> operateResult = ReadFromCoreServer(BuildBrowseRequest(537u, new uint[0])[0]);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<List<S7Object>>(operateResult);
			}
			return OperateResult.CreateSuccessResult(ExploreResponse(operateResult.Content));
		}

		/// <summary>
		/// 批量读取多个节点数据信息，需要传入节点数组
		/// </summary>
		/// <param name="s7Tags">节点数组信息</param>
		/// <returns>成功的结果列表</returns>
		public OperateResult<List<S7Value>> ReadTags(S7Tag[] s7Tags)
		{
			OperateResult<byte[]> operateResult = ReadFromCoreServer(BuildMultiGetRequest(s7Tags));
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<List<S7Value>>(operateResult);
			}
			return ExploreS7Values(s7Tags, operateResult.Content);
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.Siemens.SiemensS7Plus.ReadTags(HslCommunication.Profinet.Siemens.S7PlusHelper.S7Tag[])" />
		public OperateResult<List<S7Value>> ReadTags(string[] ids)
		{
			return ReadTags(CreateTagsFromAddress(ids));
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.Siemens.SiemensS7Plus.WriteTag(HslCommunication.Profinet.Siemens.S7PlusHelper.S7Tag[],System.Collections.Generic.List{System.Byte[]})" />
		public OperateResult WriteTag(string id, byte[] buffer)
		{
			return WriteTag(new string[1] { id }, new List<byte[]> { buffer });
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.Siemens.SiemensS7Plus.WriteTag(HslCommunication.Profinet.Siemens.S7PlusHelper.S7Tag[],System.Collections.Generic.List{System.Byte[]})" />
		public OperateResult WriteTag(string[] ids, List<byte[]> buffers)
		{
			return WriteTag(CreateTagsFromAddress(ids), buffers);
		}

		/// <summary>
		/// 将实际的数据写入到标签中去，支持批量写入的操作。需要传入实际的点位地址，以及封装后的数据内容。
		/// </summary>
		/// <param name="ids">点位信息</param>
		/// <param name="buffers">封装过的数据</param>
		/// <returns>是否写入成功</returns>
		public OperateResult WriteTag(S7Tag[] ids, List<byte[]> buffers)
		{
			OperateResult<byte[]> operateResult = ReadFromCoreServer(BuildMultiSetRequest(ids, buffers));
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<List<S7Value>>(operateResult);
			}
			return CheckReturnCode(operateResult.Content);
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.Siemens.SiemensS7Plus.ReadTags(HslCommunication.Profinet.Siemens.S7PlusHelper.S7Tag[])" />
		public async Task<OperateResult<List<S7Value>>> ReadTagsAsync(S7Tag[] s7Tags)
		{
			OperateResult<byte[]> read = await ReadFromCoreServerAsync(BuildMultiGetRequest(s7Tags));
			if (!read.IsSuccess)
			{
				return OperateResult.CreateFailedResult<List<S7Value>>(read);
			}
			return ExploreS7Values(s7Tags, read.Content);
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.Siemens.SiemensS7Plus.ReadTags(HslCommunication.Profinet.Siemens.S7PlusHelper.S7Tag[])" />
		public async Task<OperateResult<List<S7Value>>> ReadTagsAsync(string[] ids)
		{
			return await ReadTagsAsync(CreateTagsFromAddress(ids));
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.Siemens.SiemensS7Plus.WriteTag(HslCommunication.Profinet.Siemens.S7PlusHelper.S7Tag[],System.Collections.Generic.List{System.Byte[]})" />
		public async Task<OperateResult> WriteTagAsync(string id, byte[] buffer)
		{
			return await WriteTagAsync(new string[1] { id }, new List<byte[]> { buffer });
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.Siemens.SiemensS7Plus.WriteTag(HslCommunication.Profinet.Siemens.S7PlusHelper.S7Tag[],System.Collections.Generic.List{System.Byte[]})" />
		public async Task<OperateResult> WriteTagAsync(string[] ids, List<byte[]> buffers)
		{
			return await WriteTagAsync(CreateTagsFromAddress(ids), buffers);
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.Siemens.SiemensS7Plus.WriteTag(HslCommunication.Profinet.Siemens.S7PlusHelper.S7Tag[],System.Collections.Generic.List{System.Byte[]})" />
		public async Task<OperateResult> WriteTagAsync(S7Tag[] ids, List<byte[]> buffers)
		{
			OperateResult<byte[]> read = await ReadFromCoreServerAsync(BuildMultiSetRequest(ids, buffers));
			if (!read.IsSuccess)
			{
				return OperateResult.CreateFailedResult<List<S7Value>>(read);
			}
			return CheckReturnCode(read.Content);
		}

		/// <summary>
		/// 发送数据信息
		/// </summary>
		/// <param name="pdu">Pdu 报文信息</param>
		/// <returns>是否发送成功</returns>
		private OperateResult SendS7Pdu(byte[] pdu)
		{
			return CommunicationPipe.Send(BuildWithTPKTAndISO(pdu));
		}

		private void ReceiveS7Pdu(byte[] pdu)
		{
			base.LogNet?.WriteDebug(ToString(), "SSL/TLS: " + (LogMsgFormatBinary ? pdu.ToHexString(' ') : SoftBasic.GetAsciiStringRender(pdu)));
			int num = 0;
			if (pdu[num + 1] != 254 || pdu.Length > 10)
			{
			}
			protoVersionBuffer = pdu[num + 1];
			while (num < pdu.Length && pdu[num] == 114)
			{
				int num2 = base.ByteTransform.TransUInt16(pdu, num + 2);
				num += 4;
				if (num2 > 0 && protoVersionBuffer != 254)
				{
					memoryStream.Write(pdu, num, num2);
				}
				num += num2;
				if (num2 == 0)
				{
					recieveBuffer = memoryStream.ToArray();
					memoryStream = new MemoryStream();
					if (protoVersionBuffer != 254)
					{
						autoResetEventPdu.Set();
					}
				}
			}
		}

		private List<byte[]> BuildS7PlusPdu(byte[] pduData, int bytesToSend, byte protoVersion)
		{
			List<byte[]> list = new List<byte[]>();
			int num = 0;
			int num2 = 1024;
			byte[] array = new byte[num2];
			int num3 = num2 - 4 - 3 - 5 - 17 - 4 - 4;
			while (bytesToSend > 0)
			{
				int num4;
				if (bytesToSend > num3)
				{
					num4 = num3;
					bytesToSend -= num3;
				}
				else
				{
					num4 = bytesToSend;
					bytesToSend -= num4;
				}
				array[0] = 114;
				array[1] = protoVersion;
				array[2] = BitConverter.GetBytes(num4)[1];
				array[3] = BitConverter.GetBytes(num4)[0];
				Array.Copy(pduData, num, array, 4, num4);
				num += num4;
				int num5 = 4 + num4;
				if (bytesToSend == 0)
				{
					array[num5] = 114;
					num5++;
					array[num5] = protoVersion;
					num5++;
					array[num5] = 0;
					num5++;
					array[num5] = 0;
					num5++;
				}
				Array.Resize(ref array, num5);
				list.Add(array);
			}
			return list;
		}

		private List<byte[]> BuildS7PlusPdu(byte[] pduData, byte protoVersion)
		{
			return BuildS7PlusPdu(pduData, pduData.Length, protoVersion);
		}

		private byte[] GetISOTelegrams()
		{
			base.ByteTransform.TransByte(LocalTSAP).CopyTo(iso_head, 16);
			iso_head[3] = (byte)(20 + destTSAP.Length);
			iso_head[4] = (byte)(15 + destTSAP.Length);
			iso_head[19] = (byte)destTSAP.Length;
			return SoftBasic.SpliceArray<byte>(iso_head, destTSAP);
		}

		private byte[] BuildInitSslCommand()
		{
			byte[] array = new byte[18]
			{
				49, 0, 0, 5, 179, 0, 0, 0, 0, 0,
				0, 0, 0, 0, 0, 0, 0, 0
			};
			int value = (int)incrementCount.GetCurrentValue();
			array[7] = BitConverter.GetBytes(value)[1];
			array[8] = BitConverter.GetBytes(value)[0];
			array[13] = 48;
			return BuildWithTPKTAndISO(BuildS7PlusPdu(array, array.Length, 1)[0]);
		}

		private byte[] BuildCreateObjectRequest()
		{
			byte[] array = "31 00 00 04 CA 00 00 00 02 00 00 01 20 36 00 00 01 1D 00 04 00 00 00 00 00 A1 00 00 00 D3 82 1F 00 00 A3 82 2C 00 12 80 C3 C9 01 A1 00 00 00 D3 81 7F 00 00 A2 A2 00 00 00 00".ToHexBytes();
			int value = (int)incrementCount.GetCurrentValue();
			array[7] = BitConverter.GetBytes(value)[1];
			array[8] = BitConverter.GetBytes(value)[0];
			return BuildS7PlusPdu(array, array.Length, 1)[0];
		}

		private byte[] BuildMultiSetRequest(byte[] structValue)
		{
			MemoryStream memoryStream = new MemoryStream();
			byte[] array = "31 00 00 05 42 00 00 00 03 70 40 00 04 34 70 40 00 04 01 01 82 32 01".ToHexBytes();
			base.ByteTransform.TransByte(SessionID).CopyTo(array, 9);
			base.ByteTransform.TransByte(SessionID).CopyTo(array, 14);
			memoryStream.Write(array);
			memoryStream.Write(structValue);
			memoryStream.Write("00 00 00 04 E8 89 69 00 12 00 00 00 00 89 6A 00 13 00 89 6B 00 04 00 ".ToHexBytes());
			S7Object.WriteUint32(memoryStream, 0u);
			memoryStream.Write("00 00 00 00".ToHexBytes());
			array = memoryStream.ToArray();
			int value = (int)incrementCount.GetCurrentValue();
			array[7] = BitConverter.GetBytes(value)[1];
			array[8] = BitConverter.GetBytes(value)[0];
			return BuildS7PlusPdu(array, 2)[0];
		}

		private byte[] BuildMultiGetRequest(IS7Object[] s7Object)
		{
			MemoryStream memoryStream = new MemoryStream();
			memoryStream.Write("31 00 00 05 4C 00 00 00 03 70 40 00 04 34 00 00 00 00".ToHexBytes());
			S7Object.WriteUint32(memoryStream, (uint)s7Object.Length);
			S7Object.WriteUint32(memoryStream, (uint)s7Object.Sum((IS7Object m) => m.GetNumberOfFields()));
			for (int i = 0; i < s7Object.Length; i++)
			{
				s7Object[i].WriteMessgae(memoryStream);
			}
			return memoryStream.ToArray();
		}

		private byte[] BuildMultiSetRequest(IS7Object[] s7Object, List<byte[]> buffers)
		{
			MemoryStream memoryStream = new MemoryStream();
			memoryStream.Write("31 00 00 05 42 00 00 00 03 70 40 00 04 34 00 00 00 00".ToHexBytes());
			S7Object.WriteUint32(memoryStream, (uint)s7Object.Length);
			S7Object.WriteUint32(memoryStream, (uint)s7Object.Sum((IS7Object m) => m.GetNumberOfFields()));
			for (int i = 0; i < s7Object.Length; i++)
			{
				s7Object[i].WriteMessgae(memoryStream);
			}
			for (int j = 0; j < buffers.Count; j++)
			{
				S7Object.WriteUint32(memoryStream, (uint)(j + 1));
				memoryStream.Write(buffers[j]);
			}
			S7Object.WriteUint32(memoryStream, 0u);
			return memoryStream.ToArray();
		}

		private List<byte[]> BuildBrowseRequest(uint exploreId, uint[] attributes)
		{
			MemoryStream memoryStream = new MemoryStream();
			memoryStream.Write("31 00 00 04 BB 00 00 00 07 70 40 00 04 34 00 00 02 19 00 01 01 00 00".ToHexBytes());
			S7Object.WriteUint32(memoryStream, (uint)attributes.Length);
			for (int i = 0; i < attributes.Length; i++)
			{
				S7Object.WriteUint32(memoryStream, attributes[i]);
			}
			S7Object.WriteUint32(memoryStream, (uint)integrityIdCount.GetCurrentValue());
			memoryStream.Write("00 00 00 00 00".ToHexBytes());
			byte[] array = memoryStream.ToArray();
			int value = (int)incrementCount.GetCurrentValue();
			array[7] = BitConverter.GetBytes(value)[1];
			array[8] = BitConverter.GetBytes(value)[0];
			array[9] = BitConverter.GetBytes(SessionID)[3];
			array[10] = BitConverter.GetBytes(SessionID)[2];
			array[11] = BitConverter.GetBytes(SessionID)[1];
			array[12] = BitConverter.GetBytes(SessionID)[0];
			array[14] = BitConverter.GetBytes(exploreId)[3];
			array[15] = BitConverter.GetBytes(exploreId)[2];
			array[16] = BitConverter.GetBytes(exploreId)[1];
			array[17] = BitConverter.GetBytes(exploreId)[0];
			return BuildS7PlusPdu(array, array.Length, 2);
		}

		private S7Value ExtraS7Value(byte[] buffer, ref int index, bool ignoreID)
		{
			if (!ignoreID && S7Object.GetValueUint32(buffer, ref index) == 0)
			{
				return null;
			}
			byte b = buffer[index++];
			byte b2 = buffer[index++];
			S7Value s7Value = new S7Value();
			s7Value.TypeCode = b2;
			s7Value.Flag = b;
			switch (b)
			{
			case 0:
				if (b2 == 1)
				{
					s7Value.Value = Convert.ToBoolean(buffer[index]);
					s7Value.Buffer = new byte[1] { buffer[index] };
					index++;
					return s7Value;
				}
				if (b2 == 2 || b2 == 10)
				{
					s7Value.Value = buffer[index];
					s7Value.Buffer = new byte[1] { buffer[index] };
					index++;
					return s7Value;
				}
				if (b2 == 3 || b2 == 11)
				{
					s7Value.Value = base.ByteTransform.TransUInt16(buffer, index);
					s7Value.Buffer = buffer.SelectMiddle(index, 2);
					index += 2;
					return s7Value;
				}
				if (b2 == 4 || b2 == 8 || b2 == 19)
				{
					uint valueUint13 = S7Object.GetValueUint32(buffer, ref index);
					if (b2 == 4)
					{
						s7Value.Value = valueUint13;
					}
					else
					{
						s7Value.Value = (int)valueUint13;
					}
					s7Value.Buffer = base.ByteTransform.TransByte(valueUint13);
					return s7Value;
				}
				if (b2 == 5)
				{
					ulong valueUint14 = S7Object.GetValueUint64(buffer, ref index);
					s7Value.Value = valueUint14;
					s7Value.Buffer = base.ByteTransform.TransByte(valueUint14);
					return s7Value;
				}
				if (b2 == 6)
				{
					s7Value.Value = (sbyte)buffer[index];
					s7Value.Buffer = new byte[1] { buffer[index] };
					index++;
					return s7Value;
				}
				if (b2 == 7)
				{
					s7Value.Value = base.ByteTransform.TransInt16(buffer, index);
					s7Value.Buffer = buffer.SelectMiddle(index, 2);
					index += 2;
					return s7Value;
				}
				if (b2 == 9 || b2 == 13 || b2 == 16 || b2 == 17)
				{
					ulong valueUint15 = S7Object.GetValueUint64(buffer, ref index);
					if (b2 == 13 || b2 == 16)
					{
						s7Value.Value = valueUint15;
					}
					else
					{
						s7Value.Value = (long)valueUint15;
					}
					s7Value.Buffer = base.ByteTransform.TransByte(valueUint15);
					return s7Value;
				}
				if (b2 == 12 || b2 == 18)
				{
					s7Value.Value = base.ByteTransform.TransUInt32(buffer, index);
					s7Value.Buffer = buffer.SelectMiddle(index, 4);
					index += 4;
					return s7Value;
				}
				if (b2 == 13 || b2 == 16)
				{
					s7Value.Value = base.ByteTransform.TransUInt64(buffer, index);
					s7Value.Buffer = buffer.SelectMiddle(index, 8);
					index += 8;
					return s7Value;
				}
				switch (b2)
				{
				case 14:
					s7Value.Value = base.ByteTransform.TransSingle(buffer, index);
					s7Value.Buffer = buffer.SelectMiddle(index, 4);
					index += 4;
					return s7Value;
				case 15:
					s7Value.Value = base.ByteTransform.TransDouble(buffer, index);
					s7Value.Buffer = buffer.SelectMiddle(index, 8);
					index += 8;
					return s7Value;
				case 21:
				{
					int valueUint18 = (int)S7Object.GetValueUint32(buffer, ref index);
					s7Value.Buffer = buffer.SelectMiddle(index, valueUint18);
					s7Value.Value = Encoding.UTF8.GetString(s7Value.Buffer);
					index += valueUint18;
					return s7Value;
				}
				case 23:
				{
					s7Value.StructID = base.ByteTransform.TransUInt32(buffer, index);
					index += 4;
					index += 8;
					uint valueUint16 = S7Object.GetValueUint32(buffer, ref index);
					uint valueUint17 = S7Object.GetValueUint32(buffer, ref index);
					if ((valueUint16 & 0x400) != 0)
					{
						valueUint17 = S7Object.GetValueUint32(buffer, ref index);
					}
					s7Value.Buffer = buffer.SelectMiddle(index, (int)valueUint17);
					index += (int)valueUint17;
					return s7Value;
				}
				}
				break;
			case 16:
				if (b2 == 2 || b2 == 10)
				{
					int valueUint = (int)S7Object.GetValueUint32(buffer, ref index);
					s7Value.Buffer = buffer.SelectMiddle(index, valueUint);
					s7Value.Value = buffer.SelectMiddle(index, valueUint);
					index += valueUint;
					return s7Value;
				}
				if (b2 == 3 || b2 == 11)
				{
					int valueUint2 = (int)S7Object.GetValueUint32(buffer, ref index);
					s7Value.Value = base.ByteTransform.TransUInt16(buffer, index, valueUint2);
					s7Value.Buffer = buffer.SelectMiddle(index, 2 * valueUint2);
					index += 2 * valueUint2;
					return s7Value;
				}
				switch (b2)
				{
				case 4:
				{
					int valueUint12 = (int)S7Object.GetValueUint32(buffer, ref index);
					uint[] array5 = new uint[valueUint12];
					for (int m = 0; m < valueUint12; m++)
					{
						array5[m] = S7Object.GetValueUint32(buffer, ref index);
					}
					s7Value.Value = array5;
					s7Value.Buffer = base.ByteTransform.TransByte(array5);
					return s7Value;
				}
				case 5:
				{
					int valueUint11 = (int)S7Object.GetValueUint32(buffer, ref index);
					ulong[] array4 = new ulong[valueUint11];
					for (int l = 0; l < valueUint11; l++)
					{
						array4[l] = S7Object.GetValueUint64(buffer, ref index);
					}
					s7Value.Value = array4;
					s7Value.Buffer = base.ByteTransform.TransByte(array4);
					return s7Value;
				}
				case 6:
				{
					int valueUint9 = (int)S7Object.GetValueUint32(buffer, ref index);
					sbyte[] array2 = new sbyte[valueUint9];
					for (int j = 0; j < valueUint9; j++)
					{
						array2[j] = (sbyte)buffer[index + j];
					}
					s7Value.Value = array2;
					s7Value.Buffer = buffer.SelectMiddle(index, valueUint9);
					index += valueUint9;
					return s7Value;
				}
				case 7:
				{
					int valueUint8 = (int)S7Object.GetValueUint32(buffer, ref index);
					s7Value.Value = base.ByteTransform.TransInt16(buffer, index, valueUint8);
					s7Value.Buffer = buffer.SelectMiddle(index, 2 * valueUint8);
					index += 2 * valueUint8;
					return s7Value;
				}
				case 8:
				{
					int valueUint10 = (int)S7Object.GetValueUint32(buffer, ref index);
					int[] array3 = new int[valueUint10];
					for (int k = 0; k < valueUint10; k++)
					{
						array3[k] = (int)S7Object.GetValueUint32(buffer, ref index);
					}
					s7Value.Value = array3;
					s7Value.Buffer = base.ByteTransform.TransByte(array3);
					return s7Value;
				}
				case 9:
				{
					int valueUint7 = (int)S7Object.GetValueUint32(buffer, ref index);
					long[] array = new long[valueUint7];
					for (int i = 0; i < valueUint7; i++)
					{
						array[i] = (long)S7Object.GetValueUint64(buffer, ref index);
					}
					s7Value.Value = array;
					s7Value.Buffer = base.ByteTransform.TransByte(array);
					return s7Value;
				}
				case 12:
				{
					int valueUint6 = (int)S7Object.GetValueUint32(buffer, ref index);
					s7Value.Value = base.ByteTransform.TransUInt32(buffer, index, valueUint6);
					s7Value.Buffer = buffer.SelectMiddle(index, 4 * valueUint6);
					index += 4 * valueUint6;
					return s7Value;
				}
				case 13:
				{
					int valueUint5 = (int)S7Object.GetValueUint32(buffer, ref index);
					s7Value.Value = base.ByteTransform.TransUInt64(buffer, index, valueUint5);
					s7Value.Buffer = buffer.SelectMiddle(index, 8 * valueUint5);
					index += 8 * valueUint5;
					return s7Value;
				}
				case 14:
				{
					int valueUint4 = (int)S7Object.GetValueUint32(buffer, ref index);
					s7Value.Value = base.ByteTransform.TransSingle(buffer, index, valueUint4);
					s7Value.Buffer = buffer.SelectMiddle(index, 4 * valueUint4);
					index += 4 * valueUint4;
					return s7Value;
				}
				case 15:
				{
					int valueUint3 = (int)S7Object.GetValueUint32(buffer, ref index);
					s7Value.Value = base.ByteTransform.TransDouble(buffer, index, valueUint3);
					s7Value.Buffer = buffer.SelectMiddle(index, 8 * valueUint3);
					index += 8 * valueUint3;
					return s7Value;
				}
				}
				break;
			}
			return s7Value;
		}

		private OperateResult<byte[]> ExtraAttributeData(byte[] buffer, uint attributeId, ref int index)
		{
			uint valueUint = S7Object.GetValueUint32(buffer, ref index);
			if (valueUint == 0)
			{
				return null;
			}
			byte b = buffer[index++];
			byte b2 = buffer[index++];
			switch (b)
			{
			case 0:
				if (b2 == 1)
				{
					index++;
					if (valueUint == attributeId)
					{
						return OperateResult.CreateSuccessResult(new byte[1] { buffer[index] });
					}
					break;
				}
				if (b2 == 4 || b2 == 8)
				{
					uint valueUint3 = S7Object.GetValueUint32(buffer, ref index);
					if (valueUint != attributeId)
					{
						break;
					}
					return OperateResult.CreateSuccessResult(base.ByteTransform.TransByte(valueUint3));
				}
				switch (b2)
				{
				case 5:
				{
					ulong valueUint5 = S7Object.GetValueUint64(buffer, ref index);
					if (valueUint == attributeId)
					{
						return OperateResult.CreateSuccessResult(base.ByteTransform.TransByte(valueUint5));
					}
					break;
				}
				case 3:
					index += 2;
					if (valueUint == attributeId)
					{
						return OperateResult.CreateSuccessResult(buffer.SelectMiddle(index - 2, 2));
					}
					break;
				case 11:
					index += 2;
					if (valueUint == attributeId)
					{
						return OperateResult.CreateSuccessResult(buffer.SelectMiddle(index - 2, 2));
					}
					break;
				case 12:
					index += 4;
					if (valueUint == attributeId)
					{
						return OperateResult.CreateSuccessResult(buffer.SelectMiddle(index - 4, 4));
					}
					break;
				case 21:
				{
					int valueUint4 = (int)S7Object.GetValueUint32(buffer, ref index);
					index += valueUint4;
					if (valueUint == attributeId)
					{
						return OperateResult.CreateSuccessResult(buffer.SelectMiddle(index - valueUint4, valueUint4));
					}
					break;
				}
				case 23:
				{
					int num = index - 2;
					index += 4;
					for (OperateResult<byte[]> operateResult = ExtraAttributeData(buffer, attributeId, ref index); operateResult != null; operateResult = ExtraAttributeData(buffer, attributeId, ref index))
					{
						if (operateResult.IsSuccess && operateResult.Content != null)
						{
							return operateResult;
						}
					}
					if (valueUint == attributeId)
					{
						return OperateResult.CreateSuccessResult(buffer.SelectMiddle(num, index - num));
					}
					break;
				}
				}
				break;
			case 16:
				if (b2 == 2)
				{
					int valueUint2 = (int)S7Object.GetValueUint32(buffer, ref index);
					index += valueUint2;
					if (valueUint == attributeId)
					{
						return OperateResult.CreateSuccessResult(buffer.SelectMiddle(index - valueUint2, valueUint2));
					}
				}
				break;
			}
			return OperateResult.CreateSuccessResult<byte[]>(null);
		}

		private OperateResult<byte[]> ExtraResponseData(byte[] buffer, uint attributeId, ref int index)
		{
			while (index < buffer.Length)
			{
				switch (buffer[index++])
				{
				case 163:
				{
					OperateResult<byte[]> operateResult2 = ExtraAttributeData(buffer, attributeId, ref index);
					if (operateResult2.IsSuccess && operateResult2.Content != null)
					{
						return operateResult2;
					}
					continue;
				}
				case 161:
				{
					index += 4;
					S7Object.GetValueUint32(buffer, ref index);
					S7Object.GetValueUint32(buffer, ref index);
					S7Object.GetValueUint32(buffer, ref index);
					OperateResult<byte[]> operateResult = ExtraResponseData(buffer, attributeId, ref index);
					if (!operateResult.IsSuccess)
					{
						return operateResult;
					}
					if (operateResult.IsSuccess && operateResult.Content != null)
					{
						return operateResult;
					}
					continue;
				}
				}
				break;
			}
			return new OperateResult<byte[]>($"No attribute id [{attributeId}] data");
		}

		private List<S7Object> ExploreResponse(byte[] buffer)
		{
			int index = 10;
			S7Object.GetValueUint64(buffer, ref index);
			index += 4;
			S7Object.GetValueUint32(buffer, ref index);
			List<S7Object> list = new List<S7Object>();
			while (buffer[index] == 161)
			{
				list.Add(CreateS7Object(buffer, ref index));
			}
			return list;
		}

		private OperateResult<List<S7Value>> ExploreS7Values(S7Tag[] s7Tags, byte[] buffer)
		{
			try
			{
				int index = 10;
				ulong valueUint = S7Object.GetValueUint64(buffer, ref index);
				int num = 0;
				int num2 = 0;
				List<S7Value> list = new List<S7Value>();
				while (S7Object.GetValueUint32(buffer, ref index) != 0)
				{
					S7Value s7Value = ExtraS7Value(buffer, ref index, true);
					if (s7Value == null)
					{
						break;
					}
					if (num2 < s7Tags.Length && s7Tags[num2].TypeCode == 19 && s7Tags[num2].ArrayLength < 0)
					{
						s7Value.Value = Encoding.Default.GetString(s7Value.Buffer, 2, s7Value.Buffer[1]);
					}
					list.Add(s7Value);
					num2++;
				}
				return OperateResult.CreateSuccessResult(list);
			}
			catch (Exception ex)
			{
				return new OperateResult<List<S7Value>>("ExploreS7Values failed: " + ex.Message);
			}
		}

		private OperateResult CheckReturnCode(byte[] buffer)
		{
			try
			{
				int index = 10;
				ulong valueUint = S7Object.GetValueUint64(buffer, ref index);
				if (valueUint == 0)
				{
					return OperateResult.CreateSuccessResult();
				}
				return new OperateResult("ReturnCode: " + valueUint + " Source: " + buffer.ToHexString(' '));
			}
			catch (Exception ex)
			{
				return new OperateResult("ExploreS7Values failed: " + ex.Message);
			}
		}

		private S7Object CreateS7Object(byte[] buffer, ref int index)
		{
			if (buffer[index] != 161)
			{
				throw new Exception("Not S7 object: 0x" + buffer[index].ToString("X"));
			}
			index++;
			S7Object s7Object = new S7Object();
			s7Object.RelationId = base.ByteTransform.TransUInt32(buffer, index);
			if (s7Object.RelationId == 80)
			{
				s7Object.RelationId2 = 2415984640u;
			}
			if (s7Object.RelationId == 81)
			{
				s7Object.RelationId2 = 2416050176u;
			}
			if (s7Object.RelationId == 82)
			{
				s7Object.RelationId2 = 2416115712u;
			}
			if (s7Object.RelationId == 83)
			{
				s7Object.RelationId2 = 2416312320u;
			}
			if (s7Object.RelationId == 84)
			{
				s7Object.RelationId2 = 2416246784u;
			}
			index += 4;
			s7Object.ClassId = S7Object.GetValueUint32(buffer, ref index);
			s7Object.ClassFlags = S7Object.GetValueUint32(buffer, ref index);
			s7Object.AttributeId = S7Object.GetValueUint32(buffer, ref index);
			if (buffer[index] == 163)
			{
				index++;
				S7Object.GetValueUint32(buffer, ref index);
				uint valueUint = S7Object.GetValueUint32(buffer, ref index);
				uint valueUint2 = S7Object.GetValueUint32(buffer, ref index);
				if (valueUint2 != 21)
				{
					throw new Exception("Name get failed, not 0x15, actual: " + valueUint2.ToString("X"));
				}
				uint valueUint3 = S7Object.GetValueUint32(buffer, ref index);
				s7Object.Name = Encoding.UTF8.GetString(buffer, index, (int)valueUint3);
				index += (int)valueUint3;
			}
			while (true)
			{
				switch (buffer[index])
				{
				case 161:
				{
					if (s7Object.SubObjects == null)
					{
						s7Object.SubObjects = new List<S7Object>();
					}
					S7Object item = CreateS7Object(buffer, ref index);
					s7Object.SubObjects.Add(item);
					continue;
				}
				case 163:
				{
					index++;
					OperateResult<byte[]> operateResult = ExtraAttributeData(buffer, 0u, ref index);
					if (!operateResult.IsSuccess)
					{
						throw new Exception("ExtraAttributeData failed: " + operateResult.Message);
					}
					continue;
				}
				case 162:
					index++;
					break;
				case 171:
				{
					index++;
					int num4 = base.ByteTransform.TransUInt16(buffer, index);
					index += 2;
					s7Object.S7Tags = new List<S7Tag>();
					int num5 = index + 4;
					while (num5 < index + num4)
					{
						int num6 = 0;
						S7Tag s7Tag = new S7Tag();
						s7Tag.LID = new List<uint> { BitConverter.ToUInt32(buffer, num5) };
						s7Tag.TypeCode = buffer[num5 + 8];
						switch (buffer[num5 + 9] >> 4)
						{
						case 0:
							num6 = 104;
							break;
						case 1:
						case 8:
							num6 = 4;
							break;
						case 2:
						case 9:
							num6 = 12;
							break;
						case 3:
						case 10:
							s7Tag.ArrayLength = BitConverter.ToInt32(buffer, num5 + 28);
							num6 = 20;
							break;
						case 4:
						case 11:
							num6 = 68;
							break;
						case 5:
						case 12:
							s7Tag.StructID = BitConverter.ToUInt32(buffer, num5 + 24);
							num6 = 32;
							break;
						case 6:
						case 13:
							num6 = 48;
							break;
						case 7:
						case 14:
							num6 = 96;
							break;
						default:
							num6 = 60;
							break;
						}
						if (num6 == 4)
						{
							s7Tag.StructOffset = BitConverter.ToUInt16(buffer, num5 + 14);
						}
						else
						{
							s7Tag.StructOffset = BitConverter.ToInt32(buffer, num5 + 16);
						}
						num5 += 12 + num6;
						s7Object.S7Tags.Add(s7Tag);
					}
					index += num4;
					index += 2;
					continue;
				}
				case 172:
				{
					index++;
					int num = base.ByteTransform.TransUInt16(buffer, index);
					index += 2;
					int num2 = 0;
					int num3 = index;
					while (num3 < index + num)
					{
						byte b = buffer[num3];
						if (s7Object.S7Tags != null && num2 < s7Object.S7Tags.Count)
						{
							s7Object.S7Tags[num2].Name = Encoding.UTF8.GetString(buffer, num3 + 1, b);
						}
						num3 += 2 + b;
						num2++;
					}
					index += num;
					index += 2;
					continue;
				}
				}
				break;
			}
			return s7Object;
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return $"SiemensS7Plus[{IpAddress}:{Port}]";
		}

		/// <summary>
		/// 构建S7完整的通信报文
		/// </summary>
		/// <param name="pdu">报文信息</param>
		/// <returns>报文</returns>
		public static byte[] BuildWithTPKTAndISO(byte[] pdu)
		{
			byte[] array = SoftBasic.SpliceArray<byte>(new byte[7] { 3, 0, 0, 31, 2, 240, 128 }, pdu);
			array[2] = BitConverter.GetBytes(array.Length)[1];
			array[3] = BitConverter.GetBytes(array.Length)[0];
			return array;
		}
	}
}
