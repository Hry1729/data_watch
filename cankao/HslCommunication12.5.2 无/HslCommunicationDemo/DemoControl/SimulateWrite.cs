using System;
using System.Windows.Forms;
using System.Xml.Linq;
using DynamicExpresso;

namespace HslCommunicationDemo.DemoControl
{
	public class SimulateWrite
	{
		public string Name { get; set; }

		public string Address { get; set; }

		public int Time { get; set; } = 100;

		public string Express { get; set; }

		public string Mark { get; set; }

		public Interpreter Script { get; set; }

		public long X { get; set; }

		public DateTime ExecuteTime { get; set; } = DateTime.Now;

		public DataGridViewRow Dgvr { get; set; }

		public XElement ToXmlElement()
		{
			XElement xElement = new XElement("SimulateWrite");
			xElement.SetAttributeValue("Name", Name);
			xElement.SetAttributeValue("Address", Address);
			xElement.SetAttributeValue("Time", Time);
			xElement.SetAttributeValue("Express", Express);
			xElement.SetAttributeValue("Mark", Mark);
			return xElement;
		}

		public void LoadByXmlElement(XElement element)
		{
			Name = DataTableItem.GetXmlValue(element, "Name", Name, (string m) => m);
			Address = DataTableItem.GetXmlValue(element, "Address", Address, (string m) => m);
			Time = DataTableItem.GetXmlValue(element, "Time", Time, int.Parse);
			Express = DataTableItem.GetXmlValue(element, "Express", Express, (string m) => m);
			Mark = DataTableItem.GetXmlValue(element, "Mark", Mark, (string m) => m);
		}
	}
}
