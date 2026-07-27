using HslCommunication.Core.Security;

namespace HslCommunicationDemo.Vip
{
	public class CertificateItem
	{
		public string Name { get; set; }

		public HslCertificate Certificate { get; set; }

		public byte[] CertBinary { get; set; }

		public override string ToString()
		{
			return Name;
		}
	}
}
