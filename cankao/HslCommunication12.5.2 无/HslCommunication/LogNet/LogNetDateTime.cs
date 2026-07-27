using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace HslCommunication.LogNet
{
	/// <summary>
	/// 一个日志组件，可以根据时间来区分不同的文件存储<br />
	/// A log component that can distinguish different file storages based on time
	/// </summary>
	/// <remarks>
	/// 此日志实例将根据日期时间来进行分类，支持的时间分类如下：分钟, 小时, 天, 周, 月份, 季度, 年<br />
	/// 当然你也可以指定允许存在多少个日志文件，比如我允许存在最多10个文件，如果你的日志是根据天来分文件的，那就是10天的数据。
	/// 同理，如果你的日志是根据年来分文件的，那就是10年的日志文件。<br /><br />
	/// 2025年9月9日更新: 路径里支持日期时间的格式化，支持动态路径，例如我希望按照"年"，"月"，"日"目录逐级存储，可以指定路径：D:\\Logs\\{yyyy}\\{MM}\\{dd}\\ 日志文件再按小时存储
	/// </remarks>
	/// <example>
	/// <code lang="cs" source="HslCommunication_Net45.Test\Documentation\Samples\LogNet\LogNetSingle.cs" region="Example3" title="日期存储实例化" />
	/// <code lang="cs" source="HslCommunication_Net45.Test\Documentation\Samples\LogNet\LogNetSingle.cs" region="Example4" title="基本的使用" />
	/// <code lang="cs" source="HslCommunication_Net45.Test\Documentation\Samples\LogNet\LogNetSingle.cs" region="Example5" title="所有日志不存储" />
	/// <code lang="cs" source="HslCommunication_Net45.Test\Documentation\Samples\LogNet\LogNetSingle.cs" region="Example6" title="仅存储ERROR等级" />
	/// <code lang="cs" source="HslCommunication_Net45.Test\Documentation\Samples\LogNet\LogNetSingle.cs" region="Example7" title="不指定路径" />
	/// </example>
	public class LogNetDateTime : LogPathBase, ILogNet, IDisposable
	{
		private GenerateMode generateMode = GenerateMode.ByEveryYear;

		private int fileSize = -1;

		/// <summary>
		/// 实例化一个根据时间存储的日志组件，需要指定每个文件的存储时间范围<br />
		/// Instantiate a log component based on time, you need to specify the storage time range for each file
		/// </summary>
		/// <param name="filePath">文件存储的路径</param>
		/// <param name="generateMode">存储文件的间隔</param>
		/// <param name="fileQuantity">指定当前的日志文件数量上限，如果小于0，则不限制，文件一直增加，如果设置为10，就限制最多10个文件，会删除最近时间的10个文件之外的文件。</param>
		public LogNetDateTime(string filePath, GenerateMode generateMode = GenerateMode.ByEveryYear, int fileQuantity = -1)
		{
			base.filePath = filePath;
			this.generateMode = generateMode;
			base.LogSaveMode = LogSaveMode.Time;
			controlFileQuantity = fileQuantity;
			if (string.IsNullOrEmpty(filePath))
			{
				return;
			}
			if (filePath.Contains("{") && filePath.Contains("}"))
			{
				string path = Regex.Replace(filePath, "{[yYmMdDHhsS]+}", DateMatchEvaluator);
				if (!Directory.Exists(path))
				{
					Directory.CreateDirectory(path);
				}
			}
			else if (!Directory.Exists(filePath))
			{
				Directory.CreateDirectory(filePath);
			}
		}

		/// <summary>
		/// 实例化一个根据时间存储的日志组件，需要指定每个文件的存储时间范围<br />
		/// Instantiate a log component based on time, you need to specify the storage time range for each file
		/// </summary>
		/// <param name="filePath">文件存储的路径</param>
		/// <param name="generateMode">存储文件的间隔</param>
		/// <param name="timeQuantity">指定当前的日志文件的时间单位数量，如果小于0，则不限制，文件一直增加，如果设置为10，就限制最多10个单位的文件，会删除最近时间的10天之外的文件。</param>
		/// <param name="fileSize">指定文件的大小</param>
		public LogNetDateTime(string filePath, GenerateMode generateMode, int timeQuantity, int fileSize)
			: this(filePath, generateMode, timeQuantity)
		{
			this.fileSize = fileSize;
		}

		private string DateMatchEvaluator(Match match)
		{
			return DateTime.Now.ToString(match.Value.Substring(1, match.Value.Length - 2));
		}

		private string GetFileSaveName(int index)
		{
			if (string.IsNullOrEmpty(filePath))
			{
				return string.Empty;
			}
			string text = ((index > 0) ? $"({index}).txt" : ".txt");
			string path = Regex.Replace(filePath, "{[yYmMdDHhsS]+}", DateMatchEvaluator);
			switch (generateMode)
			{
			case GenerateMode.ByEveryMinute:
				return Path.Combine(path, LogNetManagment.LogFileHeadString + DateTime.Now.ToString("yyyyMMdd_HHmm") + text);
			case GenerateMode.ByEveryHour:
				return Path.Combine(path, LogNetManagment.LogFileHeadString + DateTime.Now.ToString("yyyyMMdd_HH") + text);
			case GenerateMode.ByEveryDay:
				return Path.Combine(path, LogNetManagment.LogFileHeadString + DateTime.Now.ToString("yyyyMMdd") + text);
			case GenerateMode.ByEveryWeek:
			{
				int weekOfYear = new GregorianCalendar().GetWeekOfYear(DateTime.Now, CalendarWeekRule.FirstDay, DayOfWeek.Monday);
				return Path.Combine(path, LogNetManagment.LogFileHeadString + DateTime.Now.Year + "_W" + weekOfYear + text);
			}
			case GenerateMode.ByEveryMonth:
				return Path.Combine(path, LogNetManagment.LogFileHeadString + DateTime.Now.ToString("yyyy_MM") + text);
			case GenerateMode.ByEverySeason:
				return Path.Combine(path, LogNetManagment.LogFileHeadString + DateTime.Now.Year + "_Q" + (DateTime.Now.Month / 3 + 1) + text);
			case GenerateMode.ByEveryYear:
				return Path.Combine(path, LogNetManagment.LogFileHeadString + DateTime.Now.Year + text);
			default:
				return string.Empty;
			}
		}

		/// <inheritdoc />
		protected override string GetFileSaveName()
		{
			if (string.IsNullOrEmpty(filePath))
			{
				return string.Empty;
			}
			if (fileSize <= 0)
			{
				return GetFileSaveName(0);
			}
			int num = 0;
			string fileSaveName;
			while (true)
			{
				fileSaveName = GetFileSaveName(num);
				if (!File.Exists(fileSaveName))
				{
					break;
				}
				try
				{
					if (new FileInfo(fileSaveName).Length < fileSize)
					{
						return fileSaveName;
					}
				}
				catch
				{
					return fileSaveName;
				}
				num++;
			}
			return fileSaveName;
		}

		private DateTime GetDeleteTime()
		{
			switch (generateMode)
			{
			case GenerateMode.ByEveryMinute:
				return DateTime.Now.AddMinutes(-controlFileQuantity);
			case GenerateMode.ByEveryHour:
				return DateTime.Now.AddHours(-controlFileQuantity);
			case GenerateMode.ByEveryDay:
				return DateTime.Now.AddDays(-controlFileQuantity);
			case GenerateMode.ByEveryWeek:
				return DateTime.Now.AddDays(-controlFileQuantity * 7);
			case GenerateMode.ByEveryMonth:
				return DateTime.Now.AddMonths(-controlFileQuantity);
			case GenerateMode.ByEverySeason:
				return DateTime.Now.AddMonths(-controlFileQuantity * 3);
			case GenerateMode.ByEveryYear:
				return DateTime.Now.AddYears(-controlFileQuantity);
			default:
				return DateTime.Now.AddDays(-controlFileQuantity);
			}
		}

		/// <inheritdoc />
		public override string[] GetExistLogFileNames()
		{
			if (!string.IsNullOrEmpty(filePath))
			{
				return Directory.GetFiles(Regex.Replace(filePath, "{[yYmMdDHhsS]+}", DateMatchEvaluator), LogNetManagment.LogFileHeadString + "*.txt");
			}
			return new string[0];
		}

		/// <inheritdoc />
		protected override void OnWriteCompleted(bool createNewLogFile)
		{
			if (fileSize <= 0)
			{
				base.OnWriteCompleted(createNewLogFile);
			}
			else
			{
				if (!createNewLogFile || controlFileQuantity <= 1)
				{
					return;
				}
				try
				{
					string[] existLogFileNames = GetExistLogFileNames();
					if (existLogFileNames.Length > controlFileQuantity)
					{
						List<FileInfo> list = new List<FileInfo>();
						for (int i = 0; i < existLogFileNames.Length; i++)
						{
							list.Add(new FileInfo(existLogFileNames[i]));
						}
						list.Sort((FileInfo m, FileInfo n) => m.CreationTime.CompareTo(n.CreationTime));
						DateTime deleteTime = GetDeleteTime();
						for (int j = 0; j < list.Count && list[j].CreationTime < deleteTime; j++)
						{
							File.Delete(list[j].FullName);
						}
					}
				}
				catch
				{
				}
			}
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return $"LogNetDateTime[{generateMode}]";
		}
	}
}
