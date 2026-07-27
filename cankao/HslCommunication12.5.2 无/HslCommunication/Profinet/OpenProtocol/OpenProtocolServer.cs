using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using HslCommunication.BasicFramework;
using HslCommunication.Core.IMessage;
using HslCommunication.Core.Net;
using HslCommunication.Core.Pipe;

namespace HslCommunication.Profinet.OpenProtocol
{
	/// <summary>
	/// 开放协议的虚拟服务器
	/// </summary>
	public class OpenProtocolServer : CommunicationServer
	{
		private Timer timer;

		private long tick = 0L;

		private DateTime mid80Time = DateTime.Now;

		private string parameterSetID = "001";

		private DateTime parameterSetTime = DateTime.Now;

		/// <summary>
		/// 实例化一个默认的对象
		/// </summary>
		public OpenProtocolServer()
		{
			base.OnPipeMessageReceived += OpenProtocolServer_OnPipeMessageReceived;
			base.CreatePipeSession = (CommunicationPipe m) => new OpenProtocolSession
			{
				Communication = m
			};
			timer = new Timer(ThreadKeepAlive, null, 2000, 2000);
		}

		private byte[] CreateMID0015Message()
		{
			return OpenProtocolNet.BuildOpenProtocolMessage(15, 1, 0, -1, -1, false, parameterSetID, parameterSetTime.ToString("yyyy-MM-dd:HH:mm:ss")).Content;
		}

		private void ThreadKeepAlive(object state)
		{
			SendAll(CreateMID0015Message(), (OpenProtocolSession m) => m.MID0014Subscribe);
			SendAll(OpenProtocolNet.BuildOpenProtocolMessage(35, 1, 0, -1, -1, true, "01", "0", "0", $"{tick:D4}", $"{tick:D4}", GetTimeNowOpenString()).Content, (OpenProtocolSession m) => m.MID0034Subscribe);
			SendAll(OpenProtocolNet.BuildOpenProtocolMessage(52, 1, 0, -1, -1, true, $"{tick:D25}").Content, (OpenProtocolSession m) => m.MID0051Subscribe);
			SendAll(OpenProtocolNet.BuildOpenProtocolMessage(61, 1, 0, -1, -1, true, "0001", "01", "airbag" + $"{tick:D19}", "KPOL3456JKLO897".PadRight(25, ' '), "00", "003", "0000", $"{tick:D4}", "0", "0", "1", $"{tick:D6}", "001400", "001200", "000739", "00000", "09999", "00000", "00000", GetTimeNowOpenString(-10), GetTimeNowOpenString(), "1", "3456798765").Content, (OpenProtocolSession m) => m.MID0060Subscribe);
			SendAll(OpenProtocolNet.BuildOpenProtocolMessage(71, 1, 0, -1, -1, true, "E404", "1", "1", GetTimeNowOpenString()).Content, (OpenProtocolSession m) => m.MID0070Subscribe);
			SendAll(OpenProtocolNet.BuildOpenProtocolMessage(106, 1, 0, -1, -1, true, "11", "01", "4294967295", "07", "ABCD1234569876543210", GetTimeNowOpenString(), "50", $"{tick:D20}", "1", "0", $"{tick:D40}", "01").Content, (OpenProtocolSession m) => m.MID0105Subscribe);
			tick++;
			if (tick >= 10000)
			{
				tick = 0L;
			}
		}

		/// <inheritdoc />
		protected override INetMessage GetNewNetMessage()
		{
			return new OpenProtocolMessage();
		}

		private byte[] CreateCommandAccepted(int mid)
		{
			return OpenProtocolNet.BuildOpenProtocolMessage(5, 1, -1, -1, -1, false, mid.ToString("D4")).Content;
		}

		private byte[] CreateCommandError(int mid, int err)
		{
			return OpenProtocolNet.BuildOpenProtocolMessage(4, 1, -1, -1, -1, false, mid.ToString("D4") + err.ToString("D2")).Content;
		}

		private string GetTimeNowOpenString(int seconds = 0)
		{
			return DateTime.Now.AddSeconds(seconds).ToString("yyyy-MM-dd:HH:mm:ss");
		}

