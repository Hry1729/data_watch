using System;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.MQTT;
using Newtonsoft.Json.Linq;

namespace HslCommunicationDemo.MQTT
{
	public class FormMqttSubscribe : Form
	{
		private MqttClient mqttClient = null;

		private long receiveCount = 0L;

		private IContainer components = null;

		private TextBox textBox5;

		private Label label7;

		private Label label10;

		private Panel panel3;

		private RadioButton radioButton_binary;

		private RadioButton radioButton_json;

		private RadioButton radioButton_text;

		private RadioButton radioButton_xml;

		private RadioButton radioButton2;

		private RadioButton radioButton1;

		private TextBox textBox8;

		private Label label12;

		private Button button4;

		private Button button8;

		private Button button7;

		private CheckBox checkBox_long_message_hide;

		public FormMqttSubscribe(MqttClient mqttClient)
		{
			InitializeComponent();
			this.mqttClient = mqttClient;
		}

		private void FormMqttSubscribe_Load(object sender, EventArgs e)
		{
			button8.Enabled = false;
		}

		private void button7_Click(object sender, EventArgs e)
		{
			OperateResult operateResult = mqttClient.SubscribeMessage(new string[1] { textBox5.Text });
			if (!operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("SubscribeMessage Failed:" + operateResult.Message);
				return;
			}
			SubscribeTopic subscribeTopic = mqttClient.GetSubscribeTopic(textBox5.Text);
			if (subscribeTopic != null)
			{
				subscribeTopic.OnMqttMessageReceived += FormMqttSubscribe_OnMqttMessageReceived;
			}
			button7.Enabled = false;
			button8.Enabled = true;
		}

		private void FormMqttSubscribe_OnMqttMessageReceived(MqttClient client, MqttApplicationMessage message)
		{
			try
			{
				string topic = message.Topic;
				byte[] payload = message.Payload;
				Invoke((Action)delegate
				{
					receiveCount++;
					label10.Text = "Receive Count: " + receiveCount;
					string text = string.Empty;
					if (radioButton_binary.Checked)
					{
						text = payload.ToHexString(' ');
					}
					else if (radioButton_text.Checked)
					{
						text = Encoding.UTF8.GetString(payload);
					}
					else if (radioButton_xml.Checked)
					{
						try
						{
							text = XElement.Parse(Encoding.UTF8.GetString(payload)).ToString();
						}
						catch
						{
							text = Encoding.UTF8.GetString(payload);
						}
					}
					else if (radioButton_json.Checked)
					{
						try
						{
							text = JObject.Parse(Encoding.UTF8.GetString(payload)).ToString();
						}
						catch
						{
							text = Encoding.UTF8.GetString(payload);
						}
					}
					if (checkBox_long_message_hide.Checked && text != null && text.Length > 200)
					{
						text = text.Substring(200);
					}
					if (radioButton2.Checked)
					{
						textBox8.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " Topic[" + topic + "]: " + text + Environment.NewLine);
					}
					else if (radioButton1.Checked)
					{
						textBox8.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " Topic[" + topic + "]: " + text;
					}
				});
			}
			catch
			{
			}
		}

		private void button8_Click(object sender, EventArgs e)
		{
			SubscribeTopic subscribeTopic = mqttClient.GetSubscribeTopic(textBox5.Text);
			if (subscribeTopic == null)
			{
				return;
			}
			subscribeTopic.OnMqttMessageReceived -= FormMqttSubscribe_OnMqttMessageReceived;
			if (subscribeTopic.RemoveSubscribeTick() <= 0)
			{
				OperateResult operateResult = mqttClient.UnSubscribeMessage(textBox5.Text);
				if (!operateResult.IsSuccess)
				{
					DemoUtils.ShowMessage("UnSubscribeMessage Failed:" + operateResult.Message);
					return;
				}
				button7.Enabled = true;
				button8.Enabled = false;
			}
			else
			{
				button7.Enabled = true;
				button8.Enabled = false;
			}
		}

