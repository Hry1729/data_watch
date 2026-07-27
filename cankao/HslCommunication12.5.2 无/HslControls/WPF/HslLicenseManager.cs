using System.Diagnostics;

namespace HslControls.WPF
{
	public static class HslLicenseManager
	{
		public static bool IsInDesignMode
		{
			get
			{
				if (Process.GetCurrentProcess().ProcessName == "devenv" || Process.GetCurrentProcess().ProcessName == "VCSExpress" || Process.GetCurrentProcess().ProcessName == "vbexpress" || Process.GetCurrentProcess().ProcessName == "WDExpress")
				{
					return true;
				}
				return false;
			}
		}
	}
}
