using System;
using System.ComponentModel;
using System.Xml.Linq;
using HslCommunication.BasicFramework;

namespace HslCommunicationDemo.DemoControl
{
	[TypeConverter(typeof(ExpandableObjectConverter))]
	public class DataTableItem
	{
		public string Name { get; set; }

		public string Description { get; set; }

		public string Address { get; set; }

		public int Length { get; set; } = -1;

		public string DataTypeCode { get; set; }

		public string Unit { get; set; } = "";

		public StringEncoding StringEncoding { get; set; }

		public string Expression { get; set; }

		public virtual void LoadByXmlElement(XElement element)
		{
			Name = GetXmlValue(element, "Name", Name, (string m) => m);
			Description = GetXmlValue(element, "Description", Description, (string m) => m);
			Address = element.Attribute("Address").Value;
			DataTypeCode = GetXmlValue(element, "DataTypeCode", DataTypeCode, (string m) => m).ToLower();
			Length = GetXmlValue(element, "Length", Length, int.Parse);
			Unit = GetXmlValue(element, "Unit", Unit, (string m) => m);
			StringEncoding = GetXmlEnum(element, "StringEncoding", StringEncoding.ASCII);
			Expression = GetXmlValue(element, "Expression", Expression, (string m) => m);
		}

		public virtual XElement ToXmlElement()
		{
			XElement xElement = new XElement("DataTableItem");
			xElement.SetAttributeValue("Name", Name);
			if (!string.IsNullOrEmpty(Description))
			{
				xElement.SetAttributeValue("Description", Description);
			}
			xElement.SetAttributeValue("Address", Address);
			xElement.SetAttributeValue("DataTypeCode", DataTypeCode);
			if (Length != -1)
			{
				xElement.SetAttributeValue("Length", Length);
			}
			if (!string.IsNullOrEmpty(Unit))
			{
				xElement.SetAttributeValue("Unit", Unit);
			}
			if (DataTypeCode == "string")
			{
				xElement.SetAttributeValue("StringEncoding", StringEncoding.ToString());
			}
			if (!string.IsNullOrEmpty(Expression))
			{
				xElement.SetAttributeValue("Expression", Expression);
			}
			return xElement;
		}

		public static T GetXmlValue<T>(XElement element, string name, T defaultValue, Func<string, T> trans)
		{
			XAttribute xAttribute = element.Attribute(name);
			if (xAttribute == null)
			{
				return defaultValue;
			}
			try
			{
				return trans(xAttribute.Value);
			}
			catch
			{
				return defaultValue;
			}
		}

		public static T GetXmlEnum<T>(XElement element, string name, T defaultValue) where T : struct
		{
			XAttribute xAttribute = element.Attribute(name);
			if (xAttribute == null)
			{
				return defaultValue;
			}
			try
			{
				return SoftBasic.GetEnumFromString<T>(xAttribute.Value);
			}
			catch
			{
				return defaultValue;
			}
		}

		public override string ToString()
		{
			return "CurveLine[" + (string.IsNullOrEmpty(Name) ? Address : Name) + "]";
		}
	}
}
