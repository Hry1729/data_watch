using System;
using System.Collections.Generic;
using System.Drawing;

namespace HslControls.GameSupport
{
	public class TankEngine : ClassicGameEngine
	{
		private MoveGameItem userTank;

		private List<MoveGameItem> bullets;

		private List<MoveGameItem> enemies;

		private List<MoveGameItem> enemyBullets;

		private int eliminateCount = 0;

		public MoveGameItem UserTank => userTank;

		public Action OnGameOver { get; set; }

		public int GetEliminateCount => eliminateCount;

		public TankEngine(int pixelCol, int pixelRow)
			: base(pixelCol, pixelRow)
		{
			userTank = CreateTankFromPosition(new Point(base.PixelCol / 2 - 1, 0));
			userTank.PaintColor = Color.Blue;
			bullets = new List<MoveGameItem>();
			enemies = new List<MoveGameItem>();
			enemyBullets = new List<MoveGameItem>();
			for (int i = 10; i < 30; i++)
			{
				gameArrays[i, 8].HasItem = true;
				gameArrays[i, 8].ItemColor = Color.Tomato;
			}
			CreateRandomEnemies();
		}

		private bool OnMoveTankSuccess(Point[] points)
		{
			for (int i = 0; i < points.Length; i++)
			{
				if (points[i].X >= 0 && points[i].X < base.PixelCol && points[i].Y >= 0 && points[i].Y < base.PixelRow)
				{
					if (gameArrays[points[i].X, points[i].Y].HasItem)
					{
						return false;
					}
					continue;
				}
				return false;
			}
			return true;
		}

		private bool OnMoveBulletSuccess(Point[] points)
		{
			for (int i = 0; i < points.Length; i++)
			{
				if (points[i].X >= 0 && points[i].X < base.PixelCol && points[i].Y >= 0 && points[i].Y < base.PixelRow)
				{
					if (gameArrays[points[i].X, points[i].Y].HasItem)
					{
						return false;
					}
					continue;
				}
				return false;
			}
			return true;
		}

		protected override void DrawCustomer(Graphics g)
		{
			base.DrawCustomer(g);
			DrawGameItem(g, userTank);
			for (int i = 0; i < enemies.Count; i++)
			{
				DrawGameItem(g, enemies[i]);
			}
			for (int num = bullets.Count - 1; num >= 0; num--)
			{
				DrawGameItem(g, bullets[num]);
			}
			for (int num2 = enemyBullets.Count - 1; num2 >= 0; num2--)
			{
				DrawGameItem(g, enemyBullets[num2]);
			}
		}

		public void MoveLeft()
		{
			if (!userTank.TurnLeft())
			{
				userTank.MoveLeft();
			}
			OnBitmapShow(GetRenderBitmap());
		}

		public void MoveRight()
		{
			if (!userTank.TurnRight())
			{
				userTank.MoveRight();
			}
			OnBitmapShow(GetRenderBitmap());
		}

		public void MoveUp()
		{
			if (!userTank.TurnUp())
			{
				userTank.MoveUp();
			}
			OnBitmapShow(GetRenderBitmap());
		}

		public void MoveDown()
		{
			if (!userTank.TurnDown())
			{
				userTank.MoveDown();
			}
			OnBitmapShow(GetRenderBitmap());
		}

		public MoveGameItem CreateTankFromPosition(Point position)
		{
			return new MoveGameItem(new Point[6]
			{
				new Point(0, 0),
				new Point(0, 1),
				new Point(1, 1),
				new Point(1, 2),
				new Point(2, 1),
				new Point(2, 0)
			}, position, new Size(base.PixelCol, base.PixelRow), OnMoveTankSuccess);
		}

		public MoveGameItem CreateBulletFromTank(MoveGameItem tank)
		{
			Point locationDefault = default(Point);
			switch (tank.Direction)
			{
			case 0:
				locationDefault = new Point(tank.Location.X + 1, tank.Location.Y + 2);
				break;
			case 1:
				locationDefault = new Point(tank.Location.X + 2, tank.Location.Y + 1);
				break;
			case 2:
				locationDefault = new Point(tank.Location.X + 1, tank.Location.Y);
				break;
			case 3:
				locationDefault = new Point(tank.Location.X, tank.Location.Y + 1);
				break;
			}
			return new MoveGameItem(new Point[1]
			{
				new Point(0, 0)
			}, locationDefault, new Size(base.PixelCol, base.PixelRow), OnMoveBulletSuccess)
			{
				Direction = tank.Direction,
				PaintColor = tank.PaintColor
			};
		}

		public void CreateUserBullet()
		{
			MoveGameItem item = CreateBulletFromTank(userTank);
			bullets.Add(item);
		}

		public void MoveBullet()
		{
			for (int num = bullets.Count - 1; num >= 0; num--)
			{
				if (!bullets[num].MoveNext())
				{
					bullets.RemoveAt(num);
				}
				else
				{
					for (int num2 = enemies.Count - 1; num2 >= 0; num2--)
					{
						if (enemies[num2].IsPositionInMoveItem(bullets[num].GetActualPoints()))
						{
							enemies.RemoveAt(num2);
							bullets.RemoveAt(num);
							eliminateCount++;
							break;
						}
					}
				}
			}
			for (int num3 = enemyBullets.Count - 1; num3 >= 0; num3--)
			{
				if (!enemyBullets[num3].MoveNext())
				{
					enemyBullets.RemoveAt(num3);
				}
				else if (userTank.IsPositionInMoveItem(enemyBullets[num3].GetActualPoints()))
				{
					OnGameOver?.Invoke();
				}
			}
			OnBitmapShow(GetRenderBitmap());
		}

		private bool IsAreaEmpty(int col)
		{
			for (int i = 0; i < enemies.Count; i++)
			{
				if (enemies[i].Location.X > col - 3 && enemies[i].Location.X < col + 3 && enemies[i].Location.Y > base.PixelRow - 3)
				{
					return false;
				}
			}
			return true;
		}

		public void CreateRandomEnemies(int count = 100)
		{
			int num = 0;
			for (int i = 0; i < base.PixelCol - 3; i += 3)
			{
				if (IsAreaEmpty(i))
				{
					MoveGameItem moveGameItem = CreateTankFromPosition(new Point(i, base.PixelRow - 3));
					moveGameItem.TurnDown();
					enemies.Add(moveGameItem);
					num++;
				}
				if (num > count)
				{
					break;
				}
			}
		}

		public void RemoveProtection()
		{
			for (int i = 10; i < 30; i++)
			{
				gameArrays[i, 8].HasItem = false;
			}
		}

		public void EnemyFire()
		{
			for (int i = 0; i < enemies.Count; i++)
			{
				MoveGameItem item = CreateBulletFromTank(enemies[i]);
				enemyBullets.Add(item);
			}
		}

		public void EnemyMoveRandom()
		{
			for (int i = 0; i < enemies.Count; i++)
			{
				enemies[i].MoveRandom();
			}
		}

		public int GetEnemyCount()
		{
			return enemies.Count;
		}
	}
}
