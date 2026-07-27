namespace HslControls.Animation
{
	internal class AnimationInt32
	{
		private int currentValue = 0;

		private int startValue = 0;

		private int finishValue = 100;

		private int updateTime = 100;

		private int updateCount = 1;

		public int CurrentValue => currentValue;

		public int StartValue => startValue;

		public int FinishValue => finishValue;

		public int UpdateTime => updateTime;

		public int UpdateCount => updateCount;

		public int UpdataTimes { get; set; } = 0;

		public AnimationInt32(ref int value, int start, int finish, int time)
		{
			currentValue = value;
			startValue = start;
			finishValue = finish;
			updateTime = time;
			updateCount = updateTime / 20;
			if (updateCount <= 0)
			{
				updateCount = 0;
			}
		}

		public bool UpdateValue()
		{
			if (UpdataTimes < updateCount)
			{
				currentValue += (finishValue - startValue) / updateCount;
				updateTime++;
				if (updateTime >= updateCount)
				{
					currentValue = finishValue;
					return true;
				}
				return false;
			}
			return true;
		}
	}
}
