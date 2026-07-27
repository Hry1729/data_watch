using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using HslCommunication.Algorithms.Fourier;
using HslControls;

namespace HslCommunicationDemo.Algorithms
{
	public class FourierFilter : HslFormContent
	{
		private Random random;

		private int count = 1024;

		private float[] sources;

		private float[] values;

		private float[] filters;

		private DateTime[] dateTimes;

		private IContainer components = null;

		private HslCurveHistory hslCurveHistory1;

		private Label label1;

		private Label label2;

		private Button button1;

		private Label label3;

		private TextBox textBox1;

		private Button button2;

		private Label label4;

		private Button button3;

		public FourierFilter()
		{
			InitializeComponent();
			sources = new float[count];
			values = new float[count];
			filters = new float[count];
			dateTimes = new DateTime[count];
		}

		private void FourierFilter_Load(object sender, EventArgs e)
		{
			random = new Random();
			for (int i = 0; i < sources.Length; i++)
			{
				sources[i] = (float)(Math.Sin(Math.PI * 2.0 * (double)i / 500.0) * 50.0 + 40.0);
				values[i] = sources[i];
				if (random.Next(100) < 50)
				{
					values[i] = sources[i] + (float)random.Next(101) / 20f - 2f;
				}
				dateTimes[i] = DateTime.Now.AddSeconds(i - 1000);
			}
			filters = FFTFilter.FilterFFT(values, 0.002);
			hslCurveHistory1.SetLeftCurve("原始数据", values, Color.LightGray);
			hslCurveHistory1.SetLeftCurve("滤波数据", filters, Color.Tomato);
			hslCurveHistory1.SetDateTimes(dateTimes);
			hslCurveHistory1.RenderCurveUI();
		}

		private void Button1_Click(object sender, EventArgs e)
		{
			using (FormImage formImage = new FormImage(FFTHelper.GetFFTImage(((IEnumerable<float>)values).Select((Func<float, double>)((float m) => m)).ToArray(), 1000, 600, Color.Blue)))
			{
				formImage.ShowDialog();
			}
		}

		private void Button2_Click(object sender, EventArgs e)
		{
			try
			{
				filters = FFTFilter.FilterFFT(values, double.Parse(textBox1.Text));
				hslCurveHistory1.SetLeftCurve("滤波数据", filters, Color.Tomato);
				hslCurveHistory1.RenderCurveUI();
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage("数据输入错误！" + ex.Message);
			}
		}

		private void button3_Click(object sender, EventArgs e)
		{
			using (FormImage formImage = new FormImage(FFTHelper.GetFFTImage(((IEnumerable<float>)values).Select((Func<float, double>)((float m) => m)).ToArray(), 1000, 600, Color.Blue, true)))
			{
				formImage.ShowDialog();
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
			this.hslCurveHistory1 = new HslControls.HslCurveHistory();
			this.label1 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.button1 = new System.Windows.Forms.Button();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.button2 = new System.Windows.Forms.Button();
			this.label4 = new System.Windows.Forms.Label();
			this.button3 = new System.Windows.Forms.Button();
			base.SuspendLayout();
			this.hslCurveHistory1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.hslCurveHistory1.CurveRanges = null;
			this.hslCurveHistory1.Location = new System.Drawing.Point(12, 75);
			this.hslCurveHistory1.MarkTextColor = System.Drawing.Color.Yellow;
			this.hslCurveHistory1.Name = "hslCurveHistory1";
			this.hslCurveHistory1.ReferenceAxisLeft.Min = -20f;
			this.hslCurveHistory1.ReferenceAxisRight.Min = -20f;
			this.hslCurveHistory1.ScaleMode = HslControls.ScaleMode.Both;
			this.hslCurveHistory1.Size = new System.Drawing.Size(980, 558);
			this.hslCurveHistory1.TabIndex = 0;
			this.hslCurveHistory1.Text = "hslCurveHistory1";
			this.hslCurveHistory1.ValueSegment = 12;
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(291, 9);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(440, 17);
			this.label1.TabIndex = 1;
			this.label1.Text = "这是一个基于傅立叶变换的高阶曲线拟合，现在只要一个方法就可以方便的实现了";
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(9, 55);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(188, 17);
			this.label2.TabIndex = 2;
			this.label2.Text = "我们生成一个随机波动的正弦曲线";
			this.button1.Location = new System.Drawing.Point(684, 49);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(117, 23);
			this.button1.TabIndex = 3;
			this.button1.Text = "傅立叶图形";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(Button1_Click);
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(9, 29);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(32, 17);
			this.label3.TabIndex = 4;
			this.label3.Text = "系数";
			this.textBox1.Location = new System.Drawing.Point(47, 26);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(100, 23);
			this.textBox1.TabIndex = 5;
			this.textBox1.Text = "0.002";
			this.button2.Location = new System.Drawing.Point(153, 26);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(117, 23);
			this.button2.TabIndex = 6;
			this.button2.Text = "调整系数";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(Button2_Click);
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(273, 28);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(600, 17);
			this.label4.TabIndex = 7;
			this.label4.Text = "滤波值：最大值为1，不能低于0，越接近1，滤波强度越强，也可能会导致失去真实信号，为0时没有滤波效果。";
			this.button3.Location = new System.Drawing.Point(807, 49);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(185, 23);
			this.button3.TabIndex = 8;
			this.button3.Text = "傅立叶图形(2次根)";
			this.button3.UseVisualStyleBackColor = true;
			this.button3.Click += new System.EventHandler(button3_Click);
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.button3);
			base.Controls.Add(this.label4);
			base.Controls.Add(this.button2);
			base.Controls.Add(this.textBox1);
			base.Controls.Add(this.label3);
			base.Controls.Add(this.button1);
			base.Controls.Add(this.label2);
			base.Controls.Add(this.label1);
			base.Controls.Add(this.hslCurveHistory1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FourierFilter";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "傅立叶滤波";
			base.Load += new System.EventHandler(FourierFilter_Load);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
