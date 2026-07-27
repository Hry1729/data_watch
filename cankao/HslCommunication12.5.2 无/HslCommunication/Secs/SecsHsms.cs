using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using HslCommunication.BasicFramework;
using HslCommunication.Core;
using HslCommunication.Core.IMessage;
using HslCommunication.Core.Net;
using HslCommunication.Secs.Helper;
using HslCommunication.Secs.Message;
using HslCommunication.Secs.Types;

namespace HslCommunication.Secs
{
	/// <summary>
	/// HSMS的协议实现，SECS基于TCP的版本，实现了secs1和secs2规约的封装，可以收发任意的功能码数据，业务逻辑部分需要二次开发
	/// </summary>
	/// <remarks>
	/// </remarks>
	/// <example>
	/// 下面就看看基本的操作内容
	/// <code lang="cs" source="HslCommunication_Net45.Test\Documentation\Samples\Secs\SecsGemSample.cs" region="Sample1" title="基本的读写" />
	/// 如果想要手动处理下设备主要返回的数据，比如报警之类的，可以参考下面的方法
	/// <code lang="cs" source="HslCommunication_Net45.Test\Documentation\Samples\Secs\SecsGemSample.cs" region="Sample3" title="事件回调处理" />
	/// 关于<see cref="T:HslCommunication.Secs.Types.SecsValue" />类型，可以非常灵活的实例化，参考下面的示例代码
	/// <code lang="cs" source="HslCommunication_Net45.Test\Documentation\Samples\Secs\SecsGemSample.cs" region="Sample2" title="SecsValue说明" />
	/// </example>
	public class SecsHsms : NetworkDoubleBase, ISecs
	{
		/// <summary>
		/// Secs消息接收的事件
		/// </summary>
		/// <param name="sender">数据的发送方</param>
		/// <param name="secsMessage">消息内容</param>
		public delegate void OnSecsMessageReceivedDelegate(object sender, SecsMessage secsMessage);

		private Encoding stringEncoding = Encoding.Default;

		private SoftIncrementCount incrementCount;

		private List<uint> identityQAs = new List<uint>();

		/// <summary>
		/// 获取或设置当前的DeivceID信息
		/// </summary>
		public ushort DeviceID { get; set; }

		/// <summary>
		/// 获取或设置当前的GEM信息，可以用来方便的调用一些常用的功能接口，或是自己实现自定义的接口方法
		/// </summary>
		public Gem Gem { get; set; }

		/// <summary>
		/// 是否使用S0F0来初始化当前的设备对象信息
		/// </summary>
		public bool InitializationS0F0 { get; set; } = false;

		/// <summary>
		/// 获取或设置用于字符串解析的编码信息
		/// </summary>
		public Encoding StringEncoding
		{
			get
			{
				return stringEncoding;
			}
			set
			{
				stringEncoding = value;
			}
		}

		/// <summary>
		/// 获取或设置是否自动处理S1F1功能码的返回，默认为True:自动处理，实际还包含 S1F13, S2F17 的自动处理，如果需要手动处理这些功能码，请设置为 False
		/// </summary>
		/// <remarks>
		/// 因为考虑代码的兼容性升级，所以遗留了本属性手动设置
		/// </remarks>
		public bool AutoBackS1F1 { get; set; } = true;

		/// <summary>
		/// 当接收到非应答消息的时候触发的事件
		/// </summary>
		public event OnSecsMessageReceivedDelegate OnSecsMessageReceived;

		/// <summary>
		/// 实例化一个默认的对象<br />
		/// instantiate a default object
		/// </summary>
		public SecsHsms()
		{
			incrementCount = new SoftIncrementCount(4294967295L, 1L);
			base.ByteTransform = new ReverseBytesTransform();
			base.UseServerActivePush = true;
			Gem = new Gem(this);
		}

		/// <summary>
		/// 指定ip地址和端口号来实例化一个默认的对象<br />
		/// Specify the IP address and port number to instantiate a default object
		/// </summary>
		/// <param name="ipAddress">PLC的Ip地址</param>
		/// <param name="port">PLC的端口</param>
		public SecsHsms(string ipAddress, int port)
			: this()
		{
			IpAddress = ipAddress;
			Port = port;
		}

		/// <inheritdoc />
		protected override INetMessage GetNewNetMessage()
		{
			return new SecsHsmsMessage();
		}

