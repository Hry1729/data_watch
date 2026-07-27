using System.Xml.Linq;

namespace HslCommunicationDemo.PLC.Siemens
{
	public class DbItem
	{
		public int DbNumber { get; set; }

		public int Size { get; set; }

		public string Name { get; set; }

		public string Note { get; set; }

		public DbItem()
		{
			DbNumber = 1;
			Size = 65536;
			Name = "";
			Note = string.Empty;
		}

		public DbItem(XElement element)
			: this()
		{
			DbNumber = int.Parse(element.Attribute("DbNumber").Value);
			Size = HslFormContent.GetXmlValue(element, "Size", 65535, int.Parse);
			Name = HslFormContent.GetXmlValue(element, "Name", "", (string m) => m);
			Note = HslFormContent.GetXmlValue(element, "Note", "", (string m) => m);
		}

		public override string ToString()
		{
			return $"DB{DbNumber} {Size}Byte {Name}";
		}

		public XElement ToXml()
		{
			return new XElement("DbItem", new XAttribute("DbNumber", DbNumber), new XAttribute("Size", Size), new XAttribute("Name", Name), new XAttribute("Note", Note));
		}

		public static DbItem Parse(XElement element)
		{
			return new DbItem(element);
		}
	}
}
