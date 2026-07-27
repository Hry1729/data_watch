using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HslCommunication.Profinet.Siemens.S7PlusHelper
{
	/// <summary>
	/// 节点类对象
	/// </summary>
	public class S7Tag : IS7Object
	{
		/// <summary>
		/// 数据名称
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// 数据ID信息，第一个为DB块的信息，第二个是数据信息，如果是结构体，则继续往后面拓展
		/// </summary>
		public List<uint> LID { get; set; }

		/// <summary>
		/// 类型代号
		/// </summary>
		public byte TypeCode { get; set; }

		/// <summary>
		/// 数组长度，如果小于0则表示标量的数据
		/// </summary>
		public int ArrayLength { get; set; } = -1;

		/// <summary>
		/// 当类型为结构体的时候，关联的其他结构体的ID信息
		/// </summary>
		public uint StructID { get; set; }

		/// <summary>
		/// 当类型为结构体的时候，标签的偏移信息
		/// </summary>
		public int StructOffset { get; set; }

		/// <summary>
		/// 获取类型的文本描述
		/// </summary>
		/// <returns>string info</returns>
		public string GetTypeText()
		{
			string text = null;
			switch (TypeCode)
			{
			case 0:
				text = "Void";
				break;
			case 1:
				text = "Bool";
				break;
			case 2:
				text = "Byte";
				break;
			case 3:
				text = "Char";
				break;
			case 4:
				text = "Word";
				break;
			case 5:
				text = "Int";
				break;
			case 6:
				text = "DWord";
				break;
			case 7:
				text = "DInt";
				break;
			case 8:
				text = "Real";
				break;
			case 9:
				text = "Date";
				break;
			case 10:
				text = "TimeOfDay";
				break;
			case 11:
				text = "Time";
				break;
			case 12:
				text = "S5Time";
				break;
			case 13:
				text = "S5Count";
				break;
			case 14:
				text = "DateAndTime";
				break;
			case 15:
				text = "InteretTime";
				break;
			case 16:
				text = "Array";
				break;
			case 17:
				text = "Struct";
				break;
			case 18:
				text = "EndStruct";
				break;
			case 19:
				text = "String";
				break;
			case 28:
				text = "Counter";
				break;
			case 29:
				text = "Timer";
				break;
			case 48:
				text = "LReal";
				break;
			case 49:
				text = "ULInt";
				break;
			case 50:
				text = "LInt";
				break;
			case 51:
				text = "LWord";
				break;
			case 52:
				text = "USInt";
				break;
			case 53:
				text = "UInt";
				break;
			case 54:
				text = "UDInt";
				break;
			case 55:
				text = "SInt";
				break;
			case 56:
				text = "Bcd8";
				break;
			case 61:
				text = "WChar";
				break;
			case 62:
				text = "WString";
				break;
			default:
				text = $"Unknown({TypeCode})";
				break;
			}
			return (ArrayLength >= 0) ? $"{text}[{ArrayLength}]" : text;
		}

		/// <summary>
		/// 获取LID的文本信息
		/// </summary>
		/// <returns></returns>
		public string GetLIDText()
		{
			StringBuilder stringBuilder = new StringBuilder();
			if (LID != null)
			{
				for (int i = 0; i < LID.Count; i++)
				{
					stringBuilder.Append(LID[i].ToString("X"));
					if (i != LID.Count - 1)
					{
						stringBuilder.Append(".");
					}
				}
			}
			return stringBuilder.ToString();
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.Siemens.S7PlusHelper.IS7Object.WriteMessgae(System.IO.MemoryStream)" />
		public void WriteMessgae(MemoryStream ms)
		{
			S7Object.WriteUint32(ms, 0u);
			S7Object.WriteUint32(ms, LID[0]);
			S7Object.WriteUint32(ms, (uint)LID.Count);
			if (LID[0] >= 2316173312u)
			{
				S7Object.WriteUint32(ms, 2550u);
			}
			else
			{
				S7Object.WriteUint32(ms, 3736u);
			}
			for (int i = 1; i < LID.Count; i++)
			{
				S7Object.WriteUint32(ms, LID[i]);
			}
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.Siemens.S7PlusHelper.IS7Object.GetNumberOfFields" />
		public int GetNumberOfFields()
		{
			return 3 + LID.Count;
		}

		/// <summary>
		/// 深度克隆一个对象
		/// </summary>
		/// <returns></returns>
		public S7Tag Clone()
		{
			S7Tag s7Tag = new S7Tag();
			s7Tag.Name = Name;
			if (LID != null)
			{
				s7Tag.LID = new List<uint>(LID.ToArray());
			}
			s7Tag.TypeCode = TypeCode;
			s7Tag.ArrayLength = ArrayLength;
			s7Tag.StructID = StructID;
			s7Tag.StructOffset = StructOffset;
			return s7Tag;
		}
	}
}
