namespace HslCommunication.Core.Net
{
	/// <summary>
	/// DTU的状态信息
	/// </summary>
	public enum DtuStatus
	{
		/// <summary>
		/// 新创建的对象
		/// </summary>
		Create,
		/// <summary>
		/// 连接中
		/// </summary>
		Connecting,
		/// <summary>
		/// 已连接
		/// </summary>
		Connected,
		/// <summary>
		/// 已关闭
		/// </summary>
		Closed
	}
}
