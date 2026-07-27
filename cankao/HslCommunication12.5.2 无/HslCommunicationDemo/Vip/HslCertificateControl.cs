using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.Core.Security;
using HslCommunication.MQTT;

namespace HslCommunicationDemo.Vip
{
	public class HslCertificateControl : UserControl
	{
		private CertificateItem certificateItem;

		private MqttSyncClient rpc = null;

		private bool editMode = false;

		private IContainer components = null;

		private Label label14;

		private Label label13;

		private Label label12;

		private Label label3;

		private Button button_create;

		private TextBox textBox_hours;

		private Label label_hours;

		private TextBox textBox_createDate;

		private Label label11;

		private TextBox textBox_Descriptions;

		private Label label10;

		private TextBox textBox_Unique;

		private Label label9;

		private TextBox textBox_KeyWord;

		private Label label8;

		private TextBox textBox_NotAfter;

		private Label label7;

		private TextBox textBox_NotBefore;

		private Label label6;

		private TextBox textBox_To;

		private Label label5;

		private TextBox textBox_From;

		private Label label2;

		private Panel panel1;

		private RadioButton radioButton_java;

		private RadioButton radioButton_dotnet;

		private LinkLabel linkLabel1;

		public HslCertificateControl()
		{
			InitializeComponent();
			base.SizeChanged += HslCertificateControl_SizeChanged;
		}

		private void HslCertificateControl_SizeChanged(object sender, EventArgs e)
		{
			if (base.Width > 200 && base.Height > 200)
			{
				button_create.Location = new Point(base.Width / 2 - button_create.Width / 2, base.Height - 48);
			}
		}

		private void HslCertificateControl_Load(object sender, EventArgs e)
		{
			textBox_createDate.Text = DateTime.Now.ToString("yyyy-MM-dd");
			textBox_NotBefore.Text = DateTime.Now.ToString("yyyy-MM-dd");
			textBox_NotAfter.Text = DateTime.Now.AddYears(1).ToString("yyyy-MM-dd");
			SetEditMode(false);
			if (Program.Language == 2)
			{
				linkLabel1.Text = "Edit Mode";
			}
		}

		public void SetCompanyName(string company)
		{
			textBox_From.Text = company;
		}

		public void RenderHslCertificate(CertificateItem cert)
		{
			certificateItem = cert;
			HslCertificate certificate = cert.Certificate;
			if (certificate == null)
			{
				textBox_To.Text = string.Empty;
				textBox_Unique.Text = string.Empty;
				textBox_Descriptions.Text = string.Empty;
				return;
			}
			textBox_From.Text = certificate.From;
			textBox_To.Text = certificate.To;
			textBox_NotBefore.Text = certificate.NotBefore.ToString("yyyy-MM-dd");
			textBox_NotAfter.Text = certificate.NotAfter.ToString("yyyy-MM-dd");
			textBox_createDate.Text = certificate.CreateTime.ToString("yyyy-MM-dd");
			textBox_hours.Text = certificate.EffectiveHours.ToString();
			textBox_KeyWord.Text = certificate.KeyWord;
			textBox_Unique.Text = certificate.UniqueID;
			StringBuilder stringBuilder = new StringBuilder();
			foreach (KeyValuePair<string, string> description in certificate.Descriptions)
			{
				stringBuilder.AppendLine(description.Key + ":" + description.Value);
				if (description.Key.Equals("语言", StringComparison.OrdinalIgnoreCase))
				{
					if (description.Value.Equals("DotNet", StringComparison.OrdinalIgnoreCase))
					{
						radioButton_dotnet.Checked = true;
					}
					else
					{
						radioButton_java.Checked = true;
					}
				}
			}
			textBox_Descriptions.Text = stringBuilder.ToString();
			if (editMode)
			{
				SetEditMode(false);
			}
		}

		public void SetReadOnly(bool readOnly)
		{
			textBox_To.ReadOnly = readOnly;
			textBox_NotBefore.ReadOnly = readOnly;
			textBox_NotAfter.ReadOnly = readOnly;
			textBox_hours.ReadOnly = readOnly;
			textBox_Unique.ReadOnly = readOnly;
			textBox_Descriptions.ReadOnly = readOnly;
			radioButton_dotnet.Enabled = !readOnly;
			radioButton_java.Enabled = !readOnly;
		}

		public void SetRpcClient(MqttSyncClient rpc)
		{
			this.rpc = rpc;
		}

		public void SetEditMode(bool edit)
		{
			SetReadOnly(!edit);
			editMode = edit;
			if (edit)
			{
				button_create.Text = ((Program.Language == 1) ? "创建证书" : "Create Certificate");
			}
			else
			{
				button_create.Text = ((Program.Language == 1) ? "导出密钥" : "Export Key");
			}
		}

