using System.Xml.Linq;
using HslCommunicationDemo.DemoControl;

namespace HslCommunicationDemo.HslDebug
{
	public class PacketMessageItem
	{
		public string Name { get; set; }

		public string Description { get; set; }

		public string Message { get; set; }

		public virtual void LoadByXmlElement(XElement element)
		{
			Name = DataTableItem.GetXmlValue(element, "Name", Name, (string m) => m);
			Description = DataTableItem.GetXmlValue(element, "Description", Description, (string m) => m);
			Message = DataTableItem.GetXmlValue(element, "Message", Message, (string m) => m);
		}

		public virtual XElement ToXmlElement()
		{
			XElement xElement = new XElement("PacketMessageItem");
			xElement.SetAttributeValue("Name", Name);
			if (!string.IsNullOrEmpty(Description))
			{
				xElement.SetAttributeValue("Description", Description);
			}
			if (!string.IsNullOrEmpty(Message))
			{
				xElement.SetAttributeValue("Message", Message);
			}
			return xElement;
		}

		public override string ToString()
		{
			if (!string.IsNullOrEmpty(Name))
			{
				return Name;
			}
			return Message;
		}
	}
}