		/// <inheritdoc />
		protected override OperateResult InitializationOnConnect(Socket socket)
		{
			if (InitializationS0F0)
			{
				Send(socket, Secs1.BuildHSMSMessage(ushort.MaxValue, 0, 0, 1, (uint)incrementCount.GetCurrentValue(), null, false));
			}
			return base.InitializationOnConnect(socket);
		}

		/// <inheritdoc />
		protected override async Task<OperateResult> InitializationOnConnectAsync(Socket socket)
		{
			if (InitializationS0F0)
			{
				await SendAsync(socket, Secs1.BuildHSMSMessage(ushort.MaxValue, 0, 0, 1, (uint)incrementCount.GetCurrentValue(), null, false));
			}
			return await base.InitializationOnConnectAsync(socket);
		}

		/// <inheritdoc />
		protected override bool DecideWhetherQAMessage(Socket socket, OperateResult<byte[]> receive)
		{
			if (!receive.IsSuccess)
			{
				return false;
			}
			byte[] content = receive.Content;
			SecsMessage secsMessage = null;
			try
			{
				secsMessage = new SecsMessage(content, 4);
			}
			catch (Exception ex)
			{
				base.LogNet?.WriteException(ToString(), "DecideWhetherQAMessage.SecsMessage.cor", ex);
				return false;
			}
			secsMessage.StringEncoding = stringEncoding;
			if (secsMessage.StreamNo == 0 && secsMessage.FunctionNo == 0 && secsMessage.BlockNo % 2 == 1)
			{
				Send(socket, Secs1.BuildHSMSMessage(ushort.MaxValue, 0, 0, (ushort)(secsMessage.BlockNo + 1), secsMessage.MessageID, null, false));
				return false;
			}
			if (secsMessage.FunctionNo % 2 == 0 && secsMessage.FunctionNo != 0)
			{
				bool flag = false;
				lock (identityQAs)
				{
					flag = identityQAs.Remove(secsMessage.MessageID);
				}
				if (flag)
				{
					return flag;
				}
			}
			if (AutoBackS1F1)
			{
				if (secsMessage.StreamNo == 1 && secsMessage.FunctionNo == 13)
				{
					SendByCommand(1, 14, new SecsValue(new object[2]
					{
						new byte[1],
						SecsValue.EmptyListValue()
					}).ToSourceBytes(), false);
					return false;
				}
				if (secsMessage.StreamNo == 2 && secsMessage.FunctionNo == 17)
				{
					SendByCommand(2, 18, new SecsValue(DateTime.Now.ToString("yyyyMMddHHmmssff")), false);
					return false;
				}
				if (secsMessage.StreamNo == 1 && secsMessage.FunctionNo == 1)
				{
					SendByCommand(1, 2, SecsValue.EmptyListValue(), false);
					return false;
				}
			}
			this.OnSecsMessageReceived?.Invoke(this, secsMessage);
			return false;
		}

		/// <inheritdoc cref="M:HslCommunication.Secs.Types.ISecs.SendByCommand(System.Byte,System.Byte,System.Byte[],System.Boolean)" />
		public OperateResult<uint> SendByCommand(byte stream, byte function, byte[] data, bool back)
		{
			uint num = (uint)incrementCount.GetCurrentValue();
			return ReadFromCoreServer(Secs1.BuildHSMSMessage(DeviceID, stream, function, 0, num, data, back), false).Convert(num);
		}

		/// <inheritdoc cref="M:HslCommunication.Secs.Types.ISecs.SendByCommand(System.Byte,System.Byte,HslCommunication.Secs.Types.SecsValue,System.Boolean)" />
		public OperateResult<uint> SendByCommand(byte stream, byte function, SecsValue data, bool back)
		{
			return SendByCommand(stream, function, data.ToSourceBytes(stringEncoding), back);
		}

		/// <summary>
		/// 发送Secs数据到服务器上，根据收到的<see cref="T:HslCommunication.Secs.Types.SecsMessage" />消息来自动确定功能码，消息号信息
		/// </summary>
		/// <param name="secsMessage">收到的Secs消息对象</param>
		/// <param name="data">Secs数据对象</param>
		/// <param name="back">是否返回</param>
		/// <returns>是否发送成功</returns>
		public OperateResult SendByCommand(SecsMessage secsMessage, SecsValue data, bool back)
		{
			return ReadFromCoreServer(Secs1.BuildHSMSMessage(DeviceID, secsMessage.StreamNo, (byte)(secsMessage.FunctionNo + 1), 0, secsMessage.MessageID, data.ToSourceBytes(stringEncoding), back), false);
		}

