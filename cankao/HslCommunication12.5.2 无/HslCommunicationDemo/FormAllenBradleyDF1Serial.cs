using System;
using System.ComponentModel;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;
using System.Xml.Linq;
using HslCommunication;
using HslCommunication.Profinet.AllenBradley;
using HslCommunicationDemo.DemoControl;
using HslCommunicationDemo.PLC.AllenBrandly;

namespace HslCommunicationDemo
{
	public class FormAllenBradleyDF1Serial : HslFormContent
	{
		private AllenBradleyDF1Serial allenBradley = null;

		private AddressExampleControl addressExampleControl;

		private CodeExampleControl codeExampleControl;

		private IContainer components = null;

		private Panel panel1;

		private Panel panel2;

		private Button button2;

		private Button button1;

		private TextBox textBox15;

		private Label label21;

		private UserControlHead userControlHead1;

		private TextBox textBox3;

		private Label label4;

		private TextBox textBox1;

		private Label label2;

		private UserControlReadWriteDevice userControlReadWriteDevice1;

		private PipeSelectControl pipeSelectControl1;

		public FormAllenBradleyDF1Serial()
		{
			InitializeComponent();
			DemoUtils.SetPanelAnchor(panel1, panel2);
		}

		private void FormSiemens_Load(object sender, EventArgs e)
		{
			Language(Program.Language);
			addressExampleControl = new AddressExampleControl();
			addressExampleControl.SetAddressExample(Helper.GetDF1AddressExamples());
			userControlReadWriteDevice1.AddSpecialFunctionTab(addressExampleControl, false, DeviceAddressExample.GetTitle());
			codeExampleControl = new CodeExampleControl();
			userControlReadWriteDevice1.AddSpecialFunctionTab(codeExampleControl, false, CodeExampleControl.GetTitle());
			userControlReadWriteDevice1.SetEnable(false);
		}

		private void Language(int language)
		{
			if (language == 2)
			{
				Text = "AB-DF1 Read Demo";
				label21.Text = "station";
				button1.Text = "Connect";
				button2.Text = "Disconnect";
				label2.Text = "Target:";
				label4.Text = "Sender:";
			}
		}

