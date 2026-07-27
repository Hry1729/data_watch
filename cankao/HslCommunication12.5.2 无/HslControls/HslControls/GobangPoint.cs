using System.Drawing;

namespace HslControls
{
	public class GobangPoint
	{
		public GobangPlayer GobangPlayer { get; set; } = GobangPlayer.NonePlayer;

		public int StepNumber { get; set; } = 0;

		public int WeightScore { get; set; } = 0;

		public Brush GetPawnBrush
		{
			get
			{
				switch (GobangPlayer)
				{
				case GobangPlayer.Player1:
					return Brushes.Orange;
				case GobangPlayer.Player2:
					return Brushes.DimGray;
				default:
					return Brushes.Transparent;
				}
			}
		}

		public bool IsEmpty => GobangPlayer == GobangPlayer.NonePlayer;

		public static bool operator ==(GobangPoint gp1, GobangPoint gp2)
		{
			return gp1.GobangPlayer == gp2.GobangPlayer;
		}

		public static bool operator !=(GobangPoint gp1, GobangPoint gp2)
		{
			return gp1.GobangPlayer != gp2.GobangPlayer;
		}

		public override bool Equals(object obj)
		{
			return base.Equals(obj);
		}

		public override int GetHashCode()
		{
			return base.GetHashCode();
		}
	}
}
