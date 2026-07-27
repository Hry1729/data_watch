namespace HslCommunicationDemo.DemoControl
{
	public class DeviceAddressExample
	{
		public string AddressExample { get; set; }

		public string AddressType { get; set; }

		public bool BitEnable { get; set; }

		public bool WordEnable { get; set; }

		public string Mark { get; set; }

		public bool IsHeader { get; set; }

		public string Unit { get; set; }

		public bool FillTagNameWithAddressType { get; set; } = false;

		public string DataType { get; set; }

		public DeviceAddressExample()
		{
		}

		public DeviceAddressExample(string address, string type, bool bit, bool word, string mark, bool header = false, bool fill = false, string unit = "", string dataType = "")
		{
			AddressExample = address;
			AddressType = type;
			BitEnable = bit;
			WordEnable = word;
			Mark = mark;
			IsHeader = header;
			FillTagNameWithAddressType = fill;
			Unit = unit;
			DataType = dataType;
		}

		public override string ToString()
		{
			return "DeviceAddressExample[" + AddressExample + "]";
		}

		public static string GetTitle()
		{
			return (Program.Language == 1) ? "地址示例" : "Address Example";
		}
	}
}
