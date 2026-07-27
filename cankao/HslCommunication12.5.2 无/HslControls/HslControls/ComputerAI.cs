using System.Collections.Generic;
using System.Drawing;

namespace HslControls
{
	internal class ComputerAI
	{
		private GobangPoint[,] main_chess = null;

		public ComputerAI(GobangPoint[,] chess)
		{
			main_chess = chess;
		}

		public void CalculateAllPoints()
		{
			for (int i = 0; i < 21; i++)
			{
				for (int j = 0; j < 21; j++)
				{
					main_chess[i, j].WeightScore = GetWeight(i, j, GobangPlayer.Player2).TotleScore;
				}
			}
		}

		public Point CalculateComputerAI()
		{
			ChessWeight[,] array = new ChessWeight[21, 21];
			ChessWeight[,] array2 = new ChessWeight[21, 21];
			for (int i = 0; i <= 20; i++)
			{
				for (int j = 0; j <= 20; j++)
				{
					if (main_chess[i, j].GobangPlayer != GobangPlayer.NonePlayer)
					{
						array[i, j] = new ChessWeight(new int[4] { -1, -1, -1, -1 }, GobangPlayer.Player1);
						array2[i, j] = new ChessWeight(new int[4] { -1, -1, -1, -1 }, GobangPlayer.Player2);
					}
					else
					{
						array[i, j] = GetWeight(i, j, GobangPlayer.Player1);
						array2[i, j] = GetWeight(i, j, GobangPlayer.Player2);
						array[i, j].WeightOpponent = array2[i, j].WeightMax;
						array2[i, j].WeightOpponent = array[i, j].WeightMax;
					}
				}
			}
			List<Point> list = new List<Point>();
			int num = 0;
			int num2 = 0;
			List<Point> list2 = new List<Point>();
			for (int k = 0; k <= 20; k++)
			{
				for (int l = 0; l <= 20; l++)
				{
					if (array[k, l].TotleScore > num)
					{
						num = array[k, l].TotleScore;
						list.Clear();
						list.Add(new Point(k, l));
					}
					else if (array[k, l].TotleScore == num)
					{
						list.Add(new Point(k, l));
					}
					if (array2[k, l].TotleScore > num2)
					{
						num2 = array2[k, l].TotleScore;
						list2.Clear();
						list2.Add(new Point(k, l));
					}
					else if (array2[k, l].TotleScore == num2)
					{
						list2.Add(new Point(k, l));
					}
				}
			}
			if ((num > 49 && num2 < 200) || (num >= 200 && num2 >= 200))
			{
				int num3 = 0;
				Point result = default(Point);
				for (int m = 0; m < list.Count; m++)
				{
					if (array[list[m].X, list[m].Y].WeightOpponent > num3)
					{
						num3 = array[list[m].X, list[m].Y].WeightOpponent;
						result = list[m];
					}
				}
				return result;
			}
			if (num >= num2)
			{
				int num4 = 0;
				Point result2 = default(Point);
				for (int n = 0; n < list.Count; n++)
				{
					if (array[list[n].X, list[n].Y].WeightOpponent > num4)
					{
						num4 = array[list[n].X, list[n].Y].WeightOpponent;
						result2 = list[n];
					}
				}
				return result2;
			}
			int num5 = 0;
			Point result3 = default(Point);
			for (int num6 = 0; num6 < list2.Count; num6++)
			{
				if (array2[list2[num6].X, list2[num6].Y].WeightOpponent > num5)
				{
					num5 = array2[list2[num6].X, list2[num6].Y].WeightOpponent;
					result3 = list2[num6];
				}
			}
			return result3;
		}

		private GobangPoint GetClassPoint(int x, int y, GobangPlayer m_play)
		{
			if (x >= 0 && x < 21 && y >= 0 && y < 21)
			{
				return main_chess[x, y];
			}
			if (m_play == GobangPlayer.Player1)
			{
				return new GobangPoint
				{
					GobangPlayer = GobangPlayer.Player2
				};
			}
			return new GobangPoint
			{
				GobangPlayer = GobangPlayer.Player1
			};
		}

		private ChessWeight GetWeight(int m_x, int m_y, GobangPlayer m_play)
		{
			int[] array = new int[4];
			for (int i = 0; i < 4; i++)
			{
				array[i] = GetWeightSingle(i, m_x, m_y, m_play);
			}
			return new ChessWeight(array, m_play);
		}

