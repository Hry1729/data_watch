using System.Text;

namespace HslCommunication.Profinet.Special
{
	/// <summary>
	/// 数据对象
	/// </summary>
	public class EcFanData
	{
		/// <summary>
		/// 应急状态，True表示应急状态，False表示正常
		/// </summary>
		public bool EmergencyState { get; set; } = false;

		/// <summary>
		/// 运行状态，True表示运行，False表示停止
		/// </summary>
		public bool RunState { get; set; }

		/// <summary>
		/// 堵转状态，True表示堵转，False表示正常
		/// </summary>
		public bool LockState { get; set; }

		/// <summary>
		/// 过热状态，True表示过热，False表示正常
		/// </summary>
		public bool OverHotState { get; set; }

		/// <summary>
		/// 失速状态，True表示失速，False表示正常
		/// </summary>
		public bool LostSpeedState { get; set; }

		/// <summary>
		/// 实际的转速
		/// </summary>
		public int Speed { get; set; } = 0;

		/// <inheritdoc />
		public override string ToString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("应急状态: " + (EmergencyState ? "处于应急状态" : "正常状态"));
			stringBuilder.AppendLine("运行状态: " + (RunState ? "运行中" : "停止"));
			stringBuilder.AppendLine("堵转状态: " + (LockState ? "处于堵转中" : "正常状态"));
			stringBuilder.AppendLine("过热状态: " + (OverHotState ? "驱动器处于过热状态" : "驱动器处于正常状态"));
			stringBuilder.AppendLine("失速状态: " + (LostSpeedState ? "处于失速中" : "正常状态"));
			stringBuilder.Append("实际转速: " + Speed);
			return stringBuilder.ToString();
		}
	}
}
