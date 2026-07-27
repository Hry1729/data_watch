using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace HslCommunicationDemo.HslDebug
{
	public class SocketDebugSession
	{
		private int useMode = 0;

		private long sessionId = 0L;

		private static long sessionIdCreate;

		public Socket WorkSocket { get; private set; }

		public EndPoint LocalEndPoint { get; private set; }

		public EndPoint RemoteEndPoint { get; private set; }

		public string PortName { get; set; }

		public SerialPort SerialPort { get; set; }

		public int UseMode => useMode;

		public long SessionID => sessionId;

		public bool CloseWhenRemove { get; set; } = true;

		public byte[] Buffer { get; set; }

		public SocketDebugSession(Socket socket)
		{
			sessionId = Interlocked.Increment(ref sessionIdCreate);
			WorkSocket = socket;
			try
			{
				RemoteEndPoint = WorkSocket.RemoteEndPoint;
				LocalEndPoint = WorkSocket.LocalEndPoint;
			}
			catch
			{
			}
		}

		public SocketDebugSession(Socket socket, EndPoint udp)
		{
			sessionId = Interlocked.Increment(ref sessionIdCreate);
			WorkSocket = socket;
			RemoteEndPoint = udp;
			try
			{
				LocalEndPoint = WorkSocket.LocalEndPoint;
			}
			catch
			{
			}
			useMode = 1;
		}

		public SocketDebugSession(SerialPort SerialPort, string com)
		{
			sessionId = Interlocked.Increment(ref sessionIdCreate);
			this.SerialPort = SerialPort;
			PortName = com;
			useMode = 2;
		}

		public void SendData(byte[] data)
		{
			if (data != null)
			{
				if (useMode == 0)
				{
					WorkSocket.Send(data);
				}
				else if (useMode == 1)
				{
					WorkSocket.SendTo(data, RemoteEndPoint);
				}
				else if (useMode == 2)
				{
					SerialPort.Write(data, 0, data.Length);
				}
			}
		}

		public void Close()
		{
			if (UseMode == 0 || UseMode == 1)
			{
				if (CloseWhenRemove)
				{
					WorkSocket?.Close();
				}
			}
			else
			{
				SerialPort?.Dispose();
			}
		}

		public override string ToString()
		{
			return (useMode < 2) ? RemoteEndPoint.ToString() : PortName;
		}
	}
}