		private void button4_Click(object sender, EventArgs e)
		{
			textBox8.Clear();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && components != null)
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		private void InitializeComponent()
		{
			this.textBox5 = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.label10 = new System.Windows.Forms.Label();
			this.panel3 = new System.Windows.Forms.Panel();
			this.radioButton_binary = new System.Windows.Forms.RadioButton();
			this.radioButton_json = new System.Windows.Forms.RadioButton();
			this.radioButton_text = new System.Windows.Forms.RadioButton();
			this.radioButton_xml = new System.Windows.Forms.RadioButton();
			this.radioButton2 = new System.Windows.Forms.RadioButton();
			this.radioButton1 = new System.Windows.Forms.RadioButton();
			this.textBox8 = new System.Windows.Forms.TextBox();
			this.label12 = new System.Windows.Forms.Label();
			this.button4 = new System.Windows.Forms.Button();
			this.button8 = new System.Windows.Forms.Button();
			this.button7 = new System.Windows.Forms.Button();
			this.checkBox_long_message_hide = new System.Windows.Forms.CheckBox();
			this.panel3.SuspendLayout();
			base.SuspendLayout();
			this.textBox5.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox5.Location = new System.Drawing.Point(70, 19);
			this.textBox5.Name = "textBox5";
			this.textBox5.Size = new System.Drawing.Size(559, 23);
			this.textBox5.TabIndex = 11;
			this.textBox5.Text = "A";
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(12, 23);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(52, 17);
			this.label7.TabIndex = 10;
			this.label7.Text = "Topic：";
			this.label10.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(69, 430);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(93, 17);
			this.label10.TabIndex = 35;
			this.label10.Text = "Receive Count:";
			this.panel3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.panel3.Controls.Add(this.radioButton_binary);
			this.panel3.Controls.Add(this.radioButton_json);
			this.panel3.Controls.Add(this.radioButton_text);
			this.panel3.Controls.Add(this.radioButton_xml);
			this.panel3.Location = new System.Drawing.Point(538, 62);
			this.panel3.Name = "panel3";
			this.panel3.Size = new System.Drawing.Size(246, 28);
			this.panel3.TabIndex = 34;
			this.radioButton_binary.AutoSize = true;
			this.radioButton_binary.Location = new System.Drawing.Point(3, 3);
			this.radioButton_binary.Name = "radioButton_binary";
			this.radioButton_binary.Size = new System.Drawing.Size(62, 21);
			this.radioButton_binary.TabIndex = 29;
			this.radioButton_binary.Text = "Binary";
			this.radioButton_binary.UseVisualStyleBackColor = true;
			this.radioButton_json.AutoSize = true;
			this.radioButton_json.Location = new System.Drawing.Point(175, 3);
			this.radioButton_json.Name = "radioButton_json";
			this.radioButton_json.Size = new System.Drawing.Size(52, 21);
			this.radioButton_json.TabIndex = 28;
			this.radioButton_json.Text = "Json";
			this.radioButton_json.UseVisualStyleBackColor = true;
			this.radioButton_text.AutoSize = true;
			this.radioButton_text.Checked = true;
			this.radioButton_text.Location = new System.Drawing.Point(65, 3);
			this.radioButton_text.Name = "radioButton_text";
			this.radioButton_text.Size = new System.Drawing.Size(50, 21);
			this.radioButton_text.TabIndex = 26;
			this.radioButton_text.TabStop = true;
			this.radioButton_text.Text = "Text";
			this.radioButton_text.UseVisualStyleBackColor = true;
			this.radioButton_xml.AutoSize = true;
			this.radioButton_xml.Location = new System.Drawing.Point(121, 3);
			this.radioButton_xml.Name = "radioButton_xml";
			this.radioButton_xml.Size = new System.Drawing.Size(48, 21);
			this.radioButton_xml.TabIndex = 27;
			this.radioButton_xml.Text = "Xml";
			this.radioButton_xml.UseVisualStyleBackColor = true;
			this.radioButton2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.radioButton2.AutoSize = true;
			this.radioButton2.Checked = true;
			this.radioButton2.Location = new System.Drawing.Point(458, 56);
			this.radioButton2.Name = "radioButton2";
			this.radioButton2.Size = new System.Drawing.Size(74, 21);
			this.radioButton2.TabIndex = 33;
			this.radioButton2.TabStop = true;
			this.radioButton2.Text = "追加显示";
			this.radioButton2.UseVisualStyleBackColor = true;
			this.radioButton1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.radioButton1.AutoSize = true;
			this.radioButton1.Location = new System.Drawing.Point(458, 74);
			this.radioButton1.Name = "radioButton1";
			this.radioButton1.Size = new System.Drawing.Size(74, 21);
			this.radioButton1.TabIndex = 32;
			this.radioButton1.Text = "覆盖显示";
			this.radioButton1.UseVisualStyleBackColor = true;
			this.textBox8.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox8.Location = new System.Drawing.Point(72, 96);
			this.textBox8.Multiline = true;
			this.textBox8.Name = "textBox8";
			this.textBox8.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox8.Size = new System.Drawing.Size(783, 331);
			this.textBox8.TabIndex = 30;
			this.label12.AutoSize = true;
			this.label12.Location = new System.Drawing.Point(12, 98);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(64, 17);
			this.label12.TabIndex = 31;
			this.label12.Text = "Receive：";
			this.button4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button4.Location = new System.Drawing.Point(784, 61);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(71, 28);
			this.button4.TabIndex = 29;
			this.button4.Text = "清空";
			this.button4.UseVisualStyleBackColor = true;
			this.button4.Click += new System.EventHandler(button4_Click);
			this.button8.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button8.Location = new System.Drawing.Point(750, 17);
			this.button8.Name = "button8";
			this.button8.Size = new System.Drawing.Size(104, 28);
			this.button8.TabIndex = 37;
			this.button8.Text = "取消订阅";
			this.button8.UseVisualStyleBackColor = true;
			this.button8.Click += new System.EventHandler(button8_Click);
			this.button7.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button7.Location = new System.Drawing.Point(646, 17);
			this.button7.Name = "button7";
			this.button7.Size = new System.Drawing.Size(98, 28);
			this.button7.TabIndex = 36;
			this.button7.Text = "订阅";
			this.button7.UseVisualStyleBackColor = true;
			this.button7.Click += new System.EventHandler(button7_Click);
			this.checkBox_long_message_hide.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.checkBox_long_message_hide.AutoSize = true;
			this.checkBox_long_message_hide.Checked = true;
			this.checkBox_long_message_hide.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox_long_message_hide.Location = new System.Drawing.Point(756, 433);
			this.checkBox_long_message_hide.Name = "checkBox_long_message_hide";
			this.checkBox_long_message_hide.Size = new System.Drawing.Size(99, 21);
			this.checkBox_long_message_hide.TabIndex = 38;
			this.checkBox_long_message_hide.Text = "超长消息简略";
			this.checkBox_long_message_hide.UseVisualStyleBackColor = true;
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			base.ClientSize = new System.Drawing.Size(867, 456);
			base.Controls.Add(this.checkBox_long_message_hide);
			base.Controls.Add(this.button8);
			base.Controls.Add(this.button7);
			base.Controls.Add(this.label10);
			base.Controls.Add(this.panel3);
			base.Controls.Add(this.radioButton2);
			base.Controls.Add(this.radioButton1);
			base.Controls.Add(this.textBox8);
			base.Controls.Add(this.label12);
			base.Controls.Add(this.button4);
			base.Controls.Add(this.textBox5);
			base.Controls.Add(this.label7);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(4);
			base.Name = "FormMqttSubscribe";
			this.Text = "FormMqttSubscribe";
			base.Load += new System.EventHandler(FormMqttSubscribe_Load);
			this.panel3.ResumeLayout(false);
			this.panel3.PerformLayout();
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
