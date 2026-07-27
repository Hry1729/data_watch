using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication;
using HslCommunicationDemo;

namespace HslRedisDesktop
{
	public class StringValueControl : UserControl
	{
		private string stringKeyName = string.Empty;

		private RedisSettings redisSettings = null;

		private IContainer components = null;

		private KeyOperateControl keyOperateControl1;

		private ValueControl valueControl1;

		private Button button1;

		private Button button5;

		private TextBox textBox1;

		public StringValueControl()
		{
			InitializeComponent();
		}

		private void StringValueControl_Load(object sender, EventArgs e)
		{
			keyOperateControl1.LoadValue.Click += LoadValue_Click;
		}

		public void SetNewKey(RedisSettings redisSettings, string key)
		{
			this.redisSettings = redisSettings;
			stringKeyName = key;
			keyOperateControl1.SetRedisClient(redisSettings.Redis, key);
			RefreshKey();
		}

		private void LoadValue_Click(object sender, EventArgs e)
		{
			RefreshKey();
		}

		private void RefreshKey()
		{
			if (redisSettings == null)
			{
				DemoUtils.ShowMessage("当前的连接为空，无法读取！");
				return;
			}
			if (redisSettings.Redis == null)
			{
				DemoUtils.ShowMessage("当前的连接为空，无法读取！");
				return;
			}
			OperateResult<string> operateResult = redisSettings.Redis.ReadKey(stringKeyName);
			if (!operateResult.IsSuccess)
			{
				DemoUtils.ShowMessage("读取失败：" + operateResult.Message);
			}
			else
			{
				valueControl1.SetValue(operateResult.Content);
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			using (StringValueEditForm stringValueEditForm = new StringValueEditForm(redisSettings, stringKeyName, valueControl1.KeySourceValue()))
			{
				stringValueEditForm.ShowDialog();
			}
		}

		private void button5_Click(object sender, EventArgs e)
		{
			DemoUtils.ShowMessage("当前版本暂时不支持！");
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
			this.valueControl1 = new HslRedisDesktop.ValueControl();
			this.keyOperateControl1 = new HslRedisDesktop.KeyOperateControl();
			this.button1 = new System.Windows.Forms.Button();
			this.button5 = new System.Windows.Forms.Button();
			this.textBox1 = new System.Windows.Forms.TextBox();
			base.SuspendLayout();
			this.valueControl1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.valueControl1.BackColor = System.Drawing.Color.White;
			this.valueControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.valueControl1.Location = new System.Drawing.Point(3, 37);
			this.valueControl1.Name = "valueControl1";
			this.valueControl1.Size = new System.Drawing.Size(756, 552);
			this.valueControl1.TabIndex = 1;
			this.keyOperateControl1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.keyOperateControl1.BackColor = System.Drawing.Color.White;
			this.keyOperateControl1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.keyOperateControl1.Location = new System.Drawing.Point(3, 3);
			this.keyOperateControl1.Name = "keyOperateControl1";
			this.keyOperateControl1.Size = new System.Drawing.Size(865, 28);
			this.keyOperateControl1.TabIndex = 0;
			this.button1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button1.Location = new System.Drawing.Point(765, 59);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(100, 25);
			this.button1.TabIndex = 2;
			this.button1.Text = "编辑内容";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.button5.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button5.Location = new System.Drawing.Point(765, 164);
			this.button5.Name = "button5";
			this.button5.Size = new System.Drawing.Size(100, 25);
			this.button5.TabIndex = 8;
			this.button5.Text = "定时刷新";
			this.button5.UseVisualStyleBackColor = true;
			this.button5.Click += new System.EventHandler(button5_Click);
			this.textBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.textBox1.Location = new System.Drawing.Point(765, 135);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(100, 23);
			this.textBox1.TabIndex = 7;
			this.textBox1.Text = "1000";
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.BackColor = System.Drawing.Color.White;
			base.Controls.Add(this.button5);
			base.Controls.Add(this.textBox1);
			base.Controls.Add(this.button1);
			base.Controls.Add(this.valueControl1);
			base.Controls.Add(this.keyOperateControl1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "StringValueControl";
			base.Size = new System.Drawing.Size(869, 592);
			base.Load += new System.EventHandler(StringValueControl_Load);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
