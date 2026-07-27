using System;

namespace HslCommunication.Reflection
{
	/// <summary>
	/// 结构体的字节偏移信息定义，用于方法 <see cref="M:HslCommunication.Core.Device.DeviceCommunication.ReadStruct``1(System.String,System.UInt16)" />读取字节数据并实现解析操作的<br />
	/// The byte offset information of the structure is defined, Method used for <see cref="M:HslCommunication.Core.Device.DeviceCommunication.ReadStruct``1(System.String,System.UInt16)" /> read byte data and implement the parsing operation
	/// </summary>
	public class HslStructAttribute : Attribute
	{
		/// <summary>
		/// 字节偏移字节信息，如果是bool，就是位偏移地址，按照位为单位<br />
		/// Byte offset Indicates the byte information. If the value is bool, it is the address of the bit offset. The unit is bits
		/// </summary>
		public int Index { get; set; }

		/// <summary>
		/// 长度信息，如果是普通类型，则表示数组，如果是字符串，则表示字符串占用的最大字节长度<br />
		/// Length information, if a common type, represents an array, if a string, represents the maximum length of bytes taken by the string
		/// </summary>
		public int Length { get; set; }

		/// <summary>
		/// 编码信息，如果是字符串类型，则表示字符串的编码信息，可选 ASCII UNICODE UTF8 GB2312 ANSI BIG-UNICODE<br />
		/// Encoding information. If it is a string, it indicates the encoding information of a string. The option is ASCII UNICODE UTF8 GB2312 ANSI BIG-UNICODE
		/// </summary>
		public string Encoding { get; set; }

		/// <summary>
		/// 字符串的解析模式，0：默认没有字节头 1: 一个头子节，表示长度，2: 两个头子节，最大字符数和实际字符数，3: 两个字节头，实际字符数，4: 四个头字节，最大字符数和实际字符数，5: 四个头字节，字符串实际长度<br />
		/// Parsing mode of the string, 0: default byte header 1: one head section, indicating the length, 2: two head sections, maximum number of characters and actual number of characters, 
		/// 3: two head bytes, actual number of characters, 4: four head bytes, maximum number of characters and actual number of characters, 5: four head bytes, actual length of the string
		/// </summary>
		public int StringMode { get; set; }

		/// <summary>
		/// 指定偏移地址来实例化一个对象<br />
		/// Specify an offset address to instantiate an object
		/// </summary>
		/// <param name="index">字节偏移字节信息，如果是bool，就是位偏移地址，按照位为单位</param>
		public HslStructAttribute(int index)
		{
			Index = index;
		}

		/// <summary>
		/// 指定偏移地址和长度信息来实例化一个对象<br />
		/// Specify offset address and length information to instantiate an object
		/// </summary>
		/// <param name="index">字节偏移字节信息，如果是bool，就是位偏移地址，按照位为单位</param>
		/// <param name="length">长度信息，如果是普通类型，则表示数组，如果是字符串，则表示字符串占用的最大字节长度</param>
		public HslStructAttribute(int index, int length)
			: this(index)
		{
			Length = length;
		}

		/// <summary>
		/// 指定偏移地址，长度信息，编码信息来实例化一个对象，通常应用于字符串数据<br />
		/// Specify offset address, length information, and encoding information to instantiate an object, usually applied to string data
		/// </summary>
		/// <param name="index">字节偏移字节信息，如果是bool，就是位偏移地址，按照位为单位</param>
		/// <param name="length">长度信息，如果是普通类型，则表示数组，如果是字符串，则表示字符串占用的最大字节长度</param>
		/// <param name="encoding">编码信息，如果是字符串类型，则表示字符串的编码信息，可选 ASCII UNICODE UTF8 GB2312 ANSI BIG-UNICODE</param>
		public HslStructAttribute(int index, int length, string encoding)
			: this(index, length)
		{
			Encoding = encoding;
		}

		/// <summary>
		/// 指定偏移地址，长度信息，编码信息，字符串解析模式来实例化一个对象，通常应用于字符串数据<br />
		/// Specify offset address, length information, encoding information, string parsing mode to instantiate an object, usually applied to string data
		/// </summary>
		/// <param name="index">字节偏移字节信息，如果是bool，就是位偏移地址，按照位为单位</param>
		/// <param name="length">长度信息，如果是普通类型，则表示数组，如果是字符串，则表示字符串占用的最大字节长度</param>
		/// <param name="encoding">编码信息，如果是字符串类型，则表示字符串的编码信息，可选 ASCII UNICODE UTF8 GB2312 ANSI BIG-UNICODE</param>
		/// <param name="mode">字符串的解析模式，0：默认没有字节头 1: 一个头子节，表示长度，2: 两个头子节，最大字符数和实际字符数，3: 两个字节头，实际字符数，4: 四个头字节，字符串实际长度</param>
		public HslStructAttribute(int index, int length, string encoding, int mode)
			: this(index, length, encoding)
		{
			StringMode = mode;
		}
	}
}
