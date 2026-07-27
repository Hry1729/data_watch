using System.Collections.Generic;
using System.Net.NetworkInformation;

namespace HslCommunicationDemo.HslDebug
{
	public class NetworkStatusResult
	{
		public string Target { get; set; }

		public long AverageRoundtripTime { get; set; }

		public double PacketLossRate { get; set; }

		public int TotalSuccess { get; set; }

		public long TotalRoundtripTime { get; set; }

		public NetworkStatus Status { get; set; }

		public List<PingReply> PingReplies { get; } = new List<PingReply>();
	}
}
