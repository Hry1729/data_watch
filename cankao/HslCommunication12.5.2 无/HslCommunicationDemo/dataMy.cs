using System;

namespace HslCommunicationDemo
{
	public class dataMy : IComparable<dataMy>
	{
		public string Key { get; set; }

		public long Value { get; set; }

		public dataMy()
		{
		}

		public dataMy(string key, long value)
		{
			Key = key;
			Value = value;
		}

		public int CompareTo(dataMy other)
		{
			return Value.CompareTo(other.Value);
		}
	}
}
