using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using HslCommunicationDemo.Properties;
using HslControls;

namespace HslCommunicationDemo
{
	public class FormIndex : HslFormContent
	{
		private IContainer components = null;

		private HslTitle hslTitle1;

		private LinkLabel linkLabel1;

		private Label label10;

		private TextBox textBox1;

		private Label label14;

		private Label label15;

		private SplitContainer splitContainer1;

		private PictureBox pictureBox1;

		private PictureBox pictureBox2;

		public FormIndex()
		{
			InitializeComponent();
		}

		private void LinkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			FormLoad.OpenWebside(linkLabel1.Text);
		}

		private void FormCharge_Load(object sender, EventArgs e)
		{
			SetUpdayeInfo();
			if (Program.Language == 2)
			{
				Text = "Start Page";
			}
		}

		private void SetUpdayeInfo()
		{
			textBox1.Text = "V12.5.2\r\n1. OmronHostLinkCModeServer: 修复CMode协议的虚拟服务器，在客户端执行远程写入操作的时候，导致数据错位的bug。\r\n2. OmronCipServer: 修复欧姆龙PLC的CIP虚拟服务器在客户端读取数组索引时，返回数据不正确的bug，优化字符串标签读写时候的数据包大小。\r\n3. AllenBradleyNet: 在写入地址 i=A[10] 这类地址时，写入位到PLC里的整数方法现在支持SINT,INT,DINT,LINT类型，同时支持 i=A.10 这种地址格式。\r\n4. Modbus: Modbus协议里，设置DataFormat为BADC，DCBA时，读取\"100.0\"的bool数组时，调整为小端模式，跟随DataFormat变化。\r\n5. AllenBradleyHelper: ab-cip的协议支持二维数组的地址格式，同时支持了A[1,2]格式和 A[1][2] 格式，修复原本二维数组地址输入后面格式解析异常的bug。\r\n6. PipeTcpNet: 优化TCP管道的异常消息号计数的功能，优化连接及重新连接的部分代码。\r\n7. HttpServer: 修复HttpServer服务器启动后，手动关闭服务的时候，不停的反复触发异常日志记录的bug。\r\n8. Demo: 倍福的ADS协议新增一个地址示例说明，如果是TWINCAT2的情况，全局变量地址前加一个点，例如 s=.A\r\n9. Demo: HttpClient的webapi测试界面，当get请求时，参数在body里输入时，修复数组的参数解析不正确的bug。\r\n10. Demo: 在所有设备测试读取界面，显示读取到的单个变量值时，在十进制和十六进制之外，支持显示二进制的数据内容。\r\n11. Demo: 修复虚拟服务器在生成示例代码的时候，如果绑定了本地的ip地址，结果生成代码异常的bug。\r\n12. Demo: 西门子的虚拟服务器手动添加DB块的时候，支持直接生成示例代码，方便显示复制。\r\n13. Demo: 修复DEMO已经保存的测试界面，重新保存不一样的名字窗口时，两个菜单指向一致的小问题。\r\n14. Demo: 已保存的窗体双击打开时，修复之前的bug，现在改为绑定配置的GUID码，无论树形控件怎么刷新都支持重新打开旧的。\r\n15. Demo: 保存列表双击当前的树形图窗体时，优先显示已经打开的窗体界面，如果需要新的窗体，可以鼠标右键菜单操作。\r\n16. Demo: 测试界面的\"点位变量\"和\"数据模拟\"支持导入导出CSV格式字符串到剪贴板，可以直接复制到Excel，数据模拟界面可以自己设置常用表达式。\r\n17. 新官网：http://www.hsltechnology.cn/，还有全新的使用文档的地址(V12版本升级说明)：http://www.hsltechnology.cn/Doc/HslCommunication\r\n18. 本软件已经申请软件著作权，软著登记号：2020SR0340826，任何盗用软件，破解软件，未经正式合同授权而商业使用均视为侵权。";
		}

		private void ShowActiveCode()
		{
			textBox1.Text = "/// <summary>\r\n\t\t\t\t\t\t\t\t\t\t\t\t\t\t  /// 应用程序的主入口点。\r\n\t\t\t\t\t\t\t\t\t\t\t\t\t\t  /// </summary>\r\n[STAThread]\r\nstatic void Main( )\r\n{\r\n\t// 中文授权示例\r\n\tif(!HslCommunication.Authorization.SetAuthorizationCode( \"你的激活码\" ))\r\n\t{\r\n\t\tDemoUtils.ShowMessage( \"授权失败！当前程序只能使用24小时！\" );\r\n\t\treturn; // 激活失败就退出系统\r\n\t}\r\n\r\n\t// English For example\r\n\tif(!HslCommunication.Authorization.SetAuthorizationCode( \"Your Active Code\" ))\r\n\t{\r\n\t\tDemoUtils.ShowMessage( \"Active Failed! it can only use 24 hours\" );\r\n\t\treturn;  // quit if active failed\r\n\t}\r\n\tApplication.EnableVisualStyles( );\r\n\tApplication.SetCompatibleTextRenderingDefault( false );\r\n\tApplication.Run( new Form1( ) );\r\n}";
		}

		private void label15_Click(object sender, EventArgs e)
		{
			ShowActiveCode();
		}

