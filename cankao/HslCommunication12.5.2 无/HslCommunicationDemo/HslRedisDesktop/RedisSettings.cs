using HslCommunication.Enthernet.Redis;
using Newtonsoft.Json;

namespace HslRedisDesktop
{
	public class RedisSettings
	{
		public string Name { get; set; }

		public string IpAddress { get; set; }

		public int Port { get; set; }

		public string Password { get; set; }

		[JsonIgnore]
		public int DBBlock { get; set; }

		[JsonIgnore]
		public RedisClient Redis { get; set; }

		public RedisClient GetClient()
		{
			return new RedisClient(IpAddress, Port, Password)
			{
				ConnectTimeOut = 5000
			};
		}

		public override string ToString()
		{
			return Name;
		}
	}
}
