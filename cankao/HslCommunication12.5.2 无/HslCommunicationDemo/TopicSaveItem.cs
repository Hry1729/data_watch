using System;
using System.Threading;
using System.Windows.Forms;
using HslCommunication.MQTT;

namespace HslCommunicationDemo
{
	public class TopicSaveItem
	{
		private static long newId;

		public long ID { get; set; }

		public string Topic { get; set; }

		public DateTime ReceiveDateTime { get; set; }

		public MqttSession Session { get; set; }

		public MqttClientApplicationMessage Message { get; set; }

		public long Count { get; set; }

		public DataGridViewRow ViewRow { get; set; }

		public TopicSaveItem()
		{
			ID = Interlocked.Increment(ref newId);
		}

		public void RenderRow(DataGridViewRow row)
		{
			row.Cells[0].Value = ID.ToString();
			row.Cells[1].Value = Topic;
			row.Cells[2].Value = Count.ToString();
			row.Tag = this;
			ViewRow = row;
		}

		public void RenderRow()
		{
			if (ViewRow != null)
			{
				ViewRow.Cells[0].Value = ID.ToString();
				ViewRow.Cells[1].Value = Topic;
				ViewRow.Cells[2].Value = Count.ToString();
			}
		}
	}
}
