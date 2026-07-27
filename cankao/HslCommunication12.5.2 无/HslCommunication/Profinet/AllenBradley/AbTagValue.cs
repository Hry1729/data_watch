namespace HslCommunication.Profinet.AllenBradley
{
	/// <summary>
	/// AB标签的数据信息<br />
	/// </summary>
	public class AbTagValue
	{
		/// <summary>
		/// 标签的数据类型
		/// </summary>
		public ushort Type { get; set; }

		/// <summary>
		/// 标签的数据内容
		/// </summary>
		public byte[] Value { get; set; }

		/// <summary>
		/// 实例化一个默认的对象<br />
		/// </summary>
		public AbTagValue()
		{
		}

		/// <summary>
		/// 指定标签的名称，数据类型，数据内容来实例化一个对象<br />
		/// </summary>
		/// <param name="type"></param>
		/// <param name="value"></param>
		public AbTagValue(ushort type, byte[] value)
		{
			Type = type;
			Value = value;
		}
	}
}
