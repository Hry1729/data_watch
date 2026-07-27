using System.Net;
using System.Net.Sockets;

namespace HslCommunicationDemo
{
	internal class ClientSession
	{
		public Socket Socket { get; set; }

		public IPEndPoint EndPoint { get; set; }

		public override string ToString()
		{
			return EndPoint.ToString();
		}
	}
}
