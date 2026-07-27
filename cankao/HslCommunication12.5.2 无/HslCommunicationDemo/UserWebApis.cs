using System.Xml.Linq;
using HslCommunication.BasicFramework;

namespace HslCommunicationDemo
{
	public class UserWebApis
	{
		public string Name { get; set; }

		public string HttpMethod { get; set; }

		public string ContentType { get; set; }

		public string Body { get; set; }

		public override string ToString()
		{
			return "[" + HttpMethod + "] " + Name;
		}

		public XElement ToXml()
		{
			XElement xElement = new XElement("UserWebApis");
			xElement.SetAttributeValue("Name", Name);
			xElement.SetAttributeValue("HttpMethod", HttpMethod);
			xElement.SetAttributeValue("ContentType", ContentType);
			xElement.SetAttributeValue("Body", Body);
			return xElement;
		}

		public void LoadByXml(XElement element)
		{
			Name = SoftBasic.GetXmlValue(element, "Name", Name);
			HttpMethod = SoftBasic.GetXmlValue(element, "HttpMethod", HttpMethod);
			ContentType = SoftBasic.GetXmlValue(element, "ContentType", ContentType);
			Body = SoftBasic.GetXmlValue(element, "Body", Body);
		}
	}
}
