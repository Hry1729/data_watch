using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace HslControls.GameSupport
{
	public class MoveGameItem
	{
		private Point[] activePoints;

		private Size boardSize;

		private Func<Point[], bool> moveJudge = null;

		private int direction = 0;

		private int rank = 3;

		private static Random random = new Random();

		public Color PaintColor { get; set; } = Color.Black;

		public Point LocationDefault { get; set; }

		public Point Location { get; set; }

		public int Direction
		{
			get
			{
				return direction;
			}
			set
			{
				direction = value;
			}
		}

		public MoveGameItem(Point[] pt, Point locationDefault, Size board, Func<Point[], bool> moveJudge)
		{
			activePoints = pt;
			boardSize = board;
			LocationDefault = locationDefault;
			Location = locationDefault;
			this.moveJudge = moveJudge;
			CalculatePointsRank();
		}

		public MoveGameItem(MoveGameItem tp)
		{
			boardSize = default(Size);
			Location = LocationDefault;
			SetNewPoints(tp.GetItemPoints());
			random = new Random();
		}

		public Point[] GetActualPoints()
		{
			Point[] array = new Point[activePoints.Length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = new Point(activePoints[i].X + Location.X, activePoints[i].Y + Location.Y);
			}
			return array;
		}

		public Point[] GetItemPoints()
		{
			return activePoints.ToArray();
		}

		public void SetNewPoints(Point[] points)
		{
			Location = LocationDefault;
			activePoints = new Point[points.Length];
			for (int i = 0; i < activePoints.Length; i++)
			{
				activePoints[i] = new Point(points[i].X, points[i].Y);
			}
			direction = 0;
			CalculatePointsRank();
		}

		public bool MoveRandom()
		{
			if (random.Next(100) < 80)
			{
				return MoveNext();
			}
			List<int> list = new List<int> { 0, 1, 2, 3 };
			while (list.Count > 0)
			{
				int index = random.Next(list.Count);
				if (TurnDirection(list[index]))
				{
					return true;
				}
				list.RemoveAt(index);
			}
			return false;
		}

		public bool MoveLeft()
		{
			bool result = false;
			if (moveJudge(GetLeftPoints()))
			{
				Location = new Point(Location.X - 1, Location.Y);
				result = true;
			}
			return result;
		}

		public bool TurnLeft()
		{
			bool result = false;
			if (direction != 3 && moveJudge(GetTurnLeftPoints()))
			{
				activePoints = RotationChangePoints(3 - direction);
				while (RightXPoint(GetActualPoints()))
				{
					Location = new Point(Location.X - 1, Location.Y);
				}
				direction = 3;
				result = true;
			}
			return result;
		}

		public bool MoveRight()
		{
			bool result = false;
			if (moveJudge(GetRightPoints()))
			{
				Location = new Point(Location.X + 1, Location.Y);
				result = true;
			}
			return result;
		}

		public bool TurnRight()
		{
			bool result = false;
			if (direction != 1 && moveJudge(GetTurnRightPoints()))
			{
				activePoints = RotationChangePoints((direction > 1) ? (5 - direction) : (1 - direction));
				while (RightXPoint(GetActualPoints()))
				{
					Location = new Point(Location.X - 1, Location.Y);
				}
				direction = 1;
				result = true;
			}
			return result;
		}

		public bool MoveUp()
		{
			bool result = false;
			if (moveJudge(GetUpPoints()))
			{
				Location = new Point(Location.X, Location.Y + 1);
				result = true;
			}
			return result;
		}

		public bool TurnUp()
		{
			bool result = false;
			if (direction != 0 && moveJudge(GetTurnUpPoints()))
			{
				activePoints = RotationChangePoints(4 - direction);
				while (RightXPoint(GetActualPoints()))
				{
					Location = new Point(Location.X - 1, Location.Y);
				}
				direction = 0;
				result = true;
			}
			return result;
		}

		public bool MoveDown()
		{
			bool result = false;
			if (moveJudge(GetDownPoints()))
			{
				Location = new Point(Location.X, Location.Y - 1);
				result = true;
			}
			return result;
		}

		public bool TurnDown()
		{
			bool result = false;
			if (direction != 2 && moveJudge(GetTurnDownPoints()))
			{
				activePoints = RotationChangePoints((direction > 2) ? (6 - direction) : (2 - direction));
				while (RightXPoint(GetActualPoints()))
				{
					Location = new Point(Location.X - 1, Location.Y);
				}
				direction = 2;
				result = true;
			}
			return result;
		}

		public bool MoveNext()
		{
			switch (direction)
			{
			case 0:
				return MoveUp();
			case 1:
				return MoveRight();
			case 2:
				return MoveDown();
			case 3:
				return MoveLeft();
			default:
				return false;
			}
		}

		public bool TurnDirection(int direction)
		{
			switch (direction)
			{
			case 0:
				return TurnUp();
			case 1:
				return TurnRight();
			case 2:
				return TurnDown();
			case 3:
				return TurnLeft();
			default:
				return false;
			}
		}

		public bool RotationChange()
		{
			bool result = false;
			if (moveJudge(GetRotationPoints(1)))
			{
				activePoints = RotationChangePoints(1);
				while (RightXPoint(GetActualPoints()))
				{
					Location = new Point(Location.X - 1, Location.Y);
				}
				direction++;
				if (direction >= 4)
				{
					direction = 0;
				}
				result = true;
			}
			return result;
		}

		public Point[] GetUpPoints()
		{
			Point[] array = new Point[activePoints.Length];
			for (int i = 0; i < activePoints.Length; i++)
			{
				array[i] = new Point(activePoints[i].X + Location.X, activePoints[i].Y + Location.Y + 1);
			}
			return array;
		}

		public Point[] GetDownPoints()
		{
			Point[] array = new Point[activePoints.Length];
			for (int i = 0; i < activePoints.Length; i++)
			{
				array[i] = new Point(activePoints[i].X + Location.X, activePoints[i].Y + Location.Y - 1);
			}
			return array;
		}

		public Point[] GetLeftPoints()
		{
			Point[] array = new Point[activePoints.Length];
			for (int i = 0; i < activePoints.Length; i++)
			{
				array[i] = new Point(activePoints[i].X + Location.X - 1, activePoints[i].Y + Location.Y);
			}
			return array;
		}

		public Point[] GetRightPoints()
		{
			Point[] array = new Point[activePoints.Length];
			for (int i = 0; i < activePoints.Length; i++)
			{
				array[i] = new Point(activePoints[i].X + Location.X + 1, activePoints[i].Y + Location.Y);
			}
			return array;
		}

		public Point[] GetRotationPoints(int rate)
		{
			Point[] array = RotationChangePoints(rate);
			for (int i = 0; i < activePoints.Length; i++)
			{
				array[i] = new Point(array[i].X + Location.X, array[i].Y + Location.Y);
			}
			while (RightXPoint(array))
			{
				for (int j = 0; j < array.Length; j++)
				{
					array[j].X--;
				}
			}
			return array;
		}

		public Point[] GetTurnUpPoints()
		{
			return GetRotationPoints((direction > 0) ? (4 - direction) : (-direction));
		}

		public Point[] GetTurnRightPoints()
		{
			return GetRotationPoints((direction > 1) ? (5 - direction) : (1 - direction));
		}

		public Point[] GetTurnDownPoints()
		{
			return GetRotationPoints((direction > 2) ? (6 - direction) : (2 - direction));
		}

		public Point[] GetTurnLeftPoints()
		{
			return GetRotationPoints(3 - direction);
		}

		public bool IsPositionInMoveItem(Point point)
		{
			for (int i = 0; i < activePoints.Length; i++)
			{
				if (point.X == activePoints[i].X + Location.X && point.Y == activePoints[i].Y + Location.Y)
				{
					return true;
				}
			}
			return false;
		}

		public bool IsPositionInMoveItem(Point[] points)
		{
			for (int i = 0; i < points.Length; i++)
			{
				if (IsPositionInMoveItem(points[i]))
				{
					return true;
				}
			}
			return false;
		}

		private bool ResetXPoint(Point[] points)
		{
			for (int i = 0; i < points.Length; i++)
			{
				if (points[i].X == 0)
				{
					return false;
				}
			}
			return true;
		}

		private bool ResetYPoint(Point[] points)
		{
			for (int i = 0; i < points.Length; i++)
			{
				if (points[i].Y == 0)
				{
					return false;
				}
			}
			return true;
		}

		private bool RightXPoint(Point[] points)
		{
			for (int i = 0; i < points.Length; i++)
			{
				if (points[i].X >= boardSize.Width)
				{
					return true;
				}
			}
			return false;
		}

		private Point[] RotationChangePoints(int rate)
		{
			Point[] array = new Point[activePoints.Length];
			for (int i = 0; i < activePoints.Length; i++)
			{
				array[i] = new Point(activePoints[i].X, activePoints[i].Y);
			}
			for (int j = 0; j < rate; j++)
			{
				Point[] array2 = new Point[array.Length];
				for (int k = 0; k < array.Length; k++)
				{
					array2[k] = new Point(array[k].Y, rank - array[k].X);
				}
				while (ResetXPoint(array2))
				{
					for (int l = 0; l < array2.Length; l++)
					{
						array2[l].X--;
					}
				}
				while (ResetYPoint(array2))
				{
					for (int m = 0; m < array2.Length; m++)
					{
						array2[m].Y--;
					}
				}
				array = array2;
			}
			return array;
		}

		private void CalculatePointsRank()
		{
			rank = 0;
			for (int i = 0; i < activePoints.Length; i++)
			{
				if (rank < activePoints[i].X)
				{
					rank = activePoints[i].X;
				}
				if (rank < activePoints[i].Y)
				{
					rank = activePoints[i].Y;
				}
			}
			if (rank == 0)
			{
				rank = 1;
			}
		}
	}
}
