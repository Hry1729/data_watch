using System;
using System.Drawing;

namespace HslControls.GameSupport
{
	public class ClassicGameEngine
	{
		private int playerScore = 0;

		private bool isGameStart = false;

		private int widthOfItem = 20;

		private readonly int pixelCol = 0;

		private readonly int pixelRow = 0;

		private Bitmap bitmap = null;

		private Graphics graphics = null;

		protected ClassicGameItem[,] gameArrays = null;

		protected int PixelCol => pixelCol;

		protected int PixelRow => pixelRow;

		public event EventHandler<ClassicGameEventArgs> OnBitmapShowEvent;

		public ClassicGameEngine(int pixelX, int pixelY)
		{
			pixelCol = pixelX;
			pixelRow = pixelY;
			gameArrays = new ClassicGameItem[pixelCol, pixelRow];
			for (int i = 0; i < pixelCol; i++)
			{
				for (int j = 0; j < pixelRow; j++)
				{
					gameArrays[i, j] = new ClassicGameItem();
				}
			}
			GameArrayAction(delegate(ClassicGameItem t)
			{
				t.ItemColor = Color.Black;
			});
			bitmap = new Bitmap(pixelCol * (widthOfItem + 1), pixelRow * (widthOfItem + 1));
			graphics = Graphics.FromImage(bitmap);
		}

		public void GameArrayAction(Action<ClassicGameItem> action)
		{
			for (int i = 0; i < pixelCol; i++)
			{
				for (int j = 0; j < pixelRow; j++)
				{
					action(gameArrays[i, j]);
				}
			}
		}

		public void GameArraysReset()
		{
			GameArrayAction(delegate(ClassicGameItem m)
			{
				m.HasItem = false;
			});
		}

		public void GameArraysColReset(int col, bool hasItem)
		{
			if (col >= 0 && col < pixelCol)
			{
				for (int i = 0; i < pixelRow; i++)
				{
					gameArrays[col, i].HasItem = hasItem;
				}
			}
		}

		public void GameArraysRowReset(int row, bool hasItem)
		{
			if (row >= 0 && row < pixelRow)
			{
				for (int i = 0; i < pixelCol; i++)
				{
					gameArrays[i, row].HasItem = hasItem;
				}
			}
		}

		public void SetPointFixed(Point[] points)
		{
			for (int i = 0; i < points.Length; i++)
			{
				if (points[i].X >= 0 && points[i].X < pixelCol && points[i].Y >= 0 && points[i].Y < pixelRow)
				{
					gameArrays[points[i].X, points[i].Y].HasItem = true;
				}
			}
		}

		public void SetPointFixed(MoveGameItem gameItem)
		{
			if (gameItem != null)
			{
				Point[] actualPoints = gameItem.GetActualPoints();
				for (int i = 0; i < actualPoints.Length; i++)
				{
					if (actualPoints[i].X >= 0 && actualPoints[i].X < pixelCol && actualPoints[i].Y >= 0 && actualPoints[i].Y < pixelRow)
					{
						gameArrays[actualPoints[i].X, actualPoints[i].Y].HasItem = true;
						gameArrays[actualPoints[i].X, actualPoints[i].Y].ItemColor = gameItem.PaintColor;
					}
				}
			}
			else
			{
				Console.WriteLine("ClassicGameEngine:SetPointFixed:gameItem is NULL");
			}
		}

		protected bool IsRowAllHasItem(int row)
		{
			for (int i = 0; i < PixelCol; i++)
			{
				if (!gameArrays[i, row].HasItem)
				{
					return false;
				}
			}
			return true;
		}

		protected bool IsRowAllEmpty(int row)
		{
			for (int i = 0; i < PixelCol; i++)
			{
				if (gameArrays[i, row].HasItem)
				{
					return false;
				}
			}
			return true;
		}

		protected bool IsColAllHasItem(int col)
		{
			for (int i = 0; i < PixelRow; i++)
			{
				if (!gameArrays[col, i].HasItem)
				{
					return false;
				}
			}
			return true;
		}

		protected bool IsColAllEmpty(int col)
		{
			for (int i = 0; i < PixelRow; i++)
			{
				if (gameArrays[col, i].HasItem)
				{
					return false;
				}
			}
			return true;
		}

