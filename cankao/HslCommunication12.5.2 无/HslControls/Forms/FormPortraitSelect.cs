using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace HslControls.Forms
{
	public class FormPortraitSelect : Form
	{
		private Brush brush = new SolidBrush(Color.FromArgb(120, Color.Gray));

		private IContainer components = null;

		private PictureBox pictureBox1;

		private PictureBox pictureBox2;

		private HslButton userButton1;

		private HslButton userButton2;

		private Label label1;

		private PictureBox pictureBox3;

		private PictureBox pictureBox4;

		private Label label2;

		private Label label3;

		private Label label4;

		private GroupBox groupBox1;

		private GroupBox groupBox2;

		private GroupBox groupBox3;

		private bool HasPicture { get; set; }

		private Rectangle RectangleSelected { get; set; }

		private Rectangle RectangleMoved { get; set; }

		private Rectangle RectangleImage { get; set; }

		private bool IsMouseOver { get; set; }

		private bool IsMouseOverOnImage { get; set; }

		private bool IsMouseDown { get; set; }

		private Point MouseDownPoint { get; set; }

		private Point MouseMovePrecives { get; set; }

		public FormPortraitSelect()
		{
			InitializeComponent();
		}

		private void pictureBox1_MouseEnter(object sender, EventArgs e)
		{
			IsMouseOver = true;
			pictureBox1.Refresh();
		}

		private void pictureBox1_MouseLeave(object sender, EventArgs e)
		{
			IsMouseOver = false;
			pictureBox1.Refresh();
		}

		private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
		{
			if (!IsMouseOver || !HasPicture)
			{
				return;
			}
			if (RectangleSelected.Contains(e.Location))
			{
				Cursor = Cursors.SizeAll;
				if (IsMouseDown)
				{
					MoveByPoint(e.Location);
				}
			}
			else
			{
				Cursor = Cursors.Default;
			}
		}

		private void MoveByPoint(Point point)
		{
			if (RectangleImage.Width >= RectangleImage.Height)
			{
				int num = point.X - MouseMovePrecives.X + RectangleSelected.X;
				if (num >= 0 && num <= RectangleImage.Width - RectangleSelected.Width)
				{
					RectangleSelected = new Rectangle(num, RectangleSelected.Y, RectangleSelected.Width, RectangleSelected.Height);
					pictureBox1.Refresh();
				}
			}
			else
			{
				int num2 = point.Y - MouseMovePrecives.Y + RectangleSelected.Y;
				if (num2 >= 0 && num2 <= RectangleImage.Height - RectangleSelected.Height)
				{
					RectangleSelected = new Rectangle(RectangleSelected.X, num2, RectangleSelected.Width, RectangleSelected.Height);
					pictureBox1.Refresh();
				}
			}
			MouseMovePrecives = point;
		}

		private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
		{
			if (IsMouseOver && HasPicture && RectangleSelected.Contains(e.Location))
			{
				IsMouseDown = true;
				MouseDownPoint = e.Location;
				MouseMovePrecives = e.Location;
			}
			pictureBox1.Focus();
		}

		private void pictureBox1_MouseUp(object sender, MouseEventArgs e)
		{
			if (HasPicture && RectangleSelected.Contains(e.Location))
			{
				SetImgaeMiniShow();
			}
			IsMouseDown = false;
			pictureBox1.Refresh();
		}

		private void SetImgaeMiniShow()
		{
			Rectangle des = RectangleRestore();
			pictureBox2.Image?.Dispose();
			pictureBox2.Image = GetSpecicalSizeFromImage(100, des);
			pictureBox3.Image?.Dispose();
			pictureBox3.Image = GetSpecicalSizeFromImage(64, des);
			pictureBox4.Image?.Dispose();
			pictureBox4.Image = GetSpecicalSizeFromImage(32, des);
		}

		private Rectangle RectangleRestore()
		{
			int num = pictureBox1.Image.Width;
			int num2 = pictureBox1.Image.Height;
			if (num < num2)
			{
				return new Rectangle(0, RectangleSelected.Y * num2 / 370, num, num);
			}
			return new Rectangle(RectangleSelected.X * num / 370, 0, num2, num2);
		}

		private Bitmap GetSpecicalSizeFromImage(int size, Rectangle des)
		{
			Bitmap bitmap = new Bitmap(size, size);
			using (Graphics graphics = Graphics.FromImage(bitmap))
			{
				graphics.DrawImage(pictureBox1.Image, new Rectangle(0, 0, size, size), des, GraphicsUnit.Pixel);
			}
			return bitmap;
		}

		private void userButton1_Click(object sender, EventArgs e)
		{
			using (OpenFileDialog openFileDialog = new OpenFileDialog())
			{
				openFileDialog.Multiselect = false;
				openFileDialog.Title = "请选择一张图片";
				openFileDialog.Filter = "图片文件(*.jpg)|*.jpg|图片文件(*.png)|*.png";
				if (openFileDialog.ShowDialog() == DialogResult.OK)
				{
					LoadPictureFile(openFileDialog.FileName);
				}
			}
		}

		private void LoadPictureFile(string picPath)
		{
			Bitmap bitmap = null;
			try
			{
				bitmap = (Bitmap)Image.FromFile(picPath);
				int num = bitmap.Width;
				int num2 = bitmap.Height;
				if (num > num2)
				{
					num2 = num2 * 370 / num;
					num = 369;
				}
				else
				{
					num = num * 370 / num2;
					num2 = 369;
				}
				label1.Text = $"({num},{num2})";
				RectangleImage = new Rectangle((370 - num) / 2, (370 - num2) / 2, num, num2);
				if (RectangleImage.Width >= RectangleImage.Height)
				{
					RectangleSelected = new Rectangle(RectangleImage.X, RectangleImage.Y, RectangleImage.Height, RectangleImage.Height);
				}
				else
				{
					RectangleSelected = new Rectangle(RectangleImage.X, RectangleImage.Y, RectangleImage.Width, RectangleImage.Width);
				}
				HasPicture = true;
				pictureBox1.Refresh();
			}
			catch (Exception ex)
			{
				HslHelper.ShowExceptionMessage(ex);
				return;
			}
			pictureBox1.Image = bitmap;
			SetImgaeMiniShow();
		}

		private void FormPortraitSelect_Load(object sender, EventArgs e)
		{
		}

		private void pictureBox1_Paint(object sender, PaintEventArgs e)
		{
			if (HasPicture)
			{
				Graphics graphics = e.Graphics;
				graphics.FillRectangle(brush, RectangleSelected);
				graphics.DrawRectangle(Pens.LightSkyBlue, RectangleSelected);
			}
		}

		private void userButton2_Click(object sender, EventArgs e)
		{
			if (HasPicture)
			{
				base.DialogResult = DialogResult.OK;
			}
			else
			{
				base.DialogResult = DialogResult.Cancel;
			}
		}

		private void pictureBox1_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
		{
		}

		public Bitmap GetSpecifiedSizeImage(int size)
		{
			if (HasPicture)
			{
				return GetSpecicalSizeFromImage(size, RectangleRestore());
			}
			return null;
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(HslControls.Forms.FormPortraitSelect));
			this.pictureBox1 = new System.Windows.Forms.PictureBox();
			this.pictureBox2 = new System.Windows.Forms.PictureBox();
			this.userButton1 = new HslControls.HslButton();
			this.userButton2 = new HslControls.HslButton();
			this.label1 = new System.Windows.Forms.Label();
			this.pictureBox3 = new System.Windows.Forms.PictureBox();
			this.pictureBox4 = new System.Windows.Forms.PictureBox();
			this.label2 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.label4 = new System.Windows.Forms.Label();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.groupBox2 = new System.Windows.Forms.GroupBox();
			this.groupBox3 = new System.Windows.Forms.GroupBox();
			((System.ComponentModel.ISupportInitialize)this.pictureBox1).BeginInit();
			((System.ComponentModel.ISupportInitialize)this.pictureBox2).BeginInit();
			((System.ComponentModel.ISupportInitialize)this.pictureBox3).BeginInit();
			((System.ComponentModel.ISupportInitialize)this.pictureBox4).BeginInit();
			this.groupBox1.SuspendLayout();
			this.groupBox2.SuspendLayout();
			this.groupBox3.SuspendLayout();
			base.SuspendLayout();
			this.pictureBox1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.pictureBox1.Location = new System.Drawing.Point(12, 12);
			this.pictureBox1.Name = "pictureBox1";
			this.pictureBox1.Size = new System.Drawing.Size(372, 372);
			this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
			this.pictureBox1.TabIndex = 0;
			this.pictureBox1.TabStop = false;
			this.pictureBox1.Paint += new System.Windows.Forms.PaintEventHandler(pictureBox1_Paint);
			this.pictureBox1.MouseDown += new System.Windows.Forms.MouseEventHandler(pictureBox1_MouseDown);
			this.pictureBox1.MouseEnter += new System.EventHandler(pictureBox1_MouseEnter);
			this.pictureBox1.MouseLeave += new System.EventHandler(pictureBox1_MouseLeave);
			this.pictureBox1.MouseMove += new System.Windows.Forms.MouseEventHandler(pictureBox1_MouseMove);
			this.pictureBox1.MouseUp += new System.Windows.Forms.MouseEventHandler(pictureBox1_MouseUp);
			this.pictureBox1.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(pictureBox1_PreviewKeyDown);
			this.pictureBox2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.pictureBox2.Location = new System.Drawing.Point(18, 33);
			this.pictureBox2.Name = "pictureBox2";
			this.pictureBox2.Size = new System.Drawing.Size(102, 102);
			this.pictureBox2.TabIndex = 1;
			this.pictureBox2.TabStop = false;
			this.userButton1.Cursor = System.Windows.Forms.Cursors.Arrow;
			this.userButton1.CustomerInformation = "";
			this.userButton1.Font = new System.Drawing.Font("微软雅黑", 9f);
			this.userButton1.Location = new System.Drawing.Point(78, 31);
			this.userButton1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userButton1.Name = "userButton1";
			this.userButton1.Size = new System.Drawing.Size(105, 39);
			this.userButton1.TabIndex = 3;
			this.userButton1.Text = "select";
			this.userButton1.Click += new System.EventHandler(userButton1_Click);
			this.userButton2.CustomerInformation = "";
			this.userButton2.Font = new System.Drawing.Font("微软雅黑", 9f);
			this.userButton2.Location = new System.Drawing.Point(78, 34);
			this.userButton2.Margin = new System.Windows.Forms.Padding(2, 3, 2, 3);
			this.userButton2.Name = "userButton2";
			this.userButton2.Size = new System.Drawing.Size(105, 39);
			this.userButton2.TabIndex = 4;
			this.userButton2.Text = "sure";
			this.userButton2.Click += new System.EventHandler(userButton2_Click);
			this.label1.AutoSize = true;
			this.label1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label1.Location = new System.Drawing.Point(12, 392);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(16, 17);
			this.label1.TabIndex = 5;
			this.label1.Text = "()";
			this.label1.Visible = false;
			this.pictureBox3.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.pictureBox3.Location = new System.Drawing.Point(134, 33);
			this.pictureBox3.Name = "pictureBox3";
			this.pictureBox3.Size = new System.Drawing.Size(66, 66);
			this.pictureBox3.TabIndex = 6;
			this.pictureBox3.TabStop = false;
			this.pictureBox4.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.pictureBox4.Location = new System.Drawing.Point(218, 33);
			this.pictureBox4.Name = "pictureBox4";
			this.pictureBox4.Size = new System.Drawing.Size(34, 34);
			this.pictureBox4.TabIndex = 7;
			this.pictureBox4.TabStop = false;
			this.label2.AutoSize = true;
			this.label2.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label2.Location = new System.Drawing.Point(16, 138);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(55, 17);
			this.label2.TabIndex = 8;
			this.label2.Text = "100*100";
			this.label3.AutoSize = true;
			this.label3.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label3.Location = new System.Drawing.Point(131, 102);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(41, 17);
			this.label3.TabIndex = 9;
			this.label3.Text = "64*64";
			this.label4.AutoSize = true;
			this.label4.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label4.Location = new System.Drawing.Point(215, 70);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(41, 17);
			this.label4.TabIndex = 10;
			this.label4.Text = "32*32";
			this.groupBox1.Controls.Add(this.pictureBox2);
			this.groupBox1.Controls.Add(this.label4);
			this.groupBox1.Controls.Add(this.pictureBox3);
			this.groupBox1.Controls.Add(this.label3);
			this.groupBox1.Controls.Add(this.pictureBox4);
			this.groupBox1.Controls.Add(this.label2);
			this.groupBox1.Location = new System.Drawing.Point(390, 12);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(275, 170);
			this.groupBox1.TabIndex = 11;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Multi Resolution";
			this.groupBox2.Controls.Add(this.userButton1);
			this.groupBox2.Location = new System.Drawing.Point(390, 188);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(275, 101);
			this.groupBox2.TabIndex = 12;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "File Selected";
			this.groupBox3.Controls.Add(this.userButton2);
			this.groupBox3.Location = new System.Drawing.Point(390, 295);
			this.groupBox3.Name = "groupBox3";
			this.groupBox3.Size = new System.Drawing.Size(275, 91);
			this.groupBox3.TabIndex = 13;
			this.groupBox3.TabStop = false;
			this.groupBox3.Text = "Click to Sure";
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.ClientSize = new System.Drawing.Size(678, 416);
			base.Controls.Add(this.groupBox3);
			base.Controls.Add(this.groupBox2);
			base.Controls.Add(this.groupBox1);
			base.Controls.Add(this.label1);
			base.Controls.Add(this.pictureBox1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
			base.Name = "FormPortraitSelect";
			base.ShowIcon = false;
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Image Select";
			base.Load += new System.EventHandler(FormPortraitSelect_Load);
			((System.ComponentModel.ISupportInitialize)this.pictureBox1).EndInit();
			((System.ComponentModel.ISupportInitialize)this.pictureBox2).EndInit();
			((System.ComponentModel.ISupportInitialize)this.pictureBox3).EndInit();
			((System.ComponentModel.ISupportInitialize)this.pictureBox4).EndInit();
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			this.groupBox2.ResumeLayout(false);
			this.groupBox3.ResumeLayout(false);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
