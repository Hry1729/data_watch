using System.Xml.Linq;

namespace HslCommunicationDemo
{
	public class DemoDeviceList
	{
		private XElement xElement;

		public static readonly string XmlName = "Name";

		public static readonly string XmlGuid = "Guid";

		public static readonly string XmlType = "Type";

		public static readonly string XmlEncrypt = "EncryptXml";

		public static readonly string XmlEncrypt2 = "EncryptKeyXml";

		public static readonly string XmlIpAddress = "IP";

		public static readonly string XmlPort = "Port";

		public static readonly string XmlConnectTimeOut = "ConnectTimeOut";

		public static readonly string XmlReceiveTimeOut = "ReceiveTimeOut";

		public static readonly string XmlRack = "Rack";

		public static readonly string XmlSlot = "Slot";

		public static readonly string XmlCom = "ComPort";

		public static readonly string XmlBaudRate = "BaudRate";

		public static readonly string XmlDataBits = "DataBit";

		public static readonly string XmlStopBit = "StopBit";

		public static readonly string XmlParity = "Parity";

		public static readonly string XmlStation = "Station";

		public static readonly string XmlTimeout = "Timeout";

		public static readonly string XmlSumCheck = "SumCheck";

		public static readonly string XmlBinary = "Binary";

		public static readonly string XmlAddressStartWithZero = "AddressStartWithZero";

		public static readonly string XmlDataFormat = "DataFormat";

		public static readonly string XmlStringReverse = "StringReverse";

		public static readonly string XmlUserName = "UserName";

		public static readonly string XmlPassword = "Password";

		public static readonly string XmlRtsEnable = "RtsEnable";

		public static readonly string XmlNetNumber = "NetNumber";

		public static readonly string XmlUnitNumber = "UnitNumber";

		public static readonly string XmlChangeSA1 = "ChangeSA1";

		public static readonly string XmlPCUnitNumber = "PCUnitNumber";

		public static readonly string XmlDeviceId = "DeviceId";

		public static readonly string XmlCpuType = "CpuType";

		public static readonly string XmlCompanyID = "CompanyID";

		public static readonly string XmlTarget = "Target";

		public static readonly string XmlSender = "Sender";

		public static readonly string XmlTagCache = "TagCache";

		public static readonly string XmlKeepLive = "KeepLive";

		public static readonly string XmlTopic = "Topic";

		public static readonly string XmlUrl = "Url";

		public static readonly string XmlRetureMessage = "ReturnMessage";

		public static readonly string XmlCrossDomain = "CrossDomain";

		public static readonly string XmlContentType = "ContentType";

		public static readonly string XmlToken = "Token";

		public static readonly string XmlAlias = "Alias";

		public static readonly string XmlFilePath = "FilePath";

		public XElement GetDevices => xElement;

		public DemoDeviceList()
		{
			xElement = new XElement("DeviceList");
		}

		public void AddDevice(XElement device)
		{
			XElement xElement = null;
			foreach (XElement item in this.xElement.Elements())
			{
				if (item.Attribute(XmlGuid)?.Value == device.Attribute(XmlGuid)?.Value)
				{
					xElement = item;
					break;
				}
			}
			if (xElement != null)
			{
				xElement.RemoveAll();
				foreach (XAttribute item2 in device.Attributes())
				{
					xElement.SetAttributeValue(item2.Name, item2.Value);
				}
				{
					foreach (XElement item3 in device.Elements())
					{
						xElement.Add(item3);
					}
					return;
				}
			}
			this.xElement.Add(device);
		}

		public void DeleteDevice(XElement device)
		{
			XElement xElement = null;
			foreach (XElement item in this.xElement.Elements())
			{
				if (item.Attribute(XmlGuid) == device.Attribute(XmlGuid))
				{
					xElement = item;
					break;
				}
			}
			xElement?.Remove();
		}

		public void DeleteDevice(string device)
		{
			foreach (XElement item in xElement.Elements())
			{
				string text = item.Attribute("Name")?.Value;
				if (text != null && text.StartsWith(device))
				{
					item.Remove();
				}
			}
		}

		public void RenameDevice(XElement device, string name)
		{
			if (device == null)
			{
				return;
			}
			foreach (XElement item in xElement.Elements())
			{
				if (item.Attribute(XmlGuid) == device.Attribute(XmlGuid))
				{
					item.SetAttributeValue("Name", name);
					break;
				}
			}
		}

		public void SetDevices(XElement element)
		{
			xElement = element;
		}
	}
}