		private void SendData(PipeSession session, byte[] buffer)
		{
			session.Communication.Send(buffer);
			LogDebugMsg($"{session} Send: " + SoftBasic.GetAsciiStringRender(buffer));
		}

		private static int GetRevisionInfo(byte[] cmds)
		{
			string @string = Encoding.ASCII.GetString(cmds, 8, 3);
			if (string.IsNullOrEmpty(@string))
			{
				return 1;
			}
			return Convert.ToInt32(@string);
		}

		private void OpenProtocolServer_OnPipeMessageReceived(PipeSession session, byte[] buffer)
		{
			LogDebugMsg($"{session} Recv: " + SoftBasic.GetAsciiStringRender(buffer));
			if (buffer.Length < 20)
			{
				return;
			}
			try
			{
				int num = Convert.ToInt32(Encoding.ASCII.GetString(buffer, 4, 4));
				if (num > 0 && num < 10)
				{
					switch (num)
					{
					case 1:
						SendData(session, OpenProtocolNet.BuildOpenProtocolMessage(2, 1, -1, -1, -1, true, "0001", "01", "Airbag1".PadRight(25, ' ')).Content);
						break;
					case 3:
						SendData(session, CreateCommandAccepted(num));
						break;
					case 8:
						if (Convert.ToInt32(Encoding.ASCII.GetString(buffer, 20, 4)) == 1202 && session is OpenProtocolSession openProtocolSession2)
						{
							openProtocolSession2.MID0060Subscribe = true;
						}
						SendData(session, CreateCommandAccepted(num));
						break;
					case 9:
						if (Convert.ToInt32(Encoding.ASCII.GetString(buffer, 20, 4)) == 1202 && session is OpenProtocolSession openProtocolSession)
						{
							openProtocolSession.MID0060Subscribe = false;
						}
						SendData(session, CreateCommandAccepted(num));
						break;
					}
				}
				else if (num >= 10 && num < 30)
				{
					switch (num)
					{
					case 10:
						SendData(session, OpenProtocolNet.BuildOpenProtocolMessage(11, 1, -1, -1, -1, false, "002001002").Content);
						break;
					case 12:
					{
						string @string = Encoding.ASCII.GetString(buffer, 20, 3);
						SendData(session, OpenProtocolNet.BuildOpenProtocolMessage(13, 1, -1, -1, -1, true, @string, "Airbag1".PadRight(25, ' '), "1", "00", "123456", "654321", "112233", "00000", "99999", $"0{tick:D4}").Content);
						break;
					}
					case 14:
						if (session is OpenProtocolSession openProtocolSession4)
						{
							if (!openProtocolSession4.MID0014Subscribe)
							{
								openProtocolSession4.MID0014Subscribe = true;
								SendData(session, CreateCommandAccepted(num));
								SendData(session, CreateMID0015Message());
							}
							else
							{
								SendData(session, CreateCommandError(num, 13));
							}
						}
						break;
					case 17:
						if (session is OpenProtocolSession openProtocolSession3)
						{
							if (openProtocolSession3.MID0014Subscribe)
							{
								openProtocolSession3.MID0014Subscribe = false;
								SendData(session, CreateCommandAccepted(num));
							}
							else
							{
								SendData(session, CreateCommandError(num, 14));
							}
						}
						break;
					case 18:
						parameterSetID = Encoding.ASCII.GetString(buffer, 20, 3);
						parameterSetTime = DateTime.Now;
						SendData(session, CreateCommandAccepted(num));
						break;
					case 19:
						SendData(session, CreateCommandAccepted(num));
						break;
					case 20:
						SendData(session, CreateCommandAccepted(num));
						break;
					}
				}
				else if (num >= 30 && num < 40)
				{
					switch (num)
					{
					case 30:
						switch (GetRevisionInfo(buffer))
						{
						case 1:
							SendData(session, OpenProtocolNet.BuildOpenProtocolMessage(31, 1, -1, -1, -1, false, "020102").Content);
							break;
						case 2:
							SendData(session, OpenProtocolNet.BuildOpenProtocolMessage(31, 2, -1, -1, -1, false, "000200010002").Content);
							break;
						}
						break;
					case 32:
					{
						if (buffer.Length < 22)
						{
							SendData(session, CreateCommandError(num, 1));
							break;
						}
						string string2 = Encoding.ASCII.GetString(buffer, 20, 2);
						SendData(session, OpenProtocolNet.BuildOpenProtocolMessage(33, 1, -1, -1, -1, true, string2, "Airbag1".PadRight(25, ' '), "0", $"{tick:D4}", "00000", "0", "1", "1", "0", "0", "1", "01", "15:011:0:22;").Content);
						break;
					}
					case 34:
						if (session is OpenProtocolSession openProtocolSession6)
						{
							if (!openProtocolSession6.MID0034Subscribe)
							{
								openProtocolSession6.MID0034Subscribe = true;
								SendData(session, CreateCommandAccepted(num));
							}
							else
							{
								SendData(session, CreateCommandError(num, 18));
							}
						}
						break;
					case 37:
						if (session is OpenProtocolSession openProtocolSession5)
						{
							if (openProtocolSession5.MID0034Subscribe)
							{
								openProtocolSession5.MID0034Subscribe = false;
								SendData(session, CreateCommandAccepted(num));
							}
							else
							{
								SendData(session, CreateCommandError(num, 19));
							}
						}
						break;
					case 38:
						if (GetRevisionInfo(buffer) == 1)
						{
							if (Encoding.ASCII.GetString(buffer, 20, 2) == "01")
							{
								SendData(session, CreateCommandAccepted(num));
							}
							else
							{
								SendData(session, CreateCommandError(num, 17));
							}
						}
						else if (Encoding.ASCII.GetString(buffer, 20, 4) == "0001")
						{
							SendData(session, CreateCommandAccepted(num));
						}
						else
						{
							SendData(session, CreateCommandError(num, 17));
						}
						break;
					case 39:
						SendData(session, CreateCommandAccepted(num));
						break;
					}
				}
				else if (num >= 40 && num < 50)
				{
					if (num == 40)
					{
						SendData(session, OpenProtocolNet.BuildOpenProtocolMessage(41, 1, -1, -1, -1, true, "C341212".PadRight(14, ' '), "548796".PadRight(10, ' '), GetTimeNowOpenString(), "670919".PadRight(10, ' ')).Content);
					}
					else
					{
						SendData(session, CreateCommandAccepted(num));
					}
				}
				else if (num >= 50 && num < 60)
				{
					switch (num)
					{
					case 50:
						SendData(session, CreateCommandAccepted(num));
						SetVinNumber(Encoding.ASCII.GetString(buffer, 20, 25).Trim());
						break;
					case 51:
						if (session is OpenProtocolSession openProtocolSession8)
						{
							if (!openProtocolSession8.MID0051Subscribe)
							{
								openProtocolSession8.MID0051Subscribe = true;
								SendData(session, CreateCommandAccepted(num));
							}
							else
							{
								SendData(session, CreateCommandError(num, 6));
							}
						}
						break;
					case 54:
						if (session is OpenProtocolSession openProtocolSession7)
						{
							if (openProtocolSession7.MID0051Subscribe)
							{
								openProtocolSession7.MID0051Subscribe = false;
								SendData(session, CreateCommandAccepted(num));
							}
							else
							{
								SendData(session, CreateCommandError(num, 7));
							}
						}
						break;
					}
				}
				else if (num >= 60 && num < 70)
				{
					switch (num)
					{
					case 60:
						if (session is OpenProtocolSession openProtocolSession10)
						{
							if (!openProtocolSession10.MID0060Subscribe)
							{
								openProtocolSession10.MID0060Subscribe = true;
								SendData(session, CreateCommandAccepted(num));
							}
							else
							{
								SendData(session, CreateCommandError(num, 9));
							}
						}
						break;
					case 63:
						if (session is OpenProtocolSession openProtocolSession9)
						{
							if (openProtocolSession9.MID0060Subscribe)
							{
								openProtocolSession9.MID0060Subscribe = false;
								SendData(session, CreateCommandAccepted(num));
							}
							else
							{
								SendData(session, CreateCommandError(num, 10));
							}
						}
						break;
					case 64:
					{
						string string3 = Encoding.ASCII.GetString(buffer, 20, 10);
						SendData(session, OpenProtocolNet.BuildOpenProtocolMessage(65, 1, -1, -1, -1, true, string3, "AIRBAG".PadRight(25, ' '), "001", $"{tick:D4}", "0", "0", "0", "001467", "00046", GetTimeNowOpenString(), "2").Content);
						break;
					}
					}
				}
				else if (num >= 70 && num < 80)
				{
					switch (num)
					{
					case 70:
						if (session is OpenProtocolSession openProtocolSession12)
						{
							if (!openProtocolSession12.MID0070Subscribe)
							{
								openProtocolSession12.MID0070Subscribe = true;
								SendData(session, CreateCommandAccepted(num));
							}
							else
							{
								SendData(session, CreateCommandError(num, 11));
							}
						}
						break;
					case 73:
						if (session is OpenProtocolSession openProtocolSession11)
						{
							if (openProtocolSession11.MID0070Subscribe)
							{
								openProtocolSession11.MID0070Subscribe = false;
								SendData(session, CreateCommandAccepted(num));
							}
							else
							{
								SendData(session, CreateCommandError(num, 12));
							}
						}
						break;
					case 78:
						SendData(session, CreateCommandAccepted(num));
						break;
					}
				}
				else if (num >= 80 && num < 90)
				{
					switch (num)
					{
					case 80:
						SendData(session, OpenProtocolNet.BuildOpenProtocolMessage(81, 1, -1, -1, -1, false, mid80Time.ToString("yyyy-MM-dd:HH:mm:ss")).Content);
						break;
					case 82:
					{
						byte[] array = buffer.SelectMiddle(20, 19);
						array[10] = 32;
						if (DateTime.TryParse(Encoding.ASCII.GetString(array), out var result))
						{
							mid80Time = result;
							SendData(session, CreateCommandAccepted(num));
						}
						else
						{
							SendData(session, CreateCommandError(num, 1));
						}
						break;
					}
					}
				}
				else if (num >= 105 && num < 110)
				{
					switch (num)
					{
					case 105:
						if (session is OpenProtocolSession openProtocolSession14)
						{
							if (!openProtocolSession14.MID0105Subscribe)
							{
								openProtocolSession14.MID0105Subscribe = true;
								SendData(session, CreateCommandAccepted(num));
							}
							else
							{
								SendData(session, CreateCommandError(num, 9));
							}
						}
						break;
					case 109:
						if (session is OpenProtocolSession openProtocolSession13)
						{
							if (openProtocolSession13.MID0105Subscribe)
							{
								openProtocolSession13.MID0105Subscribe = false;
								SendData(session, CreateCommandAccepted(num));
							}
							else
							{
								SendData(session, CreateCommandError(num, 10));
							}
						}
						break;
					}
				}
				else if (num == 9999)
				{
					SendData(session, buffer);
				}
				else
				{
					SendData(session, CreateCommandError(num, 1));
				}
			}
			catch (Exception ex)
			{
				base.LogNet?.WriteException("OpenProtocolServer_OnPipeMessageReceived -> ", ex);
			}
		}

