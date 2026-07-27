using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using HslControls.GameSupport;

namespace HslControls
{
	[Description("一个坦克大战的控件，用于实现一些基础的游戏玩法")]
	public class HslTank : UserControl
	{
		private StringFormat sf = null;

		private TankEngine tankEngine = null;

		private bool isGameRunning = false;

		private Timer timerPlayer = null;

		private bool isGameAccelerate = false;

		private int bulletTimeStamp = 21;

		private int enemyBulletTimeStamp = 0;

		private int bulletSpeed = 0;

		private int enemyMoveSpeed = 0;

		private int userTankMoveSpeed = 21;

		private int userTankMoveDirection = -1;

		private int createEnemyTankSpeed = 0;

		private int count = 0;

		private IContainer components = null;

		private PictureBox pictureBox1;

		private Label label1;

		public HslTank()
		{
			InitializeComponent();
			sf = new StringFormat();
			sf.Alignment = StringAlignment.Center;
			sf.LineAlignment = StringAlignment.Center;
			SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
			SetStyle(ControlStyles.ResizeRedraw, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			SetStyle(ControlStyles.AllPaintingInWmPaint, true);
			tankEngine = new TankEngine(40, 20);
		}

		private void HslTank_Load(object sender, EventArgs e)
		{
			pictureBox1.Image = tankEngine.GetRenderBitmap();
			tankEngine.OnBitmapShowEvent += TankEngine_OnBitmapShowEvent;
			tankEngine.OnGameOver = delegate
			{
				timerPlayer.Stop();
				label1.Text = "游戏结束。消灭：" + tankEngine.GetEliminateCount;
			};
			timerPlayer = new Timer();
			timerPlayer.Interval = 30;
			timerPlayer.Tick += TimerPlayer_Tick;
			timerPlayer.Start();
		}

		private void TankEngine_OnBitmapShowEvent(object sender, ClassicGameEventArgs e)
		{
			pictureBox1.Image = e.RenderBitmap;
		}

		protected override bool ProcessDialogKey(Keys keyData)
		{
			switch (keyData)
			{
			case Keys.Left:
				if (userTankMoveDirection != 3)
				{
					userTankMoveDirection = 3;
					tankEngine.MoveLeft();
				}
				break;
			case Keys.Right:
				if (userTankMoveDirection != 1)
				{
					userTankMoveDirection = 1;
					tankEngine.MoveRight();
				}
				break;
			case Keys.Up:
				if (userTankMoveDirection != 0)
				{
					userTankMoveDirection = 0;
					tankEngine.MoveUp();
				}
				break;
			case Keys.Down:
				if (userTankMoveDirection != 2)
				{
					userTankMoveDirection = 2;
					tankEngine.MoveDown();
				}
				break;
			case Keys.X:
				if (bulletTimeStamp > 20)
				{
					tankEngine.CreateUserBullet();
					bulletTimeStamp = 0;
				}
				break;
			}
			return base.ProcessDialogKey(keyData);
		}

		private void HslTank_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Right || e.KeyCode == Keys.Down || e.KeyCode == Keys.Left)
			{
				userTankMoveDirection = -1;
			}
		}

		private void TimerPlayer_Tick(object sender, EventArgs e)
		{
			bulletSpeed++;
			if (bulletSpeed >= 3)
			{
				tankEngine.MoveBullet();
				bulletSpeed = 0;
			}
			userTankMoveSpeed++;
			if (userTankMoveSpeed >= 3)
			{
				userTankMoveSpeed = 0;
				if (userTankMoveDirection == 0)
				{
					tankEngine.MoveUp();
				}
				else if (userTankMoveDirection == 1)
				{
					tankEngine.MoveRight();
				}
				else if (userTankMoveDirection == 2)
				{
					tankEngine.MoveDown();
				}
				else if (userTankMoveDirection == 3)
				{
					tankEngine.MoveLeft();
				}
			}
			enemyMoveSpeed++;
			if (enemyMoveSpeed > 20)
			{
				tankEngine.EnemyMoveRandom();
				enemyMoveSpeed = 0;
			}
			bulletTimeStamp++;
			enemyBulletTimeStamp++;
			if (enemyBulletTimeStamp >= 20)
			{
				enemyBulletTimeStamp = 0;
				tankEngine.EnemyFire();
			}
			createEnemyTankSpeed++;
			if (createEnemyTankSpeed > 100)
			{
				createEnemyTankSpeed = 0;
				if (tankEngine.GetEnemyCount() < 10)
				{
					tankEngine.CreateRandomEnemies(1);
				}
				label1.Text = "以消灭：" + tankEngine.GetEliminateCount;
			}
			count++;
			if (count > 300)
			{
				tankEngine.RemoveProtection();
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
			this.label1 = new System.Windows.Forms.Label();
			((System.ComponentModel.ISupportInitialize)this.pictureBox1).BeginInit();
			base.SuspendLayout();
			this.pictureBox1.Location = new System.Drawing.Point(3, 3);
			this.pictureBox1.Name = "pictureBox1";
			this.pictureBox1.Size = new System.Drawing.Size(286, 186);
			this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
			this.pictureBox1.TabIndex = 0;
			this.pictureBox1.TabStop = false;
			this.label1.AutoSize = true;
			this.label1.Font = new System.Drawing.Font("微软雅黑", 12f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.label1.Location = new System.Drawing.Point(3, 463);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(102, 21);
			this.label1.TabIndex = 1;
			this.label1.Text = "正在游戏中...";
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.Controls.Add(this.label1);
			base.Controls.Add(this.pictureBox1);
			base.Name = "HslTank";
			base.Size = new System.Drawing.Size(845, 503);
			base.Load += new System.EventHandler(HslTank_Load);
			base.KeyUp += new System.Windows.Forms.KeyEventHandler(HslTank_KeyUp);
			((System.ComponentModel.ISupportInitialize)this.pictureBox1).EndInit();
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
