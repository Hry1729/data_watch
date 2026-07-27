using System;
using HslCommunication.Profinet.Siemens;

namespace HslCommunicationDemo
{
	public class FormSiemensS300 : FormSiemens
	{
		public FormSiemensS300()
			: base(SiemensPLCS.S300)
		{
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			userControlHead1.ProtocolInfo = "s7-300";
		}
	}
}
