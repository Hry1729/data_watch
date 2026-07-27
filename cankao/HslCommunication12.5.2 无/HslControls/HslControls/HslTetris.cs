using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using HslControls.GameSupport;

namespace HslControls
{
	[Description("一个俄罗斯方块游戏的控件，支持标准的玩法和变态的模式")]
	public class HslTetris : UserControl
	{
		private StringFormat sf = null;

		private TetrisEngine tetrisMain = null;

		private TetrisEngine tetrisMini = null;

		private bool isGameRunning = false;

		private Timer timerPlayer = null;

		private bool isGameAccelerate = false;

		private Func<Point[]> getNewTetrisItem;

		private IContainer components = null;

		private PictureBox pictureBox1;

		private PictureBox pictureBox2;

		private HslButton hslButton1;

		private Label label1;

		[Browsable(false)]
		public Func<Point[]> FuncGetNewTetrisItem
		{
			get
			{
				return getNewTetrisItem;
			}
			set
			{
				if (value != null)
				{
					getNewTetrisItem = value;
					tetrisMini.MoveTeries.SetNewPoints(getNewTetrisItem());
					pictureBox2.Image = tetrisMini.GetRenderBitmap();
				}
			}
		}

		public HslTetris()
		{
			InitializeComponent();
			sf = new StringFormat();
			sf.Alignment = StringAlignment.Center;
			sf.LineAlignment = StringAlignment.Center;
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
			SetStyle(ControlStyles.ResizeRedraw, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
			tetrisMain = new TetrisEngine(12, 20);
			tetrisMini = new TetrisEngine(4, 4);
		}

		private void HslTetris_Load(object sender, EventArgs e)
		{
			tetrisMini.MoveTeries = new MoveGameItem(tetrisMini.GetNewItemPoints(), new Point(0, 0), new Size(4, 4), null);
			if (getNewTetrisItem != null)
			{
				tetrisMini.MoveTeries.SetNewPoints(getNewTetrisItem());
			}
			else
			{
				tetrisMini.MoveTeries.SetNewPoints(tetrisMain.GetNewItemPoints());
			}
			pictureBox1.Image = tetrisMain.GetRenderBitmap();
			pictureBox2.Image = tetrisMini.GetRenderBitmap();
			tetrisMain.OnBitmapShowEvent += TetrisMain_OnBitmapShowEvent;
			tetrisMini.OnBitmapShowEvent += TetrisMin_OnBitmapShowEvent;
			timerPlayer = new Timer();
			timerPlayer.Interval = 400;
			timerPlayer.Tick += TimerPlayer_Tick;
			timerPlayer.Start();
		}

		private void TetrisMin_OnBitmapShowEvent(object sender, ClassicGameEventArgs e)
		{
			pictureBox2.Image = e.RenderBitmap;
		}

		private void TetrisMain_OnBitmapShowEvent(object sender, ClassicGameEventArgs e)
		{
			pictureBox1.Image = e.RenderBitmap;
		}

		private void hslButton1_Click(object sender, EventArgs e)
		{
			if (hslButton1.Text == "开始")
			{
				tetrisMain.GameArraysReset();
				pictureBox1.Image = tetrisMain.GetRenderBitmap();
				tetrisMain.MoveTeries.SetNewPoints(tetrisMini.MoveTeries.GetItemPoints());
				Point[] source = tetrisMain.GetNewItemPoints();
				if (getNewTetrisItem != null)
				{
					source = getNewTetrisItem();
				}
				tetrisMini.MoveTeries.SetNewPoints(source.ToArray());
				pictureBox2.Image = tetrisMini.GetRenderBitmap();
				tetrisMain.PlayerScore = 0;
				hslButton1.Text = "暂停";
				isGameRunning = true;
			}
			else if (hslButton1.Text == "暂停")
			{
				timerPlayer.Stop();
				hslButton1.Text = "继续";
			}
			else
			{
				hslButton1.Text = "暂停";
				timerPlayer.Start();
			}
		}

		protected override bool ProcessDialogKey(Keys keyData)
		{
			switch (keyData)
			{
			case Keys.Left:
				tetrisMain.MoveLeft();
				break;
			case Keys.Right:
				tetrisMain.MoveRight();
				break;
			case Keys.Up:
				tetrisMain.RotationChange();
				break;
			case Keys.Down:
				if (!isGameAccelerate)
				{
					timerPlayer.Interval = 15;
					TimerPlayer_Tick(null, null);
					isGameAccelerate = true;
				}
				break;
			}
			return base.ProcessDialogKey(keyData);
		}

		private void TimerPlayer_Tick(object sender, EventArgs e)
		{
			if (!isGameRunning)
			{
				return;
			}
			switch (tetrisMain.MoveDown())
			{
			case 1:
			{
				isGameAccelerate = false;
				timerPlayer.Interval = 400;
				Point[] itemPoints = tetrisMini.MoveTeries.GetItemPoints();
				tetrisMain.MoveTeries.SetNewPoints(itemPoints);
				if (getNewTetrisItem != null)
				{
					tetrisMini.MoveTeries.SetNewPoints(getNewTetrisItem());
				}
				else
				{
					tetrisMini.MoveTeries.SetNewPoints(tetrisMain.GetNewItemPoints());
				}
				pictureBox2.Image = tetrisMini.GetRenderBitmap();
				label1.Text = "Score: " + tetrisMain.PlayerScore;
				break;
			}
			case 2:
				isGameRunning = false;
				hslButton1.Text = "开始";
				break;
			}
		}

		private void HslTetris_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Down && isGameAccelerate)
			{
				timerPlayer.Interval = 400;
				isGameAccelerate = false;
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
			this.pictureBox1 = new System.Windows.Forms.PictureBox();
			this.pictureBox2 = new System.Windows.Forms.PictureBox();
			this.label1 = new System.Windows.Forms.Label();
			this.hslButton1 = new HslControls.HslButton();
			((System.ComponentModel.ISupportInitialize)this.pictureBox1).BeginInit();
			((System.ComponentModel.ISupportInitialize)this.pictureBox2).BeginInit();
			base.SuspendLayout();
			this.pictureBox1.Location = new System.Drawing.Point(3, 3);
			this.pictureBox1.Name = "pictureBox1";
			this.pictureBox1.Size = new System.Drawing.Size(248, 386);
			this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
			this.pictureBox1.TabIndex = 0;
			this.pictureBox1.TabStop = false;
			this.pictureBox2.Location = new System.Drawing.Point(282, 3);
			this.pictureBox2.Name = "pictureBox2";
			this.pictureBox2.Size = new System.Drawing.Size(107, 89);
			this.pictureBox2.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
			this.pictureBox2.TabIndex = 1;
			this.pictureBox2.TabStop = false;
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(280, 112);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(41, 12);
			this.label1.TabIndex = 3;
			this.label1.Text = "Score:";
			this.hslButton1.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
			this.hslButton1.CustomerInformation = null;
			this.hslButton1.Location = new System.Drawing.Point(282, 346);
			this.hslButton1.Name = "hslButton1";
			this.hslButton1.Size = new System.Drawing.Size(146, 43);
			this.hslButton1.TabIndex = 2;
			this.hslButton1.Text = "开始";
			this.hslButton1.Click += new System.EventHandler(hslButton1_Click);
			this.hslButton1.KeyUp += new System.Windows.Forms.KeyEventHandler(HslTetris_KeyUp);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.Controls.Add(this.label1);
			base.Controls.Add(this.hslButton1);
			base.Controls.Add(this.pictureBox2);
			base.Controls.Add(this.pictureBox1);
			base.Name = "HslTetris";
			base.Size = new System.Drawing.Size(442, 401);
			base.Load += new System.EventHandler(HslTetris_Load);
			base.KeyUp += new System.Windows.Forms.KeyEventHandler(HslTetris_KeyUp);
			((System.ComponentModel.ISupportInitialize)this.pictureBox1).EndInit();
			((System.ComponentModel.ISupportInitialize)this.pictureBox2).EndInit();
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
