using System;
using HslCommunication.Profinet.Siemens;

namespace HslCommunicationDemo
{
	public class FormSiemensS200Smart : FormSiemens200
	{
		public FormSiemensS200Smart()
			: base(SiemensPLCS.S200Smart)
		{
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			userControlHead1.ProtocolInfo = "s7-200Smart";
		}
	}
}
