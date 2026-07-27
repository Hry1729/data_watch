using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using HslCommunication.Core.IMessage;
using HslCommunication.Core.Pipe;

namespace HslCommunication.Core.Net
{
	/// <summary>
	/// 异形客户端的基类，提供了基础的异形操作<br />
	/// The base class of the profiled client provides the basic profiled operation
	/// </summary>
	public class NetworkAlienClient : CommunicationTcpServer, IDisposable
	{
		/// <summary>
		/// 远程连接的客户端上线的委托事件<br />
		/// The delegate event for the client to which the remote connection goes online
		/// </summary>
		/// <param name="pipe">异形客户端的会话信息</param>
		public delegate void OnClientConnectedDelegate(PipeDtuNet pipe);

		private byte[] password;

		private List<string> trustOnline;

		private SimpleHybirdLock trustLock;

		private bool isResponseAck = true;

		private bool isCheckPwd = true;

		private bool disposedValue;

		private bool useRegistrationPackage = true;

		/// <summary>
		/// 状态登录成功
		/// </summary>
		public const byte StatusOk = 0;

		/// <summary>
		/// 重复登录
		/// </summary>
		public const byte StatusLoginRepeat = 1;

		/// <summary>
		/// 禁止登录
		/// </summary>
		public const byte StatusLoginForbidden = 2;

		/// <summary>
		/// 密码错误
		/// </summary>
		public const byte StatusPasswodWrong = 3;

		/// <summary>
		/// 在DTU设备发送了注册报文的时候，指示是否返回响应报文，用来通知DTU设备是否登录成功，默认为 <c>True</c><br />
		/// When a DTU sends a registration packet, it indicates whether to return a response packet to notify the DTU whether the DTU is logged in, The default is <c>True</c>
		/// </summary>
		public bool IsResponseAck
		{
			get
			{
				return isResponseAck;
			}
			set
			{
				isResponseAck = value;
			}
		}

		/// <summary>
		/// 是否统一检查密码，如果每个会话需要自己检查密码，就需要设置为false<br />
		/// Whether to check the password uniformly, if each session needs to check the password by itself, it needs to be set to false
		/// </summary>
		public bool IsCheckPwd
		{
			get
			{
				return isCheckPwd;
			}
			set
			{
				isCheckPwd = value;
			}
		}

		/// <summary>
		/// 是否启用注册包的功能，如果不启用，就不会进行注册包的验证，有连接上来就自动覆盖本地的会话信息，不是很安全<br />
		/// Whether to enable the registration package function. If not enabled, the registration package verification will not be performed, 
		/// and the local session information will be automatically overwritten when a connection is made, which is not very secure
		/// </summary>
		public bool UseRegistrationPackage
		{
			get
			{
				return useRegistrationPackage;
			}
			set
			{
				useRegistrationPackage = value;
			}
		}

		/// <summary>
		/// 当有服务器连接上来的时候触发<br />
		/// Triggered when a server is connected
		/// </summary>
		public event OnClientConnectedDelegate OnClientConnected = null;

		/// <summary>
		/// 默认的无参构造方法<br />
		/// The default parameterless constructor
		/// </summary>
		public NetworkAlienClient()
		{
			password = new byte[6];
			trustOnline = new List<string>();
			trustLock = new SimpleHybirdLock();
		}

