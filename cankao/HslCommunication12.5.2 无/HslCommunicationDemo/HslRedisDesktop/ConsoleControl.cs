using System;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.Enthernet.Redis;

namespace HslRedisDesktop
{
	public class ConsoleControl : UserControl
	{
		private RedisSettings redisSettings;

		private IContainer components = null;

		private TextBox textBox1;

		private TextBox textBox2;

		public ConsoleControl()
		{
			InitializeComponent();
		}

		private void textBox2_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode != Keys.Return || redisSettings == null || string.IsNullOrEmpty(textBox2.Text) || redisSettings == null)
			{
				return;
			}
			textBox1.AppendText(">> " + textBox2.Text + Environment.NewLine);
			byte[] send = RedisHelper.PackStringCommand(textBox2.Text.Split(' '));
			OperateResult<byte[]> operateResult = redisSettings.Redis.ReadFromCoreServer(send);
			if (!operateResult.IsSuccess)
			{
				textBox1.AppendText(">> Error:" + operateResult.Message + Environment.NewLine);
			}
			else if (operateResult.IsSuccess)
			{
				if (operateResult.Content[0] == 43)
				{
					textBox1.AppendText(">> " + Encoding.UTF8.GetString(operateResult.Content).TrimEnd('\r', '\n') + Environment.NewLine);
				}
				else if (operateResult.Content[0] == 58)
				{
					OperateResult<long> longNumberFromCommandLine = RedisHelper.GetLongNumberFromCommandLine(operateResult.Content);
					if (longNumberFromCommandLine.IsSuccess)
					{
						textBox1.AppendText(">> Integer(" + longNumberFromCommandLine.Content + ")" + Environment.NewLine);
					}
					else
					{
						textBox1.AppendText(">> Error:" + longNumberFromCommandLine.Message + Environment.NewLine);
					}
				}
				else if (operateResult.Content[0] == 36)
				{
					OperateResult<string[]> stringFromCommandLine = RedisHelper.GetStringFromCommandLine(operateResult.Content);
					if (stringFromCommandLine.IsSuccess)
					{
						textBox1.AppendText(">> " + stringFromCommandLine.Content[0] + Environment.NewLine);
					}
					else
					{
						textBox1.AppendText(">> Error:" + stringFromCommandLine.Message + Environment.NewLine);
					}
				}
				else if (operateResult.Content[0] == 42)
				{
					OperateResult<string[]> stringsFromCommandLine = RedisHelper.GetStringsFromCommandLine(operateResult.Content);
					if (stringsFromCommandLine.IsSuccess)
					{
						for (int i = 0; i < stringsFromCommandLine.Content.Length; i++)
						{
							textBox1.AppendText(">>(" + i + ") " + stringsFromCommandLine.Content[i] + Environment.NewLine);
						}
					}
					else
					{
						textBox1.AppendText(">> Error:" + stringsFromCommandLine.Message + Environment.NewLine);
					}
				}
				else
				{
					textBox1.AppendText(">> " + Encoding.UTF8.GetString(operateResult.Content) + Environment.NewLine);
				}
			}
			else
			{
				textBox1.AppendText(">> Faild: " + operateResult.Message + Environment.NewLine);
			}
		}

		public void SetRedis(RedisSettings settings)
		{
			redisSettings = settings;
		}

		private void ConsoleControl_Load(object sender, EventArgs e)
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
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.textBox2 = new System.Windows.Forms.TextBox();
			base.SuspendLayout();
			this.textBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.textBox1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.textBox1.ForeColor = System.Drawing.Color.WhiteSmoke;
			this.textBox1.Location = new System.Drawing.Point(3, 36);
			this.textBox1.Multiline = true;
			this.textBox1.Name = "textBox1";
			this.textBox1.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox1.Size = new System.Drawing.Size(545, 466);
			this.textBox1.TabIndex = 0;
			this.textBox2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox2.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.textBox2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.textBox2.ForeColor = System.Drawing.Color.WhiteSmoke;
			this.textBox2.Location = new System.Drawing.Point(3, 4);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(545, 23);
			this.textBox2.TabIndex = 1;
			this.textBox2.KeyDown += new System.Windows.Forms.KeyEventHandler(textBox2_KeyDown);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.BackColor = System.Drawing.Color.FromArgb(36, 36, 36);
			base.Controls.Add(this.textBox2);
			base.Controls.Add(this.textBox1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "ConsoleControl";
			base.Size = new System.Drawing.Size(551, 505);
			base.Load += new System.EventHandler(ConsoleControl_Load);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
