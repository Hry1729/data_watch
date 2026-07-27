using System;
using HslCommunication;

namespace HslCommunicationDemo.HslDebug
{
	public class MessgaeCreate
	{
		public string Text { get; set; }

		public bool HexBinary { get; set; } = true;

		public Func<string, ushort, OperateResult<byte[]>> BuildReadBool { get; set; }

		public Func<string, ushort, OperateResult<byte[]>> BuildReadWord { get; set; }

		public string Result { get; set; }
	}
}