		private void SendAll(byte[] buffer, Func<OpenProtocolSession, bool> select)
		{
			PipeSession[] pipeSessions = GetPipeSessions();
			for (int i = 0; i < pipeSessions.Length; i++)
			{
				if (pipeSessions[i] is OpenProtocolSession openProtocolSession && select(openProtocolSession))
				{
					SendData(openProtocolSession, buffer);
				}
			}
		}

		/// <summary>
		/// 设置一个新的 vin number 数据，并且发布给所有订阅的客户端
		/// </summary>
		/// <param name="vin">VIN Number</param>
		public void SetVinNumber(string vin)
		{
			SendAll(OpenProtocolNet.BuildOpenProtocolMessage(51, 1, 0, -1, -1, true, vin.PadRight(25, ' ')).Content, (OpenProtocolSession m) => m.MID0051Subscribe);
		}

		/// <summary>
		/// 发布一个SECS的消息，需要指定一些参数信息
		/// </summary>
		public void Publish(int mid, int revison, int stationId, int spindleId, List<string> parameters)
		{
			SendAll(OpenProtocolNet.BuildOpenProtocolMessage(mid, revison, 0, stationId, spindleId, false, parameters.ToArray()).Content, (OpenProtocolSession m) => true);
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return $"OpenProtocolServer[{base.Port}]";
		}
	}
}
