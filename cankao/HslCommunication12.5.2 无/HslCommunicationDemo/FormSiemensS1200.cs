using System;
using HslCommunication.Profinet.Siemens;

namespace HslCommunicationDemo
{
	public class FormSiemensS1200 : FormSiemens
	{
		public FormSiemensS1200()
			: base(SiemensPLCS.S1200)
		{
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			userControlHead1.ProtocolInfo = "s7-1200";
		}
	}
}
