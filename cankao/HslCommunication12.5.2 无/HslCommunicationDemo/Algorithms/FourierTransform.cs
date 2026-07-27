using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using HslCommunication.Algorithms.Fourier;
using HslControls;

namespace HslCommunicationDemo.Algorithms
{
	public class FourierTransform : HslFormContent
	{
		private IContainer components = null;

		private Label label5;

		private HslCurve userCurve1;

		private HslCurve userCurve2;

		private Label label1;

		private Label label3;

		private HslCurve userCurve3;

		private HslCurve userCurve4;

		private Label label6;

		private HslCurve userCurve5;

		private HslCurve userCurve6;

		private HslButton userButton1;

		private HslButton userButton2;

		private HslButton userButton3;

		private HslButton userButton4;

		private TextBox textBox_curve3;

		private TextBox textBox_curve2;

		private HslButton userButton5;

		private TextBox textBox_curve1;

		private HslButton userButton6;

		public FourierTransform()
		{
			InitializeComponent();
		}

		private void 傅立叶变换_Load(object sender, EventArgs e)
		{
			Squarewave();
			Sinawave();
			Others();
			Language(Program.Language);
		}

		private void Language(int language)
		{
			if (language == 1)
			{
				Text = "傅里叶变换测试";
				label5.Text = "FFT 快速离散傅立叶变换";
				label1.Text = "方波及变换后的波形";
				label3.Text = "正弦波及变换后的波形";
				label6.Text = "混合波及变换后的波形";
				userButton1.Text = "专用图形";
				userButton2.Text = "专用图形";
				userButton3.Text = "专用图形";
			}
			else
			{
				Text = "FFT Test";
				label5.Text = "FFT Fast discrete Fourier transform";
				label1.Text = "The waveform of the square sweep and the transformed";
				label3.Text = "Waveform after sine wave and transform";
				label6.Text = "Waveform after mixed sweep and transform";
				userButton1.Text = "Graphics";
				userButton2.Text = "Graphics";
				userButton3.Text = "Graphics";
			}
		}

		private void Squarewave()
		{
			double[] wave = GetWave1();
			userCurve1.SetCurve("A", 0, wave.Select((double m) => (float)m).ToArray(), Color.Red, 1f, CurveStyle.Curve);
			double[] source = FFTHelper.FFT(wave);
			userCurve2.ReferenceAxisLeft.Max = (float)source.Max();
			userCurve2.ReferenceAxisRight.Max = (float)source.Max();
			userCurve2.SetCurve("A", 0, source.Select((double m) => (float)m).ToArray(), Color.Blue, 1f, CurveStyle.Curve);
		}

		private void Sinawave()
		{
			double[] wave = GetWave2();
			userCurve4.SetCurve("A", 0, wave.Select((double m) => (float)m).ToArray(), Color.Red, 1f, CurveStyle.Curve);
			double[] source = FFTHelper.FFT(wave);
			userCurve3.ReferenceAxisLeft.Max = (float)source.Max();
			userCurve3.ReferenceAxisRight.Max = (float)source.Max();
			userCurve3.SetCurve("A", 0, source.Select((double m) => (float)m).ToArray(), Color.Blue, 1f, CurveStyle.Curve);
		}

		private void Others()
		{
			double[] wave = GetWave3();
			userCurve6.SetCurve("A", 0, wave.Select((double m) => (float)m).ToArray(), Color.Red, 1f, CurveStyle.Curve);
			double[] source = FFTHelper.FFT(wave);
			userCurve5.ReferenceAxisLeft.Max = (float)source.Max();
			userCurve5.ReferenceAxisRight.Max = (float)source.Max();
			userCurve5.SetCurve("A", 0, source.Select((double m) => (float)m).ToArray(), Color.Blue, 1f, CurveStyle.Curve);
		}

		private void userButton1_Click(object sender, EventArgs e)
		{
			using (FormImage formImage = new FormImage(FFTHelper.GetFFTImage(GetWave1(), 1000, 600, Color.Blue)))
			{
				formImage.ShowDialog();
			}
		}

		private void userButton2_Click(object sender, EventArgs e)
		{
			using (FormImage formImage = new FormImage(FFTHelper.GetFFTImage(GetWave2(), 1000, 600, Color.Blue)))
			{
				formImage.ShowDialog();
			}
		}

		private void userButton3_Click(object sender, EventArgs e)
		{
			using (FormImage formImage = new FormImage(FFTHelper.GetFFTImage(GetWave3(), 1000, 600, Color.Blue)))
			{
				formImage.ShowDialog();
			}
		}

