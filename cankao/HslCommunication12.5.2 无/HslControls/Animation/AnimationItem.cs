using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace HslControls.Animation
{
	internal class AnimationItem
	{
		private List<AnimationInt32> AnimationInt32s;

		private UserControl userControl;

		public AnimationItem(UserControl control)
		{
			userControl = control;
			AnimationInt32s = new List<AnimationInt32>();
		}

		public void AddAnimationInt32(AnimationInt32 animationInt32)
		{
			AnimationInt32s.Add(animationInt32);
		}

		public bool UpdateUI()
		{
			bool flag = false;
			for (int num = AnimationInt32s.Count - 1; num >= 0; num--)
			{
				if (AnimationInt32s[num].UpdateValue())
				{
					AnimationInt32s.RemoveAt(num);
				}
				flag = true;
			}
			if (flag)
			{
				userControl?.Invoke((Action)delegate
				{
					userControl.Invalidate();
				});
			}
			return AnimationInt32s.Count == 0;
		}
	}
}
