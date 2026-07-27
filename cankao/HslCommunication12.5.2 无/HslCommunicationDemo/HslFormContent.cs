using System;
using System.Drawing;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.Core.Device;
using HslCommunication.Core.Net;
using HslCommunication.Core.Pipe;
using HslCommunication.Core.Security;
using HslCommunication.LogNet;
using HslCommunication.Serial;
using HslCommunicationDemo.Control;
using HslCommunicationDemo.DemoControl;
using WeifenLuo.WinFormsUI.Docking;

namespace HslCommunicationDemo
{
	public class HslFormContent : DockContent
	{
		private static RSACryptoServiceProvider rsa = RSAHelper.CreateRsaProviderFromPublicKey("MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCqzFn3dRlmxu3qk+aCddTdIUFYhc6rxUpERe8XG+Vf8DQ70JnfWVcTmlCxpdIu6R0VWzk03o6F20rai0ZnBI1SkxRta34wm1mPDzEFs/vdY7FBFy153c7S3dwM5t9leSBSVRrV1SVWjlphwnGZ+FwMtGXlZ7W+kI5IY1ONhIRXwQIDAQAB");

		private XElement xElement = null;

		public ILogNet LogNet { get; set; }

		public string Password { get; set; }

		public string FormGuid { get; set; }

		public Image DeviceImage { get; set; }

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			AutoScroll = true;
		}

		public virtual void LoadXmlParameter(XElement element)
		{
			xElement = element;
			Text = xElement.Attribute(DemoDeviceList.XmlName).Value;
		}

		public void SetXml(XElement element)
		{
			xElement = element;
		}

		public virtual void SaveXmlParameter(XElement element)
		{
			throw new NotImplementedException("SaveXmlParameter Not Implemented");
		}

		public void SetProtocolImage(Image image)
		{
			DeviceImage = image;
			foreach (System.Windows.Forms.Control control in base.Controls)
			{
				if (control is UserControlHead userControlHead)
				{
					userControlHead.SetProtocolImage(image);
					break;
				}
			}
		}

		private void SaveDeviceXml(FormInputString form, XElement element, bool showDialog = true)
		{
			if (!showDialog || form.ShowDialog() == DialogResult.OK)
			{
				element.SetAttributeValue(DemoDeviceList.XmlGuid, Guid.NewGuid().ToString("N"));
				element.SetAttributeValue(DemoDeviceList.XmlType, GetType().Name);
				element.SetAttributeValue(DemoDeviceList.XmlName, form.DeviceAlias);
				if (!string.IsNullOrEmpty(form.Password))
				{
					element.SetAttributeValue(DemoDeviceList.XmlEncrypt2, Convert.ToBase64String(rsa.Encrypt(Encoding.UTF8.GetBytes(form.Password), false)));
					XElement xElement = new XElement("Device");
					xElement.SetAttributeValue(DemoDeviceList.XmlType, GetType().Name);
					xElement.SetAttributeValue(DemoDeviceList.XmlName, form.DeviceAlias);
					SaveXmlParameter(xElement);
					element.SetAttributeValue(value: new AesCryptography(form.Password.PadRight(32, '0')).Encrypt(xElement.ToString()), name: DemoDeviceList.XmlEncrypt);
				}
				else
				{
					SaveXmlParameter(element);
				}
				FormMain.Form?.GetPanelLeft()?.AddDeviceList(element, this);
				DemoUtils.ShowMessage("Save Success");
			}
		}

