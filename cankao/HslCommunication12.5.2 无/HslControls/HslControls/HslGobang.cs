using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace HslControls
{
	[Description("五子棋控件，支持人机对战")]
	public class HslGobang : UserControl
	{
		private Point MouseActivePosition = new Point(-1, -1);

		private Point LastPressPosition = new Point(-1, -1);

		private int PawnIndex = 0;

		private bool IsMouseHoverOnChess = true;

		private bool IsGamePlaying = false;

		private GobangPlayer CurrentPlayer = GobangPlayer.Player1;

		private Point LastPointOfPlayerUser = new Point(-1, -1);

		private StringFormat sf = null;

		private GobangPoint[,] main_chess = new GobangPoint[21, 21];

		private ComputerAI computerAI = null;

		private IContainer components = null;

		private Label label3;

		private CheckBox checkBox2;

		private Button button1;

		private GroupBox groupBox2;

		private CheckBox checkBox1;

		private GroupBox groupBox1;

		private Label label_score_you;

		private Label label_score_computer;

		private Label label2;

		private Label label1;

		private PictureBox pictureBox1;

		private CheckBox checkBox3;

		private GroupBox groupBox3;

		private RadioButton radioButton1;

		public HslGobang()
		{
			InitializeComponent();
			sf = new StringFormat();
			sf.Alignment = StringAlignment.Center;
			sf.LineAlignment = StringAlignment.Center;
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
			SetStyle(ControlStyles.ResizeRedraw, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
		}

		private bool IsGameOver(int x, int y, GobangPlayer player)
		{
			int num = 1;
			for (int i = 0; i < 4; i++)
			{
				num = 1;
				for (int j = 1; j < 5; j++)
				{
					Point point;
					switch (i)
					{
					case 0:
						point = new Point(x - j, y);
						break;
					case 1:
						point = new Point(x, y - j);
						break;
					case 2:
						point = new Point(x - j, y + j);
						break;
					default:
						point = new Point(x - j, y - j);
						break;
					}
					if (IsPositionHasPawn(point, player))
					{
						num++;
						continue;
					}
					break;
				}
				for (int k = 1; k < 5; k++)
				{
					Point point;
					switch (i)
					{
					case 0:
						point = new Point(x + k, y);
						break;
					case 1:
						point = new Point(x, y + k);
						break;
					case 2:
						point = new Point(x + k, y - k);
						break;
					default:
						point = new Point(x + k, y + k);
						break;
					}
					if (IsPositionHasPawn(point, player))
					{
						num++;
						continue;
					}
					break;
				}
				if (num >= 5)
				{
					return true;
				}
				num = 1;
			}
			return false;
		}

		private void StartGame()
		{
			for (int i = 0; i < 21; i++)
			{
				for (int j = 0; j < 21; j++)
				{
					main_chess[i, j].GobangPlayer = GobangPlayer.NonePlayer;
					main_chess[i, j].StepNumber = 0;
					main_chess[i, j].WeightScore = 0;
				}
			}
			IsGamePlaying = true;
			CurrentPlayer = GobangPlayer.NonePlayer;
			PawnIndex = 1;
			if (checkBox2.Checked)
			{
				main_chess[10, 10].StepNumber = PawnIndex++;
				main_chess[10, 10].GobangPlayer = GobangPlayer.Player1;
				LastPressPosition = new Point(10, 10);
			}
			label3.Text = "Waitting";
			pictureBox1.Refresh();
		}

		private Bitmap GetBackgroundImage()
		{
			Bitmap bitmap = new Bitmap(525, 525);
			Graphics graphics = Graphics.FromImage(bitmap);
			graphics.Clear(Color.White);
			Pen lightGray = Pens.LightGray;
			for (int i = 10; i <= 510; i += 25)
			{
				graphics.DrawLine(lightGray, new Point(10, i), new Point(510, i));
				graphics.DrawLine(lightGray, new Point(i, 10), new Point(i, 510));
			}
			return bitmap;
		}

		private bool IsPositionHasPawn(int x, int y)
		{
			return main_chess[x, y].GobangPlayer != GobangPlayer.NonePlayer;
		}

		private bool IsPositionHasPawn(int x, int y, GobangPlayer player)
		{
			if (x < 0 || y < 0 || x > 20 || y > 20)
			{
				return false;
			}
			return main_chess[x, y].GobangPlayer == player;
		}

		private bool IsPositionHasPawn(Point point, GobangPlayer player)
		{
			return IsPositionHasPawn(point.X, point.Y, player);
		}

		private bool IsPositionHasPawn(Point point)
		{
			return IsPositionHasPawn(point.X, point.Y);
		}

		private void pictureBox1_Paint(object sender, PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			graphics.SmoothingMode = SmoothingMode.HighQuality;
			for (int i = 0; i < 21; i++)
			{
				for (int j = 0; j < 21; j++)
				{
					int num = i * 25 + 10;
					int num2 = (20 - j) * 25 + 10;
					if (main_chess[i, j].GobangPlayer == GobangPlayer.NonePlayer)
					{
						if (checkBox3.Checked)
						{
							Rectangle rectangle = new Rectangle(num - 20, num2 - 10, 40, 20);
							using (Font font = new Font("Microsoft YaHei UI", 8f))
							{
								graphics.DrawString(main_chess[i, j].WeightScore.ToString(), font, Brushes.Gray, rectangle, sf);
							}
						}
						continue;
					}
					Rectangle rectangle2 = new Rectangle(num - 10, num2 - 10, 20, 20);
					graphics.FillEllipse(main_chess[i, j].GetPawnBrush, rectangle2);
					graphics.DrawEllipse(Pens.DimGray, rectangle2);
					rectangle2.Offset(-10, 0);
					rectangle2.Width += 20;
					if (checkBox1.Checked)
					{
						using (Font font2 = new Font("Microsoft YaHei UI", 8f))
						{
							graphics.DrawString(main_chess[i, j].StepNumber.ToString(), font2, Brushes.White, rectangle2, sf);
						}
					}
				}
			}
			if (IsMouseHoverOnChess)
			{
				PaintMarkPoint(graphics, MouseActivePosition);
			}
			if (!checkBox1.Checked)
			{
				PaintMarkPoint(graphics, LastPressPosition);
			}
		}

		private Point MouseMovePoint(int picture_x, int picture_y)
		{
			return new Point(picture_x / 25, 20 - picture_y / 25);
		}

		private void PaintMarkPoint(Graphics g, Point point)
		{
			if (point.X >= 0)
			{
				int num = point.X * 25 + 10;
				int num2 = (20 - point.Y) * 25 + 10;
				Pen pen = Pens.LightGray;
				if (main_chess[point.X, point.Y].GobangPlayer == GobangPlayer.Player1)
				{
					pen = Pens.Red;
				}
				g.DrawLines(pen, new Point[3]
				{
					new Point(num - 3, num2 - 9),
					new Point(num - 3, num2 - 3),
					new Point(num - 9, num2 - 3)
				});
				g.DrawLines(pen, new Point[3]
				{
					new Point(num - 3, num2 + 9),
					new Point(num - 3, num2 + 3),
					new Point(num - 9, num2 + 3)
				});
				g.DrawLines(pen, new Point[3]
				{
					new Point(num + 3, num2 - 9),
					new Point(num + 3, num2 - 3),
					new Point(num + 9, num2 - 3)
				});
				g.DrawLines(pen, new Point[3]
				{
					new Point(num + 3, num2 + 9),
					new Point(num + 3, num2 + 3),
					new Point(num + 9, num2 + 3)
				});
			}
		}

		private void HslGobang_Load(object sender, EventArgs e)
		{
			for (int i = 0; i < 21; i++)
			{
				for (int j = 0; j < 21; j++)
				{
					main_chess[i, j] = new GobangPoint();
				}
			}
			computerAI = new ComputerAI(main_chess);
			pictureBox1.Image = GetBackgroundImage();
		}

		private void pictureBox1_MouseLeave(object sender, EventArgs e)
		{
			MouseActivePosition = new Point(-1, -1);
			pictureBox1.Refresh();
		}

		private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
		{
			if (e.X % 25 > 20 || e.X > 520 || e.Y % 25 > 20 || e.Y > 520)
			{
				IsMouseHoverOnChess = false;
				pictureBox1.Refresh();
			}
			else
			{
				MouseActivePosition = MouseMovePoint(e.X, e.Y);
				IsMouseHoverOnChess = !IsPositionHasPawn(MouseActivePosition);
				pictureBox1.Refresh();
			}
		}

		private void button1_Click(object sender, EventArgs e)
		{
			StartGame();
		}

		private void SetPointPawn(Point point, GobangPlayer player)
		{
			main_chess[point.X, point.Y].GobangPlayer = player;
			main_chess[point.X, point.Y].StepNumber = PawnIndex++;
			LastPressPosition = point;
			CurrentPlayer = player;
			if (player == GobangPlayer.Player1)
			{
				computerAI.CalculateAllPoints();
			}
		}

		private void pictureBox1_Click(object sender, EventArgs e)
		{
			if (!IsGamePlaying)
			{
				return;
			}
			Point point = pictureBox1.PointToClient(Cursor.Position);
			if (point.X % 25 > 20 || point.X > 520 || point.Y % 25 > 20 || point.Y > 520)
			{
				return;
			}
			Point point2 = MouseMovePoint(point.X, point.Y);
			if (IsPositionHasPawn(point2) || CurrentPlayer == GobangPlayer.Player2)
			{
				return;
			}
			SetPointPawn(point2, GobangPlayer.Player2);
			LastPointOfPlayerUser = point2;
			if (IsGameOver(point2.X, point2.Y, GobangPlayer.Player2))
			{
				IsGamePlaying = false;
				label3.Text = "You Win";
				label_score_you.Text = (Convert.ToInt32(label_score_you.Text) + 1).ToString();
				return;
			}
			label3.Text = "Thinking";
			label3.Refresh();
			Point point3 = computerAI.CalculateComputerAI();
			SetPointPawn(point3, GobangPlayer.Player1);
			if (IsGameOver(point3.X, point3.Y, GobangPlayer.Player1))
			{
				label3.Text = "You lose";
				IsGamePlaying = false;
				label_score_computer.Text = (Convert.ToInt32(label_score_computer.Text) + 1).ToString();
			}
			else
			{
				label3.Text = "waitting";
			}
			pictureBox1.Refresh();
		}

		private void checkBox1_CheckedChanged(object sender, EventArgs e)
		{
			pictureBox1.Refresh();
		}

		private void checkBox3_CheckedChanged(object sender, EventArgs e)
		{
			pictureBox1.Refresh();
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
			this.label3 = new System.Windows.Forms.Label();
			this.checkBox2 = new System.Windows.Forms.CheckBox();
			this.button1 = new System.Windows.Forms.Button();
			this.groupBox2 = new System.Windows.Forms.GroupBox();
			this.checkBox3 = new System.Windows.Forms.CheckBox();
			this.checkBox1 = new System.Windows.Forms.CheckBox();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.label_score_you = new System.Windows.Forms.Label();
			this.label_score_computer = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.label1 = new System.Windows.Forms.Label();
			this.pictureBox1 = new System.Windows.Forms.PictureBox();
			this.groupBox3 = new System.Windows.Forms.GroupBox();
			this.radioButton1 = new System.Windows.Forms.RadioButton();
			this.groupBox2.SuspendLayout();
			this.groupBox1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)this.pictureBox1).BeginInit();
			this.groupBox3.SuspendLayout();
			base.SuspendLayout();
			this.label3.Font = new System.Drawing.Font("等线 Light", 36f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label3.ForeColor = System.Drawing.Color.Blue;
			this.label3.Location = new System.Drawing.Point(553, 317);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(240, 95);
			this.label3.TabIndex = 14;
			this.label3.Text = "Playing";
			this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.checkBox2.AutoSize = true;
			this.checkBox2.Checked = true;
			this.checkBox2.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBox2.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.checkBox2.Location = new System.Drawing.Point(556, 437);
			this.checkBox2.Name = "checkBox2";
			this.checkBox2.Size = new System.Drawing.Size(173, 21);
			this.checkBox2.TabIndex = 12;
			this.checkBox2.Text = "Computer First [电脑先手]";
			this.checkBox2.UseVisualStyleBackColor = true;
			this.button1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.button1.Location = new System.Drawing.Point(556, 464);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(237, 52);
			this.button1.TabIndex = 11;
			this.button1.Text = "Start Game [开始游戏]";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.groupBox2.Controls.Add(this.checkBox3);
			this.groupBox2.Controls.Add(this.checkBox1);
			this.groupBox2.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.groupBox2.Location = new System.Drawing.Point(556, 87);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(237, 76);
			this.groupBox2.TabIndex = 10;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "Settings [游戏设置]";
			this.checkBox3.AutoSize = true;
			this.checkBox3.Location = new System.Drawing.Point(19, 45);
			this.checkBox3.Name = "checkBox3";
			this.checkBox3.Size = new System.Drawing.Size(154, 21);
			this.checkBox3.TabIndex = 2;
			this.checkBox3.Text = "Show score [显示得分]";
			this.checkBox3.UseVisualStyleBackColor = true;
			this.checkBox3.CheckedChanged += new System.EventHandler(checkBox3_CheckedChanged);
			this.checkBox1.AutoSize = true;
			this.checkBox1.Location = new System.Drawing.Point(19, 22);
			this.checkBox1.Name = "checkBox1";
			this.checkBox1.Size = new System.Drawing.Size(147, 21);
			this.checkBox1.TabIndex = 0;
			this.checkBox1.Text = "Show step [显示步序]";
			this.checkBox1.UseVisualStyleBackColor = true;
			this.checkBox1.CheckedChanged += new System.EventHandler(checkBox1_CheckedChanged);
			this.groupBox1.Controls.Add(this.label_score_you);
			this.groupBox1.Controls.Add(this.label_score_computer);
			this.groupBox1.Controls.Add(this.label2);
			this.groupBox1.Controls.Add(this.label1);
			this.groupBox1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.groupBox1.Location = new System.Drawing.Point(556, 3);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(237, 78);
			this.groupBox1.TabIndex = 9;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Score [比分]";
			this.label_score_you.ForeColor = System.Drawing.Color.FromArgb(192, 0, 192);
			this.label_score_you.Location = new System.Drawing.Point(155, 46);
			this.label_score_you.Name = "label_score_you";
			this.label_score_you.Size = new System.Drawing.Size(64, 17);
			this.label_score_you.TabIndex = 3;
			this.label_score_you.Text = "0";
			this.label_score_you.TextAlign = System.Drawing.ContentAlignment.TopRight;
			this.label_score_computer.ForeColor = System.Drawing.Color.FromArgb(192, 0, 192);
			this.label_score_computer.Location = new System.Drawing.Point(155, 24);
			this.label_score_computer.Name = "label_score_computer";
			this.label_score_computer.Size = new System.Drawing.Size(64, 17);
			this.label_score_computer.TabIndex = 2;
			this.label_score_computer.Text = "0";
			this.label_score_computer.TextAlign = System.Drawing.ContentAlignment.TopRight;
			this.label2.AutoSize = true;
			this.label2.ForeColor = System.Drawing.Color.FromArgb(192, 0, 192);
			this.label2.Location = new System.Drawing.Point(16, 46);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(97, 17);
			this.label2.TabIndex = 1;
			this.label2.Text = "You [用户玩家] :";
			this.label1.AutoSize = true;
			this.label1.ForeColor = System.Drawing.Color.FromArgb(192, 0, 192);
			this.label1.Location = new System.Drawing.Point(16, 24);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(133, 17);
			this.label1.TabIndex = 0;
			this.label1.Text = "Computer [电脑玩家] :";
			this.pictureBox1.BackColor = System.Drawing.Color.White;
			this.pictureBox1.Location = new System.Drawing.Point(3, 8);
			this.pictureBox1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.pictureBox1.Name = "pictureBox1";
			this.pictureBox1.Size = new System.Drawing.Size(530, 522);
			this.pictureBox1.TabIndex = 8;
			this.pictureBox1.TabStop = false;
			this.pictureBox1.Click += new System.EventHandler(pictureBox1_Click);
			this.pictureBox1.Paint += new System.Windows.Forms.PaintEventHandler(pictureBox1_Paint);
			this.pictureBox1.MouseLeave += new System.EventHandler(pictureBox1_MouseLeave);
			this.pictureBox1.MouseMove += new System.Windows.Forms.MouseEventHandler(pictureBox1_MouseMove);
			this.groupBox3.Controls.Add(this.radioButton1);
			this.groupBox3.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.groupBox3.Location = new System.Drawing.Point(556, 169);
			this.groupBox3.Name = "groupBox3";
			this.groupBox3.Size = new System.Drawing.Size(237, 145);
			this.groupBox3.TabIndex = 15;
			this.groupBox3.TabStop = false;
			this.groupBox3.Text = "Game Level [游戏级别]";
			this.radioButton1.AutoSize = true;
			this.radioButton1.Checked = true;
			this.radioButton1.Location = new System.Drawing.Point(19, 22);
			this.radioButton1.Name = "radioButton1";
			this.radioButton1.Size = new System.Drawing.Size(160, 21);
			this.radioButton1.TabIndex = 0;
			this.radioButton1.TabStop = true;
			this.radioButton1.Text = "simple by author [简单]";
			this.radioButton1.UseVisualStyleBackColor = true;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.BackColor = System.Drawing.Color.Transparent;
			base.Controls.Add(this.groupBox3);
			base.Controls.Add(this.label3);
			base.Controls.Add(this.checkBox2);
			base.Controls.Add(this.button1);
			base.Controls.Add(this.groupBox2);
			base.Controls.Add(this.groupBox1);
			base.Controls.Add(this.pictureBox1);
			base.Name = "HslGobang";
			base.Size = new System.Drawing.Size(810, 543);
			base.Load += new System.EventHandler(HslGobang_Load);
			this.groupBox2.ResumeLayout(false);
			this.groupBox2.PerformLayout();
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			((System.ComponentModel.ISupportInitialize)this.pictureBox1).EndInit();
			this.groupBox3.ResumeLayout(false);
			this.groupBox3.PerformLayout();
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
