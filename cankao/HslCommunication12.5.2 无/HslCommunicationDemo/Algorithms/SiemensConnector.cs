using System;
using HslCommunication.Algorithms.ConnectPool;
using HslCommunication.Profinet.Siemens;

namespace HslCommunicationDemo.Algorithms
{
	public class SiemensConnector : IConnector
	{
		private SiemensS7Net siemens;

		public bool IsConnectUsing { get; set; }

		public string GuidToken { get; set; }

		public DateTime LastUseTime { get; set; }

		public SiemensConnector(string ipAddress)
		{
			siemens = new SiemensS7Net(SiemensPLCS.S1200, ipAddress);
		}

		public void Open()
		{
			siemens.ConnectServer();
		}

		public void Close()
		{
			siemens.ConnectClose();
		}

		public SiemensS7Net GetSiemens()
		{
			return siemens;
		}
	}
}
