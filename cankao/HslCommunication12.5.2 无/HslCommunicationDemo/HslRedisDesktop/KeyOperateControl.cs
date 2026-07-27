using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.Enthernet.Redis;
using HslCommunicationDemo;

namespace HslRedisDesktop
{
	public class KeyOperateControl : UserControl
	{
		private string keyType = string.Empty;

		private string keyName = string.Empty;

		private RedisClient redisClient = null;

		private IContainer components = null;

		private Label label_type;

		private TextBox textBox1;

		private Button button_rename;

		private Button button_ttl;

		private Button button_delete;

		private Button button_move;

		private Button button_reload;

		[Browsable(true)]
		[Description("获取或设置当前键值的类型表述")]
		[DefaultValue("String:")]
		public string KeyType
		{
			get
			{
				return label_type.Text;
			}
			set
			{
				label_type.Text = value;
			}
		}

		[Browsable(false)]
		public Button LoadValue => button_reload;

		public KeyOperateControl()
		{
			InitializeComponent();
		}

		private void KeyOperateControl_Load(object sender, EventArgs e)
		{
			button_rename.Click += Button_rename_Click;
			button_ttl.Click += Button_ttl_Click;
			button_delete.Click += Button_delete_Click;
			button_move.Click += Button_move_Click;
		}

		private void Button_move_Click(object sender, EventArgs e)
		{
			if (redisClient == null)
			{
				return;
			}
			using (FormInputString formInputString = new FormInputString())
			{
				formInputString.TextInfo = "等待输入新的DB块号:";
				if (formInputString.ShowDialog() != DialogResult.OK)
				{
					return;
				}
				if (int.TryParse(formInputString.InputValue, out var result))
				{
					OperateResult operateResult = redisClient.MoveKey(keyName, result);
					if (!operateResult.IsSuccess)
					{
						DemoUtils.ShowMessage("移动关键字失败！" + operateResult.Message);
					}
					else
					{
						DemoUtils.ShowMessage("移动关键字成功！");
					}
				}
				else
				{
					DemoUtils.ShowMessage("移动关键字失败，在DB块里需要输入数字！");
				}
			}
		}

