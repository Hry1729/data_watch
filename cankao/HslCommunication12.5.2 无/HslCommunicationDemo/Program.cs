using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.BasicFramework;

namespace HslCommunicationDemo
{
	internal static class Program
	{
		public static int Language = 1;

		public static bool ShowAuthorInfomation = true;

		public static DateTime StartTime = DateTime.Now;

		public static string SystemName = "欢迎登录HSL调试平台";

		public static bool IsActive { get; private set; }

		[STAThread]
		private static void Main()
		{
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
			
				IsActive = true;
			
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			ThreadPool.SetMaxThreads(2000, 800);
			Application.Run(new FormMain());
		}

		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			File.WriteAllText("123.txt", SoftBasic.GetExceptionMessage(e.ExceptionObject as Exception));
		}
	}
}