		private void label14_Click(object sender, EventArgs e)
		{
			SetUpdayeInfo();
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
			this.hslTitle1 = new HslControls.HslTitle();
			this.linkLabel1 = new System.Windows.Forms.LinkLabel();
			this.label10 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label14 = new System.Windows.Forms.Label();
			this.label15 = new System.Windows.Forms.Label();
			this.splitContainer1 = new System.Windows.Forms.SplitContainer();
			this.pictureBox1 = new System.Windows.Forms.PictureBox();
			this.pictureBox2 = new System.Windows.Forms.PictureBox();
			((System.ComponentModel.ISupportInitialize)this.splitContainer1).BeginInit();
			this.splitContainer1.Panel1.SuspendLayout();
			this.splitContainer1.Panel2.SuspendLayout();
			this.splitContainer1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)this.pictureBox1).BeginInit();
			((System.ComponentModel.ISupportInitialize)this.pictureBox2).BeginInit();
			base.SuspendLayout();
			this.hslTitle1.Dock = System.Windows.Forms.DockStyle.Top;
			this.hslTitle1.EdgeColor = System.Drawing.Color.Bisque;
			this.hslTitle1.Font = new System.Drawing.Font("微软雅黑", 18f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.hslTitle1.FontLeft = null;
			this.hslTitle1.FontRight = null;
			this.hslTitle1.LeftRightEdgeColor = System.Drawing.Color.FromArgb(66, 81, 119);
			this.hslTitle1.Location = new System.Drawing.Point(0, 0);
			this.hslTitle1.Name = "hslTitle1";
			this.hslTitle1.Size = new System.Drawing.Size(700, 58);
			this.hslTitle1.TabIndex = 0;
			this.hslTitle1.Text = "HslCommunication";
			this.hslTitle1.TextLeft = "";
			this.hslTitle1.TextRight = "";
			this.linkLabel1.AutoSize = true;
			this.linkLabel1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.linkLabel1.Location = new System.Drawing.Point(56, 367);
			this.linkLabel1.Name = "linkLabel1";
			this.linkLabel1.Size = new System.Drawing.Size(199, 17);
			this.linkLabel1.TabIndex = 2;
			this.linkLabel1.TabStop = true;
			this.linkLabel1.Text = "http://www.hslcommunication.cn/";
			this.linkLabel1.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(LinkLabel1_LinkClicked);
			this.label10.AutoSize = true;
			this.label10.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label10.Location = new System.Drawing.Point(12, 367);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(44, 17);
			this.label10.TabIndex = 3;
			this.label10.Text = "官网：";
			this.textBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.textBox1.BackColor = System.Drawing.Color.AliceBlue;
			this.textBox1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.textBox1.Location = new System.Drawing.Point(13, 386);
			this.textBox1.Multiline = true;
			this.textBox1.Name = "textBox1";
			this.textBox1.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox1.Size = new System.Drawing.Size(676, 93);
			this.textBox1.TabIndex = 4;
			this.label14.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label14.AutoSize = true;
			this.label14.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label14.ForeColor = System.Drawing.Color.Gray;
			this.label14.Location = new System.Drawing.Point(584, 367);
			this.label14.Name = "label14";
			this.label14.Size = new System.Drawing.Size(104, 17);
			this.label14.TabIndex = 5;
			this.label14.Text = "本次更新日志如下";
			this.label14.Click += new System.EventHandler(label14_Click);
			this.label15.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			this.label15.AutoSize = true;
			this.label15.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label15.ForeColor = System.Drawing.Color.Blue;
			this.label15.Location = new System.Drawing.Point(522, 367);
			this.label15.Name = "label15";
			this.label15.Size = new System.Drawing.Size(56, 17);
			this.label15.TabIndex = 6;
			this.label15.Text = "激活方法";
			this.label15.Click += new System.EventHandler(label15_Click);
			this.splitContainer1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.splitContainer1.Location = new System.Drawing.Point(15, 65);
			this.splitContainer1.Name = "splitContainer1";
			this.splitContainer1.Panel1.Controls.Add(this.pictureBox1);
			this.splitContainer1.Panel2.Controls.Add(this.pictureBox2);
			this.splitContainer1.Size = new System.Drawing.Size(673, 299);
			this.splitContainer1.SplitterDistance = 339;
			this.splitContainer1.TabIndex = 8;
			this.pictureBox1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.pictureBox1.Image = HslCommunicationDemo.Properties.Resources.SupportDevice;
			this.pictureBox1.Location = new System.Drawing.Point(0, 0);
			this.pictureBox1.Name = "pictureBox1";
			this.pictureBox1.Size = new System.Drawing.Size(339, 299);
			this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
			this.pictureBox1.TabIndex = 0;
			this.pictureBox1.TabStop = false;
			this.pictureBox2.Dock = System.Windows.Forms.DockStyle.Fill;
			this.pictureBox2.Image = HslCommunicationDemo.Properties.Resources.SupportRobot;
			this.pictureBox2.Location = new System.Drawing.Point(0, 0);
			this.pictureBox2.Name = "pictureBox2";
			this.pictureBox2.Size = new System.Drawing.Size(330, 299);
			this.pictureBox2.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
			this.pictureBox2.TabIndex = 1;
			this.pictureBox2.TabStop = false;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.AutoScroll = true;
			this.BackColor = System.Drawing.Color.AliceBlue;
			base.ClientSize = new System.Drawing.Size(700, 491);
			base.Controls.Add(this.splitContainer1);
			base.Controls.Add(this.label15);
			base.Controls.Add(this.label14);
			base.Controls.Add(this.textBox1);
			base.Controls.Add(this.label10);
			base.Controls.Add(this.linkLabel1);
			base.Controls.Add(this.hslTitle1);
			base.Name = "FormIndex";
			this.Text = "起始页";
			base.Load += new System.EventHandler(FormCharge_Load);
			this.splitContainer1.Panel1.ResumeLayout(false);
			this.splitContainer1.Panel2.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)this.splitContainer1).EndInit();
			this.splitContainer1.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)this.pictureBox1).EndInit();
			((System.ComponentModel.ISupportInitialize)this.pictureBox2).EndInit();
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
