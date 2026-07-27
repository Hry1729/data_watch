using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.MQTT;
using HslCommunicationDemo.Properties;

namespace HslCommunicationDemo
{
	public class FormLogin : Form
	{
		private MqttSyncClient mqttSyncClient;

		private IContainer components = null;

		private Label label1;

		private TextBox textBox1;

		private TextBox textBox2;

		private Label label2;

		private Button button1;

		private Label label3;

		private LinkLabel linkLabel1;

		public FormLogin()
		{
			InitializeComponent();
			base.Icon = DemoUtils.ConvertToIcon(Resources.HslCommunication);
		}

		private void FormLogin_Load(object sender, EventArgs e)
		{
			string loginName = Settings1.Default.LoginName;
			textBox1.Text = loginName;
			if (Program.Language == 1)
			{
				label3.Text = Program.SystemName;
				return;
			}
			label1.Text = "UserName:";
			label2.Text = "Password:";
			label3.Text = "Industrial equipment debugging system";
			button1.Text = "Login";
		}

		private void button1_Click(object sender, EventArgs e)
		{
			mqttSyncClient = new MqttSyncClient(new MqttConnectionOptions
			{
				IpAddress = "118.195.180.167",
				Port = 7790,
				UseRSAProvider = true
			});
			mqttSyncClient.ConnectionOptions.Credentials = new MqttCredential
			{
				UserName = textBox1.Text,
				Password = textBox2.Text
			};
			OperateResult operateResult = mqttSyncClient.ConnectServer();
			if (!operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("连接失败：" + operateResult.Message);
				return;
			}
			OperateResult<List<byte[]>> operateResult2 = mqttSyncClient.ReadRpc<List<byte[]>>("GetAllCertificate", "");
			if (!operateResult2.IsSuccess)
			{
				DemoUtils.ShowMessage("读取证书列表失败: " + operateResult2.Message);
				return;
			}
			bool flag = true;
			Settings1.Default.LoginName = textBox1.Text;
			Settings1.Default.Save();
			base.DialogResult = DialogResult.OK;
		}

		public MqttSyncClient GetMqttSyncClient()
		{
			return mqttSyncClient;
		}

		private void FormLogin_Shown(object sender, EventArgs e)
		{
			textBox2.Focus();
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
			this.label1 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.button1 = new System.Windows.Forms.Button();
			this.label3 = new System.Windows.Forms.Label();
			this.linkLabel1 = new System.Windows.Forms.LinkLabel();
			base.SuspendLayout();
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(46, 73);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(56, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "用户名：";
			this.textBox1.Location = new System.Drawing.Point(122, 69);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(237, 23);
			this.textBox1.TabIndex = 1;
			this.textBox2.Location = new System.Drawing.Point(122, 105);
			this.textBox2.Name = "textBox2";
			this.textBox2.PasswordChar = '*';
			this.textBox2.Size = new System.Drawing.Size(237, 23);
			this.textBox2.TabIndex = 3;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(46, 109);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(44, 17);
			this.label2.TabIndex = 2;
			this.label2.Text = "密码：";
			this.button1.Location = new System.Drawing.Point(140, 154);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(140, 36);
			this.button1.TabIndex = 4;
			this.button1.Text = "登录";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.label3.Font = new System.Drawing.Font("微软雅黑", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label3.Location = new System.Drawing.Point(12, 9);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(402, 57);
			this.label3.TabIndex = 5;
			this.label3.Text = "欢迎企业用户登录HSL平台";
			this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.linkLabel1.AutoSize = true;
			this.linkLabel1.Location = new System.Drawing.Point(353, 195);
			this.linkLabel1.Name = "linkLabel1";
			this.linkLabel1.Size = new System.Drawing.Size(68, 17);
			this.linkLabel1.TabIndex = 6;
			this.linkLabel1.TabStop = true;
			this.linkLabel1.Text = "忘记密码？";
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.ClientSize = new System.Drawing.Size(426, 220);
			base.Controls.Add(this.linkLabel1);
			base.Controls.Add(this.label3);
			base.Controls.Add(this.button1);
			base.Controls.Add(this.textBox2);
			base.Controls.Add(this.label2);
			base.Controls.Add(this.textBox1);
			base.Controls.Add(this.label1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.MaximumSize = new System.Drawing.Size(442, 259);
			this.MinimumSize = new System.Drawing.Size(442, 259);
			base.Name = "FormLogin";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Login";
			base.Load += new System.EventHandler(FormLogin_Load);
			base.Shown += new System.EventHandler(FormLogin_Shown);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