		private void FormSiemens_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (!button1.Enabled)
			{
				button2_Click(null, EventArgs.Empty);
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			if (!byte.TryParse(textBox15.Text, out var result))
			{
				DemoUtils.ShowMessage("Station input wrong！");
				return;
			}
			if (!byte.TryParse(textBox1.Text, out var result2))
			{
				DemoUtils.ShowMessage("DstNode input wrong！");
				return;
			}
			if (!byte.TryParse(textBox3.Text, out var result3))
			{
				DemoUtils.ShowMessage("SrcNode input wrong！");
				return;
			}
			allenBradley?.Close();
			allenBradley = new AllenBradleyDF1Serial();
			allenBradley.Station = result;
			allenBradley.DstNode = result2;
			allenBradley.SrcNode = result3;
			allenBradley.LogNet = base.LogNet;
			try
			{
				pipeSelectControl1.IniPipe(allenBradley);
				OperateResult operateResult = DeviceConnectPLC(allenBradley);
				if (operateResult.IsSuccess)
				{
					button2.Enabled = true;
					button1.Enabled = false;
					userControlReadWriteDevice1.SetEnable(true);
					userControlReadWriteDevice1.SetReadWriteNet(allenBradley, "N7:0", true);
					userControlReadWriteDevice1.BatchRead.SetReadWriteNet(allenBradley, "N7:0", string.Empty);
					userControlReadWriteDevice1.MessageRead.SetReadSourceBytes((byte[] m) => allenBradley.ReadFromCoreServer(m, true, false), string.Empty, string.Empty);
					userControlReadWriteDevice1.SetDeviceVariableName(DemoUtils.PlcDeviceName);
					codeExampleControl.SetCodeText(allenBradley, "Station", "DstNode", "SrcNode");
				}
				else
				{
					DemoUtils.ShowMessage(StringResources.Language.ConnectedFailed + operateResult.Message + Environment.NewLine + "Error: " + operateResult.ErrorCode);
				}
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage(ex.Message);
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			button2.Enabled = false;
			button1.Enabled = true;
			userControlReadWriteDevice1.SetEnable(false);
			pipeSelectControl1.ExtraCloseAction(allenBradley);
			allenBradley?.Close();
		}

		public override void SaveXmlParameter(XElement element)
		{
			pipeSelectControl1.SaveXmlParameter(element);
			element.SetAttributeValue(DemoDeviceList.XmlStation, textBox15.Text);
			element.SetAttributeValue(DemoDeviceList.XmlTarget, textBox1.Text);
			element.SetAttributeValue(DemoDeviceList.XmlSender, textBox3.Text);
			userControlReadWriteDevice1.GetDataTable(element);
		}

		public override void LoadXmlParameter(XElement element)
		{
			base.LoadXmlParameter(element);
			pipeSelectControl1.LoadXmlParameter(element, SettingPipe.SerialPipe);
			textBox15.Text = element.Attribute(DemoDeviceList.XmlStation).Value;
			textBox1.Text = element.Attribute(DemoDeviceList.XmlTarget).Value;
			textBox3.Text = element.Attribute(DemoDeviceList.XmlSender).Value;
			if (userControlReadWriteDevice1.LoadDataTable(element) > 0)
			{
				userControlReadWriteDevice1.SelectTabDataTable();
			}
		}

		private void userControlHead1_SaveConnectEvent_1(object sender, EventArgs e)
		{
			userControlHead1_SaveConnectEvent(sender, e);
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
			this.panel1 = new System.Windows.Forms.Panel();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.textBox15 = new System.Windows.Forms.TextBox();
			this.label21 = new System.Windows.Forms.Label();
			this.button2 = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.panel2 = new System.Windows.Forms.Panel();
			this.userControlReadWriteDevice1 = new HslCommunicationDemo.DemoControl.UserControlReadWriteDevice();
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.pipeSelectControl1 = new HslCommunicationDemo.DemoControl.PipeSelectControl();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			base.SuspendLayout();
			this.panel1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.Add(this.pipeSelectControl1);
			this.panel1.Controls.Add(this.textBox3);
			this.panel1.Controls.Add(this.label4);
			this.panel1.Controls.Add(this.textBox1);
			this.panel1.Controls.Add(this.label2);
			this.panel1.Controls.Add(this.textBox15);
			this.panel1.Controls.Add(this.label21);
			this.panel1.Controls.Add(this.button2);
			this.panel1.Controls.Add(this.button1);
			this.panel1.Location = new System.Drawing.Point(3, 35);
			this.panel1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(998, 66);
			this.panel1.TabIndex = 0;
			this.textBox3.Location = new System.Drawing.Point(357, 35);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(37, 23);
			this.textBox3.TabIndex = 34;
			this.textBox3.Text = "1";
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(290, 38);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(68, 17);
			this.label4.TabIndex = 33;
			this.label4.Text = "来源节点：";
			this.textBox1.Location = new System.Drawing.Point(216, 35);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(37, 23);
			this.textBox1.TabIndex = 32;
			this.textBox1.Text = "2";
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(145, 38);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(68, 17);
			this.label2.TabIndex = 31;
			this.label2.Text = "目标节点：";
			this.textBox15.Location = new System.Drawing.Point(60, 35);
			this.textBox15.Name = "textBox15";
			this.textBox15.Size = new System.Drawing.Size(37, 23);
			this.textBox15.TabIndex = 7;
			this.textBox15.Text = "1";
			this.label21.AutoSize = true;
			this.label21.Location = new System.Drawing.Point(8, 38);
			this.label21.Name = "label21";
			this.label21.Size = new System.Drawing.Size(44, 17);
			this.label21.TabIndex = 6;
			this.label21.Text = "站号：";
			this.button2.Enabled = false;
			this.button2.Location = new System.Drawing.Point(904, 3);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(91, 28);
			this.button2.TabIndex = 5;
			this.button2.Text = "关闭串口";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(button2_Click);
			this.button1.Location = new System.Drawing.Point(807, 3);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(91, 28);
			this.button1.TabIndex = 4;
			this.button1.Text = "打开串口";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.panel2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.Add(this.userControlReadWriteDevice1);
			this.panel2.Location = new System.Drawing.Point(3, 104);
			this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(998, 538);
			this.panel2.TabIndex = 1;
			this.userControlReadWriteDevice1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.userControlReadWriteDevice1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlReadWriteDevice1.Location = new System.Drawing.Point(0, 0);
			this.userControlReadWriteDevice1.Name = "userControlReadWriteDevice1";
			this.userControlReadWriteDevice1.Size = new System.Drawing.Size(996, 536);
			this.userControlReadWriteDevice1.TabIndex = 0;
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.ProtocolInfo = "DF1";
			this.userControlHead1.Size = new System.Drawing.Size(1005, 32);
			this.userControlHead1.SupportListVisiable = true;
			this.userControlHead1.TabIndex = 2;
			this.userControlHead1.SaveConnectEvent += new System.EventHandler<System.EventArgs>(userControlHead1_SaveConnectEvent_1);
			this.pipeSelectControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.pipeSelectControl1.Location = new System.Drawing.Point(3, 2);
			this.pipeSelectControl1.Name = "pipeSelectControl1";
			this.pipeSelectControl1.SerialBaudRate = "9600";
			this.pipeSelectControl1.SerialDataBits = "8";
			this.pipeSelectControl1.SerialParity = System.IO.Ports.Parity.None;
			this.pipeSelectControl1.SerialStopBits = "1";
			this.pipeSelectControl1.SettingPipe = HslCommunicationDemo.DemoControl.SettingPipe.SerialPipe;
			this.pipeSelectControl1.Size = new System.Drawing.Size(790, 28);
			this.pipeSelectControl1.TabIndex = 35;
			this.pipeSelectControl1.TcpPortText = "44818";
			this.pipeSelectControl1.UdpPortText = "44818";
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1005, 646);
			base.Controls.Add(this.userControlHead1);
			base.Controls.Add(this.panel2);
			base.Controls.Add(this.panel1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormAllenBradleyDF1Serial";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Allen-bradley SF1 ";
			base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormSiemens_FormClosing);
			base.Load += new System.EventHandler(FormSiemens_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			this.panel2.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
}