		protected void LinesMoveDown(int rowIndex)
		{
			for (int i = rowIndex; i < PixelRow; i++)
			{
				if (i < PixelRow - 1)
				{
					for (int j = 0; j < PixelCol; j++)
					{
						gameArrays[j, i].HasItem = gameArrays[j, i + 1].HasItem;
						gameArrays[j, i].ItemColor = gameArrays[j, i + 1].ItemColor;
						gameArrays[j, i].ItemCode = gameArrays[j, i + 1].ItemCode;
					}
				}
				else
				{
					for (int k = 0; k < PixelCol; k++)
					{
						gameArrays[k, i].HasItem = false;
					}
				}
			}
		}

		protected void DrawGameItem(Graphics g, int x, int y)
		{
			if (x < 0 || x >= pixelCol || y < 0 || y >= pixelRow)
			{
				return;
			}
			int num = x * (widthOfItem + 1);
			int num2 = (pixelRow - y - 1) * (widthOfItem + 1);
			if (gameArrays[x, y].HasItem)
			{
				using (Brush brush = new SolidBrush(gameArrays[x, y].ItemColor))
				{
					g.FillRectangle(brush, new Rectangle(num, num2, widthOfItem, widthOfItem));
					g.FillRectangle(Brushes.LightGray, new Rectangle(num + 2, num2 + 2, widthOfItem - 4, widthOfItem - 4));
					g.FillRectangle(brush, new Rectangle(num + widthOfItem / 4, num2 + widthOfItem / 4, widthOfItem / 2, widthOfItem / 2));
					return;
				}
			}
			g.FillRectangle(Brushes.LightGray, new Rectangle(num, num2, widthOfItem, widthOfItem));
			g.FillRectangle(Brushes.WhiteSmoke, new Rectangle(num + 2, num2 + 2, widthOfItem - 4, widthOfItem - 4));
			g.FillRectangle(Brushes.LightGray, new Rectangle(num + widthOfItem / 4, num2 + widthOfItem / 4, widthOfItem / 2, widthOfItem / 2));
		}

		protected void DrawGameItem(Graphics g, int x, int y, Color color)
		{
			if (x >= 0 && x < pixelCol && y >= 0 && y < pixelRow)
			{
				int num = x * (widthOfItem + 1);
				int num2 = (pixelRow - y - 1) * (widthOfItem + 1);
				using (Brush brush = new SolidBrush(color))
				{
					g.FillRectangle(brush, new Rectangle(num, num2, widthOfItem, widthOfItem));
					g.FillRectangle(Brushes.LightGray, new Rectangle(num + 2, num2 + 2, widthOfItem - 4, widthOfItem - 4));
					g.FillRectangle(brush, new Rectangle(num + widthOfItem / 4, num2 + widthOfItem / 4, widthOfItem / 2, widthOfItem / 2));
				}
			}
		}

		protected void DrawGameItem(Graphics g, Point[] points, Color color)
		{
			if (points != null)
			{
				for (int i = 0; i < points.Length; i++)
				{
					DrawGameItem(g, points[i].X, points[i].Y, color);
				}
			}
			else
			{
				Console.WriteLine("ClassicGameEngine:DrawGameItem:points is null");
			}
		}

		protected void DrawGameItem(Graphics g, MoveGameItem gameItem)
		{
			if (gameItem != null)
			{
				DrawGameItem(g, gameItem.GetActualPoints(), gameItem.PaintColor);
			}
			else
			{
				Console.WriteLine("ClassicGameEngine:DrawGameItem:gameItem is null");
			}
		}

		protected void DrawBackgroundImage(Graphics g)
		{
			for (int i = 0; i < pixelCol; i++)
			{
				for (int j = 0; j < pixelRow; j++)
				{
					DrawGameItem(g, i, j);
				}
			}
		}

		protected virtual void DrawCustomer(Graphics g)
		{
		}

		public Bitmap GetRenderBitmap()
		{
			graphics.Clear(SystemColors.Control);
			DrawBackgroundImage(graphics);
			DrawCustomer(graphics);
			return bitmap;
		}

		protected void OnBitmapShow(Bitmap bitmap)
		{
			this.OnBitmapShowEvent?.Invoke(this, new ClassicGameEventArgs
			{
				RenderBitmap = bitmap
			});
		}

		protected void OnBitmapShow()
		{
			this.OnBitmapShowEvent?.Invoke(this, new ClassicGameEventArgs
			{
				RenderBitmap = GetRenderBitmap()
			});
		}
	}
}
