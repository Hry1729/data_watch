using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HslCommunicationDemo
{
	[CompilerGenerated]
	[GeneratedCode("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "17.13.0.0")]
	internal sealed class Settings1 : ApplicationSettingsBase
	{
		private static Settings1 defaultInstance = (Settings1)SettingsBase.Synchronized(new Settings1());

		public static Settings1 Default => defaultInstance;

		[UserScopedSetting]
		[DebuggerNonUserCode]
		[DefaultSettingValue("1")]
		public int language
		{
			get
			{
				return (int)this["language"];
			}
			set
			{
				this["language"] = value;
			}
		}

		[UserScopedSetting]
		[DebuggerNonUserCode]
		[DefaultSettingValue("False")]
		public bool UseAdmin
		{
			get
			{
				return (bool)this["UseAdmin"];
			}
			set
			{
				this["UseAdmin"] = value;
			}
		}

		[UserScopedSetting]
		[DebuggerNonUserCode]
		[DefaultSettingValue("True")]
		public bool UseHdd
		{
			get
			{
				return (bool)this["UseHdd"];
			}
			set
			{
				this["UseHdd"] = value;
			}
		}

		[UserScopedSetting]
		[DebuggerNonUserCode]
		[DefaultSettingValue("")]
		public string LoginName
		{
			get
			{
				return (string)this["LoginName"];
			}
			set
			{
				this["LoginName"] = value;
			}
		}

		[UserScopedSetting]
		[DebuggerNonUserCode]
		[DefaultSettingValue("")]
		public string LoginPassword
		{
			get
			{
				return (string)this["LoginPassword"];
			}
			set
			{
				this["LoginPassword"] = value;
			}
		}

		private void SettingChangingEventHandler(object sender, SettingChangingEventArgs e)
		{
		}

		private void SettingsSavingEventHandler(object sender, CancelEventArgs e)
		{
		}
	}
}