		public void userControlHead1_SaveConnectEvent(object sender, EventArgs e)
		{
			try
			{
				if (this.xElement == null)
				{
					this.xElement = new XElement("Device");
					using (FormInputString formInputString = new FormInputString())
					{
						SaveDeviceXml(formInputString, this.xElement);
						Text = formInputString.DeviceAlias;
						return;
					}
				}
				string text = this.xElement.Attribute(DemoDeviceList.XmlName)?.Value;
				using (FormInputString formInputString2 = new FormInputString(text, Password))
				{
					if (formInputString2.ShowDialog() != DialogResult.OK)
					{
						return;
					}
					if (text != formInputString2.DeviceAlias)
					{
						string xmlValue = GetXmlValue(this.xElement, DemoDeviceList.XmlGuid, "", (string m) => m);
						if (!string.IsNullOrEmpty(xmlValue))
						{
							FormMain.Form?.GetPanelLeft()?.RemoveDeviceForm(xmlValue);
						}
						this.xElement = new XElement("Device");
						SaveDeviceXml(formInputString2, this.xElement, false);
						Text = formInputString2.DeviceAlias;
						return;
					}
					Password = formInputString2.Password;
					string value = this.xElement.Attribute(DemoDeviceList.XmlGuid)?.Value;
					this.xElement = new XElement("Device");
					this.xElement.SetAttributeValue(DemoDeviceList.XmlGuid, value);
					this.xElement.SetAttributeValue(DemoDeviceList.XmlType, GetType().Name);
					this.xElement.SetAttributeValue(DemoDeviceList.XmlName, formInputString2.DeviceAlias);
					if (!string.IsNullOrEmpty(Password))
					{
						AesCryptography aesCryptography = new AesCryptography(Password.PadRight(32, '0'));
						try
						{
							this.xElement.SetAttributeValue(DemoDeviceList.XmlEncrypt2, Convert.ToBase64String(rsa.Encrypt(Encoding.UTF8.GetBytes(formInputString2.Password), false)));
							XElement xElement = new XElement("Device");
							xElement.SetAttributeValue(DemoDeviceList.XmlType, GetType().Name);
							xElement.SetAttributeValue(DemoDeviceList.XmlName, this.xElement.Attribute(DemoDeviceList.XmlName)?.Value);
							SaveXmlParameter(xElement);
							this.xElement.SetAttributeValue(DemoDeviceList.XmlEncrypt, aesCryptography.Encrypt(xElement.ToString()));
							FormMain.Form?.GetPanelLeft()?.AddDeviceList(this.xElement, this);
							DemoUtils.ShowMessage("Save Success");
							return;
						}
						catch (Exception ex)
						{
							DemoUtils.ShowMessage(string.Concat(str1: ex.Message, str0: (Program.Language == 1) ? "加密失败，无法加载当前的配置信息" : "Encryption failed and the current configuration information could not be loaded "));
							return;
						}
					}
					SaveXmlParameter(this.xElement);
					FormMain.Form?.GetPanelLeft()?.AddDeviceList(this.xElement, this);
					DemoUtils.ShowMessage("Save Success");
				}
			}
			catch (Exception ex2)
			{
				SoftBasic.ShowExceptionMessage("Save failed:", ex2);
			}
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

		protected OperateResult DeviceConnectPLC(DeviceTcpNet device)
		{
			if (device.CommunicationPipe.GetType() == typeof(PipeDtuNet))
			{
				return OperateResult.CreateSuccessResult();
			}
			return device.ConnectServer();
		}

		protected OperateResult DeviceConnectPLC(TcpNetCommunication device)
		{
			if (device.CommunicationPipe.GetType() == typeof(PipeDtuNet))
			{
				return OperateResult.CreateSuccessResult();
			}
			return device.ConnectServer();
		}

		protected OperateResult DeviceConnectPLC(DeviceSerialPort device)
		{
			if (device.CommunicationPipe.GetType() == typeof(PipeDtuNet))
			{
				return OperateResult.CreateSuccessResult();
			}
			return device.Open();
		}

		protected OperateResult DeviceConnectPLC(SerialBase device)
		{
			if (device.CommunicationPipe.GetType() == typeof(PipeDtuNet))
			{
				return OperateResult.CreateSuccessResult();
			}
			return device.Open();
		}

		protected OperateResult DeviceConnectPLC(DeviceUdpNet device)
		{
			return OperateResult.CreateSuccessResult();
		}
	}
}
