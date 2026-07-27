using System.Collections.Generic;
using System.IO;

namespace HslCommunication.Profinet.Siemens.S7PlusHelper
{
	/// <summary>
	/// 西门子的数据对象信息
	/// </summary>
	public class S7Object : IS7Object
	{
		/// <summary>
		/// RelationId
		/// </summary>
		public uint RelationId { get; set; }

		/// <summary>
		/// 第二个关联的信息
		/// </summary>
		public uint RelationId2 { get; set; }

		/// <summary>
		/// ClassId
		/// </summary>
		public uint ClassId { get; set; }

		/// <summary>
		/// ClassFlags
		/// </summary>
		public uint ClassFlags { get; set; }

		/// <summary>
		/// AttributeId
		/// </summary>
		public uint AttributeId { get; set; }

		/// <summary>
		/// 名称信息
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// 数据对象关联的子对象信息
		/// </summary>
		public List<S7Object> SubObjects { get; set; }

		/// <summary>
		/// 关联的节点信息
		/// </summary>
		public List<S7Tag> S7Tags { get; set; }

		/// <inheritdoc cref="M:HslCommunication.Profinet.Siemens.S7PlusHelper.IS7Object.WriteMessgae(System.IO.MemoryStream)" />
		public void WriteMessgae(MemoryStream ms)
		{
			WriteUint32(ms, 0u);
			WriteUint32(ms, RelationId);
			WriteUint32(ms, 2u);
			WriteUint32(ms, 2550u);
			WriteUint32(ms, 1u);
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.Siemens.S7PlusHelper.IS7Object.GetNumberOfFields" />
		public int GetNumberOfFields()
		{
			return 5;
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return "S7Object[" + Name + "]";
		}

		private static int GetLongLength(ulong value)
		{
			ulong num = 128uL;
			int num2 = 0;
			while (true)
			{
				num2++;
				if (num2 >= 9)
				{
					return num2;
				}
				if (value < num)
				{
					break;
				}
				num <<= 7;
			}
			return num2;
		}

		private static void WriteAutoLength(MemoryStream ms, int len, ulong value)
		{
			byte[] array = new byte[len];
			for (int num = len - 1; num >= 0; num--)
			{
				if (num == len - 1)
				{
					if (num == 8)
					{
						array[num] = (byte)value;
						value >>= 8;
					}
					else
					{
						array[num] = (byte)(value & 0x7F);
						value >>= 7;
					}
				}
				else
				{
					array[num] = (byte)(value | 0x80);
					value >>= 7;
				}
			}
			ms.Write(array, 0, array.Length);
		}

		/// <summary>
		/// 写入一个uint类型的数据，使用动态长度表示方式，写入到字节流里去
		/// </summary>
		/// <param name="ms">字节流</param>
		/// <param name="value">值信息</param>
		public static void WriteUint32(MemoryStream ms, uint value)
		{
			WriteAutoLength(ms, GetLongLength(value), value);
		}

		/// <summary>
		/// 写入一个ulong类型的数据，使用动态长度表示方式，写入到字节流里去
		/// </summary>
		/// <param name="ms">字节流</param>
		/// <param name="value">值信息</param>
		public static void WriteUint64(MemoryStream ms, ulong value)
		{
			WriteAutoLength(ms, GetLongLength(value), value);
		}

		/// <summary>
		/// 从缓存中获取动态长度的uint类型数据
		/// </summary>
		/// <param name="buffer">缓存值</param>
		/// <param name="index">索引信息</param>
		/// <returns>结果值</returns>
		public static uint GetValueUint32(byte[] buffer, ref int index)
		{
			int num = 0;
			int num2 = 0;
			for (int i = 0; i < 5; i++)
			{
				num2++;
				num = (num << 7) + (byte)(buffer[index + i] & 0x7F);
				if (!buffer[index + i].GetBoolByIndex(7))
				{
					break;
				}
			}
			index += num2;
			return (uint)num;
		}

		/// <summary>
		/// 从缓存中获取动态长度的ulong类型数据
		/// </summary>
		/// <param name="buffer">缓存值</param>
		/// <param name="index">索引信息</param>
		/// <returns>结果值</returns>
		public static ulong GetValueUint64(byte[] buffer, ref int index)
		{
			ulong num = 0uL;
			int num2 = 0;
			for (int i = 0; i < 9; i++)
			{
				num2++;
				if (i == 8)
				{
					num = (num << 8) + buffer[index + i];
					continue;
				}
				num = (num << 7) + (byte)(buffer[index + i] & 0x7F);
				if (!buffer[index + i].GetBoolByIndex(7))
				{
					break;
				}
			}
			index += num2;
			return num;
		}
	}
}
