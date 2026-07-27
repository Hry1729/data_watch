using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.Robot.EFORT;

namespace HslCommunicationDemo.Robot
{
	public class FormEfortPrevious : FormEfort
	{
		private ER7BC10Previous efortRobot;

		private IContainer components = null;

		public FormEfortPrevious()
		{
			InitializeComponent();
			userControlHead1.ProtocolInfo = "Previous";
		}

		protected override void button1_Click(object sender, EventArgs e)
		{
			try
			{
				efortRobot = new ER7BC10Previous(textBox1.Text, int.Parse(textBox2.Text));
				efortRobot.ConnectTimeOut = 2000;
				OperateResult operateResult = efortRobot.ConnectServer();
				if (operateResult.IsSuccess)
				{
					DemoUtils.ShowMessage(StringResources.Language.ConnectedSuccess);
					button1.Enabled = false;
					button2.Enabled = true;
					panel2.Enabled = true;
					codeExampleControl.SetCodeText("robot", efortRobot);
				}
				else
				{
					DemoUtils.ShowMessage(StringResources.Language.ConnectedFailed + operateResult.ToMessageShowString());
				}
			}
			catch (Exception ex)
			{
				SoftBasic.ShowExceptionMessage(ex);
			}
		}

		protected override void button2_Click(object sender, EventArgs e)
		{
			efortRobot?.ConnectClose();
			base.button2_Click(sender, e);
		}

		protected override OperateResult<EfortData> ReadFromRobot()
		{
			return efortRobot.ReadEfortData();
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
			base.SuspendLayout();
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(982, 648);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormEfortPrevious";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "埃夫特机器人(旧版)";
			base.ResumeLayout(false);
		}
	}
}
