using System;

namespace HslControls
{
	public class ChessWeight
	{
		private GobangPlayer ValuePlayer = GobangPlayer.Player1;

		private int ValueOpponent = 0;

		public int WeightOpponent
		{
			get
			{
				return ValueOpponent;
			}
			set
			{
				if (ValuePlayer == GobangPlayer.Player1)
				{
					ValueOpponent = value;
					return;
				}
				ValueOpponent = value;
				if (value == 12 || value == 13)
				{
					WeightMax += 5;
				}
			}
		}

		public int WeightMax { get; set; } = 0;

		public int WeightLarge { get; set; } = 0;

		public int WeightSmall { get; set; } = 0;

		public int WeightMin { get; set; } = 0;

		public int TotleWeight
		{
			get
			{
				int num = 0;
				if (WeightMax > 4)
				{
					num += WeightMax;
				}
				if (WeightLarge > 4)
				{
					num += WeightLarge;
				}
				if (WeightSmall > 4)
				{
					num += WeightSmall;
				}
				if (WeightMin > 4)
				{
					num += WeightMin;
				}
				if (num < 7)
				{
					num = WeightMax + WeightLarge + WeightSmall + WeightMin;
				}
				return num;
			}
		}

		public int TotleScore => WeightMax + WeightLarge + WeightSmall + WeightMin;

		public ChessWeight()
		{
		}

		public ChessWeight(int[] data, GobangPlayer m_play)
		{
			ValuePlayer = m_play;
			Array.Sort(data);
			Array.Reverse(data);
			WeightMax = data[0];
			WeightLarge = data[1];
			WeightSmall = data[2];
			WeightMin = data[3];
			if ((WeightMax == 11 || WeightMax == 12) && (WeightLarge == 11 || WeightLarge == 12))
			{
				WeightMax = 40;
			}
			if ((WeightMax == 11 || WeightMax == 12) && (WeightLarge == 9 || WeightLarge == 10))
			{
				WeightMax = 30;
			}
			if (ValuePlayer == GobangPlayer.Player2 && (WeightMax == 11 || WeightMax == 12) && WeightLarge < 7)
			{
				WeightMax = 7;
			}
		}

		public static bool operator >(ChessWeight cw1, ChessWeight cw2)
		{
			if (cw1.WeightMax > cw2.WeightMax)
			{
				return true;
			}
			if (cw1.WeightMax < cw2.WeightMax)
			{
				return false;
			}
			if (cw1.WeightLarge > cw2.WeightLarge)
			{
				return true;
			}
			if (cw1.WeightLarge < cw2.WeightLarge)
			{
				return false;
			}
			if (cw1.WeightSmall > cw2.WeightSmall)
			{
				return true;
			}
			if (cw1.WeightSmall < cw2.WeightSmall)
			{
				return false;
			}
			if (cw1.WeightMin > cw2.WeightMin)
			{
				return true;
			}
			return false;
		}

		public static bool operator <(ChessWeight cw1, ChessWeight cw2)
		{
			if (cw1.WeightMax < cw2.WeightMax)
			{
				return true;
			}
			if (cw1.WeightMax > cw2.WeightMax)
			{
				return false;
			}
			if (cw1.WeightLarge < cw2.WeightLarge)
			{
				return true;
			}
			if (cw1.WeightLarge > cw2.WeightLarge)
			{
				return false;
			}
			if (cw1.WeightSmall < cw2.WeightSmall)
			{
				return true;
			}
			if (cw1.WeightSmall > cw2.WeightSmall)
			{
				return false;
			}
			if (cw1.WeightMin < cw2.WeightMin)
			{
				return true;
			}
			return false;
		}

		public static bool operator >=(ChessWeight cw1, ChessWeight cw2)
		{
			if (cw1.WeightMax > cw2.WeightMax)
			{
				return true;
			}
			if (cw1.WeightMax < cw2.WeightMax)
			{
				return false;
			}
			if (cw1.WeightLarge > cw2.WeightLarge)
			{
				return true;
			}
			if (cw1.WeightLarge < cw2.WeightLarge)
			{
				return false;
			}
			if (cw1.WeightSmall > cw2.WeightSmall)
			{
				return true;
			}
			if (cw1.WeightSmall < cw2.WeightSmall)
			{
				return false;
			}
			if (cw1.WeightMin > cw2.WeightMin)
			{
				return true;
			}
			if (cw1.WeightMin < cw2.WeightMin)
			{
				return false;
			}
			return true;
		}

		public static bool operator <=(ChessWeight cw1, ChessWeight cw2)
		{
			if (cw1.WeightMax < cw2.WeightMax)
			{
				return true;
			}
			if (cw1.WeightMax > cw2.WeightMax)
			{
				return false;
			}
			if (cw1.WeightLarge < cw2.WeightLarge)
			{
				return true;
			}
			if (cw1.WeightLarge > cw2.WeightLarge)
			{
				return false;
			}
			if (cw1.WeightSmall < cw2.WeightSmall)
			{
				return true;
			}
			if (cw1.WeightSmall > cw2.WeightSmall)
			{
				return false;
			}
			if (cw1.WeightMin < cw2.WeightMin)
			{
				return true;
			}
			if (cw1.WeightMin > cw2.WeightMin)
			{
				return false;
			}
			return true;
		}

		public static bool operator ==(ChessWeight cw1, ChessWeight cw2)
		{
			if (cw1.WeightLarge == cw2.WeightLarge && cw1.WeightMax == cw2.WeightMax && cw1.WeightSmall == cw2.WeightSmall && cw1.WeightMin == cw2.WeightMin)
			{
				return true;
			}
			return false;
		}

		public static bool operator !=(ChessWeight cw1, ChessWeight cw2)
		{
			if (cw1.WeightLarge == cw2.WeightLarge && cw1.WeightMax == cw2.WeightMax && cw1.WeightSmall == cw2.WeightSmall && cw1.WeightMin == cw2.WeightMin)
			{
				return false;
			}
			return true;
		}
	}
}