		private void Button_delete_Click(object sender, EventArgs e)
		{
			if (redisClient != null && DemoUtils.ShowMessage("是否真的确认删除？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
			{
				OperateResult operateResult = redisClient.DeleteKey(keyName);
				if (!operateResult.IsSuccess)
				{
					DemoUtils.ShowMessage("删除关键字失败！" + operateResult.Message);
				}
				else
				{
					DemoUtils.ShowMessage("删除关键字成功！");
				}
			}
		}

		private void Button_ttl_Click(object sender, EventArgs e)
		{
			if (redisClient == null)
			{
				return;
			}
			using (FormInputString formInputString = new FormInputString())
			{
				formInputString.TextInfo = "等待输入新的超时时间，单位秒，-1为永久:";
				if (formInputString.ShowDialog() != DialogResult.OK)
				{
					return;
				}
				int result;
				if (formInputString.InputValue == "-1")
				{
					OperateResult operateResult = redisClient.PersistKey(keyName);
					if (!operateResult.IsSuccess)
					{
						DemoUtils.ShowMessage("设置永久失败！" + operateResult.Message);
					}
					else
					{
						DemoUtils.ShowMessage("设置永久成功！");
					}
				}
				else if (int.TryParse(formInputString.InputValue, out result))
				{
					OperateResult operateResult2 = redisClient.ExpireKey(keyName, result);
					if (!operateResult2.IsSuccess)
					{
						DemoUtils.ShowMessage("设置过期时间失败！" + operateResult2.Message);
					}
					else
					{
						DemoUtils.ShowMessage("设置过期时间成功！");
					}
				}
				else
				{
					DemoUtils.ShowMessage("过期时间输入失败，需要输入数字！");
				}
			}
		}

		private void Button_rename_Click(object sender, EventArgs e)
		{
			if (redisClient == null)
			{
				return;
			}
			using (FormInputString formInputString = new FormInputString())
			{
				formInputString.TextInfo = "等待输入新的关键字名称：";
				if (formInputString.ShowDialog() == DialogResult.OK)
				{
					OperateResult operateResult = redisClient.RenameKey(keyName, formInputString.InputValue);
					if (!operateResult.IsSuccess)
					{
						DemoUtils.ShowMessage("重命名失败！" + operateResult.Message);
					}
					else
					{
						DemoUtils.ShowMessage("重命名成功");
					}
				}
			}
		}

		public void SetRedisClient(RedisClient client, string keyName)
		{
			redisClient = client;
			textBox1.Text = keyName;
			this.keyName = keyName;
			if (client != null)
			{
				OperateResult<int> operateResult = client.OperateNumberFromServer(new string[2] { "TTL", keyName });
				if (operateResult.IsSuccess)
				{
					button_ttl.Text = "TTL:" + operateResult.Content;
				}
			}
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
			this.label_type = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.button_rename = new System.Windows.Forms.Button();
			this.button_ttl = new System.Windows.Forms.Button();
			this.button_delete = new System.Windows.Forms.Button();
			this.button_move = new System.Windows.Forms.Button();
			this.button_reload = new System.Windows.Forms.Button();
			base.SuspendLayout();
			this.label_type.AutoSize = true;
			this.label_type.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 134);
			this.label_type.Location = new System.Drawing.Point(3, 5);
			this.label_type.Name = "label_type";
			this.label_type.Size = new System.Drawing.Size(48, 17);
			this.label_type.TabIndex = 0;
			this.label_type.Text = "String:";
			this.textBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox1.BackColor = System.Drawing.Color.White;
			this.textBox1.Location = new System.Drawing.Point(57, 2);
			this.textBox1.Name = "textBox1";
			this.textBox1.ReadOnly = true;
			this.textBox1.Size = new System.Drawing.Size(394, 23);
			this.textBox1.TabIndex = 1;
			this.button_rename.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_rename.Location = new System.Drawing.Point(457, 1);
			this.button_rename.Name = "button_rename";
			this.button_rename.Size = new System.Drawing.Size(64, 25);
			this.button_rename.TabIndex = 2;
			this.button_rename.Text = "重命名";
			this.button_rename.UseVisualStyleBackColor = true;
			this.button_ttl.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_ttl.Location = new System.Drawing.Point(527, 1);
			this.button_ttl.Name = "button_ttl";
			this.button_ttl.Size = new System.Drawing.Size(82, 25);
			this.button_ttl.TabIndex = 3;
			this.button_ttl.Text = "TTL:-1";
			this.button_ttl.UseVisualStyleBackColor = true;
			this.button_delete.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_delete.Location = new System.Drawing.Point(615, 1);
			this.button_delete.Name = "button_delete";
			this.button_delete.Size = new System.Drawing.Size(55, 25);
			this.button_delete.TabIndex = 4;
			this.button_delete.Text = "删除";
			this.button_delete.UseVisualStyleBackColor = true;
			this.button_move.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_move.Location = new System.Drawing.Point(676, 1);
			this.button_move.Name = "button_move";
			this.button_move.Size = new System.Drawing.Size(55, 25);
			this.button_move.TabIndex = 5;
			this.button_move.Text = "移动";
			this.button_move.UseVisualStyleBackColor = true;
			this.button_reload.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.button_reload.Location = new System.Drawing.Point(737, 1);
			this.button_reload.Name = "button_reload";
			this.button_reload.Size = new System.Drawing.Size(55, 25);
			this.button_reload.TabIndex = 6;
			this.button_reload.Text = "重加载";
			this.button_reload.UseVisualStyleBackColor = true;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.BackColor = System.Drawing.Color.White;
			base.Controls.Add(this.button_reload);
			base.Controls.Add(this.button_move);
			base.Controls.Add(this.button_delete);
			base.Controls.Add(this.button_ttl);
			base.Controls.Add(this.button_rename);
			base.Controls.Add(this.textBox1);
			base.Controls.Add(this.label_type);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "KeyOperateControl";
			base.Size = new System.Drawing.Size(796, 28);
			base.Load += new System.EventHandler(KeyOperateControl_Load);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