		private void userButton6_Click(object sender, EventArgs e)
		{
			try
			{
				float[] data = (from m in FFTFilter.FilterFFT(GetWave1(), double.Parse(textBox_curve1.Text))
					select (float)m).ToArray();
				userCurve1.SetLeftCurve("滤波数据", data, Color.DodgerBlue);
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage("数据输入错误！" + ex.Message);
			}
		}

		private void userButton5_Click(object sender, EventArgs e)
		{
			try
			{
				float[] data = (from m in FFTFilter.FilterFFT(GetWave2(), double.Parse(textBox_curve2.Text))
					select (float)m).ToArray();
				userCurve4.SetLeftCurve("滤波数据", data, Color.DodgerBlue);
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage("数据输入错误！" + ex.Message);
			}
		}

		private void userButton4_Click(object sender, EventArgs e)
		{
			try
			{
				float[] data = (from m in FFTFilter.FilterFFT(GetWave3(), double.Parse(textBox_curve3.Text))
					select (float)m).ToArray();
				userCurve6.SetLeftCurve("滤波数据", data, Color.DodgerBlue);
			}
			catch (Exception ex)
			{
				DemoUtils.ShowMessage("数据输入错误！" + ex.Message);
			}
		}

		private double[] GetWave1()
		{
			double[] array = new double[256];
			for (int i = 0; i < array.Length / 2; i++)
			{
				array[i] = 5.0;
			}
			return array;
		}

		private double[] GetWave2()
		{
			double[] array = new double[256];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = 6.0 * Math.Sin((double)i * Math.PI / 128.0) + 5.0 + 0.5 * Math.Cos((double)(i * 8) * Math.PI / 128.0);
			}
			return array;
		}

