using System;
using System.Collections.Generic;
using System.Threading;

namespace HslControls.Animation
{
	internal class ThreadUpdateUI
	{
		private static long hslTimeoutId;

		private static List<AnimationItem> WaitHandleTimeOut;

		private static object listLock;

		private static Thread threadUpdateUI;

		private static long threadUniqueId;

		private static DateTime threadActiveTime;

		private static int activeDisableCount;

		public static int TimeOutCheckCount => WaitHandleTimeOut.Count;

		public static void HandleTimeOutCheck(AnimationItem updateUI)
		{
			lock (listLock)
			{
				if ((DateTime.Now - threadActiveTime).TotalSeconds > 60.0)
				{
					threadActiveTime = DateTime.Now;
					if (Interlocked.Increment(ref activeDisableCount) >= 2)
					{
						CreateTimeoutCheckThread();
					}
				}
				if (!WaitHandleTimeOut.Contains(updateUI))
				{
					WaitHandleTimeOut.Add(updateUI);
				}
			}
		}

		public static AnimationItem[] GetHslTimeOutsSnapShoot()
		{
			lock (listLock)
			{
				return WaitHandleTimeOut.ToArray();
			}
		}

		static ThreadUpdateUI()
		{
			hslTimeoutId = 0L;
			WaitHandleTimeOut = new List<AnimationItem>(128);
			listLock = new object();
			threadUniqueId = 0L;
			activeDisableCount = 0;
			CreateTimeoutCheckThread();
		}

		private static void CreateTimeoutCheckThread()
		{
			threadActiveTime = DateTime.Now;
			threadUpdateUI?.Abort();
			threadUpdateUI = new Thread(CheckTimeOut);
			threadUpdateUI.IsBackground = true;
			threadUpdateUI.Priority = ThreadPriority.AboveNormal;
			threadUpdateUI.Start(Interlocked.Increment(ref threadUniqueId));
		}

		private static void CheckTimeOut(object obj)
		{
			long num = (long)obj;
			while (true)
			{
				Thread.Sleep(50);
				if (num != threadUniqueId)
				{
					break;
				}
				threadActiveTime = DateTime.Now;
				activeDisableCount = 0;
				lock (listLock)
				{
					for (int num2 = WaitHandleTimeOut.Count - 1; num2 >= 0; num2--)
					{
						if (WaitHandleTimeOut[num2].UpdateUI())
						{
							WaitHandleTimeOut.RemoveAt(num2);
						}
					}
				}
			}
		}
	}
}