		private int GetWeightSingle(int Direction, int x, int y, GobangPlayer player)
		{
			GobangPoint[] array = new GobangPoint[9];
			switch (Direction)
			{
			case 0:
			{
				for (int l = 0; l < 9; l++)
				{
					array[l] = GetClassPoint(x + l - 4, y, player);
				}
				break;
			}
			case 1:
			{
				for (int j = 0; j < 9; j++)
				{
					array[j] = GetClassPoint(x, y + j - 4, player);
				}
				break;
			}
			case 2:
			{
				for (int k = 0; k < 9; k++)
				{
					array[k] = GetClassPoint(x + k - 4, y + k - 4, player);
				}
				break;
			}
			default:
			{
				for (int i = 0; i < 9; i++)
				{
					array[i] = GetClassPoint(x - (i - 4), y + i - 4, player);
				}
				break;
			}
			}
			GobangPlayer gobangPlayer = GobangPlayer.Player1;
			if (player == GobangPlayer.Player1)
			{
				gobangPlayer = GobangPlayer.Player2;
			}
			GobangPoint gobangPoint = new GobangPoint
			{
				GobangPlayer = player
			};
			GobangPoint gobangPoint2 = new GobangPoint
			{
				GobangPlayer = gobangPlayer
			};
			if (array[1].IsEmpty && array[2].IsEmpty && array[3].IsEmpty && array[5].IsEmpty && array[6].IsEmpty && array[7].IsEmpty)
			{
				return 1;
			}
			if (array[0] == gobangPoint && array[1] == gobangPoint && array[2] == gobangPoint && array[3] == gobangPoint)
			{
				return 200;
			}
			if (array[1] == gobangPoint && array[2] == gobangPoint && array[3] == gobangPoint && array[5] == gobangPoint)
			{
				return 200;
			}
			if (array[2] == gobangPoint && array[3] == gobangPoint && array[5] == gobangPoint && array[6] == gobangPoint)
			{
				return 200;
			}
			if (array[3] == gobangPoint && array[5] == gobangPoint && array[6] == gobangPoint && array[7] == gobangPoint)
			{
				return 200;
			}
			if (array[5] == gobangPoint && array[6] == gobangPoint && array[7] == gobangPoint && array[8] == gobangPoint)
			{
				return 200;
			}
			if (array[0].IsEmpty && array[1] == gobangPoint && array[2] == gobangPoint && array[3] == gobangPoint && array[5].IsEmpty)
			{
				return 50;
			}
			if (array[1].IsEmpty && array[2] == gobangPoint && array[3] == gobangPoint && array[5] == gobangPoint && array[6].IsEmpty)
			{
				return 50;
			}
			if (array[2].IsEmpty && array[3] == gobangPoint && array[5] == gobangPoint && array[6] == gobangPoint && array[7].IsEmpty)
			{
				return 50;
			}
			if (array[3].IsEmpty && array[5] == gobangPoint && array[6] == gobangPoint && array[7] == gobangPoint && array[8].IsEmpty)
			{
				return 50;
			}
			if (array[1] == gobangPoint && array[2].IsEmpty && array[3] == gobangPoint && array[5] == gobangPoint && array[6].IsEmpty && array[7] == gobangPoint)
			{
				return 50;
			}
			if (array[0] == gobangPoint2 && array[1] == gobangPoint && array[2] == gobangPoint && array[3] == gobangPoint && array[5].IsEmpty)
			{
				return 12;
			}
			if (array[1] == gobangPoint2 && array[2] == gobangPoint && array[3] == gobangPoint && array[5] == gobangPoint && array[6].IsEmpty)
			{
				return 12;
			}
			if (array[2] == gobangPoint2 && array[3] == gobangPoint && array[5] == gobangPoint && array[6] == gobangPoint && array[7].IsEmpty)
			{
				return 12;
			}
			if (array[3] == gobangPoint2 && array[5] == gobangPoint && array[6] == gobangPoint && array[7] == gobangPoint && array[8].IsEmpty)
			{
				return 12;
			}
			if (array[0].IsEmpty && array[1] == gobangPoint && array[2] == gobangPoint && array[3] == gobangPoint && array[5] == gobangPoint2)
			{
				return 12;
			}
			if (array[1].IsEmpty && array[2] == gobangPoint && array[3] == gobangPoint && array[5] == gobangPoint && array[6] == gobangPoint2)
			{
				return 12;
			}
			if (array[2].IsEmpty && array[3] == gobangPoint && array[5] == gobangPoint && array[6] == gobangPoint && array[7] == gobangPoint2)
			{
				return 12;
			}
			if (array[3].IsEmpty && array[5] == gobangPoint && array[6] == gobangPoint && array[7] == gobangPoint && array[8] == gobangPoint2)
			{
				return 12;
			}
			if (array[0] == gobangPoint && array[1].IsEmpty && array[2] == gobangPoint && array[3] == gobangPoint)
			{
				return 11;
			}
			if (array[0] == gobangPoint && array[1] == gobangPoint && array[2].IsEmpty && array[3] == gobangPoint)
			{
				return 11;
			}
			if (array[1] == gobangPoint && array[2].IsEmpty && array[3] == gobangPoint && array[5] == gobangPoint)
			{
				return 11;
			}
			if (array[0] == gobangPoint && array[1] == gobangPoint && array[2] == gobangPoint && array[3].IsEmpty)
			{
				return 11;
			}
			if (array[1] == gobangPoint && array[2] == gobangPoint && array[3].IsEmpty && array[5] == gobangPoint)
			{
				return 11;
			}
			if (array[2] == gobangPoint && array[3].IsEmpty && array[5] == gobangPoint && array[6] == gobangPoint)
			{
				return 11;
			}
			if (array[5].IsEmpty && array[6] == gobangPoint && array[7] == gobangPoint && array[8] == gobangPoint)
			{
				return 11;
			}
			if (array[3] == gobangPoint && array[5].IsEmpty && array[6] == gobangPoint && array[7] == gobangPoint)
			{
				return 11;
			}
			if (array[2] == gobangPoint && array[3] == gobangPoint && array[5].IsEmpty && array[6] == gobangPoint)
			{
				return 11;
			}
			if (array[5] == gobangPoint && array[6].IsEmpty && array[7] == gobangPoint && array[8] == gobangPoint)
			{
				return 11;
			}
			if (array[3] == gobangPoint && array[5] == gobangPoint && array[6].IsEmpty && array[7] == gobangPoint)
			{
				return 11;
			}
			if (array[5] == gobangPoint && array[6] == gobangPoint && array[7].IsEmpty && array[8] == gobangPoint)
			{
				return 11;
			}
			if (array[0] == gobangPoint2 && array[1] == gobangPoint && array[2] == gobangPoint && array[3] == gobangPoint && array[5] == gobangPoint2)
			{
				return 0;
			}
			if (array[1] == gobangPoint2 && array[2] == gobangPoint && array[3] == gobangPoint && array[5] == gobangPoint && array[6] == gobangPoint2)
			{
				return 0;
			}
			if (array[2] == gobangPoint2 && array[3] == gobangPoint && array[5] == gobangPoint && array[6] == gobangPoint && array[7] == gobangPoint2)
			{
				return 0;
			}
			if (array[3] == gobangPoint2 && array[5] == gobangPoint && array[6] == gobangPoint && array[7] == gobangPoint && array[8] == gobangPoint2)
			{
				return 0;
			}
			if (array[0] == gobangPoint2 && array[1].IsEmpty && array[2] == gobangPoint && array[3] == gobangPoint && array[5].IsEmpty && array[6] == gobangPoint2)
			{
				return 7;
			}
			if (array[1] == gobangPoint2 && array[2].IsEmpty && array[3] == gobangPoint && array[5] == gobangPoint && array[6].IsEmpty && array[7] == gobangPoint2)
			{
				return 7;
			}
			if (array[2] == gobangPoint2 && array[3].IsEmpty && array[5] == gobangPoint && array[6] == gobangPoint && array[7].IsEmpty && array[8] == gobangPoint2)
			{
				return 7;
			}
			if (array[1].IsEmpty && array[2] == gobangPoint && array[3] == gobangPoint && array[5].IsEmpty)
			{
				return 10;
			}
			if (array[2].IsEmpty && array[3] == gobangPoint && array[5] == gobangPoint && array[6].IsEmpty)
			{
				return 10;
			}
			if (array[3].IsEmpty && array[5] == gobangPoint && array[6] == gobangPoint && array[7].IsEmpty)
			{
				return 10;
			}
			if (array[0].IsEmpty && array[1] == gobangPoint && array[2] == gobangPoint && array[3].IsEmpty && array[5].IsEmpty)
			{
				return 9;
			}
			if (array[0].IsEmpty && array[1] == gobangPoint && array[2].IsEmpty && array[3] == gobangPoint && array[5].IsEmpty)
			{
				return 9;
			}
			if (array[1].IsEmpty && array[2] == gobangPoint && array[3].IsEmpty && array[5] == gobangPoint && array[6].IsEmpty)
			{
				return 9;
			}
			if (array[2].IsEmpty && array[3] == gobangPoint && array[5].IsEmpty && array[6] == gobangPoint && array[7].IsEmpty)
			{
				return 9;
			}
			if (array[3].IsEmpty && array[5].IsEmpty && array[6] == gobangPoint && array[7] == gobangPoint && array[8].IsEmpty)
			{
				return 9;
			}
			if (array[3].IsEmpty && array[5] == gobangPoint && array[6].IsEmpty && array[7] == gobangPoint && array[8].IsEmpty)
			{
				return 9;
			}
			if (array[1] == gobangPoint2 && array[2] == gobangPoint && array[3] == gobangPoint && array[5].IsEmpty && array[6].IsEmpty)
			{
				return 8;
			}
			if (array[2] == gobangPoint2 && array[3] == gobangPoint && array[5] == gobangPoint && array[6].IsEmpty && array[7].IsEmpty)
			{
				return 8;
			}
			if (array[3] == gobangPoint2 && array[5] == gobangPoint && array[6] == gobangPoint && array[7].IsEmpty && array[8].IsEmpty)
			{
				return 8;
			}
			if (array[0].IsEmpty && array[1].IsEmpty && array[2] == gobangPoint && array[3] == gobangPoint && array[5] == gobangPoint2)
			{
				return 8;
			}
			if (array[1].IsEmpty && array[2].IsEmpty && array[3] == gobangPoint && array[5] == gobangPoint && array[6] == gobangPoint2)
			{
				return 8;
			}
			if (array[2].IsEmpty && array[3].IsEmpty && array[5] == gobangPoint && array[6] == gobangPoint && array[7] == gobangPoint2)
			{
				return 8;
			}
			if (array[0] == gobangPoint2 && array[1] == gobangPoint && array[2] == gobangPoint && array[3].IsEmpty && array[5].IsEmpty)
			{
				return 7;
			}
			if (array[0] == gobangPoint2 && array[1] == gobangPoint && array[2].IsEmpty && array[3] == gobangPoint && array[5].IsEmpty)
			{
				return 7;
			}
			if (array[1] == gobangPoint2 && array[2] == gobangPoint && array[3].IsEmpty && array[5] == gobangPoint && array[6].IsEmpty)
			{
				return 7;
			}
			if (array[2] == gobangPoint2 && array[3] == gobangPoint && array[5].IsEmpty && array[6] == gobangPoint && array[7].IsEmpty)
			{
				return 7;
			}
			if (array[3] == gobangPoint2 && array[5].IsEmpty && array[6] == gobangPoint && array[7] == gobangPoint && array[8].IsEmpty)
			{
				return 7;
			}
			if (array[3] == gobangPoint2 && array[5] == gobangPoint && array[6].IsEmpty && array[7] == gobangPoint && array[8].IsEmpty)
			{
				return 7;
			}
			if (array[0].IsEmpty && array[1] == gobangPoint && array[2] == gobangPoint && array[3].IsEmpty && array[5] == gobangPoint2)
			{
				return 7;
			}
			if (array[0].IsEmpty && array[1] == gobangPoint && array[3] == gobangPoint && array[2].IsEmpty && array[5] == gobangPoint2)
			{
				return 7;
			}
			if (array[1].IsEmpty && array[2] == gobangPoint && array[5] == gobangPoint && array[3].IsEmpty && array[6] == gobangPoint2)
			{
				return 7;
			}
			if (array[2].IsEmpty && array[3] == gobangPoint && array[6] == gobangPoint && array[5].IsEmpty && array[7] == gobangPoint2)
			{
				return 7;
			}
			if (array[3].IsEmpty && array[6] == gobangPoint && array[7] == gobangPoint && array[5].IsEmpty && array[8] == gobangPoint2)
			{
				return 7;
			}
			if (array[3].IsEmpty && array[5] == gobangPoint && array[7] == gobangPoint && array[6].IsEmpty && array[8] == gobangPoint2)
			{
				return 7;
			}
			if (array[7] == gobangPoint && array[8] == gobangPoint && array[5].IsEmpty && array[6].IsEmpty)
			{
				return 7;
			}
			if (array[0] == gobangPoint && array[1] == gobangPoint && array[2].IsEmpty && array[3].IsEmpty)
			{
				return 7;
			}
			if (array[1] == gobangPoint2 && array[2] == gobangPoint && array[3] == gobangPoint && array[5] == gobangPoint2)
			{
				return 0;
			}
			if (array[2] == gobangPoint2 && array[3] == gobangPoint && array[5] == gobangPoint && array[6] == gobangPoint2)
			{
				return 0;
			}
			if (array[3] == gobangPoint2 && array[5] == gobangPoint && array[6] == gobangPoint && array[7] == gobangPoint2)
			{
				return 0;
			}
			if (array[0] == gobangPoint && array[1].IsEmpty && array[2] == gobangPoint && array[3].IsEmpty && array[5].IsEmpty)
			{
				return 6;
			}
			if (array[3].IsEmpty && array[6] == gobangPoint && array[5].IsEmpty && array[7].IsEmpty && array[8] == gobangPoint)
			{
				return 6;
			}
			if (array[2].IsEmpty && array[3] == gobangPoint && array[5].IsEmpty)
			{
				return 5;
			}
			if (array[3].IsEmpty && array[5] == gobangPoint && array[6].IsEmpty)
			{
				return 5;
			}
			if (array[1].IsEmpty && array[2] == gobangPoint && array[3].IsEmpty && array[5].IsEmpty)
			{
				return 5;
			}
			if (array[3].IsEmpty && array[6] == gobangPoint && array[5].IsEmpty && array[7].IsEmpty)
			{
				return 5;
			}
			if (array[0].IsEmpty && array[1] == gobangPoint && array[2].IsEmpty && array[3].IsEmpty && array[5].IsEmpty)
			{
				return 5;
			}
			if (array[3].IsEmpty && array[7] == gobangPoint && array[6].IsEmpty && array[5].IsEmpty && array[8].IsEmpty)
			{
				return 5;
			}
			if (array[2] == gobangPoint2 && array[3] == gobangPoint && array[5].IsEmpty)
			{
				return 4;
			}
			if (array[3] == gobangPoint2 && array[5] == gobangPoint && array[6].IsEmpty)
			{
				return 4;
			}
			if (array[2].IsEmpty && array[3] == gobangPoint && array[5] == gobangPoint2)
			{
				return 4;
			}
			if (array[3].IsEmpty && array[5] == gobangPoint && array[6] == gobangPoint2)
			{
				return 4;
			}
			if (array[3] == gobangPoint2 && array[6] == gobangPoint && array[5].IsEmpty && array[7].IsEmpty)
			{
				return 3;
			}
			if (array[1] == gobangPoint2 && array[2] == gobangPoint && array[3].IsEmpty && array[5].IsEmpty)
			{
				return 3;
			}
			if (array[5] == gobangPoint2 && array[2] == gobangPoint && array[3].IsEmpty && array[1].IsEmpty)
			{
				return 3;
			}
			if (array[7] == gobangPoint2 && array[6] == gobangPoint && array[5].IsEmpty && array[3].IsEmpty)
			{
				return 3;
			}
			if (array[2] == gobangPoint2 && array[3] == gobangPoint && array[5] == gobangPoint2)
			{
				return 0;
			}
			if (array[3] == gobangPoint2 && array[5] == gobangPoint && array[6] == gobangPoint2)
			{
				return 0;
			}
			if (array[3].IsEmpty && array[5].IsEmpty)
			{
				return 1;
			}
			if (array[3] == gobangPoint2 && array[5].IsEmpty)
			{
				return 2;
			}
			if (array[3].IsEmpty && array[5] == gobangPoint2)
			{
				return 2;
			}
			if (array[3] == gobangPoint2 && array[5] == gobangPoint2)
			{
				return 0;
			}
			return 1;
		}
	}
}
