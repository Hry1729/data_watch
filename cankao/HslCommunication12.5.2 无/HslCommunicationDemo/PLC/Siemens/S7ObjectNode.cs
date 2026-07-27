using System.Windows.Forms;
using HslCommunication.Profinet.Siemens.S7PlusHelper;

namespace HslCommunicationDemo.PLC.Siemens
{
	public class S7ObjectNode
	{
		public S7Object S7Object { get; set; }

		public TreeNode Node { get; set; }
	}
}
