using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace HslControls.GameSupport
{
	public class TetrisEngine : ClassicGameEngine
	{
		private List<Point[]> list = new List<Point[]>
		{
			new Point[4]
			{
				new Point(0, 0),
				new Point(0, 1),
				new Point(1, 1),
				new Point(1, 0)
			},
			new Point[4]
			{
				new Point(0, 0),
				new Point(0, 1),
				new Point(0, 2),
				new Point(0, 3)
			},
			new Point[4]
			{
				new Point(0, 0),
				new Point(0, 1),
				new Point(1, 1),
				new Point(1, 2)
			},
			new Point[4]
			{
				new Point(0, 2),
				new Point(0, 1),
				new Point(1, 1),
				new Point(1, 0)
			},
			new Point[4]
			{
				new Point(0, 2),
				new Point(1, 2),
				new Point(1, 1),
				new Point(1, 0)
			},
			new Point[4]
			{
				new Point(0, 0),
				new Point(1, 0),
				new Point(1, 1),
				new Point(2, 0)
			},
			new Point[4]
			{
				new Point(0, 0),
				new Point(0, 1),
				new Point(0, 2),
				new Point(1, 2)
			}
		};

		private Random random = new Random();

		public MoveGameItem MoveTeries { get; set; }

		public int PlayerScore { get; set; }

		public TetrisEngine(int pixelX, int pixelY)
			: base(pixelX, pixelY)
		{
			MoveTeries = new MoveGameItem(new Point[4]
			{
				new Point(0, 0),
				new Point(0, 1),
				new Point(1, 1),
				new Point(1, 2)
			}, new Point(base.PixelCol / 2 - 2, 20), new Size(base.PixelCol, base.PixelRow), OnMoveObjectSuccess);
		}

		protected override void DrawCustomer(Graphics g)
		{
			base.DrawCustomer(g);
			DrawGameItem(g, MoveTeries);
		}

		private int ClearLines()
		{
			int num = 0;
			for (int i = 0; i < base.PixelRow; i++)
			{
				while (IsRowAllHasItem(i))
				{
					num++;
					LinesMoveDown(i);
				}
			}
			return num;
		}

		private bool IsGameOver()
		{
			for (int i = 0; i < base.PixelCol; i++)
			{
				if (gameArrays[i, base.PixelRow - 1].HasItem)
				{
					return true;
				}
			}
			return false;
		}

		private int GetScoreFromLinesClear(int line)
		{
			switch (line)
			{
			case 0:
				return 0;
			case 1:
				return 1;
			case 2:
				return 3;
			case 3:
				return 6;
			case 4:
				return 10;
			default:
				return 0;
			}
		}

		private bool OnMoveObjectSuccess(Point[] points)
		{
			for (int i = 0; i < points.Length; i++)
			{
				if (points[i].X >= 0 && points[i].X < base.PixelCol && points[i].Y >= 0 && points[i].Y < base.PixelRow + 4)
				{
					if (points[i].Y < base.PixelRow && gameArrays[points[i].X, points[i].Y].HasItem)
					{
						return false;
					}
					continue;
				}
				return false;
			}
			return true;
		}

		public void MoveLeft()
		{
			MoveTeries.MoveLeft();
			OnBitmapShow(GetRenderBitmap());
		}

		public void MoveRight()
		{
			MoveTeries.MoveRight();
			OnBitmapShow(GetRenderBitmap());
		}

		public void RotationChange()
		{
			MoveTeries.RotationChange();
			OnBitmapShow(GetRenderBitmap());
		}

		public int MoveDown()
		{
			if (OnMoveObjectSuccess(MoveTeries.GetDownPoints()))
			{
				MoveTeries.MoveDown();
				OnBitmapShow(GetRenderBitmap());
				return 0;
			}
			SetPointFixed(MoveTeries);
			MoveTeries.Location = MoveTeries.LocationDefault;
			int line = ClearLines();
			if (!IsGameOver())
			{
				PlayerScore += GetScoreFromLinesClear(line);
				OnBitmapShow(GetRenderBitmap());
				return 1;
			}
			return 2;
		}

		public Point[] GetNewItemPoints()
		{
			return list[random.Next(list.Count)].ToArray();
		}
	}
}