		/// <inheritdoc cref="M:HslCommunication.Secs.Types.ISecs.ReadSecsMessage(System.Byte,System.Byte,System.Byte[],System.Boolean)" />
		public OperateResult<SecsMessage> ReadSecsMessage(byte stream, byte function, byte[] data, bool back)
		{
			uint num = (uint)incrementCount.GetCurrentValue();
			lock (identityQAs)
			{
				identityQAs.Add(num);
			}
			OperateResult<byte[]> operateResult = ReadFromCoreServer(Secs1.BuildHSMSMessage(DeviceID, stream, function, 0, num, data, back));
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<SecsMessage>(operateResult);
			}
			return OperateResult.CreateSuccessResult(new SecsMessage(operateResult.Content, 4)
			{
				StringEncoding = stringEncoding
			});
		}

		/// <inheritdoc cref="M:HslCommunication.Secs.Types.ISecs.ReadSecsMessage(System.Byte,System.Byte,HslCommunication.Secs.Types.SecsValue,System.Boolean)" />
		public OperateResult<SecsMessage> ReadSecsMessage(byte stream, byte function, SecsValue data, bool back)
		{
			return ReadSecsMessage(stream, function, data.ToSourceBytes(stringEncoding), back);
		}

		/// <summary>
		/// 发送控制命令，需要指定 <paramref name="pType" /> 以及 <paramref name="sType" /> 参数信息，返回是否发送成功
		/// </summary>
		/// <param name="pType">控制码一</param>
		/// <param name="sType">控制码二</param>
		/// <returns>是否发送成功</returns>
		public OperateResult SendControlMessage(byte pType, byte sType)
		{
			return ReadFromCoreServer(Secs1.BuildHSMSMessage(ushort.MaxValue, 0, 0, (ushort)(pType * 256 + sType), (uint)incrementCount.GetCurrentValue(), null, true), false);
		}

		/// <inheritdoc cref="M:HslCommunication.Secs.Types.ISecs.SendByCommand(System.Byte,System.Byte,System.Byte[],System.Boolean)" />
		public async Task<OperateResult> SendByCommandAsync(byte stream, byte function, byte[] data, bool back)
		{
			return await ReadFromCoreServerAsync(Secs1.BuildHSMSMessage(DeviceID, stream, function, 0, (uint)incrementCount.GetCurrentValue(), data, back), false);
		}

		/// <inheritdoc cref="M:HslCommunication.Secs.Types.ISecs.SendByCommand(System.Byte,System.Byte,HslCommunication.Secs.Types.SecsValue,System.Boolean)" />
		public async Task<OperateResult> SendByCommandAsync(byte stream, byte function, SecsValue data, bool back)
		{
			return await SendByCommandAsync(stream, function, data.ToSourceBytes(stringEncoding), back);
		}

		/// <inheritdoc cref="M:HslCommunication.Secs.Types.ISecs.ReadSecsMessage(System.Byte,System.Byte,System.Byte[],System.Boolean)" />
		public async Task<OperateResult<SecsMessage>> ReadSecsMessageAsync(byte stream, byte function, byte[] data, bool back)
		{
			uint identityQA = (uint)incrementCount.GetCurrentValue();
			lock (identityQAs)
			{
				identityQAs.Add(identityQA);
			}
			OperateResult<byte[]> read = await ReadFromCoreServerAsync(Secs1.BuildHSMSMessage(DeviceID, stream, function, 0, identityQA, data, back));
			if (!read.IsSuccess)
			{
				return OperateResult.CreateFailedResult<SecsMessage>(read);
			}
			return OperateResult.CreateSuccessResult(new SecsMessage(read.Content, 4)
			{
				StringEncoding = stringEncoding
			});
		}

		/// <inheritdoc cref="M:HslCommunication.Secs.Types.ISecs.ReadSecsMessage(System.Byte,System.Byte,HslCommunication.Secs.Types.SecsValue,System.Boolean)" />
		public async Task<OperateResult<SecsMessage>> ReadSecsMessageAsync(byte stream, byte function, SecsValue data, bool back)
		{
			return await ReadSecsMessageAsync(stream, function, data.ToSourceBytes(stringEncoding), back);
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return $"SecsHsms[{IpAddress}:{Port}]";
		}
	}
}
