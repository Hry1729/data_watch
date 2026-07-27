using System.Text;
using HslCommunication;
using HslCommunication.Core;

namespace HslCommunicationDemo
{
	public class UserType : IDataTransfer
	{
		private IByteTransform ByteTransform = new RegularByteTransform();

		public ushort ReadCount => 10;

		public int count { get; set; }

		public float temp { get; set; }

		public short name1 { get; set; }

		public string barcode { get; set; }

		public void ParseSource(byte[] Content)
		{
			int num = ByteTransform.TransInt32(Content, 0);
			float num2 = ByteTransform.TransSingle(Content, 4);
			short num3 = ByteTransform.TransInt16(Content, 8);
			string @string = Encoding.ASCII.GetString(Content, 10, 10);
		}

		public byte[] ToSource()
		{
			byte[] array = new byte[20];
			ByteTransform.TransByte(count).CopyTo(array, 0);
			ByteTransform.TransByte(temp).CopyTo(array, 4);
			ByteTransform.TransByte(name1).CopyTo(array, 8);
			Encoding.ASCII.GetBytes(barcode).CopyTo(array, 10);
			return array;
		}
	}
}