		private double[] GetWave3()
		{
			double[] array = new double[256];
			for (int i = 0; i < array.Length; i++)
			{
				if (i > 140 && i < 160)
				{
					array[i] = -5.0;
				}
			}
			return array;
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
			this.label5 = new System.Windows.Forms.Label();
			this.label1 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.label6 = new System.Windows.Forms.Label();
			this.userButton1 = new HslControls.HslButton();
			this.userCurve5 = new HslControls.HslCurve();
			this.userCurve6 = new HslControls.HslCurve();
			this.userCurve3 = new HslControls.HslCurve();
			this.userCurve4 = new HslControls.HslCurve();
			this.userCurve2 = new HslControls.HslCurve();
			this.userCurve1 = new HslControls.HslCurve();
			this.userButton2 = new HslControls.HslButton();
			this.userButton3 = new HslControls.HslButton();
			this.userButton4 = new HslControls.HslButton();
			this.textBox_curve3 = new System.Windows.Forms.TextBox();
			this.textBox_curve2 = new System.Windows.Forms.TextBox();
			this.userButton5 = new HslControls.HslButton();
			this.textBox_curve1 = new System.Windows.Forms.TextBox();
			this.userButton6 = new HslControls.HslButton();
			base.SuspendLayout();
			this.label5.AutoSize = true;
			this.label5.ForeColor = System.Drawing.Color.Red;
			this.label5.Location = new System.Drawing.Point(404, 3);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(139, 17);
			this.label5.TabIndex = 10;
			this.label5.Text = "FFT 快速离散傅立叶变换";
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(12, 25);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(116, 17);
			this.label1.TabIndex = 14;
			this.label1.Text = "方波及变换后的波形";
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(8, 236);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(128, 17);
			this.label3.TabIndex = 17;
			this.label3.Text = "正弦波及变换后的波形";
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(8, 445);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(128, 17);
			this.label6.TabIndex = 20;
			this.label6.Text = "混合波及变换后的波形";
			this.userButton1.BackColor = System.Drawing.Color.Transparent;
			this.userButton1.CustomerInformation = "";
			this.userButton1.EnableColor = System.Drawing.Color.FromArgb(190, 190, 190);
			this.userButton1.Font = new System.Drawing.Font("微软雅黑", 9f);
			this.userButton1.Location = new System.Drawing.Point(918, 25);
			this.userButton1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userButton1.Name = "userButton1";
			this.userButton1.Size = new System.Drawing.Size(78, 25);
			this.userButton1.TabIndex = 21;
			this.userButton1.Text = "专用图形";
			this.userButton1.Click += new System.EventHandler(userButton1_Click);
			this.userCurve5.BackColor = System.Drawing.Color.Transparent;
			this.userCurve5.IsAbscissaStrech = true;
			this.userCurve5.Location = new System.Drawing.Point(510, 459);
			this.userCurve5.Margin = new System.Windows.Forms.Padding(3, 11, 3, 11);
			this.userCurve5.Name = "userCurve5";
			this.userCurve5.Size = new System.Drawing.Size(463, 190);
			this.userCurve5.StrechDataCountMax = 256;
			this.userCurve5.TabIndex = 19;
			this.userCurve6.BackColor = System.Drawing.Color.Transparent;
			this.userCurve6.IsAbscissaStrech = true;
			this.userCurve6.Location = new System.Drawing.Point(24, 459);
			this.userCurve6.Margin = new System.Windows.Forms.Padding(3, 8, 3, 8);
			this.userCurve6.Name = "userCurve6";
			this.userCurve6.Size = new System.Drawing.Size(463, 190);
			this.userCurve6.StrechDataCountMax = 256;
			this.userCurve6.TabIndex = 18;
			this.userCurve6.ReferenceAxisLeft.Max = 10f;
			this.userCurve6.ReferenceAxisRight.Max = 10f;
			this.userCurve6.ReferenceAxisLeft.Min = -10f;
			this.userCurve6.ReferenceAxisRight.Min = -10f;
			this.userCurve3.BackColor = System.Drawing.Color.Transparent;
			this.userCurve3.IsAbscissaStrech = true;
			this.userCurve3.Location = new System.Drawing.Point(510, 255);
			this.userCurve3.Margin = new System.Windows.Forms.Padding(3, 8, 3, 8);
			this.userCurve3.Name = "userCurve3";
			this.userCurve3.Size = new System.Drawing.Size(463, 190);
			this.userCurve3.StrechDataCountMax = 256;
			this.userCurve3.TabIndex = 16;
			this.userCurve4.BackColor = System.Drawing.Color.Transparent;
			this.userCurve4.IsAbscissaStrech = true;
			this.userCurve4.Location = new System.Drawing.Point(24, 255);
			this.userCurve4.Margin = new System.Windows.Forms.Padding(3, 6, 3, 6);
			this.userCurve4.Name = "userCurve4";
			this.userCurve4.Size = new System.Drawing.Size(463, 190);
			this.userCurve4.StrechDataCountMax = 256;
			this.userCurve4.TabIndex = 15;
			this.userCurve4.ReferenceAxisLeft.Max = 12f;
			this.userCurve4.ReferenceAxisRight.Max = 12f;
			this.userCurve4.ReferenceAxisLeft.Min = -3f;
			this.userCurve4.ReferenceAxisRight.Min = -3f;
			this.userCurve2.BackColor = System.Drawing.Color.Transparent;
			this.userCurve2.IsAbscissaStrech = true;
			this.userCurve2.Location = new System.Drawing.Point(510, 43);
			this.userCurve2.Margin = new System.Windows.Forms.Padding(3, 6, 3, 6);
			this.userCurve2.Name = "userCurve2";
			this.userCurve2.Size = new System.Drawing.Size(463, 190);
			this.userCurve2.StrechDataCountMax = 256;
			this.userCurve2.TabIndex = 13;
			this.userCurve1.BackColor = System.Drawing.Color.Transparent;
			this.userCurve1.IsAbscissaStrech = true;
			this.userCurve1.Location = new System.Drawing.Point(24, 43);
			this.userCurve1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userCurve1.Name = "userCurve1";
			this.userCurve1.Size = new System.Drawing.Size(463, 190);
			this.userCurve1.StrechDataCountMax = 256;
			this.userCurve1.TabIndex = 12;
			this.userCurve1.ReferenceAxisLeft.Max = 10f;
			this.userCurve1.ReferenceAxisRight.Max = 10f;
			this.userButton2.BackColor = System.Drawing.Color.Transparent;
			this.userButton2.CustomerInformation = "";
			this.userButton2.EnableColor = System.Drawing.Color.FromArgb(190, 190, 190);
			this.userButton2.Font = new System.Drawing.Font("微软雅黑", 9f);
			this.userButton2.Location = new System.Drawing.Point(918, 236);
			this.userButton2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userButton2.Name = "userButton2";
			this.userButton2.Size = new System.Drawing.Size(78, 25);
			this.userButton2.TabIndex = 22;
			this.userButton2.Text = "专用图形";
			this.userButton2.Click += new System.EventHandler(userButton2_Click);
			this.userButton3.BackColor = System.Drawing.Color.Transparent;
			this.userButton3.CustomerInformation = "";
			this.userButton3.EnableColor = System.Drawing.Color.FromArgb(190, 190, 190);
			this.userButton3.Font = new System.Drawing.Font("微软雅黑", 9f);
			this.userButton3.Location = new System.Drawing.Point(918, 437);
			this.userButton3.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userButton3.Name = "userButton3";
			this.userButton3.Size = new System.Drawing.Size(78, 25);
			this.userButton3.TabIndex = 23;
			this.userButton3.Text = "专用图形";
			this.userButton3.Click += new System.EventHandler(userButton3_Click);
			this.userButton4.BackColor = System.Drawing.Color.Transparent;
			this.userButton4.CustomerInformation = "";
			this.userButton4.EnableColor = System.Drawing.Color.FromArgb(190, 190, 190);
			this.userButton4.Font = new System.Drawing.Font("微软雅黑", 9f);
			this.userButton4.Location = new System.Drawing.Point(823, 437);
			this.userButton4.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userButton4.Name = "userButton4";
			this.userButton4.Size = new System.Drawing.Size(78, 25);
			this.userButton4.TabIndex = 24;
			this.userButton4.Text = "逆变操作";
			this.userButton4.Click += new System.EventHandler(userButton4_Click);
			this.textBox_curve3.Location = new System.Drawing.Point(717, 439);
			this.textBox_curve3.Name = "textBox_curve3";
			this.textBox_curve3.Size = new System.Drawing.Size(100, 23);
			this.textBox_curve3.TabIndex = 25;
			this.textBox_curve3.Text = "0";
			this.textBox_curve2.Location = new System.Drawing.Point(717, 238);
			this.textBox_curve2.Name = "textBox_curve2";
			this.textBox_curve2.Size = new System.Drawing.Size(100, 23);
			this.textBox_curve2.TabIndex = 27;
			this.textBox_curve2.Text = "0";
			this.userButton5.BackColor = System.Drawing.Color.Transparent;
			this.userButton5.CustomerInformation = "";
			this.userButton5.EnableColor = System.Drawing.Color.FromArgb(190, 190, 190);
			this.userButton5.Font = new System.Drawing.Font("微软雅黑", 9f);
			this.userButton5.Location = new System.Drawing.Point(823, 236);
			this.userButton5.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userButton5.Name = "userButton5";
			this.userButton5.Size = new System.Drawing.Size(78, 25);
			this.userButton5.TabIndex = 26;
			this.userButton5.Text = "逆变操作";
			this.userButton5.Click += new System.EventHandler(userButton5_Click);
			this.textBox_curve1.Location = new System.Drawing.Point(717, 27);
			this.textBox_curve1.Name = "textBox_curve1";
			this.textBox_curve1.Size = new System.Drawing.Size(100, 23);
			this.textBox_curve1.TabIndex = 29;
			this.textBox_curve1.Text = "0";
			this.userButton6.BackColor = System.Drawing.Color.Transparent;
			this.userButton6.CustomerInformation = "";
			this.userButton6.EnableColor = System.Drawing.Color.FromArgb(190, 190, 190);
			this.userButton6.Font = new System.Drawing.Font("微软雅黑", 9f);
			this.userButton6.Location = new System.Drawing.Point(823, 25);
			this.userButton6.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userButton6.Name = "userButton6";
			this.userButton6.Size = new System.Drawing.Size(78, 25);
			this.userButton6.TabIndex = 28;
			this.userButton6.Text = "逆变操作";
			this.userButton6.Click += new System.EventHandler(userButton6_Click);
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1017, 669);
			base.Controls.Add(this.textBox_curve1);
			base.Controls.Add(this.userButton6);
			base.Controls.Add(this.textBox_curve2);
			base.Controls.Add(this.userButton5);
			base.Controls.Add(this.textBox_curve3);
			base.Controls.Add(this.userButton4);
			base.Controls.Add(this.userButton3);
			base.Controls.Add(this.userButton2);
			base.Controls.Add(this.userButton1);
			base.Controls.Add(this.label6);
			base.Controls.Add(this.userCurve5);
			base.Controls.Add(this.userCurve6);
			base.Controls.Add(this.label3);
			base.Controls.Add(this.userCurve3);
			base.Controls.Add(this.userCurve4);
			base.Controls.Add(this.label1);
			base.Controls.Add(this.userCurve2);
			base.Controls.Add(this.userCurve1);
			base.Controls.Add(this.label5);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FourierTransform";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "傅立叶变换";
			base.Load += new System.EventHandler(傅立叶变换_Load);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