		private void button_create_Click(object sender, EventArgs e)
		{
			if (!editMode)
			{
				if (certificateItem != null)
				{
					Clipboard.SetText(Convert.ToBase64String(certificateItem.CertBinary));
					if (Program.Language == 1)
					{
						DemoUtils.ShowMessage("证书密钥已经复制到剪贴板\r\n\r\n" + Convert.ToBase64String(certificateItem.CertBinary));
					}
					else
					{
						DemoUtils.ShowMessage("Certificate Key has been copied to clipboard: \r\n\r\n" + Convert.ToBase64String(certificateItem.CertBinary));
					}
				}
				else if (Program.Language == 1)
				{
					DemoUtils.ShowMessage("当前没有任何证书信息！");
				}
				else
				{
					DemoUtils.ShowMessage("No Certificate Information Exist!");
				}
				return;
			}
			if (string.IsNullOrEmpty(textBox_To.Text))
			{
				if (Program.Language == 1)
				{
					DemoUtils.ShowMessage("请输入证书的使用者/持有者信息！");
				}
				else
				{
					DemoUtils.ShowMessage("Please Input the User Name of Certificate!");
				}
				return;
			}
			if (!DateTime.TryParse(textBox_NotBefore.Text, out var _))
			{
				if (Program.Language == 1)
				{
					DemoUtils.ShowMessage("请输入正确的证书生效时间！");
				}
				else
				{
					DemoUtils.ShowMessage("Please Input the NotBefore DateTime of Certificate!");
				}
				return;
			}
			if (!DateTime.TryParse(textBox_NotAfter.Text, out var _))
			{
				if (Program.Language == 1)
				{
					DemoUtils.ShowMessage("请输入正确的证书失效时间！");
				}
				else
				{
					DemoUtils.ShowMessage("Please Input the NotAfter DateTime of Certificate!");
				}
				return;
			}
			if (!int.TryParse(textBox_hours.Text, out var _))
			{
				if (Program.Language == 1)
				{
					DemoUtils.ShowMessage("请输入正确的有效小时数！");
				}
				else
				{
					DemoUtils.ShowMessage("Please Input the Hours value!");
				}
				return;
			}
			OperateResult<string> operateResult = rpc.ReadRpc<string>(radioButton_dotnet.Checked ? "CreateDotNetCertificate" : "CreateJavaCertificate", new
			{
				userName = textBox_To.Text,
				startDate = textBox_NotBefore.Text,
				finishDate = textBox_NotAfter.Text,
				hours = int.Parse(textBox_hours.Text),
				unique = textBox_Unique.Text,
				keyValues = textBox_Descriptions.Lines
			});
			if (operateResult.IsSuccess)
			{
				Clipboard.SetText(operateResult.Content);
				if (Program.Language == 1)
				{
					DemoUtils.ShowMessage("创建证书成功，已经复制到剪贴板\r\n\r\n" + operateResult.Content);
				}
				else
				{
					DemoUtils.ShowMessage("Create Certificate Success! Ctrl+V to Get String: \r\n\r\n" + operateResult.Content);
				}
			}
			else if (Program.Language == 1)
			{
				DemoUtils.ShowMessage("创建证书失败: " + operateResult.Message);
			}
			else
			{
				DemoUtils.ShowMessage("Create Certificate Failed: " + operateResult.Message);
			}
		}

