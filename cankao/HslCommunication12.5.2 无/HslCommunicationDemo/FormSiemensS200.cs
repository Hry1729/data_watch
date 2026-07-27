using System;
using HslCommunication.Profinet.Siemens;

namespace HslCommunicationDemo
{
	public class FormSiemensS200 : FormSiemens200
	{
		public FormSiemensS200()
			: base(SiemensPLCS.S200)
		{
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			userControlHead1.ProtocolInfo = "s7-200";
		}
	}
}
