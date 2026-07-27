using System;
using System.Drawing;

namespace HslControls
{
	public class AuxiliaryLine : IDisposable
	{
		private bool disposedValue = false;

		public float Value { get; set; }

		public float PaintValue { get; set; }

		public float PaintValueBackUp { get; set; }

		public Color LineColor { get; set; }

		public Pen PenDash { get; set; }

		public Pen PenSolid { get; set; }

		public float LineThickness { get; set; }

		public Brush LineTextBrush { get; set; }

		public bool IsLeftFrame { get; set; }

		public bool IsDashStyle { get; set; } = true;

		public Pen GetPen()
		{
			return IsDashStyle ? PenDash : PenSolid;
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					PenDash?.Dispose();
					PenSolid?.Dispose();
					LineTextBrush?.Dispose();
				}
				disposedValue = true;
			}
		}

		public void Dispose()
		{
			Dispose(true);
		}
	}
}
