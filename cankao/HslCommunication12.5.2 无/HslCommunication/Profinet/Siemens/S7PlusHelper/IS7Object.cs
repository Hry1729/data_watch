using System.IO;

namespace HslCommunication.Profinet.Siemens.S7PlusHelper
{
	/// <summary>
	/// S7对象信息
	/// </summary>
	public interface IS7Object
	{
		/// <summary>
		/// 将对象写入报文消息的方法
		/// </summary>
		/// <param name="ms">消息报文的数据流</param>
		void WriteMessgae(MemoryStream ms);

		/// <summary>
		/// 获取字段的数量
		/// </summary>
		/// <returns></returns>
		int GetNumberOfFields();
	}
}
