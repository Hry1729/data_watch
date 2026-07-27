namespace HslRedisDesktop
{
	public class DbSettings
	{
		public int DBNumber { get; set; }

		public string Filter { get; set; }

		public DbSettings()
		{
			Filter = "*";
		}
	}
}