		/// <summary>
		/// 当接收到了新的请求的时候执行的操作<br />
		/// An action performed when a new request is received
		/// </summary>
		/// <param name="pipeTcpNet">管道对象</param>
		/// <param name="endPoint">终结点</param>
		protected override async void ThreadPoolLogin(PipeTcpNet pipeTcpNet, IPEndPoint endPoint)
		{
			if (useRegistrationPackage)
			{
				OperateResult<byte[]> check = await pipeTcpNet.ReceiveMessageAsync(new AlienMessage(), null, false);
				if (!check.IsSuccess)
				{
					base.LogNet?.WriteDebug(ToString(), $"Initialize [{endPoint}] DTU failed: " + check.Message);
					return;
				}
				byte[] content = check.Content;
				if (content != null && content.Length < 22)
				{
					pipeTcpNet.CloseCommunication();
					return;
				}
				if (check.Content[0] != 72)
				{
					pipeTcpNet.CloseCommunication();
					return;
				}
				string dtu = Encoding.ASCII.GetString(check.Content, 5, 11).Trim('\0', ' ');
				bool needAckResult = true;
				if (check.Content.Length >= 29 && (check.Content[28] == 1 || check.Content[28] == 49))
				{
					needAckResult = false;
				}
				bool isPasswrodRight = true;
				if (isCheckPwd)
				{
					for (int i = 0; i < password.Length; i++)
					{
						if (check.Content[16 + i] != password[i])
						{
							isPasswrodRight = false;
							break;
						}
					}
				}
				if (!isPasswrodRight)
				{
					if (isResponseAck && needAckResult)
					{
						if (pipeTcpNet.Send(GetResponse(3)).IsSuccess)
						{
							pipeTcpNet.CloseCommunication();
						}
					}
					else
					{
						pipeTcpNet.CloseCommunication();
					}
					base.LogNet?.WriteWarn(ToString(), $"[{endPoint}] DTU:{dtu} Login Password Wrong, Id:" + dtu);
					return;
				}
				PipeDtuNet pipeDtuNet = new PipeDtuNet(pipeTcpNet)
				{
					DTU = dtu,
					DtuServer = this,
					Pwd = check.Content.SelectMiddle(16, 6).ToHexString()
				};
				if (check.Content.Length >= 28)
				{
					pipeDtuNet.DTUIpAddress = BitConverter.ToInt32(check.Content, 22);
					pipeDtuNet.DTUPort = BitConverter.ToUInt16(check.Content, 26);
				}
				if (!IsClientPermission(pipeDtuNet))
				{
					if (isResponseAck && needAckResult)
					{
						if (pipeTcpNet.Send(GetResponse(2)).IsSuccess)
						{
							pipeTcpNet.CloseCommunication();
						}
					}
					else
					{
						pipeTcpNet.CloseCommunication();
					}
					base.LogNet?.WriteWarn(ToString(), $"Initialize [{endPoint}] DTU:{dtu} Login Forbidden");
					return;
				}
				int status = IsClientOnline(pipeDtuNet);
				if (status != 0)
				{
					if (isResponseAck && needAckResult)
					{
						if (pipeTcpNet.Send(GetResponse(1)).IsSuccess)
						{
							pipeTcpNet.CloseCommunication();
						}
					}
					else
					{
						pipeTcpNet.CloseCommunication();
					}
					base.LogNet?.WriteDebug(ToString(), GetMsgFromCode($"Initialize [{endPoint}] DTU:{dtu}", status, "  Ack :" + (isResponseAck && needAckResult)));
				}
				else if (!(isResponseAck && needAckResult) || pipeTcpNet.Send(GetResponse(0)).IsSuccess)
				{
					base.LogNet?.WriteDebug(ToString(), GetMsgFromCode($"Initialize [{endPoint}] DTU:{dtu}", status, "  Ack :" + (isResponseAck && needAckResult)));
					RaiseClientConnected(pipeDtuNet);
				}
			}
			else
			{
				RaiseClientConnected(new PipeDtuNet(pipeTcpNet)
				{
					DtuServer = this
				});
			}
		}

		private void RaiseClientConnected(PipeDtuNet pipeDtuNet)
		{
			base.LogNet?.WriteDebug(ToString(), $"Dtu Session<{pipeDtuNet}> Connected");
			this.OnClientConnected?.Invoke(pipeDtuNet);
		}

		/// <summary>
		/// 获取取返回的命令信息
		/// </summary>
		/// <param name="status">状态</param>
		/// <returns>回发的指令信息</returns>
		private byte[] GetResponse(byte status)
		{
			byte[] obj = new byte[6] { 72, 115, 110, 0, 1, 0 };
			obj[5] = status;
			return obj;
		}

		/// <summary>
		/// 检测当前的DTU是否在线
		/// </summary>
		/// <param name="pipe">当前的会话信息</param>
		/// <returns>当前的会话是否在线</returns>
		public virtual int IsClientOnline(PipeDtuNet pipe)
		{
			return 0;
		}

		/// <summary>
		/// 检测当前的dtu是否允许登录
		/// </summary>
		/// <param name="session">当前的会话信息</param>
		/// <returns>当前的id是否可允许登录</returns>
		private bool IsClientPermission(PipeDtuNet session)
		{
			bool result = false;
			trustLock.Enter();
			if (trustOnline.Count == 0)
			{
				result = true;
			}
			else
			{
				for (int i = 0; i < trustOnline.Count; i++)
				{
					if (trustOnline[i] == session.DTU)
					{
						result = true;
						break;
					}
				}
			}
			trustLock.Leave();
			return result;
		}

		/// <summary>
		/// 设置密码，需要传入长度为6的字节数组<br />
		/// To set the password, you need to pass in an array of bytes of length 6
		/// </summary>
		/// <param name="password">密码信息</param>
		public void SetPassword(byte[] password)
		{
			if (password != null && password.Length == 6)
			{
				password.CopyTo(this.password, 0);
			}
		}

		/// <summary>
		/// 设置可信任的客户端列表，传入一个DTU的列表信息<br />
		/// Set up the list of trusted clients, passing in the list information for a DTU
		/// </summary>
		/// <param name="clients">客户端列表</param>
		public void SetTrustClients(string[] clients)
		{
			trustOnline = new List<string>(clients);
		}

		/// <summary>
		/// 释放当前的对象
		/// </summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					trustLock?.Dispose();
					this.OnClientConnected = null;
				}
				disposedValue = true;
			}
		}

		/// <inheritdoc cref="M:System.IDisposable.Dispose" />
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return "NetworkAlienBase";
		}

		/// <summary>
		/// 获取错误的描述信息
		/// </summary>
		/// <param name="head">dtu信息</param>
		/// <param name="code">错误码</param>
		/// <param name="info">其他消息</param>
		/// <returns>错误信息</returns>
		public static string GetMsgFromCode(string head, int code, string info)
		{
			switch (code)
			{
			case 0:
				return head + "  Login Success" + info;
			case 1:
				return head + "  Login Repeat" + info;
			case 2:
				return head + "  Login Forbidden" + info;
			case 3:
				return head + "  Login Passwod Wrong" + info;
			default:
				return head + "  Login Unknow reason:" + info;
			}
		}
	}
}