		private void linkLabel1_Click(object sender, EventArgs e)
		{
			SetEditMode(true);
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
			this.label14 = new System.Windows.Forms.Label();
			this.label13 = new System.Windows.Forms.Label();
			this.label12 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.button_create = new System.Windows.Forms.Button();
			this.textBox_hours = new System.Windows.Forms.TextBox();
			this.label_hours = new System.Windows.Forms.Label();
			this.textBox_createDate = new System.Windows.Forms.TextBox();
			this.label11 = new System.Windows.Forms.Label();
			this.textBox_Descriptions = new System.Windows.Forms.TextBox();
			this.label10 = new System.Windows.Forms.Label();
			this.textBox_Unique = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.textBox_KeyWord = new System.Windows.Forms.TextBox();
			this.label8 = new System.Windows.Forms.Label();
			this.textBox_NotAfter = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.textBox_NotBefore = new System.Windows.Forms.TextBox();
			this.label6 = new System.Windows.Forms.Label();
			this.textBox_To = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.textBox_From = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.panel1 = new System.Windows.Forms.Panel();
			this.radioButton_java = new System.Windows.Forms.RadioButton();
			this.radioButton_dotnet = new System.Windows.Forms.RadioButton();
			this.linkLabel1 = new System.Windows.Forms.LinkLabel();
			this.panel1.SuspendLayout();
			base.SuspendLayout();
			this.label14.AutoSize = true;
			this.label14.ForeColor = System.Drawing.Color.Gray;
			this.label14.Location = new System.Drawing.Point(430, 252);
			this.label14.Name = "label14";
			this.label14.Size = new System.Drawing.Size(281, 17);
			this.label14.TabIndex = 49;
			this.label14.Text = "(一般为空，当需要绑定电脑运行时设置电脑唯一ID)";
			this.label13.AutoSize = true;
			this.label13.ForeColor = System.Drawing.Color.Gray;
			this.label13.Location = new System.Drawing.Point(430, 145);
			this.label13.Name = "label13";
			this.label13.Size = new System.Drawing.Size(210, 17);
			this.label13.TabIndex = 48;
			this.label13.Text = "(在此时间内允许激活，不能超过10年)";
			this.label12.AutoSize = true;
			this.label12.ForeColor = System.Drawing.Color.Gray;
			this.label12.Location = new System.Drawing.Point(430, 192);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(239, 17);
			this.label12.TabIndex = 47;
			this.label12.Text = "(激活后连续运行的小时数，小于0表示无限)";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(4, 3);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(35, 17);
			this.label3.TabIndex = 45;
			this.label3.Text = "语言:";
			this.button_create.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.button_create.Location = new System.Drawing.Point(256, 420);
			this.button_create.Name = "button_create";
			this.button_create.Size = new System.Drawing.Size(180, 39);
			this.button_create.TabIndex = 44;
			this.button_create.Text = "创建证书";
			this.button_create.UseVisualStyleBackColor = true;
			this.button_create.Click += new System.EventHandler(button_create_Click);
			this.textBox_hours.Location = new System.Drawing.Point(309, 189);
			this.textBox_hours.Name = "textBox_hours";
			this.textBox_hours.Size = new System.Drawing.Size(115, 23);
			this.textBox_hours.TabIndex = 43;
			this.textBox_hours.Text = "-1";
			this.label_hours.AutoSize = true;
			this.label_hours.Location = new System.Drawing.Point(216, 192);
			this.label_hours.Name = "label_hours";
			this.label_hours.Size = new System.Drawing.Size(59, 17);
			this.label_hours.TabIndex = 42;
			this.label_hours.Text = "有效小时:";
			this.textBox_createDate.Location = new System.Drawing.Point(86, 189);
			this.textBox_createDate.Name = "textBox_createDate";
			this.textBox_createDate.ReadOnly = true;
			this.textBox_createDate.Size = new System.Drawing.Size(115, 23);
			this.textBox_createDate.TabIndex = 41;
			this.label11.AutoSize = true;
			this.label11.Location = new System.Drawing.Point(4, 192);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(59, 17);
			this.label11.TabIndex = 40;
			this.label11.Text = "发证日期:";
			this.textBox_Descriptions.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_Descriptions.Location = new System.Drawing.Point(7, 301);
			this.textBox_Descriptions.Multiline = true;
			this.textBox_Descriptions.Name = "textBox_Descriptions";
			this.textBox_Descriptions.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_Descriptions.Size = new System.Drawing.Size(714, 110);
			this.textBox_Descriptions.TabIndex = 39;
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(4, 275);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(227, 17);
			this.label10.TabIndex = 38;
			this.label10.Text = "额外信息(键值对+换行符): 例如： A:你好";
			this.textBox_Unique.Location = new System.Drawing.Point(86, 249);
			this.textBox_Unique.Name = "textBox_Unique";
			this.textBox_Unique.Size = new System.Drawing.Size(337, 23);
			this.textBox_Unique.TabIndex = 37;
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(4, 252);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(59, 17);
			this.label9.TabIndex = 36;
			this.label9.Text = "唯一编号:";
			this.textBox_KeyWord.Location = new System.Drawing.Point(86, 219);
			this.textBox_KeyWord.Name = "textBox_KeyWord";
			this.textBox_KeyWord.ReadOnly = true;
			this.textBox_KeyWord.Size = new System.Drawing.Size(338, 23);
			this.textBox_KeyWord.TabIndex = 35;
			this.textBox_KeyWord.Text = "HslCommunication";
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(4, 222);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(47, 17);
			this.label8.TabIndex = 34;
			this.label8.Text = "关键字:";
			this.textBox_NotAfter.Location = new System.Drawing.Point(86, 157);
			this.textBox_NotAfter.Name = "textBox_NotAfter";
			this.textBox_NotAfter.Size = new System.Drawing.Size(338, 23);
			this.textBox_NotAfter.TabIndex = 33;
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(4, 160);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(59, 17);
			this.label7.TabIndex = 32;
			this.label7.Text = "结束日期:";
			this.textBox_NotBefore.Location = new System.Drawing.Point(86, 126);
			this.textBox_NotBefore.Name = "textBox_NotBefore";
			this.textBox_NotBefore.Size = new System.Drawing.Size(338, 23);
			this.textBox_NotBefore.TabIndex = 31;
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(4, 129);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(59, 17);
			this.label6.TabIndex = 30;
			this.label6.Text = "开始日期:";
			this.textBox_To.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_To.Location = new System.Drawing.Point(86, 79);
			this.textBox_To.Multiline = true;
			this.textBox_To.Name = "textBox_To";
			this.textBox_To.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_To.Size = new System.Drawing.Size(635, 42);
			this.textBox_To.TabIndex = 29;
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(4, 82);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(47, 17);
			this.label5.TabIndex = 28;
			this.label5.Text = "持有者:";
			this.textBox_From.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox_From.Location = new System.Drawing.Point(86, 37);
			this.textBox_From.Multiline = true;
			this.textBox_From.Name = "textBox_From";
			this.textBox_From.ReadOnly = true;
			this.textBox_From.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox_From.Size = new System.Drawing.Size(635, 36);
			this.textBox_From.TabIndex = 27;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(4, 40);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(47, 17);
			this.label2.TabIndex = 26;
			this.label2.Text = "发证人:";
			this.panel1.Controls.Add(this.radioButton_java);
			this.panel1.Controls.Add(this.radioButton_dotnet);
			this.panel1.Location = new System.Drawing.Point(85, 1);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(321, 36);
			this.panel1.TabIndex = 50;
			this.radioButton_java.AutoSize = true;
			this.radioButton_java.Font = new System.Drawing.Font("微软雅黑", 15f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.radioButton_java.Location = new System.Drawing.Point(162, 2);
			this.radioButton_java.Name = "radioButton_java";
			this.radioButton_java.Size = new System.Drawing.Size(71, 31);
			this.radioButton_java.TabIndex = 1;
			this.radioButton_java.Text = "Java";
			this.radioButton_java.UseVisualStyleBackColor = true;
			this.radioButton_dotnet.AutoSize = true;
			this.radioButton_dotnet.Checked = true;
			this.radioButton_dotnet.Font = new System.Drawing.Font("微软雅黑", 15f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.radioButton_dotnet.Location = new System.Drawing.Point(5, 2);
			this.radioButton_dotnet.Name = "radioButton_dotnet";
			this.radioButton_dotnet.Size = new System.Drawing.Size(139, 31);
			this.radioButton_dotnet.TabIndex = 0;
			this.radioButton_dotnet.TabStop = true;
			this.radioButton_dotnet.Text = "DotNet(C#)";
			this.radioButton_dotnet.UseVisualStyleBackColor = true;
			this.linkLabel1.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.linkLabel1.Location = new System.Drawing.Point(618, 416);
			this.linkLabel1.Name = "linkLabel1";
			this.linkLabel1.Size = new System.Drawing.Size(109, 26);
			this.linkLabel1.TabIndex = 51;
			this.linkLabel1.TabStop = true;
			this.linkLabel1.Text = "编辑模式";
			this.linkLabel1.TextAlign = System.Drawing.ContentAlignment.TopRight;
			this.linkLabel1.Click += new System.EventHandler(linkLabel1_Click);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.Controls.Add(this.linkLabel1);
			base.Controls.Add(this.panel1);
			base.Controls.Add(this.label14);
			base.Controls.Add(this.label13);
			base.Controls.Add(this.label12);
			base.Controls.Add(this.label3);
			base.Controls.Add(this.button_create);
			base.Controls.Add(this.textBox_hours);
			base.Controls.Add(this.label_hours);
			base.Controls.Add(this.textBox_createDate);
			base.Controls.Add(this.label11);
			base.Controls.Add(this.textBox_Descriptions);
			base.Controls.Add(this.label10);
			base.Controls.Add(this.textBox_Unique);
			base.Controls.Add(this.label9);
			base.Controls.Add(this.textBox_KeyWord);
			base.Controls.Add(this.label8);
			base.Controls.Add(this.textBox_NotAfter);
			base.Controls.Add(this.label7);
			base.Controls.Add(this.textBox_NotBefore);
			base.Controls.Add(this.label6);
			base.Controls.Add(this.textBox_To);
			base.Controls.Add(this.label5);
			base.Controls.Add(this.textBox_From);
			base.Controls.Add(this.label2);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "HslCertificateControl";
			base.Size = new System.Drawing.Size(730, 468);
			base.Load += new System.EventHandler(HslCertificateControl_Load);
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
