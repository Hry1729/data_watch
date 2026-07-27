using System;
using HslCommunication.Profinet.Siemens;

namespace HslCommunicationDemo
{
	public class FormSiemensS400 : FormSiemens
	{
		public FormSiemensS400()
			: base(SiemensPLCS.S400)
		{
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			userControlHead1.ProtocolInfo = "s7-400";
		}
	}
}
