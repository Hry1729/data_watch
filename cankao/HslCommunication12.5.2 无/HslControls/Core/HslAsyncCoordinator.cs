using System;
using System.Threading;

namespace HslControls.Core
{
	public sealed class HslAsyncCoordinator
	{
		private Action action = null;

		private int OperaterStatus = 0;

		private long Target = 0L;

		public HslAsyncCoordinator(Action operater)
		{
			action = operater;
		}

		public void StartOperaterInfomation()
		{
			Interlocked.Increment(ref Target);
			if (Interlocked.CompareExchange(ref OperaterStatus, 1, 0) == 0)
			{
				ThreadPool.QueueUserWorkItem(ThreadPoolOperater, null);
			}
		}

		private void ThreadPoolOperater(object obj)
		{
			long num = Target;
			long num2 = 0L;
			long num3;
			do
			{
				num3 = num;
				action?.Invoke();
				num = Interlocked.CompareExchange(ref Target, num2, num3);
			}
			while (num3 != num);
			Interlocked.Exchange(ref OperaterStatus, 0);
			if (Target != num2)
			{
				StartOperaterInfomation();
			}
		}

		public override string ToString()
		{
			return "HslAsyncCoordinator";
		}
	}
}
