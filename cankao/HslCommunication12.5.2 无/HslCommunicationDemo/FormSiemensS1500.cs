using System;
using HslCommunication.Profinet.Siemens;

namespace HslCommunicationDemo
{
	public class FormSiemensS1500 : FormSiemens
	{
		public FormSiemensS1500()
			: base(SiemensPLCS.S1500)
		{
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			userControlHead1.ProtocolInfo = "s7-1500";
		}
	}
}
