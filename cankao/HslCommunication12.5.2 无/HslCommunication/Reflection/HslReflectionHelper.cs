using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HslCommunication.Core;
using HslCommunication.Enthernet.Redis;
using Newtonsoft.Json.Linq;

namespace HslCommunication.Reflection
{
	/// <summary>
	/// 反射的辅助类
	/// </summary>
	public class HslReflectionHelper
	{
		/// <summary>
		/// 从属性中获取对应的设备类型的地址特性信息
		/// </summary>
		/// <param name="deviceType">设备类型信息</param>
		/// <param name="property">属性信息</param>
		/// <returns>设备类型信息</returns>
		public static HslDeviceAddressAttribute GetHslDeviceAddressAttribute(Type deviceType, PropertyInfo property)
		{
			object[] customAttributes = property.GetCustomAttributes(typeof(HslDeviceAddressAttribute), false);
			if (customAttributes == null)
			{
				return null;
			}
			HslDeviceAddressAttribute hslDeviceAddressAttribute = null;
			for (int i = 0; i < customAttributes.Length; i++)
			{
				HslDeviceAddressAttribute hslDeviceAddressAttribute2 = (HslDeviceAddressAttribute)customAttributes[i];
				if (hslDeviceAddressAttribute2.DeviceType != null && hslDeviceAddressAttribute2.DeviceType == deviceType)
				{
					hslDeviceAddressAttribute = hslDeviceAddressAttribute2;
					break;
				}
			}
			if (hslDeviceAddressAttribute == null)
			{
				for (int j = 0; j < customAttributes.Length; j++)
				{
					HslDeviceAddressAttribute hslDeviceAddressAttribute3 = (HslDeviceAddressAttribute)customAttributes[j];
					if (hslDeviceAddressAttribute3.DeviceType == null)
					{
						hslDeviceAddressAttribute = hslDeviceAddressAttribute3;
						break;
					}
				}
			}
			return hslDeviceAddressAttribute;
		}

		/// <inheritdoc cref="M:HslCommunication.Reflection.HslReflectionHelper.GetHslDeviceAddressAttribute(System.Type,System.Reflection.PropertyInfo)" />
		public static HslDeviceAddressAttribute[] GetHslDeviceAddressAttributeArray(Type deviceType, PropertyInfo property)
		{
			object[] customAttributes = property.GetCustomAttributes(typeof(HslDeviceAddressAttribute), false);
			if (customAttributes == null)
			{
				return null;
			}
			List<HslDeviceAddressAttribute> list = new List<HslDeviceAddressAttribute>();
			for (int i = 0; i < customAttributes.Length; i++)
			{
				HslDeviceAddressAttribute hslDeviceAddressAttribute = (HslDeviceAddressAttribute)customAttributes[i];
				if (hslDeviceAddressAttribute.DeviceType != null && hslDeviceAddressAttribute.DeviceType == deviceType)
				{
					list.Add(hslDeviceAddressAttribute);
				}
			}
			if (list.Count == 0)
			{
				for (int j = 0; j < customAttributes.Length; j++)
				{
					HslDeviceAddressAttribute hslDeviceAddressAttribute2 = (HslDeviceAddressAttribute)customAttributes[j];
					if (hslDeviceAddressAttribute2.DeviceType == null)
					{
						list.Add(hslDeviceAddressAttribute2);
					}
				}
			}
			return list.ToArray();
		}

		/// <summary>
		/// 从设备里读取支持Hsl特性的数据内容，该特性为<see cref="T:HslCommunication.Reflection.HslDeviceAddressAttribute" />，详细参考论坛的操作说明。
		/// </summary>
		/// <typeparam name="T">自定义的数据类型对象</typeparam>
		/// <param name="readWrite">读写接口的实现</param>
		/// <returns>包含是否成功的结果对象</returns>
		public static OperateResult<T> Read<T>(IReadWriteNet readWrite) where T : class, new()
		{
			Type typeFromHandle = typeof(T);
			object obj = typeFromHandle.Assembly.CreateInstance(typeFromHandle.FullName);
			PropertyInfo[] properties = typeFromHandle.GetProperties(BindingFlags.Instance | BindingFlags.Public);
			foreach (PropertyInfo propertyInfo in properties)
			{
				Type propertyType = propertyInfo.PropertyType;
				if (propertyType == typeof(string[]))
				{
					HslDeviceAddressAttribute[] hslDeviceAddressAttributeArray = GetHslDeviceAddressAttributeArray(readWrite.GetType(), propertyInfo);
					if (hslDeviceAddressAttributeArray == null || hslDeviceAddressAttributeArray.Length == 0)
					{
						continue;
					}
					string[] array = new string[hslDeviceAddressAttributeArray.Length];
					for (int j = 0; j < hslDeviceAddressAttributeArray.Length; j++)
					{
						OperateResult<string> operateResult = readWrite.ReadString(hslDeviceAddressAttributeArray[j].Address, (ushort)((hslDeviceAddressAttributeArray[j].Length < 0) ? 1u : ((uint)hslDeviceAddressAttributeArray[j].Length)), hslDeviceAddressAttributeArray[j].GetEncoding());
						if (!operateResult.IsSuccess)
						{
							return OperateResult.CreateFailedResult<T>(operateResult);
						}
						array[j] = operateResult.Content;
					}
					propertyInfo.SetValue(obj, array, null);
					continue;
				}
				HslDeviceAddressAttribute hslDeviceAddressAttribute = GetHslDeviceAddressAttribute(readWrite.GetType(), propertyInfo);
				if (hslDeviceAddressAttribute == null)
				{
					continue;
				}
				if (propertyType == typeof(byte))
				{
					MethodInfo method = readWrite.GetType().GetMethod("ReadByte", new Type[1] { typeof(string) });
					if (method == null)
					{
						return new OperateResult<T>(readWrite.GetType().Name + " not support read byte value. ");
					}
					OperateResult<byte> operateResult2 = (OperateResult<byte>)method.Invoke(readWrite, new object[1] { hslDeviceAddressAttribute.Address });
					if (!operateResult2.IsSuccess)
					{
						return OperateResult.CreateFailedResult<T>(operateResult2);
					}
					propertyInfo.SetValue(obj, operateResult2.Content, null);
				}
				else if (propertyType == typeof(short))
				{
					OperateResult<short> operateResult3 = readWrite.ReadInt16(hslDeviceAddressAttribute.Address);
					if (!operateResult3.IsSuccess)
					{
						return OperateResult.CreateFailedResult<T>(operateResult3);
					}
					propertyInfo.SetValue(obj, operateResult3.Content, null);
				}
				else if (propertyType == typeof(short[]))
				{
					OperateResult<short[]> operateResult4 = readWrite.ReadInt16(hslDeviceAddressAttribute.Address, (ushort)((hslDeviceAddressAttribute.Length < 0) ? 1u : ((uint)hslDeviceAddressAttribute.Length)));
					if (!operateResult4.IsSuccess)
					{
						return OperateResult.CreateFailedResult<T>(operateResult4);
					}
					propertyInfo.SetValue(obj, operateResult4.Content, null);
				}
				else if (propertyType == typeof(ushort))
				{
					OperateResult<ushort> operateResult5 = readWrite.ReadUInt16(hslDeviceAddressAttribute.Address);
					if (!operateResult5.IsSuccess)
					{
						return OperateResult.CreateFailedResult<T>(operateResult5);
					}
					propertyInfo.SetValue(obj, operateResult5.Content, null);
				}
				else if (propertyType == typeof(ushort[]))
				{
					OperateResult<ushort[]> operateResult6 = readWrite.ReadUInt16(hslDeviceAddressAttribute.Address, (ushort)((hslDeviceAddressAttribute.Length < 0) ? 1u : ((uint)hslDeviceAddressAttribute.Length)));
					if (!operateResult6.IsSuccess)
					{
						return OperateResult.CreateFailedResult<T>(operateResult6);
					}
					propertyInfo.SetValue(obj, operateResult6.Content, null);
				}
				else if (propertyType == typeof(int))
				{
					OperateResult<int> operateResult7 = readWrite.ReadInt32(hslDeviceAddressAttribute.Address);
					if (!operateResult7.IsSuccess)
					{
						return OperateResult.CreateFailedResult<T>(operateResult7);
					}
					propertyInfo.SetValue(obj, operateResult7.Content, null);
				}
				else if (propertyType == typeof(int[]))
				{
					OperateResult<int[]> operateResult8 = readWrite.ReadInt32(hslDeviceAddressAttribute.Address, (ushort)((hslDeviceAddressAttribute.Length < 0) ? 1u : ((uint)hslDeviceAddressAttribute.Length)));
					if (!operateResult8.IsSuccess)
					{
						return OperateResult.CreateFailedResult<T>(operateResult8);
					}
					propertyInfo.SetValue(obj, operateResult8.Content, null);
				}
				else if (propertyType == typeof(uint))
				{
					OperateResult<uint> operateResult9 = readWrite.ReadUInt32(hslDeviceAddressAttribute.Address);
					if (!operateResult9.IsSuccess)
					{
						return OperateResult.CreateFailedResult<T>(operateResult9);
					}
					propertyInfo.SetValue(obj, operateResult9.Content, null);
				}
				else if (propertyType == typeof(uint[]))
				{
					OperateResult<uint[]> operateResult10 = readWrite.ReadUInt32(hslDeviceAddressAttribute.Address, (ushort)((hslDeviceAddressAttribute.Length < 0) ? 1u : ((uint)hslDeviceAddressAttribute.Length)));
					if (!operateResult10.IsSuccess)
					{
						return OperateResult.CreateFailedResult<T>(operateResult10);
					}
					propertyInfo.SetValue(obj, operateResult10.Content, null);
				}
				else if (propertyType == typeof(long))
				{
					OperateResult<long> operateResult11 = readWrite.ReadInt64(hslDeviceAddressAttribute.Address);
					if (!operateResult11.IsSuccess)
					{
						return OperateResult.CreateFailedResult<T>(operateResult11);
					}
					propertyInfo.SetValue(obj, operateResult11.Content, null);
				}
				else if (propertyType == typeof(long[]))
				{
					OperateResult<long[]> operateResult12 = readWrite.ReadInt64(hslDeviceAddressAttribute.Address, (ushort)((hslDeviceAddressAttribute.Length < 0) ? 1u : ((uint)hslDeviceAddressAttribute.Length)));
					if (!operateResult12.IsSuccess)
					{
						return OperateResult.CreateFailedResult<T>(operateResult12);
					}
					propertyInfo.SetValue(obj, operateResult12.Content, null);
				}
				else if (propertyType == typeof(ulong))
				{
					OperateResult<ulong> operateResult13 = readWrite.ReadUInt64(hslDeviceAddressAttribute.Address);
					if (!operateResult13.IsSuccess)
					{
						return OperateResult.CreateFailedResult<T>(operateResult13);
					}
					propertyInfo.SetValue(obj, operateResult13.Content, null);
				}
				else if (propertyType == typeof(ulong[]))
				{
					OperateResult<ulong[]> operateResult14 = readWrite.ReadUInt64(hslDeviceAddressAttribute.Address, (ushort)((hslDeviceAddressAttribute.Length < 0) ? 1u : ((uint)hslDeviceAddressAttribute.Length)));
					if (!operateResult14.IsSuccess)
					{
						return OperateResult.CreateFailedResult<T>(operateResult14);
					}
					propertyInfo.SetValue(obj, operateResult14.Content, null);
				}
				else if (propertyType == typeof(float))
				{
					OperateResult<float> operateResult15 = readWrite.ReadFloat(hslDeviceAddressAttribute.Address);
					if (!operateResult15.IsSuccess)
					{
						return OperateResult.CreateFailedResult<T>(operateResult15);
					}
					propertyInfo.SetValue(obj, operateResult15.Content, null);
				}
				else if (propertyType == typeof(float[]))
				{
					OperateResult<float[]> operateResult16 = readWrite.ReadFloat(hslDeviceAddressAttribute.Address, (ushort)((hslDeviceAddressAttribute.Length < 0) ? 1u : ((uint)hslDeviceAddressAttribute.Length)));
					if (!operateResult16.IsSuccess)
					{
						return OperateResult.CreateFailedResult<T>(operateResult16);
					}
					propertyInfo.SetValue(obj, operateResult16.Content, null);
				}
				else if (propertyType == typeof(double))
				{
					OperateResult<double> operateResult17 = readWrite.ReadDouble(hslDeviceAddressAttribute.Address);
					if (!operateResult17.IsSuccess)
					{
						return OperateResult.CreateFailedResult<T>(operateResult17);
					}
					propertyInfo.SetValue(obj, operateResult17.Content, null);
				}
				else if (propertyType == typeof(double[]))
				{
					OperateResult<double[]> operateResult18 = readWrite.ReadDouble(hslDeviceAddressAttribute.Address, (ushort)((hslDeviceAddressAttribute.Length < 0) ? 1u : ((uint)hslDeviceAddressAttribute.Length)));
					if (!operateResult18.IsSuccess)
					{
						return OperateResult.CreateFailedResult<T>(operateResult18);
					}
					propertyInfo.SetValue(obj, operateResult18.Content, null);
				}
				else if (propertyType == typeof(string))
				{
					OperateResult<string> operateResult19 = readWrite.ReadString(hslDeviceAddressAttribute.Address, (ushort)((hslDeviceAddressAttribute.Length < 0) ? 1u : ((uint)hslDeviceAddressAttribute.Length)), hslDeviceAddressAttribute.GetEncoding());
					if (!operateResult19.IsSuccess)
					{
						return OperateResult.CreateFailedResult<T>(operateResult19);
					}
					propertyInfo.SetValue(obj, operateResult19.Content, null);
				}
				else if (propertyType == typeof(byte[]))
				{
					OperateResult<byte[]> operateResult20 = readWrite.Read(hslDeviceAddressAttribute.Address, (ushort)((hslDeviceAddressAttribute.Length < 0) ? 1u : ((uint)hslDeviceAddressAttribute.Length)));
					if (!operateResult20.IsSuccess)
					{
						return OperateResult.CreateFailedResult<T>(operateResult20);
					}
					propertyInfo.SetValue(obj, operateResult20.Content, null);
				}
				else if (propertyType == typeof(bool))
				{
					OperateResult<bool> operateResult21 = readWrite.ReadBool(hslDeviceAddressAttribute.Address);
					if (!operateResult21.IsSuccess)
					{
						return OperateResult.CreateFailedResult<T>(operateResult21);
					}
					propertyInfo.SetValue(obj, operateResult21.Content, null);
				}
				else if (propertyType == typeof(bool[]))
				{
					OperateResult<bool[]> operateResult22 = readWrite.ReadBool(hslDeviceAddressAttribute.Address, (ushort)((hslDeviceAddressAttribute.Length < 0) ? 1u : ((uint)hslDeviceAddressAttribute.Length)));
					if (!operateResult22.IsSuccess)
					{
						return OperateResult.CreateFailedResult<T>(operateResult22);
					}
					propertyInfo.SetValue(obj, operateResult22.Content, null);
				}
			}
			return OperateResult.CreateSuccessResult((T)obj);
		}

		/// <summary>
		/// 从设备里读取支持Hsl特性的数据内容，该特性为<see cref="T:HslCommunication.Reflection.HslDeviceAddressAttribute" />，详细参考论坛的操作说明。
		/// </summary>
		/// <typeparam name="T">自定义的数据类型对象</typeparam>
		/// <param name="data">自定义的数据对象</param>
		/// <param name="readWrite">数据读写对象</param>
		/// <returns>包含是否成功的结果对象</returns>
		/// <exception cref="T:System.ArgumentNullException"></exception>
		public static OperateResult Write<T>(T data, IReadWriteNet readWrite) where T : class, new()
		{
			if (data == null)
			{
				throw new ArgumentNullException("data");
			}
			Type typeFromHandle = typeof(T);
			PropertyInfo[] properties = typeFromHandle.GetProperties(BindingFlags.Instance | BindingFlags.Public);
			foreach (PropertyInfo propertyInfo in properties)
			{
				Type propertyType = propertyInfo.PropertyType;
				if (propertyType == typeof(string[]))
				{
					HslDeviceAddressAttribute[] hslDeviceAddressAttributeArray = GetHslDeviceAddressAttributeArray(readWrite.GetType(), propertyInfo);
					if (hslDeviceAddressAttributeArray == null || hslDeviceAddressAttributeArray.Length == 0)
					{
						continue;
					}
					string[] array = (string[])propertyInfo.GetValue(data, null);
					for (int j = 0; j < hslDeviceAddressAttributeArray.Length; j++)
					{
						OperateResult operateResult = readWrite.Write(hslDeviceAddressAttributeArray[j].Address, array[j], hslDeviceAddressAttributeArray[j].GetEncoding());
						if (!operateResult.IsSuccess)
						{
							return operateResult;
						}
					}
					continue;
				}
				HslDeviceAddressAttribute hslDeviceAddressAttribute = GetHslDeviceAddressAttribute(readWrite.GetType(), propertyInfo);
				if (hslDeviceAddressAttribute == null)
				{
					continue;
				}
				if (propertyType == typeof(byte))
				{
					MethodInfo method = readWrite.GetType().GetMethod("Write", new Type[2]
					{
						typeof(string),
						typeof(byte)
					});
					if (method == null)
					{
						return new OperateResult<T>(readWrite.GetType().Name + " not support write byte value. ");
					}
					byte b = (byte)propertyInfo.GetValue(data, null);
					OperateResult operateResult2 = (OperateResult)method.Invoke(readWrite, new object[2] { hslDeviceAddressAttribute.Address, b });
					if (!operateResult2.IsSuccess)
					{
						return OperateResult.CreateFailedResult<T>(operateResult2);
					}
				}
				else if (propertyType == typeof(short))
				{
					OperateResult operateResult3 = readWrite.Write(value: (short)propertyInfo.GetValue(data, null), address: hslDeviceAddressAttribute.Address);
					if (!operateResult3.IsSuccess)
					{
						return operateResult3;
					}
				}
				else if (propertyType == typeof(short[]))
				{
					OperateResult operateResult4 = readWrite.Write(values: (short[])propertyInfo.GetValue(data, null), address: hslDeviceAddressAttribute.Address);
					if (!operateResult4.IsSuccess)
					{
						return operateResult4;
					}
				}
				else if (propertyType == typeof(ushort))
				{
					OperateResult operateResult5 = readWrite.Write(value: (ushort)propertyInfo.GetValue(data, null), address: hslDeviceAddressAttribute.Address);
					if (!operateResult5.IsSuccess)
					{
						return operateResult5;
					}
				}
				else if (propertyType == typeof(ushort[]))
				{
					OperateResult operateResult6 = readWrite.Write(values: (ushort[])propertyInfo.GetValue(data, null), address: hslDeviceAddressAttribute.Address);
					if (!operateResult6.IsSuccess)
					{
						return operateResult6;
					}
				}
				else if (propertyType == typeof(int))
				{
					OperateResult operateResult7 = readWrite.Write(value: (int)propertyInfo.GetValue(data, null), address: hslDeviceAddressAttribute.Address);
					if (!operateResult7.IsSuccess)
					{
						return operateResult7;
					}
				}
				else if (propertyType == typeof(int[]))
				{
					OperateResult operateResult8 = readWrite.Write(values: (int[])propertyInfo.GetValue(data, null), address: hslDeviceAddressAttribute.Address);
					if (!operateResult8.IsSuccess)
					{
						return operateResult8;
					}
				}
				else if (propertyType == typeof(uint))
				{
					OperateResult operateResult9 = readWrite.Write(value: (uint)propertyInfo.GetValue(data, null), address: hslDeviceAddressAttribute.Address);
					if (!operateResult9.IsSuccess)
					{
						return operateResult9;
					}
				}
				else if (propertyType == typeof(uint[]))
				{
					OperateResult operateResult10 = readWrite.Write(values: (uint[])propertyInfo.GetValue(data, null), address: hslDeviceAddressAttribute.Address);
					if (!operateResult10.IsSuccess)
					{
						return operateResult10;
					}
				}
				else if (propertyType == typeof(long))
				{
					OperateResult operateResult11 = readWrite.Write(value: (long)propertyInfo.GetValue(data, null), address: hslDeviceAddressAttribute.Address);
					if (!operateResult11.IsSuccess)
					{
						return operateResult11;
					}
				}
				else if (propertyType == typeof(long[]))
				{
					OperateResult operateResult12 = readWrite.Write(values: (long[])propertyInfo.GetValue(data, null), address: hslDeviceAddressAttribute.Address);
					if (!operateResult12.IsSuccess)
					{
						return operateResult12;
					}
				}
				else if (propertyType == typeof(ulong))
				{
					OperateResult operateResult13 = readWrite.Write(value: (ulong)propertyInfo.GetValue(data, null), address: hslDeviceAddressAttribute.Address);
					if (!operateResult13.IsSuccess)
					{
						return operateResult13;
					}
				}
				else if (propertyType == typeof(ulong[]))
				{
					OperateResult operateResult14 = readWrite.Write(values: (ulong[])propertyInfo.GetValue(data, null), address: hslDeviceAddressAttribute.Address);
					if (!operateResult14.IsSuccess)
					{
						return operateResult14;
					}
				}
				else if (propertyType == typeof(float))
				{
					OperateResult operateResult15 = readWrite.Write(value: (float)propertyInfo.GetValue(data, null), address: hslDeviceAddressAttribute.Address);
					if (!operateResult15.IsSuccess)
					{
						return operateResult15;
					}
				}
				else if (propertyType == typeof(float[]))
				{
					OperateResult operateResult16 = readWrite.Write(values: (float[])propertyInfo.GetValue(data, null), address: hslDeviceAddressAttribute.Address);
					if (!operateResult16.IsSuccess)
					{
						return operateResult16;
					}
				}
				else if (propertyType == typeof(double))
				{
					OperateResult operateResult17 = readWrite.Write(value: (double)propertyInfo.GetValue(data, null), address: hslDeviceAddressAttribute.Address);
					if (!operateResult17.IsSuccess)
					{
						return operateResult17;
					}
				}
				else if (propertyType == typeof(double[]))
				{
					OperateResult operateResult18 = readWrite.Write(values: (double[])propertyInfo.GetValue(data, null), address: hslDeviceAddressAttribute.Address);
					if (!operateResult18.IsSuccess)
					{
						return operateResult18;
					}
				}
				else if (propertyType == typeof(string))
				{
					OperateResult operateResult19 = readWrite.Write(value: (string)propertyInfo.GetValue(data, null), address: hslDeviceAddressAttribute.Address, encoding: hslDeviceAddressAttribute.GetEncoding());
					if (!operateResult19.IsSuccess)
					{
						return operateResult19;
					}
				}
				else if (propertyType == typeof(byte[]))
				{
					OperateResult operateResult20 = readWrite.Write(value: (byte[])propertyInfo.GetValue(data, null), address: hslDeviceAddressAttribute.Address);
					if (!operateResult20.IsSuccess)
					{
						return operateResult20;
					}
				}
				else if (propertyType == typeof(bool))
				{
					OperateResult operateResult21 = readWrite.Write(value: (bool)propertyInfo.GetValue(data, null), address: hslDeviceAddressAttribute.Address);
					if (!operateResult21.IsSuccess)
					{
						return operateResult21;
					}
				}
				else if (propertyType == typeof(bool[]))
				{
					OperateResult operateResult22 = readWrite.Write(value: (bool[])propertyInfo.GetValue(data, null), address: hslDeviceAddressAttribute.Address);
					if (!operateResult22.IsSuccess)
					{
						return operateResult22;
					}
				}
			}
			return OperateResult.CreateSuccessResult(data);
		}

		/// <summary>
		/// 根据类型信息，直接从原始字节解析出类型对象，然后赋值给对应的对象，该对象的属性需要支持特性 <see cref="T:HslCommunication.Reflection.HslStructAttribute" /> 才支持设置
		/// </summary>
		/// <typeparam name="T">类型信息</typeparam>
		/// <param name="buffer">缓存信息</param>
		/// <param name="startIndex">起始偏移地址</param>
		/// <param name="byteTransform">数据变换规则对象</param>
		/// <returns>新的实例化的类型对象</returns>
		public static T PraseStructContent<T>(byte[] buffer, int startIndex, IByteTransform byteTransform) where T : class, new()
		{
			Type typeFromHandle = typeof(T);
			object obj = typeFromHandle.Assembly.CreateInstance(typeFromHandle.FullName);
			PraseStructContent(obj, buffer, startIndex, byteTransform);
			return (T)obj;
		}

		/// <summary>
		/// 根据结构体的定义，将原始字节的数据解析出来，然后赋值给对应的对象，该对象的属性需要支持特性 <see cref="T:HslCommunication.Reflection.HslStructAttribute" /> 才支持设置
		/// </summary>
		/// <param name="obj">类型对象信息</param>
		/// <param name="buffer">读取的缓存数据信息</param>
		/// <param name="startIndex">起始的偏移地址</param>
		/// <param name="byteTransform">数据变换规则对象</param>
		public static void PraseStructContent(object obj, byte[] buffer, int startIndex, IByteTransform byteTransform)
		{
			PropertyInfo[] properties = obj.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
			foreach (PropertyInfo propertyInfo in properties)
			{
				object[] customAttributes = propertyInfo.GetCustomAttributes(typeof(HslStructAttribute), false);
				if (customAttributes == null)
				{
					continue;
				}
				HslStructAttribute hslStructAttribute = ((customAttributes.Length != 0) ? ((HslStructAttribute)customAttributes[0]) : null);
				if (hslStructAttribute == null)
				{
					continue;
				}
				Type propertyType = propertyInfo.PropertyType;
				if (propertyType == typeof(byte))
				{
					propertyInfo.SetValue(obj, buffer[startIndex + hslStructAttribute.Index], null);
				}
				else if (propertyType == typeof(byte[]))
				{
					propertyInfo.SetValue(obj, buffer.SelectMiddle(startIndex + hslStructAttribute.Index, hslStructAttribute.Length), null);
				}
				else if (propertyType == typeof(short))
				{
					propertyInfo.SetValue(obj, byteTransform.TransInt16(buffer, startIndex + hslStructAttribute.Index), null);
				}
				else if (propertyType == typeof(short[]))
				{
					propertyInfo.SetValue(obj, byteTransform.TransInt16(buffer, startIndex + hslStructAttribute.Index, hslStructAttribute.Length), null);
				}
				else if (propertyType == typeof(ushort))
				{
					propertyInfo.SetValue(obj, byteTransform.TransUInt16(buffer, startIndex + hslStructAttribute.Index), null);
				}
				else if (propertyType == typeof(ushort[]))
				{
					propertyInfo.SetValue(obj, byteTransform.TransUInt16(buffer, startIndex + hslStructAttribute.Index, hslStructAttribute.Length), null);
				}
				else if (propertyType == typeof(int))
				{
					propertyInfo.SetValue(obj, byteTransform.TransInt32(buffer, startIndex + hslStructAttribute.Index), null);
				}
				else if (propertyType == typeof(int[]))
				{
					propertyInfo.SetValue(obj, byteTransform.TransInt32(buffer, startIndex + hslStructAttribute.Index, hslStructAttribute.Length), null);
				}
				else if (propertyType == typeof(uint))
				{
					propertyInfo.SetValue(obj, byteTransform.TransUInt32(buffer, startIndex + hslStructAttribute.Index), null);
				}
				else if (propertyType == typeof(uint[]))
				{
					propertyInfo.SetValue(obj, byteTransform.TransUInt32(buffer, startIndex + hslStructAttribute.Index, hslStructAttribute.Length), null);
				}
				else if (propertyType == typeof(long))
				{
					propertyInfo.SetValue(obj, byteTransform.TransInt64(buffer, startIndex + hslStructAttribute.Index), null);
				}
				else if (propertyType == typeof(long[]))
				{
					propertyInfo.SetValue(obj, byteTransform.TransInt64(buffer, startIndex + hslStructAttribute.Index, hslStructAttribute.Length), null);
				}
				else if (propertyType == typeof(ulong))
				{
					propertyInfo.SetValue(obj, byteTransform.TransUInt64(buffer, startIndex + hslStructAttribute.Index), null);
				}
				else if (propertyType == typeof(ulong[]))
				{
					propertyInfo.SetValue(obj, byteTransform.TransUInt64(buffer, startIndex + hslStructAttribute.Index, hslStructAttribute.Length), null);
				}
				else if (propertyType == typeof(float))
				{
					propertyInfo.SetValue(obj, byteTransform.TransSingle(buffer, startIndex + hslStructAttribute.Index), null);
				}
				else if (propertyType == typeof(float[]))
				{
					propertyInfo.SetValue(obj, byteTransform.TransSingle(buffer, startIndex + hslStructAttribute.Index, hslStructAttribute.Length), null);
				}
				else if (propertyType == typeof(double))
				{
					propertyInfo.SetValue(obj, byteTransform.TransDouble(buffer, startIndex + hslStructAttribute.Index), null);
				}
				else if (propertyType == typeof(double[]))
				{
					propertyInfo.SetValue(obj, byteTransform.TransDouble(buffer, startIndex + hslStructAttribute.Index, hslStructAttribute.Length), null);
				}
				else if (propertyType == typeof(string))
				{
					Encoding uTF = Encoding.UTF8;
					uTF = (string.IsNullOrEmpty(hslStructAttribute.Encoding) ? Encoding.ASCII : (hslStructAttribute.Encoding.Equals("ASCII", StringComparison.OrdinalIgnoreCase) ? Encoding.ASCII : (hslStructAttribute.Encoding.Equals("UNICODE", StringComparison.OrdinalIgnoreCase) ? Encoding.Unicode : (hslStructAttribute.Encoding.Equals("ANSI", StringComparison.OrdinalIgnoreCase) ? Encoding.Default : (hslStructAttribute.Encoding.Equals("UTF8", StringComparison.OrdinalIgnoreCase) ? Encoding.UTF8 : (hslStructAttribute.Encoding.Equals("BIG-UNICODE", StringComparison.OrdinalIgnoreCase) ? Encoding.BigEndianUnicode : ((!hslStructAttribute.Encoding.Equals("GB2312", StringComparison.OrdinalIgnoreCase)) ? Encoding.GetEncoding(hslStructAttribute.Encoding) : Encoding.GetEncoding("GB2312"))))))));
					if (hslStructAttribute.StringMode == 0)
					{
						propertyInfo.SetValue(obj, byteTransform.TransString(buffer, startIndex + hslStructAttribute.Index, hslStructAttribute.Length, uTF), null);
					}
					else if (hslStructAttribute.StringMode == 1)
					{
						propertyInfo.SetValue(obj, byteTransform.TransString(buffer, startIndex + hslStructAttribute.Index + 1, buffer[startIndex + hslStructAttribute.Index], uTF), null);
					}
					else if (hslStructAttribute.StringMode == 2)
					{
						propertyInfo.SetValue(obj, byteTransform.TransString(buffer, startIndex + hslStructAttribute.Index + 2, buffer[startIndex + hslStructAttribute.Index + 1], uTF), null);
					}
					else if (hslStructAttribute.StringMode == 3)
					{
						propertyInfo.SetValue(obj, byteTransform.TransString(buffer, startIndex + hslStructAttribute.Index + 2, byteTransform.TransUInt16(buffer, startIndex + hslStructAttribute.Index), uTF), null);
					}
					else if (hslStructAttribute.StringMode == 4)
					{
						propertyInfo.SetValue(obj, byteTransform.TransString(buffer, startIndex + hslStructAttribute.Index + 4, byteTransform.TransUInt16(buffer, startIndex + hslStructAttribute.Index + 2), uTF), null);
					}
					else if (hslStructAttribute.StringMode == 5)
					{
						propertyInfo.SetValue(obj, byteTransform.TransString(buffer, startIndex + hslStructAttribute.Index + 4, byteTransform.TransInt32(buffer, startIndex + hslStructAttribute.Index), uTF), null);
					}
				}
				else if (propertyType == typeof(bool))
				{
					propertyInfo.SetValue(obj, buffer.GetBoolByIndex(startIndex * 8 + hslStructAttribute.Index), null);
				}
				else if (propertyType == typeof(bool[]))
				{
					bool[] array = new bool[hslStructAttribute.Length];
					for (int j = 0; j < array.Length; j++)
					{
						array[j] = buffer.GetBoolByIndex(startIndex * 8 + hslStructAttribute.Index + j);
					}
					propertyInfo.SetValue(obj, array, null);
				}
				else
				{
					object obj2 = propertyType.Assembly.CreateInstance(propertyType.FullName);
					PraseStructContent(obj2, buffer, startIndex + hslStructAttribute.Index, byteTransform);
					propertyInfo.SetValue(obj, obj2, null);
				}
			}
		}

		/// <summary>
		/// 使用表达式树的方式来给一个属性赋值
		/// </summary>
		/// <param name="propertyInfo">属性信息</param>
		/// <param name="obj">对象信息</param>
		/// <param name="objValue">实际的值</param>
		public static void SetPropertyExp<T, K>(PropertyInfo propertyInfo, T obj, K objValue)
		{
			ParameterExpression parameterExpression = Expression.Parameter(typeof(T), "obj");
			ParameterExpression parameterExpression2 = Expression.Parameter(propertyInfo.PropertyType, "objValue");
			Expression.Lambda<Action<T, K>>(Expression.Call(parameterExpression, propertyInfo.GetSetMethod(), parameterExpression2), new ParameterExpression[2] { parameterExpression, parameterExpression2 }).Compile()(obj, objValue);
		}

		/// <summary>
		/// 从设备里读取支持Hsl特性的数据内容，该特性为<see cref="T:HslCommunication.Reflection.HslDeviceAddressAttribute" />，详细参考论坛的操作说明。
		/// </summary>
		/// <typeparam name="T">自定义的数据类型对象</typeparam>
		/// <param name="readWrite">读写接口的实现</param>
		/// <returns>包含是否成功的结果对象</returns>
		public static async Task<OperateResult<T>> ReadAsync<T>(IReadWriteNet readWrite) where T : class, new()
		{
			Type type = typeof(T);
			object obj = type.Assembly.CreateInstance(type.FullName);
			PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
			foreach (PropertyInfo property in properties)
			{
				Type propertyType = property.PropertyType;
				if (propertyType == typeof(string[]))
				{
					HslDeviceAddressAttribute[] hslAttributes = GetHslDeviceAddressAttributeArray(readWrite.GetType(), property);
					if (hslAttributes == null || hslAttributes.Length == 0)
					{
						continue;
					}
					string[] strings = new string[hslAttributes.Length];
					for (int j = 0; j < hslAttributes.Length; j++)
					{
						OperateResult<string> valueResult = await readWrite.ReadStringAsync(hslAttributes[j].Address, (ushort)((hslAttributes[j].Length < 0) ? 1u : ((uint)hslAttributes[j].Length)), hslAttributes[j].GetEncoding());
						if (!valueResult.IsSuccess)
						{
							return OperateResult.CreateFailedResult<T>(valueResult);
						}
						strings[j] = valueResult.Content;
					}
					property.SetValue(obj, strings, null);
				}
				else
				{
					if (property.GetCustomAttributes(typeof(HslDeviceAddressAttribute), false) == null)
					{
						continue;
					}
					HslDeviceAddressAttribute hslAttribute = GetHslDeviceAddressAttribute(readWrite.GetType(), property);
					if (hslAttribute == null)
					{
						continue;
					}
					if (propertyType == typeof(byte))
					{
						MethodInfo readByteMethod = readWrite.GetType().GetMethod("ReadByteAsync", new Type[1] { typeof(string) });
						if (readByteMethod == null)
						{
							return new OperateResult<T>(readWrite.GetType().Name + " not support read byte value. ");
						}
						if (!(readByteMethod.Invoke(readWrite, new object[1] { hslAttribute.Address }) is Task readByteTask))
						{
							return new OperateResult<T>(readWrite.GetType().Name + " not task type result. ");
						}
						await readByteTask;
						OperateResult<byte> valueResult2 = readByteTask.GetType().GetProperty("Result").GetValue(readByteTask, null) as OperateResult<byte>;
						if (!valueResult2.IsSuccess)
						{
							return OperateResult.CreateFailedResult<T>(valueResult2);
						}
						property.SetValue(obj, valueResult2.Content, null);
					}
					else if (propertyType == typeof(short))
					{
						OperateResult<short> valueResult3 = await readWrite.ReadInt16Async(hslAttribute.Address);
						if (!valueResult3.IsSuccess)
						{
							return OperateResult.CreateFailedResult<T>(valueResult3);
						}
						property.SetValue(obj, valueResult3.Content, null);
					}
					else if (propertyType == typeof(short[]))
					{
						OperateResult<short[]> valueResult4 = await readWrite.ReadInt16Async(hslAttribute.Address, (ushort)((hslAttribute.Length < 0) ? 1u : ((uint)hslAttribute.Length)));
						if (!valueResult4.IsSuccess)
						{
							return OperateResult.CreateFailedResult<T>(valueResult4);
						}
						property.SetValue(obj, valueResult4.Content, null);
					}
					else if (propertyType == typeof(ushort))
					{
						OperateResult<ushort> valueResult5 = await readWrite.ReadUInt16Async(hslAttribute.Address);
						if (!valueResult5.IsSuccess)
						{
							return OperateResult.CreateFailedResult<T>(valueResult5);
						}
						property.SetValue(obj, valueResult5.Content, null);
					}
					else if (propertyType == typeof(ushort[]))
					{
						OperateResult<ushort[]> valueResult6 = await readWrite.ReadUInt16Async(hslAttribute.Address, (ushort)((hslAttribute.Length < 0) ? 1u : ((uint)hslAttribute.Length)));
						if (!valueResult6.IsSuccess)
						{
							return OperateResult.CreateFailedResult<T>(valueResult6);
						}
						property.SetValue(obj, valueResult6.Content, null);
					}
					else if (propertyType == typeof(int))
					{
						OperateResult<int> valueResult7 = await readWrite.ReadInt32Async(hslAttribute.Address);
						if (!valueResult7.IsSuccess)
						{
							return OperateResult.CreateFailedResult<T>(valueResult7);
						}
						property.SetValue(obj, valueResult7.Content, null);
					}
					else if (propertyType == typeof(int[]))
					{
						OperateResult<int[]> valueResult8 = await readWrite.ReadInt32Async(hslAttribute.Address, (ushort)((hslAttribute.Length < 0) ? 1u : ((uint)hslAttribute.Length)));
						if (!valueResult8.IsSuccess)
						{
							return OperateResult.CreateFailedResult<T>(valueResult8);
						}
						property.SetValue(obj, valueResult8.Content, null);
					}
					else if (propertyType == typeof(uint))
					{
						OperateResult<uint> valueResult9 = await readWrite.ReadUInt32Async(hslAttribute.Address);
						if (!valueResult9.IsSuccess)
						{
							return OperateResult.CreateFailedResult<T>(valueResult9);
						}
						property.SetValue(obj, valueResult9.Content, null);
					}
					else if (propertyType == typeof(uint[]))
					{
						OperateResult<uint[]> valueResult10 = await readWrite.ReadUInt32Async(hslAttribute.Address, (ushort)((hslAttribute.Length < 0) ? 1u : ((uint)hslAttribute.Length)));
						if (!valueResult10.IsSuccess)
						{
							return OperateResult.CreateFailedResult<T>(valueResult10);
						}
						property.SetValue(obj, valueResult10.Content, null);
					}
					else if (propertyType == typeof(long))
					{
						OperateResult<long> valueResult11 = await readWrite.ReadInt64Async(hslAttribute.Address);
						if (!valueResult11.IsSuccess)
						{
							return OperateResult.CreateFailedResult<T>(valueResult11);
						}
						property.SetValue(obj, valueResult11.Content, null);
					}
					else if (propertyType == typeof(long[]))
					{
						OperateResult<long[]> valueResult12 = await readWrite.ReadInt64Async(hslAttribute.Address, (ushort)((hslAttribute.Length < 0) ? 1u : ((uint)hslAttribute.Length)));
						if (!valueResult12.IsSuccess)
						{
							return OperateResult.CreateFailedResult<T>(valueResult12);
						}
						property.SetValue(obj, valueResult12.Content, null);
					}
					else if (propertyType == typeof(ulong))
					{
						OperateResult<ulong> valueResult13 = await readWrite.ReadUInt64Async(hslAttribute.Address);
						if (!valueResult13.IsSuccess)
						{
							return OperateResult.CreateFailedResult<T>(valueResult13);
						}
						property.SetValue(obj, valueResult13.Content, null);
					}
					else if (propertyType == typeof(ulong[]))
					{
						OperateResult<ulong[]> valueResult14 = await readWrite.ReadUInt64Async(hslAttribute.Address, (ushort)((hslAttribute.Length < 0) ? 1u : ((uint)hslAttribute.Length)));
						if (!valueResult14.IsSuccess)
						{
							return OperateResult.CreateFailedResult<T>(valueResult14);
						}
						property.SetValue(obj, valueResult14.Content, null);
					}
					else if (propertyType == typeof(float))
					{
						OperateResult<float> valueResult15 = await readWrite.ReadFloatAsync(hslAttribute.Address);
						if (!valueResult15.IsSuccess)
						{
							return OperateResult.CreateFailedResult<T>(valueResult15);
						}
						property.SetValue(obj, valueResult15.Content, null);
					}
					else if (propertyType == typeof(float[]))
					{
						OperateResult<float[]> valueResult16 = await readWrite.ReadFloatAsync(hslAttribute.Address, (ushort)((hslAttribute.Length < 0) ? 1u : ((uint)hslAttribute.Length)));
						if (!valueResult16.IsSuccess)
						{
							return OperateResult.CreateFailedResult<T>(valueResult16);
						}
						property.SetValue(obj, valueResult16.Content, null);
					}
					else if (propertyType == typeof(double))
					{
						OperateResult<double> valueResult17 = await readWrite.ReadDoubleAsync(hslAttribute.Address);
						if (!valueResult17.IsSuccess)
						{
							return OperateResult.CreateFailedResult<T>(valueResult17);
						}
						property.SetValue(obj, valueResult17.Content, null);
					}
					else if (propertyType == typeof(double[]))
					{
						OperateResult<double[]> valueResult18 = await readWrite.ReadDoubleAsync(hslAttribute.Address, (ushort)((hslAttribute.Length < 0) ? 1u : ((uint)hslAttribute.Length)));
						if (!valueResult18.IsSuccess)
						{
							return OperateResult.CreateFailedResult<T>(valueResult18);
						}
						property.SetValue(obj, valueResult18.Content, null);
					}
					else if (propertyType == typeof(string))
					{
						OperateResult<string> valueResult19 = await readWrite.ReadStringAsync(hslAttribute.Address, (ushort)((hslAttribute.Length < 0) ? 1u : ((uint)hslAttribute.Length)), hslAttribute.GetEncoding());
						if (!valueResult19.IsSuccess)
						{
							return OperateResult.CreateFailedResult<T>(valueResult19);
						}
						property.SetValue(obj, valueResult19.Content, null);
					}
					else if (propertyType == typeof(byte[]))
					{
						OperateResult<byte[]> valueResult20 = await readWrite.ReadAsync(hslAttribute.Address, (ushort)((hslAttribute.Length < 0) ? 1u : ((uint)hslAttribute.Length)));
						if (!valueResult20.IsSuccess)
						{
							return OperateResult.CreateFailedResult<T>(valueResult20);
						}
						property.SetValue(obj, valueResult20.Content, null);
					}
					else if (propertyType == typeof(bool))
					{
						OperateResult<bool> valueResult21 = await readWrite.ReadBoolAsync(hslAttribute.Address);
						if (!valueResult21.IsSuccess)
						{
							return OperateResult.CreateFailedResult<T>(valueResult21);
						}
						property.SetValue(obj, valueResult21.Content, null);
					}
					else if (propertyType == typeof(bool[]))
					{
						OperateResult<bool[]> valueResult22 = await readWrite.ReadBoolAsync(hslAttribute.Address, (ushort)((hslAttribute.Length < 0) ? 1u : ((uint)hslAttribute.Length)));
						if (!valueResult22.IsSuccess)
						{
							return OperateResult.CreateFailedResult<T>(valueResult22);
						}
						property.SetValue(obj, valueResult22.Content, null);
					}
				}
			}
			return OperateResult.CreateSuccessResult((T)obj);
		}

		/// <summary>
		/// 从设备里读取支持Hsl特性的数据内容，该特性为<see cref="T:HslCommunication.Reflection.HslDeviceAddressAttribute" />，详细参考论坛的操作说明。
		/// </summary>
		/// <typeparam name="T">自定义的数据类型对象</typeparam>
		/// <param name="data">自定义的数据对象</param>
		/// <param name="readWrite">数据读写对象</param>
		/// <returns>包含是否成功的结果对象</returns>
		/// <exception cref="T:System.ArgumentNullException"></exception>
		public static async Task<OperateResult> WriteAsync<T>(T data, IReadWriteNet readWrite) where T : class, new()
		{
			if (data == null)
			{
				throw new ArgumentNullException("data");
			}
			Type type = typeof(T);
			PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
			foreach (PropertyInfo property in properties)
			{
				Type propertyType = property.PropertyType;
				if (propertyType == typeof(string[]))
				{
					HslDeviceAddressAttribute[] hslAttributes = GetHslDeviceAddressAttributeArray(readWrite.GetType(), property);
					if (hslAttributes == null || hslAttributes.Length == 0)
					{
						continue;
					}
					string[] strings = (string[])property.GetValue(data, null);
					for (int j = 0; j < hslAttributes.Length; j++)
					{
						OperateResult writeResult = await readWrite.WriteAsync(hslAttributes[j].Address, strings[j], hslAttributes[j].GetEncoding());
						if (!writeResult.IsSuccess)
						{
							return writeResult;
						}
					}
				}
				else
				{
					if (property.GetCustomAttributes(typeof(HslDeviceAddressAttribute), false) == null)
					{
						continue;
					}
					HslDeviceAddressAttribute hslAttribute = GetHslDeviceAddressAttribute(readWrite.GetType(), property);
					if (hslAttribute == null)
					{
						continue;
					}
					if (propertyType == typeof(byte))
					{
						MethodInfo method = readWrite.GetType().GetMethod("WriteAsync", new Type[2]
						{
							typeof(string),
							typeof(byte)
						});
						if (method == null)
						{
							return new OperateResult<T>(readWrite.GetType().Name + " not support write byte value. ");
						}
						byte value = (byte)property.GetValue(data, null);
						if (!(method.Invoke(readWrite, new object[2] { hslAttribute.Address, value }) is Task writeTask))
						{
							return new OperateResult(readWrite.GetType().Name + " not task type result. ");
						}
						await writeTask;
						OperateResult valueResult = writeTask.GetType().GetProperty("Result").GetValue(writeTask, null) as OperateResult;
						if (!valueResult.IsSuccess)
						{
							return OperateResult.CreateFailedResult<T>(valueResult);
						}
					}
					else if (propertyType == typeof(short))
					{
						OperateResult writeResult2 = await readWrite.WriteAsync(value: (short)property.GetValue(data, null), address: hslAttribute.Address);
						if (!writeResult2.IsSuccess)
						{
							return writeResult2;
						}
					}
					else if (propertyType == typeof(short[]))
					{
						OperateResult writeResult3 = await readWrite.WriteAsync(values: (short[])property.GetValue(data, null), address: hslAttribute.Address);
						if (!writeResult3.IsSuccess)
						{
							return writeResult3;
						}
					}
					else if (propertyType == typeof(ushort))
					{
						OperateResult writeResult4 = await readWrite.WriteAsync(value: (ushort)property.GetValue(data, null), address: hslAttribute.Address);
						if (!writeResult4.IsSuccess)
						{
							return writeResult4;
						}
					}
					else if (propertyType == typeof(ushort[]))
					{
						OperateResult writeResult5 = await readWrite.WriteAsync(values: (ushort[])property.GetValue(data, null), address: hslAttribute.Address);
						if (!writeResult5.IsSuccess)
						{
							return writeResult5;
						}
					}
					else if (propertyType == typeof(int))
					{
						OperateResult writeResult6 = await readWrite.WriteAsync(value: (int)property.GetValue(data, null), address: hslAttribute.Address);
						if (!writeResult6.IsSuccess)
						{
							return writeResult6;
						}
					}
					else if (propertyType == typeof(int[]))
					{
						OperateResult writeResult7 = await readWrite.WriteAsync(values: (int[])property.GetValue(data, null), address: hslAttribute.Address);
						if (!writeResult7.IsSuccess)
						{
							return writeResult7;
						}
					}
					else if (propertyType == typeof(uint))
					{
						OperateResult writeResult8 = await readWrite.WriteAsync(value: (uint)property.GetValue(data, null), address: hslAttribute.Address);
						if (!writeResult8.IsSuccess)
						{
							return writeResult8;
						}
					}
					else if (propertyType == typeof(uint[]))
					{
						OperateResult writeResult9 = await readWrite.WriteAsync(values: (uint[])property.GetValue(data, null), address: hslAttribute.Address);
						if (!writeResult9.IsSuccess)
						{
							return writeResult9;
						}
					}
					else if (propertyType == typeof(long))
					{
						OperateResult writeResult10 = await readWrite.WriteAsync(value: (long)property.GetValue(data, null), address: hslAttribute.Address);
						if (!writeResult10.IsSuccess)
						{
							return writeResult10;
						}
					}
					else if (propertyType == typeof(long[]))
					{
						OperateResult writeResult11 = await readWrite.WriteAsync(values: (long[])property.GetValue(data, null), address: hslAttribute.Address);
						if (!writeResult11.IsSuccess)
						{
							return writeResult11;
						}
					}
					else if (propertyType == typeof(ulong))
					{
						OperateResult writeResult12 = await readWrite.WriteAsync(value: (ulong)property.GetValue(data, null), address: hslAttribute.Address);
						if (!writeResult12.IsSuccess)
						{
							return writeResult12;
						}
					}
					else if (propertyType == typeof(ulong[]))
					{
						OperateResult writeResult13 = await readWrite.WriteAsync(values: (ulong[])property.GetValue(data, null), address: hslAttribute.Address);
						if (!writeResult13.IsSuccess)
						{
							return writeResult13;
						}
					}
					else if (propertyType == typeof(float))
					{
						OperateResult writeResult14 = await readWrite.WriteAsync(value: (float)property.GetValue(data, null), address: hslAttribute.Address);
						if (!writeResult14.IsSuccess)
						{
							return writeResult14;
						}
					}
					else if (propertyType == typeof(float[]))
					{
						OperateResult writeResult15 = await readWrite.WriteAsync(values: (float[])property.GetValue(data, null), address: hslAttribute.Address);
						if (!writeResult15.IsSuccess)
						{
							return writeResult15;
						}
					}
					else if (propertyType == typeof(double))
					{
						OperateResult writeResult16 = await readWrite.WriteAsync(value: (double)property.GetValue(data, null), address: hslAttribute.Address);
						if (!writeResult16.IsSuccess)
						{
							return writeResult16;
						}
					}
					else if (propertyType == typeof(double[]))
					{
						OperateResult writeResult17 = await readWrite.WriteAsync(values: (double[])property.GetValue(data, null), address: hslAttribute.Address);
						if (!writeResult17.IsSuccess)
						{
							return writeResult17;
						}
					}
					else if (propertyType == typeof(string))
					{
						OperateResult writeResult18 = await readWrite.WriteAsync(value: (string)property.GetValue(data, null), address: hslAttribute.Address, encoding: hslAttribute.GetEncoding());
						if (!writeResult18.IsSuccess)
						{
							return writeResult18;
						}
					}
					else if (propertyType == typeof(byte[]))
					{
						OperateResult writeResult19 = await readWrite.WriteAsync(value: (byte[])property.GetValue(data, null), address: hslAttribute.Address);
						if (!writeResult19.IsSuccess)
						{
							return writeResult19;
						}
					}
					else if (propertyType == typeof(bool))
					{
						OperateResult writeResult20 = await readWrite.WriteAsync(value: (bool)property.GetValue(data, null), address: hslAttribute.Address);
						if (!writeResult20.IsSuccess)
						{
							return writeResult20;
						}
					}
					else if (propertyType == typeof(bool[]))
					{
						OperateResult writeResult21 = await readWrite.WriteAsync(value: (bool[])property.GetValue(data, null), address: hslAttribute.Address);
						if (!writeResult21.IsSuccess)
						{
							return writeResult21;
						}
					}
				}
			}
			return OperateResult.CreateSuccessResult(data);
		}

		internal static void SetPropertyObjectValue(PropertyInfo property, object obj, string value)
		{
			Type propertyType = property.PropertyType;
			if (propertyType == typeof(short))
			{
				property.SetValue(obj, short.Parse(value), null);
			}
			else if (propertyType == typeof(ushort))
			{
				property.SetValue(obj, ushort.Parse(value), null);
			}
			else if (propertyType == typeof(int))
			{
				property.SetValue(obj, int.Parse(value), null);
			}
			else if (propertyType == typeof(uint))
			{
				property.SetValue(obj, uint.Parse(value), null);
			}
			else if (propertyType == typeof(long))
			{
				property.SetValue(obj, long.Parse(value), null);
			}
			else if (propertyType == typeof(ulong))
			{
				property.SetValue(obj, ulong.Parse(value), null);
			}
			else if (propertyType == typeof(float))
			{
				property.SetValue(obj, float.Parse(value), null);
			}
			else if (propertyType == typeof(double))
			{
				property.SetValue(obj, double.Parse(value), null);
			}
			else if (propertyType == typeof(string))
			{
				property.SetValue(obj, value, null);
			}
			else if (propertyType == typeof(byte))
			{
				property.SetValue(obj, byte.Parse(value), null);
			}
			else if (propertyType == typeof(bool))
			{
				property.SetValue(obj, bool.Parse(value), null);
			}
			else
			{
				property.SetValue(obj, value, null);
			}
		}

		internal static void SetPropertyObjectValueArray(PropertyInfo property, object obj, string[] values)
		{
			Type propertyType = property.PropertyType;
			if (propertyType == typeof(short[]))
			{
				property.SetValue(obj, values.Select((string m) => short.Parse(m)).ToArray(), null);
			}
			else if (propertyType == typeof(List<short>))
			{
				property.SetValue(obj, values.Select((string m) => short.Parse(m)).ToList(), null);
			}
			else if (propertyType == typeof(ushort[]))
			{
				property.SetValue(obj, values.Select((string m) => ushort.Parse(m)).ToArray(), null);
			}
			else if (propertyType == typeof(List<ushort>))
			{
				property.SetValue(obj, values.Select((string m) => ushort.Parse(m)).ToList(), null);
			}
			else if (propertyType == typeof(int[]))
			{
				property.SetValue(obj, values.Select((string m) => int.Parse(m)).ToArray(), null);
			}
			else if (propertyType == typeof(List<int>))
			{
				property.SetValue(obj, values.Select((string m) => int.Parse(m)).ToList(), null);
			}
			else if (propertyType == typeof(uint[]))
			{
				property.SetValue(obj, values.Select((string m) => uint.Parse(m)).ToArray(), null);
			}
			else if (propertyType == typeof(List<uint>))
			{
				property.SetValue(obj, values.Select((string m) => uint.Parse(m)).ToList(), null);
			}
			else if (propertyType == typeof(long[]))
			{
				property.SetValue(obj, values.Select((string m) => long.Parse(m)).ToArray(), null);
			}
			else if (propertyType == typeof(List<long>))
			{
				property.SetValue(obj, values.Select((string m) => long.Parse(m)).ToList(), null);
			}
			else if (propertyType == typeof(ulong[]))
			{
				property.SetValue(obj, values.Select((string m) => ulong.Parse(m)).ToArray(), null);
			}
			else if (propertyType == typeof(List<ulong>))
			{
				property.SetValue(obj, values.Select((string m) => ulong.Parse(m)).ToList(), null);
			}
			else if (propertyType == typeof(float[]))
			{
				property.SetValue(obj, values.Select((string m) => float.Parse(m)).ToArray(), null);
			}
			else if (propertyType == typeof(List<float>))
			{
				property.SetValue(obj, values.Select((string m) => float.Parse(m)).ToList(), null);
			}
			else if (propertyType == typeof(double[]))
			{
				property.SetValue(obj, values.Select((string m) => double.Parse(m)).ToArray(), null);
			}
			else if (propertyType == typeof(double[]))
			{
				property.SetValue(obj, values.Select((string m) => double.Parse(m)).ToList(), null);
			}
			else if (propertyType == typeof(string[]))
			{
				property.SetValue(obj, values, null);
			}
			else if (propertyType == typeof(List<string>))
			{
				property.SetValue(obj, new List<string>(values), null);
			}
			else if (propertyType == typeof(byte[]))
			{
				property.SetValue(obj, values.Select((string m) => byte.Parse(m)).ToArray(), null);
			}
			else if (propertyType == typeof(List<byte>))
			{
				property.SetValue(obj, values.Select((string m) => byte.Parse(m)).ToList(), null);
			}
			else if (propertyType == typeof(bool[]))
			{
				property.SetValue(obj, values.Select((string m) => bool.Parse(m)).ToArray(), null);
			}
			else if (propertyType == typeof(List<bool>))
			{
				property.SetValue(obj, values.Select((string m) => bool.Parse(m)).ToList(), null);
			}
			else
			{
				property.SetValue(obj, values, null);
			}
		}

		/// <summary>
		/// 从设备里读取支持Hsl特性的数据内容，
		/// 该特性为<see cref="T:HslCommunication.Reflection.HslRedisKeyAttribute" />，<see cref="T:HslCommunication.Reflection.HslRedisListItemAttribute" />，
		/// <see cref="T:HslCommunication.Reflection.HslRedisListAttribute" />，<see cref="T:HslCommunication.Reflection.HslRedisHashFieldAttribute" />
		/// 详细参考代码示例的操作说明。
		/// </summary>
		/// <typeparam name="T">自定义的数据类型对象</typeparam>
		/// <param name="redis">Redis的数据对象</param>
		/// <returns>包含是否成功的结果对象</returns>
		public static OperateResult<T> Read<T>(RedisClient redis) where T : class, new()
		{
			Type typeFromHandle = typeof(T);
			object obj = typeFromHandle.Assembly.CreateInstance(typeFromHandle.FullName);
			PropertyInfo[] properties = typeFromHandle.GetProperties(BindingFlags.Instance | BindingFlags.Public);
			List<PropertyInfoKeyName> list = new List<PropertyInfoKeyName>();
			List<PropertyInfoHashKeyName> list2 = new List<PropertyInfoHashKeyName>();
			PropertyInfo[] array = properties;
			foreach (PropertyInfo propertyInfo in array)
			{
				object[] customAttributes = propertyInfo.GetCustomAttributes(typeof(HslRedisKeyAttribute), false);
				if (customAttributes != null && customAttributes.Length != 0)
				{
					list.Add(new PropertyInfoKeyName(propertyInfo, ((HslRedisKeyAttribute)customAttributes[0]).KeyName));
					continue;
				}
				customAttributes = propertyInfo.GetCustomAttributes(typeof(HslRedisListItemAttribute), false);
				if (customAttributes != null && customAttributes.Length != 0)
				{
					HslRedisListItemAttribute hslRedisListItemAttribute = (HslRedisListItemAttribute)customAttributes[0];
					OperateResult<string> operateResult = redis.ReadListByIndex(hslRedisListItemAttribute.ListKey, hslRedisListItemAttribute.Index);
					if (!operateResult.IsSuccess)
					{
						return OperateResult.CreateFailedResult<T>(operateResult);
					}
					SetPropertyObjectValue(propertyInfo, obj, operateResult.Content);
					continue;
				}
				customAttributes = propertyInfo.GetCustomAttributes(typeof(HslRedisListAttribute), false);
				if (customAttributes != null && customAttributes.Length != 0)
				{
					HslRedisListAttribute hslRedisListAttribute = (HslRedisListAttribute)customAttributes[0];
					OperateResult<string[]> operateResult2 = redis.ListRange(hslRedisListAttribute.ListKey, hslRedisListAttribute.StartIndex, hslRedisListAttribute.EndIndex);
					if (!operateResult2.IsSuccess)
					{
						return OperateResult.CreateFailedResult<T>(operateResult2);
					}
					SetPropertyObjectValueArray(propertyInfo, obj, operateResult2.Content);
				}
				else
				{
					customAttributes = propertyInfo.GetCustomAttributes(typeof(HslRedisHashFieldAttribute), false);
					if (customAttributes != null && customAttributes.Length != 0)
					{
						HslRedisHashFieldAttribute hslRedisHashFieldAttribute = (HslRedisHashFieldAttribute)customAttributes[0];
						list2.Add(new PropertyInfoHashKeyName(propertyInfo, hslRedisHashFieldAttribute.HaskKey, hslRedisHashFieldAttribute.Field));
					}
				}
			}
			if (list.Count > 0)
			{
				OperateResult<string[]> operateResult3 = redis.ReadKey(list.Select((PropertyInfoKeyName m) => m.KeyName).ToArray());
				if (!operateResult3.IsSuccess)
				{
					return OperateResult.CreateFailedResult<T>(operateResult3);
				}
				for (int j = 0; j < list.Count; j++)
				{
					SetPropertyObjectValue(list[j].PropertyInfo, obj, operateResult3.Content[j]);
				}
			}
			if (list2.Count > 0)
			{
				foreach (var item in from m in list2
					group m by m.KeyName into g
					select new
					{
						Key = g.Key,
						Values = g.ToArray()
					})
				{
					if (item.Values.Length == 1)
					{
						OperateResult<string> operateResult4 = redis.ReadHashKey(item.Key, item.Values[0].Field);
						if (!operateResult4.IsSuccess)
						{
							return OperateResult.CreateFailedResult<T>(operateResult4);
						}
						SetPropertyObjectValue(item.Values[0].PropertyInfo, obj, operateResult4.Content);
						continue;
					}
					OperateResult<string[]> operateResult5 = redis.ReadHashKey(item.Key, item.Values.Select((PropertyInfoHashKeyName m) => m.Field).ToArray());
					if (!operateResult5.IsSuccess)
					{
						return OperateResult.CreateFailedResult<T>(operateResult5);
					}
					for (int k = 0; k < item.Values.Length; k++)
					{
						SetPropertyObjectValue(item.Values[k].PropertyInfo, obj, operateResult5.Content[k]);
					}
				}
			}
			return OperateResult.CreateSuccessResult((T)obj);
		}

		/// <summary>
		/// 从设备里写入支持Hsl特性的数据内容，
		/// 该特性为<see cref="T:HslCommunication.Reflection.HslRedisKeyAttribute" /> ，<see cref="T:HslCommunication.Reflection.HslRedisHashFieldAttribute" />
		/// 需要注意的是写入并不支持<see cref="T:HslCommunication.Reflection.HslRedisListAttribute" />，<see cref="T:HslCommunication.Reflection.HslRedisListItemAttribute" />特性，详细参考代码示例的操作说明。
		/// </summary>
		/// <typeparam name="T">自定义的数据类型对象</typeparam>
		/// <param name="data">等待写入的数据参数</param>
		/// <param name="redis">Redis的数据对象</param>
		/// <returns>包含是否成功的结果对象</returns>
		public static OperateResult Write<T>(T data, RedisClient redis) where T : class, new()
		{
			Type typeFromHandle = typeof(T);
			PropertyInfo[] properties = typeFromHandle.GetProperties(BindingFlags.Instance | BindingFlags.Public);
			List<PropertyInfoKeyName> list = new List<PropertyInfoKeyName>();
			List<PropertyInfoHashKeyName> list2 = new List<PropertyInfoHashKeyName>();
			PropertyInfo[] array = properties;
			foreach (PropertyInfo propertyInfo in array)
			{
				object[] customAttributes = propertyInfo.GetCustomAttributes(typeof(HslRedisKeyAttribute), false);
				if (customAttributes != null && customAttributes.Length != 0)
				{
					list.Add(new PropertyInfoKeyName(propertyInfo, ((HslRedisKeyAttribute)customAttributes[0]).KeyName, propertyInfo.GetValue(data, null).ToString()));
					continue;
				}
				customAttributes = propertyInfo.GetCustomAttributes(typeof(HslRedisHashFieldAttribute), false);
				if (customAttributes != null && customAttributes.Length != 0)
				{
					HslRedisHashFieldAttribute hslRedisHashFieldAttribute = (HslRedisHashFieldAttribute)customAttributes[0];
					list2.Add(new PropertyInfoHashKeyName(propertyInfo, hslRedisHashFieldAttribute.HaskKey, hslRedisHashFieldAttribute.Field, propertyInfo.GetValue(data, null).ToString()));
				}
			}
			if (list.Count > 0)
			{
				OperateResult operateResult = redis.WriteKey(list.Select((PropertyInfoKeyName m) => m.KeyName).ToArray(), list.Select((PropertyInfoKeyName m) => m.Value).ToArray());
				if (!operateResult.IsSuccess)
				{
					return operateResult;
				}
			}
			if (list2.Count > 0)
			{
				foreach (var item in from m in list2
					group m by m.KeyName into g
					select new
					{
						Key = g.Key,
						Values = g.ToArray()
					})
				{
					if (item.Values.Length == 1)
					{
						OperateResult operateResult2 = redis.WriteHashKey(item.Key, item.Values[0].Field, item.Values[0].Value);
						if (!operateResult2.IsSuccess)
						{
							return operateResult2;
						}
						continue;
					}
					OperateResult operateResult3 = redis.WriteHashKey(item.Key, item.Values.Select((PropertyInfoHashKeyName m) => m.Field).ToArray(), item.Values.Select((PropertyInfoHashKeyName m) => m.Value).ToArray());
					if (operateResult3.IsSuccess)
					{
						continue;
					}
					return operateResult3;
				}
			}
			return OperateResult.CreateSuccessResult();
		}

		/// <summary>
		/// 从设备里读取支持Hsl特性的数据内容，
		/// 该特性为<see cref="T:HslCommunication.Reflection.HslRedisKeyAttribute" />，<see cref="T:HslCommunication.Reflection.HslRedisListItemAttribute" />，
		/// <see cref="T:HslCommunication.Reflection.HslRedisListAttribute" />，<see cref="T:HslCommunication.Reflection.HslRedisHashFieldAttribute" />
		/// 详细参考代码示例的操作说明。
		/// </summary>
		/// <typeparam name="T">自定义的数据类型对象</typeparam>
		/// <param name="redis">Redis的数据对象</param>
		/// <returns>包含是否成功的结果对象</returns>
		public static async Task<OperateResult<T>> ReadAsync<T>(RedisClient redis) where T : class, new()
		{
			Type type = typeof(T);
			object obj = type.Assembly.CreateInstance(type.FullName);
			PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
			List<PropertyInfoKeyName> keyPropertyInfos = new List<PropertyInfoKeyName>();
			List<PropertyInfoHashKeyName> propertyInfoHashKeys = new List<PropertyInfoHashKeyName>();
			PropertyInfo[] array = properties;
			foreach (PropertyInfo property in array)
			{
				object[] attributes = property.GetCustomAttributes(typeof(HslRedisKeyAttribute), false);
				object[] array2 = attributes;
				if (array2 != null && array2.Length != 0)
				{
					keyPropertyInfos.Add(new PropertyInfoKeyName(property, ((HslRedisKeyAttribute)attributes[0]).KeyName));
					continue;
				}
				attributes = property.GetCustomAttributes(typeof(HslRedisListItemAttribute), false);
				object[] array3 = attributes;
				if (array3 != null && array3.Length != 0)
				{
					HslRedisListItemAttribute attribute = (HslRedisListItemAttribute)attributes[0];
					OperateResult<string> read = await redis.ReadListByIndexAsync(attribute.ListKey, attribute.Index);
					if (!read.IsSuccess)
					{
						return OperateResult.CreateFailedResult<T>(read);
					}
					SetPropertyObjectValue(property, obj, read.Content);
					continue;
				}
				attributes = property.GetCustomAttributes(typeof(HslRedisListAttribute), false);
				object[] array4 = attributes;
				if (array4 != null && array4.Length != 0)
				{
					HslRedisListAttribute attribute2 = (HslRedisListAttribute)attributes[0];
					OperateResult<string[]> read2 = await redis.ListRangeAsync(attribute2.ListKey, attribute2.StartIndex, attribute2.EndIndex);
					if (!read2.IsSuccess)
					{
						return OperateResult.CreateFailedResult<T>(read2);
					}
					SetPropertyObjectValueArray(property, obj, read2.Content);
				}
				else
				{
					attributes = property.GetCustomAttributes(typeof(HslRedisHashFieldAttribute), false);
					object[] array5 = attributes;
					if (array5 != null && array5.Length != 0)
					{
						HslRedisHashFieldAttribute attribute3 = (HslRedisHashFieldAttribute)attributes[0];
						propertyInfoHashKeys.Add(new PropertyInfoHashKeyName(property, attribute3.HaskKey, attribute3.Field));
					}
				}
			}
			if (keyPropertyInfos.Count > 0)
			{
				OperateResult<string[]> readKeys = await redis.ReadKeyAsync(keyPropertyInfos.Select((PropertyInfoKeyName m) => m.KeyName).ToArray());
				if (!readKeys.IsSuccess)
				{
					return OperateResult.CreateFailedResult<T>(readKeys);
				}
				for (int j = 0; j < keyPropertyInfos.Count; j++)
				{
					SetPropertyObjectValue(keyPropertyInfos[j].PropertyInfo, obj, readKeys.Content[j]);
				}
			}
			if (propertyInfoHashKeys.Count > 0)
			{
				foreach (var item in from m in propertyInfoHashKeys
					group m by m.KeyName into g
					select new
					{
						Key = g.Key,
						Values = g.ToArray()
					})
				{
					if (item.Values.Length == 1)
					{
						OperateResult<string> readKey = await redis.ReadHashKeyAsync(item.Key, item.Values[0].Field);
						if (!readKey.IsSuccess)
						{
							return OperateResult.CreateFailedResult<T>(readKey);
						}
						SetPropertyObjectValue(item.Values[0].PropertyInfo, obj, readKey.Content);
						continue;
					}
					OperateResult<string[]> readKeys2 = await redis.ReadHashKeyAsync(item.Key, item.Values.Select((PropertyInfoHashKeyName m) => m.Field).ToArray());
					if (!readKeys2.IsSuccess)
					{
						return OperateResult.CreateFailedResult<T>(readKeys2);
					}
					for (int k = 0; k < item.Values.Length; k++)
					{
						SetPropertyObjectValue(item.Values[k].PropertyInfo, obj, readKeys2.Content[k]);
					}
				}
			}
			return OperateResult.CreateSuccessResult((T)obj);
		}

		/// <summary>
		/// 从设备里写入支持Hsl特性的数据内容，
		/// 该特性为<see cref="T:HslCommunication.Reflection.HslRedisKeyAttribute" /> ，<see cref="T:HslCommunication.Reflection.HslRedisHashFieldAttribute" />
		/// 需要注意的是写入并不支持<see cref="T:HslCommunication.Reflection.HslRedisListAttribute" />，<see cref="T:HslCommunication.Reflection.HslRedisListItemAttribute" />特性，详细参考代码示例的操作说明。
		/// </summary>
		/// <typeparam name="T">自定义的数据类型对象</typeparam>
		/// <param name="data">等待写入的数据参数</param>
		/// <param name="redis">Redis的数据对象</param>
		/// <returns>包含是否成功的结果对象</returns>
		public static async Task<OperateResult> WriteAsync<T>(T data, RedisClient redis) where T : class, new()
		{
			Type type = typeof(T);
			PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
			List<PropertyInfoKeyName> keyPropertyInfos = new List<PropertyInfoKeyName>();
			List<PropertyInfoHashKeyName> propertyInfoHashKeys = new List<PropertyInfoHashKeyName>();
			PropertyInfo[] array = properties;
			foreach (PropertyInfo property in array)
			{
				object[] attributes = property.GetCustomAttributes(typeof(HslRedisKeyAttribute), false);
				object[] array2 = attributes;
				if (array2 != null && array2.Length != 0)
				{
					keyPropertyInfos.Add(new PropertyInfoKeyName(property, ((HslRedisKeyAttribute)attributes[0]).KeyName, property.GetValue(data, null).ToString()));
					continue;
				}
				attributes = property.GetCustomAttributes(typeof(HslRedisHashFieldAttribute), false);
				object[] array3 = attributes;
				if (array3 != null && array3.Length != 0)
				{
					HslRedisHashFieldAttribute attribute = (HslRedisHashFieldAttribute)attributes[0];
					propertyInfoHashKeys.Add(new PropertyInfoHashKeyName(property, attribute.HaskKey, attribute.Field, property.GetValue(data, null).ToString()));
				}
			}
			if (keyPropertyInfos.Count > 0)
			{
				OperateResult writeResult = await redis.WriteKeyAsync(keyPropertyInfos.Select((PropertyInfoKeyName m) => m.KeyName).ToArray(), keyPropertyInfos.Select((PropertyInfoKeyName m) => m.Value).ToArray());
				if (!writeResult.IsSuccess)
				{
					return writeResult;
				}
			}
			if (propertyInfoHashKeys.Count > 0)
			{
				foreach (var item in from m in propertyInfoHashKeys
					group m by m.KeyName into g
					select new
					{
						Key = g.Key,
						Values = g.ToArray()
					})
				{
					if (item.Values.Length == 1)
					{
						OperateResult writeResult2 = await redis.WriteHashKeyAsync(item.Key, item.Values[0].Field, item.Values[0].Value);
						if (!writeResult2.IsSuccess)
						{
							return writeResult2;
						}
						continue;
					}
					OperateResult writeResult3 = await redis.WriteHashKeyAsync(item.Key, item.Values.Select((PropertyInfoHashKeyName m) => m.Field).ToArray(), item.Values.Select((PropertyInfoHashKeyName m) => m.Value).ToArray());
					if (writeResult3.IsSuccess)
					{
						continue;
					}
					return writeResult3;
				}
			}
			return OperateResult.CreateSuccessResult();
		}

		/// <summary>
		/// 从Json数据里解析出真实的数据信息，根据方法参数列表的类型进行反解析，然后返回实际的数据数组<br />
		/// Analyze the real data information from the Json data, perform de-analysis according to the type of the method parameter list, 
		/// and then return the actual data array
		/// </summary>
		/// <param name="context">当前的会话内容</param>
		/// <param name="request">当用于Http请求的时候关联的请求头对象</param>
		/// <param name="parameters">提供的参数列表信息</param>
		/// <param name="json">参数变量信息</param>
		/// <returns>已经填好的实际数据的参数数组对象</returns>
		public static object[] GetParametersFromJson(ISessionContext context, HttpListenerRequest request, ParameterInfo[] parameters, string json)
		{
			JObject jObject = (string.IsNullOrEmpty(json) ? new JObject() : JObject.Parse(json));
			object[] array = new object[parameters.Length];
			for (int i = 0; i < parameters.Length; i++)
			{
				string text = parameters[i].Name;
				if (!jObject.ContainsKey(text))
				{
					if (text == "value" && jObject.ContainsKey("values"))
					{
						text = "values";
					}
					else if (text == "values" && jObject.ContainsKey("value"))
					{
						text = "value";
					}
				}
				if (parameters[i].ParameterType == typeof(byte))
				{
					array[i] = jObject.Value<byte>(text);
				}
				else if (parameters[i].ParameterType == typeof(short))
				{
					array[i] = jObject.Value<short>(text);
				}
				else if (parameters[i].ParameterType == typeof(ushort))
				{
					array[i] = jObject.Value<ushort>(text);
				}
				else if (parameters[i].ParameterType == typeof(int))
				{
					array[i] = jObject.Value<int>(text);
				}
				else if (parameters[i].ParameterType == typeof(uint))
				{
					array[i] = jObject.Value<uint>(text);
				}
				else if (parameters[i].ParameterType == typeof(long))
				{
					array[i] = jObject.Value<long>(text);
				}
				else if (parameters[i].ParameterType == typeof(ulong))
				{
					array[i] = jObject.Value<ulong>(text);
				}
				else if (parameters[i].ParameterType == typeof(double))
				{
					array[i] = jObject.Value<double>(text);
				}
				else if (parameters[i].ParameterType == typeof(float))
				{
					array[i] = jObject.Value<float>(text);
				}
				else if (parameters[i].ParameterType == typeof(bool))
				{
					array[i] = jObject.Value<bool>(text);
				}
				else if (parameters[i].ParameterType == typeof(DateTime))
				{
					array[i] = jObject.Value<DateTime>(text);
				}
				else if (parameters[i].ParameterType == typeof(byte[]))
				{
					array[i] = jObject.Value<string>(text).ToHexBytes();
				}
				else if (parameters[i].ParameterType == typeof(short[]))
				{
					array[i] = (from m in jObject[text].ToArray()
						select m.Value<short>()).ToArray();
				}
				else if (parameters[i].ParameterType == typeof(ushort[]))
				{
					array[i] = (from m in jObject[text].ToArray()
						select m.Value<ushort>()).ToArray();
				}
				else if (parameters[i].ParameterType == typeof(int[]))
				{
					array[i] = (from m in jObject[text].ToArray()
						select m.Value<int>()).ToArray();
				}
				else if (parameters[i].ParameterType == typeof(uint[]))
				{
					array[i] = (from m in jObject[text].ToArray()
						select m.Value<uint>()).ToArray();
				}
				else if (parameters[i].ParameterType == typeof(long[]))
				{
					array[i] = (from m in jObject[text].ToArray()
						select m.Value<long>()).ToArray();
				}
				else if (parameters[i].ParameterType == typeof(ulong[]))
				{
					array[i] = (from m in jObject[text].ToArray()
						select m.Value<ulong>()).ToArray();
				}
				else if (parameters[i].ParameterType == typeof(float[]))
				{
					array[i] = (from m in jObject[text].ToArray()
						select m.Value<float>()).ToArray();
				}
				else if (parameters[i].ParameterType == typeof(double[]))
				{
					array[i] = (from m in jObject[text].ToArray()
						select m.Value<double>()).ToArray();
				}
				else if (parameters[i].ParameterType == typeof(bool[]))
				{
					array[i] = (from m in jObject[text].ToArray()
						select m.Value<bool>()).ToArray();
				}
				else if (parameters[i].ParameterType == typeof(string[]))
				{
					array[i] = (from m in jObject[text].ToArray()
						select m.Value<string>()).ToArray();
				}
				else if (parameters[i].ParameterType == typeof(DateTime[]))
				{
					array[i] = (from m in jObject[text].ToArray()
						select m.Value<DateTime>()).ToArray();
				}
				else if (parameters[i].ParameterType == typeof(ISessionContext))
				{
					array[i] = context;
				}
				else if (parameters[i].ParameterType == typeof(HttpListenerRequest))
				{
					array[i] = request;
				}
				else if (parameters[i].ParameterType.IsArray)
				{
					array[i] = ((JArray)jObject[text]).ToObject(parameters[i].ParameterType);
				}
				else if (parameters[i].ParameterType == typeof(string))
				{
					JToken jToken = jObject[text];
					if (jToken == null)
					{
						array[i] = "";
					}
					else if (jToken.Type == JTokenType.String)
					{
						array[i] = jObject.Value<string>(text);
					}
					else if (jToken.Type == JTokenType.Null)
					{
						array[i] = null;
					}
					else
					{
						array[i] = jToken.ToString();
					}
				}
				else if (parameters[i].ParameterType == typeof(JObject))
				{
					try
					{
						array[i] = (JObject)jObject[text];
					}
					catch
					{
						array[i] = JObject.Parse(jObject.Value<string>(text));
					}
				}
				else
				{
					try
					{
						array[i] = jObject[text].ToObject(parameters[i].ParameterType);
					}
					catch
					{
						array[i] = JObject.Parse(jObject.Value<string>(text)).ToObject(parameters[i].ParameterType);
					}
				}
			}
			return array;
		}

		/// <summary>
		/// 从url数据里解析出真实的数据信息，根据方法参数列表的类型进行反解析，然后返回实际的数据数组<br />
		/// Analyze the real data information from the url data, perform de-analysis according to the type of the method parameter list, 
		/// and then return the actual data array
		/// </summary>
		/// <param name="context">当前的会话内容</param>
		/// <param name="request">当用于Http请求的时候关联的请求头对象</param>
		/// <param name="parameters">提供的参数列表信息</param>
		/// <param name="url">参数变量信息</param>
		/// <returns>已经填好的实际数据的参数数组对象</returns>
		public static object[] GetParametersFromUrl(ISessionContext context, HttpListenerRequest request, ParameterInfo[] parameters, string url)
		{
			if (url.IndexOf('?') > 0)
			{
				url = url.Substring(url.IndexOf('?') + 1);
			}
			string[] array = url.Split(new char[1] { '&' }, StringSplitOptions.RemoveEmptyEntries);
			Dictionary<string, string> dictionary = new Dictionary<string, string>(array.Length);
			for (int i = 0; i < array.Length; i++)
			{
				if (!string.IsNullOrEmpty(array[i]) && array[i].IndexOf('=') > 0)
				{
					dictionary.Add(array[i].Substring(0, array[i].IndexOf('=')).Trim(' '), array[i].Substring(array[i].IndexOf('=') + 1));
				}
			}
			object[] array2 = new object[parameters.Length];
			for (int j = 0; j < parameters.Length; j++)
			{
				if (parameters[j].ParameterType == typeof(byte))
				{
					array2[j] = byte.Parse(dictionary[parameters[j].Name]);
				}
				else if (parameters[j].ParameterType == typeof(short))
				{
					array2[j] = short.Parse(dictionary[parameters[j].Name]);
				}
				else if (parameters[j].ParameterType == typeof(ushort))
				{
					array2[j] = ushort.Parse(dictionary[parameters[j].Name]);
				}
				else if (parameters[j].ParameterType == typeof(int))
				{
					array2[j] = int.Parse(dictionary[parameters[j].Name]);
				}
				else if (parameters[j].ParameterType == typeof(uint))
				{
					array2[j] = uint.Parse(dictionary[parameters[j].Name]);
				}
				else if (parameters[j].ParameterType == typeof(long))
				{
					array2[j] = long.Parse(dictionary[parameters[j].Name]);
				}
				else if (parameters[j].ParameterType == typeof(ulong))
				{
					array2[j] = ulong.Parse(dictionary[parameters[j].Name]);
				}
				else if (parameters[j].ParameterType == typeof(double))
				{
					array2[j] = double.Parse(dictionary[parameters[j].Name]);
				}
				else if (parameters[j].ParameterType == typeof(float))
				{
					array2[j] = float.Parse(dictionary[parameters[j].Name]);
				}
				else if (parameters[j].ParameterType == typeof(bool))
				{
					array2[j] = bool.Parse(dictionary[parameters[j].Name]);
				}
				else if (parameters[j].ParameterType == typeof(string))
				{
					array2[j] = dictionary[parameters[j].Name];
				}
				else if (parameters[j].ParameterType == typeof(DateTime))
				{
					array2[j] = DateTime.Parse(dictionary[parameters[j].Name]);
				}
				else if (parameters[j].ParameterType == typeof(byte[]))
				{
					array2[j] = dictionary[parameters[j].Name].ToHexBytes();
				}
				else if (parameters[j].ParameterType == typeof(short[]))
				{
					array2[j] = dictionary[parameters[j].Name].ToStringArray<short>();
				}
				else if (parameters[j].ParameterType == typeof(ushort[]))
				{
					array2[j] = dictionary[parameters[j].Name].ToStringArray<ushort>();
				}
				else if (parameters[j].ParameterType == typeof(int[]))
				{
					array2[j] = dictionary[parameters[j].Name].ToStringArray<int>();
				}
				else if (parameters[j].ParameterType == typeof(uint[]))
				{
					array2[j] = dictionary[parameters[j].Name].ToStringArray<uint>();
				}
				else if (parameters[j].ParameterType == typeof(long[]))
				{
					array2[j] = dictionary[parameters[j].Name].ToStringArray<long>();
				}
				else if (parameters[j].ParameterType == typeof(ulong[]))
				{
					array2[j] = dictionary[parameters[j].Name].ToStringArray<ulong>();
				}
				else if (parameters[j].ParameterType == typeof(float[]))
				{
					array2[j] = dictionary[parameters[j].Name].ToStringArray<float>();
				}
				else if (parameters[j].ParameterType == typeof(double[]))
				{
					array2[j] = dictionary[parameters[j].Name].ToStringArray<double>();
				}
				else if (parameters[j].ParameterType == typeof(bool[]))
				{
					array2[j] = dictionary[parameters[j].Name].ToStringArray<bool>();
				}
				else if (parameters[j].ParameterType == typeof(string[]))
				{
					array2[j] = dictionary[parameters[j].Name].ToStringArray<string>();
				}
				else if (parameters[j].ParameterType == typeof(DateTime[]))
				{
					array2[j] = dictionary[parameters[j].Name].ToStringArray<DateTime>();
				}
				else if (parameters[j].ParameterType == typeof(ISessionContext))
				{
					array2[j] = context;
				}
				else if (parameters[j].ParameterType == typeof(HttpListenerRequest))
				{
					array2[j] = request;
				}
				else
				{
					array2[j] = JToken.Parse(dictionary[parameters[j].Name]).ToObject(parameters[j].ParameterType);
				}
			}
			return array2;
		}

		/// <summary>
		/// 从方法的参数列表里，提取出实际的示例参数信息，返回一个json对象，注意：该数据是示例的数据，具体参数的限制参照服务器返回的数据声明。<br />
		/// From the parameter list of the method, extract the actual example parameter information, and return a json object. Note: The data is the example data, 
		/// and the specific parameter restrictions refer to the data declaration returned by the server.
		/// </summary>
		/// <param name="method">当前需要解析的方法名称</param>
		/// <param name="parameters">当前的参数列表信息</param>
		/// <returns>当前的参数对象信息</returns>
		public static JObject GetParametersFromJson(MethodInfo method, ParameterInfo[] parameters)
		{
			JObject jObject = new JObject();
			for (int i = 0; i < parameters.Length; i++)
			{
				if (parameters[i].ParameterType == typeof(byte))
				{
					jObject.Add(parameters[i].Name, new JValue((uint)(parameters[i].HasDefaultValue ? ((byte)parameters[i].DefaultValue) : 0)));
				}
				else if (parameters[i].ParameterType == typeof(short))
				{
					jObject.Add(parameters[i].Name, new JValue(parameters[i].HasDefaultValue ? ((short)parameters[i].DefaultValue) : 0));
				}
				else if (parameters[i].ParameterType == typeof(ushort))
				{
					jObject.Add(parameters[i].Name, new JValue((uint)(parameters[i].HasDefaultValue ? ((ushort)parameters[i].DefaultValue) : 0)));
				}
				else if (parameters[i].ParameterType == typeof(int))
				{
					jObject.Add(parameters[i].Name, new JValue(parameters[i].HasDefaultValue ? ((int)parameters[i].DefaultValue) : 0));
				}
				else if (parameters[i].ParameterType == typeof(uint))
				{
					jObject.Add(parameters[i].Name, new JValue(parameters[i].HasDefaultValue ? ((uint)parameters[i].DefaultValue) : 0));
				}
				else if (parameters[i].ParameterType == typeof(long))
				{
					jObject.Add(parameters[i].Name, new JValue(parameters[i].HasDefaultValue ? ((long)parameters[i].DefaultValue) : 0));
				}
				else if (parameters[i].ParameterType == typeof(ulong))
				{
					jObject.Add(parameters[i].Name, new JValue(parameters[i].HasDefaultValue ? ((ulong)parameters[i].DefaultValue) : 0));
				}
				else if (parameters[i].ParameterType == typeof(double))
				{
					jObject.Add(parameters[i].Name, new JValue(parameters[i].HasDefaultValue ? ((double)parameters[i].DefaultValue) : 0.0));
				}
				else if (parameters[i].ParameterType == typeof(float))
				{
					jObject.Add(parameters[i].Name, new JValue(parameters[i].HasDefaultValue ? ((float)parameters[i].DefaultValue) : 0f));
				}
				else if (parameters[i].ParameterType == typeof(bool))
				{
					jObject.Add(parameters[i].Name, new JValue(parameters[i].HasDefaultValue && (bool)parameters[i].DefaultValue));
				}
				else if (parameters[i].ParameterType == typeof(string))
				{
					jObject.Add(parameters[i].Name, new JValue(parameters[i].HasDefaultValue ? ((string)parameters[i].DefaultValue) : ""));
				}
				else if (parameters[i].ParameterType == typeof(DateTime))
				{
					jObject.Add(parameters[i].Name, new JValue(parameters[i].HasDefaultValue ? ((DateTime)parameters[i].DefaultValue).ToString("yyyy-MM-dd HH:mm:ss") : DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
				}
				else if (parameters[i].ParameterType == typeof(byte[]))
				{
					jObject.Add(parameters[i].Name, new JValue(parameters[i].HasDefaultValue ? ((byte[])parameters[i].DefaultValue).ToHexString() : "00 1A 2B 3C 4D"));
				}
				else if (parameters[i].ParameterType == typeof(short[]))
				{
					jObject.Add(parameters[i].Name, new JArray(parameters[i].HasDefaultValue ? ((short[])parameters[i].DefaultValue) : new short[3] { 1, 2, 3 }));
				}
				else if (parameters[i].ParameterType == typeof(ushort[]))
				{
					jObject.Add(parameters[i].Name, new JArray(parameters[i].HasDefaultValue ? ((ushort[])parameters[i].DefaultValue) : new ushort[3] { 1, 2, 3 }));
				}
				else if (parameters[i].ParameterType == typeof(int[]))
				{
					jObject.Add(parameters[i].Name, new JArray(parameters[i].HasDefaultValue ? ((int[])parameters[i].DefaultValue) : new int[3] { 1, 2, 3 }));
				}
				else if (parameters[i].ParameterType == typeof(uint[]))
				{
					jObject.Add(parameters[i].Name, new JArray(parameters[i].HasDefaultValue ? ((uint[])parameters[i].DefaultValue) : new uint[3] { 1u, 2u, 3u }));
				}
				else if (parameters[i].ParameterType == typeof(long[]))
				{
					jObject.Add(parameters[i].Name, new JArray(parameters[i].HasDefaultValue ? ((long[])parameters[i].DefaultValue) : new long[3] { 1L, 2L, 3L }));
				}
				else if (parameters[i].ParameterType == typeof(ulong[]))
				{
					jObject.Add(parameters[i].Name, new JArray(parameters[i].HasDefaultValue ? ((ulong[])parameters[i].DefaultValue) : new ulong[3] { 1uL, 2uL, 3uL }));
				}
				else if (parameters[i].ParameterType == typeof(float[]))
				{
					jObject.Add(parameters[i].Name, new JArray(parameters[i].HasDefaultValue ? ((float[])parameters[i].DefaultValue) : new float[3] { 1f, 2f, 3f }));
				}
				else if (parameters[i].ParameterType == typeof(double[]))
				{
					jObject.Add(parameters[i].Name, new JArray(parameters[i].HasDefaultValue ? ((double[])parameters[i].DefaultValue) : new double[3] { 1.0, 2.0, 3.0 }));
				}
				else if (parameters[i].ParameterType == typeof(bool[]))
				{
					jObject.Add(parameters[i].Name, new JArray(parameters[i].HasDefaultValue ? ((bool[])parameters[i].DefaultValue) : new bool[3] { true, false, false }));
				}
				else if (parameters[i].ParameterType == typeof(string[]))
				{
					jObject.Add(parameters[i].Name, new JArray(parameters[i].HasDefaultValue ? ((string[])parameters[i].DefaultValue) : new string[3] { "1", "2", "3" }));
				}
				else if (parameters[i].ParameterType == typeof(DateTime[]))
				{
					jObject.Add(parameters[i].Name, new JArray(parameters[i].HasDefaultValue ? ((DateTime[])parameters[i].DefaultValue).Select((DateTime m) => m.ToString("yyyy-MM-dd HH:mm:ss")).ToArray() : new string[1] { DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") }));
				}
				else if (!(parameters[i].ParameterType == typeof(ISessionContext)) && !(parameters[i].ParameterType == typeof(HttpListenerRequest)))
				{
					if (parameters[i].ParameterType.IsArray)
					{
						jObject.Add(parameters[i].Name, parameters[i].HasDefaultValue ? JToken.FromObject(parameters[i].DefaultValue) : JToken.FromObject(GetObjFromArrayParameterType(parameters[i].ParameterType)));
					}
					else
					{
						jObject.Add(parameters[i].Name, JToken.FromObject(parameters[i].HasDefaultValue ? parameters[i].DefaultValue : Activator.CreateInstance(parameters[i].ParameterType)));
					}
				}
			}
			return jObject;
		}

		private static object GetObjFromArrayParameterType(Type parameterType)
		{
			Type type = null;
			Type[] genericArguments = parameterType.GetGenericArguments();
			type = ((genericArguments.Length == 0) ? parameterType.GetElementType() : genericArguments[0]);
			Array array = Array.CreateInstance(type, 3);
			for (int i = 0; i < 3; i++)
			{
				array.SetValue(Activator.CreateInstance(type), i);
			}
			return array;
		}

		/// <summary>
		/// 将一个对象转换成 <see cref="T:HslCommunication.OperateResult`1" /> 的string 类型的对象，用于远程RPC的数据交互 
		/// </summary>
		/// <param name="obj">自定义的对象</param>
		/// <returns>转换之后的结果对象</returns>
		public static OperateResult<string> GetOperateResultJsonFromObj(object obj)
		{
			if (obj is OperateResult operateResult)
			{
				OperateResult<string> operateResult2 = new OperateResult<string>();
				operateResult2.IsSuccess = operateResult.IsSuccess;
				operateResult2.ErrorCode = operateResult.ErrorCode;
				operateResult2.Message = operateResult.Message;
				if (operateResult.IsSuccess)
				{
					PropertyInfo property = obj.GetType().GetProperty("Content");
					if (property != null)
					{
						object value = property.GetValue(obj, null);
						if (value != null)
						{
							operateResult2.Content = value.ToJsonString();
						}
						return operateResult2;
					}
					PropertyInfo property2 = obj.GetType().GetProperty("Content1");
					if (property2 == null)
					{
						return operateResult2;
					}
					PropertyInfo property3 = obj.GetType().GetProperty("Content2");
					if (property3 == null)
					{
						operateResult2.Content = new
						{
							Content1 = property2.GetValue(obj, null)
						}.ToJsonString();
						return operateResult2;
					}
					PropertyInfo property4 = obj.GetType().GetProperty("Content3");
					if (property4 == null)
					{
						operateResult2.Content = new
						{
							Content1 = property2.GetValue(obj, null),
							Content2 = property3.GetValue(obj, null)
						}.ToJsonString();
						return operateResult2;
					}
					PropertyInfo property5 = obj.GetType().GetProperty("Content4");
					if (property5 == null)
					{
						operateResult2.Content = new
						{
							Content1 = property2.GetValue(obj, null),
							Content2 = property3.GetValue(obj, null),
							Content3 = property4.GetValue(obj, null)
						}.ToJsonString();
						return operateResult2;
					}
					PropertyInfo property6 = obj.GetType().GetProperty("Content5");
					if (property6 == null)
					{
						operateResult2.Content = new
						{
							Content1 = property2.GetValue(obj, null),
							Content2 = property3.GetValue(obj, null),
							Content3 = property4.GetValue(obj, null),
							Content4 = property5.GetValue(obj, null)
						}.ToJsonString();
						return operateResult2;
					}
					PropertyInfo property7 = obj.GetType().GetProperty("Content6");
					if (property7 == null)
					{
						operateResult2.Content = new
						{
							Content1 = property2.GetValue(obj, null),
							Content2 = property3.GetValue(obj, null),
							Content3 = property4.GetValue(obj, null),
							Content4 = property5.GetValue(obj, null),
							Content5 = property6.GetValue(obj, null)
						}.ToJsonString();
						return operateResult2;
					}
					PropertyInfo property8 = obj.GetType().GetProperty("Content7");
					if (property8 == null)
					{
						operateResult2.Content = new
						{
							Content1 = property2.GetValue(obj, null),
							Content2 = property3.GetValue(obj, null),
							Content3 = property4.GetValue(obj, null),
							Content4 = property5.GetValue(obj, null),
							Content5 = property6.GetValue(obj, null),
							Content6 = property7.GetValue(obj, null)
						}.ToJsonString();
						return operateResult2;
					}
					PropertyInfo property9 = obj.GetType().GetProperty("Content8");
					if (property9 == null)
					{
						operateResult2.Content = new
						{
							Content1 = property2.GetValue(obj, null),
							Content2 = property3.GetValue(obj, null),
							Content3 = property4.GetValue(obj, null),
							Content4 = property5.GetValue(obj, null),
							Content5 = property6.GetValue(obj, null),
							Content6 = property7.GetValue(obj, null),
							Content7 = property8.GetValue(obj, null)
						}.ToJsonString();
						return operateResult2;
					}
					PropertyInfo property10 = obj.GetType().GetProperty("Content9");
					if (property10 == null)
					{
						operateResult2.Content = new
						{
							Content1 = property2.GetValue(obj, null),
							Content2 = property3.GetValue(obj, null),
							Content3 = property4.GetValue(obj, null),
							Content4 = property5.GetValue(obj, null),
							Content5 = property6.GetValue(obj, null),
							Content6 = property7.GetValue(obj, null),
							Content7 = property8.GetValue(obj, null),
							Content8 = property9.GetValue(obj, null)
						}.ToJsonString();
						return operateResult2;
					}
					PropertyInfo property11 = obj.GetType().GetProperty("Content10");
					if (property11 == null)
					{
						operateResult2.Content = new
						{
							Content1 = property2.GetValue(obj, null),
							Content2 = property3.GetValue(obj, null),
							Content3 = property4.GetValue(obj, null),
							Content4 = property5.GetValue(obj, null),
							Content5 = property6.GetValue(obj, null),
							Content6 = property7.GetValue(obj, null),
							Content7 = property8.GetValue(obj, null),
							Content8 = property9.GetValue(obj, null),
							Content9 = property10.GetValue(obj, null)
						}.ToJsonString();
						return operateResult2;
					}
					operateResult2.Content = new
					{
						Content1 = property2.GetValue(obj, null),
						Content2 = property3.GetValue(obj, null),
						Content3 = property4.GetValue(obj, null),
						Content4 = property5.GetValue(obj, null),
						Content5 = property6.GetValue(obj, null),
						Content6 = property7.GetValue(obj, null),
						Content7 = property8.GetValue(obj, null),
						Content8 = property9.GetValue(obj, null),
						Content9 = property10.GetValue(obj, null),
						Content10 = property11.GetValue(obj, null)
					}.ToJsonString();
					return operateResult2;
				}
				return operateResult2;
			}
			return OperateResult.CreateSuccessResult((obj == null) ? string.Empty : obj.ToJsonString());
		}

		/// <summary>
		/// 根据提供的类型对象，解析出符合 <see cref="T:HslCommunication.Reflection.HslDeviceAddressAttribute" /> 特性的地址列表
		/// </summary>
		/// <param name="valueType">数据类型</param>
		/// <param name="deviceType">设备类型</param>
		/// <param name="obj">类型的对象信息</param>
		/// <param name="byteTransform">数据变换对象</param>
		/// <returns>地址列表信息</returns>
		public static List<HslAddressProperty> GetHslPropertyInfos(Type valueType, Type deviceType, object obj, IByteTransform byteTransform)
		{
			List<HslAddressProperty> list = new List<HslAddressProperty>();
			PropertyInfo[] properties = valueType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
			int num = 0;
			PropertyInfo[] array = properties;
			foreach (PropertyInfo propertyInfo in array)
			{
				HslDeviceAddressAttribute hslDeviceAddressAttribute = GetHslDeviceAddressAttribute(deviceType, propertyInfo);
				if (hslDeviceAddressAttribute == null)
				{
					continue;
				}
				HslAddressProperty hslAddressProperty = new HslAddressProperty();
				hslAddressProperty.PropertyInfo = propertyInfo;
				hslAddressProperty.DeviceAddressAttribute = hslDeviceAddressAttribute;
				hslAddressProperty.ByteOffset = num;
				Type propertyType = propertyInfo.PropertyType;
				if (propertyType == typeof(byte))
				{
					num++;
					if (obj != null)
					{
						hslAddressProperty.Buffer = new byte[1] { (byte)propertyInfo.GetValue(obj, null) };
					}
				}
				else if (propertyType == typeof(short))
				{
					num += 2;
					if (obj != null)
					{
						hslAddressProperty.Buffer = byteTransform.TransByte((short)propertyInfo.GetValue(obj, null));
					}
				}
				else if (propertyType == typeof(short[]))
				{
					num += 2 * ((hslDeviceAddressAttribute.Length <= 0) ? 1 : hslDeviceAddressAttribute.Length);
					if (obj != null)
					{
						hslAddressProperty.Buffer = byteTransform.TransByte((short[])propertyInfo.GetValue(obj, null));
					}
				}
				else if (propertyType == typeof(ushort))
				{
					num += 2;
					if (obj != null)
					{
						hslAddressProperty.Buffer = byteTransform.TransByte((ushort)propertyInfo.GetValue(obj, null));
					}
				}
				else if (propertyType == typeof(ushort[]))
				{
					num += 2 * ((hslDeviceAddressAttribute.Length <= 0) ? 1 : hslDeviceAddressAttribute.Length);
					if (obj != null)
					{
						hslAddressProperty.Buffer = byteTransform.TransByte((ushort[])propertyInfo.GetValue(obj, null));
					}
				}
				else if (propertyType == typeof(int))
				{
					num += 4;
					if (obj != null)
					{
						hslAddressProperty.Buffer = byteTransform.TransByte((int)propertyInfo.GetValue(obj, null));
					}
				}
				else if (propertyType == typeof(int[]))
				{
					num += 4 * ((hslDeviceAddressAttribute.Length <= 0) ? 1 : hslDeviceAddressAttribute.Length);
					if (obj != null)
					{
						hslAddressProperty.Buffer = byteTransform.TransByte((int[])propertyInfo.GetValue(obj, null));
					}
				}
				else if (propertyType == typeof(uint))
				{
					num += 4;
					if (obj != null)
					{
						hslAddressProperty.Buffer = byteTransform.TransByte((uint)propertyInfo.GetValue(obj, null));
					}
				}
				else if (propertyType == typeof(uint[]))
				{
					num += 4 * ((hslDeviceAddressAttribute.Length <= 0) ? 1 : hslDeviceAddressAttribute.Length);
					if (obj != null)
					{
						hslAddressProperty.Buffer = byteTransform.TransByte((uint[])propertyInfo.GetValue(obj, null));
					}
				}
				else if (propertyType == typeof(long))
				{
					num += 8;
					if (obj != null)
					{
						hslAddressProperty.Buffer = byteTransform.TransByte((long)propertyInfo.GetValue(obj, null));
					}
				}
				else if (propertyType == typeof(long[]))
				{
					num += 8 * ((hslDeviceAddressAttribute.Length <= 0) ? 1 : hslDeviceAddressAttribute.Length);
					if (obj != null)
					{
						hslAddressProperty.Buffer = byteTransform.TransByte((long[])propertyInfo.GetValue(obj, null));
					}
				}
				else if (propertyType == typeof(ulong))
				{
					num += 8;
					if (obj != null)
					{
						hslAddressProperty.Buffer = byteTransform.TransByte((ulong)propertyInfo.GetValue(obj, null));
					}
				}
				else if (propertyType == typeof(ulong[]))
				{
					num += 8 * ((hslDeviceAddressAttribute.Length <= 0) ? 1 : hslDeviceAddressAttribute.Length);
					if (obj != null)
					{
						hslAddressProperty.Buffer = byteTransform.TransByte((ulong[])propertyInfo.GetValue(obj, null));
					}
				}
				else if (propertyType == typeof(float))
				{
					num += 4;
					if (obj != null)
					{
						hslAddressProperty.Buffer = byteTransform.TransByte((float)propertyInfo.GetValue(obj, null));
					}
				}
				else if (propertyType == typeof(float[]))
				{
					num += 4 * ((hslDeviceAddressAttribute.Length <= 0) ? 1 : hslDeviceAddressAttribute.Length);
					if (obj != null)
					{
						hslAddressProperty.Buffer = byteTransform.TransByte((float[])propertyInfo.GetValue(obj, null));
					}
				}
				else if (propertyType == typeof(double))
				{
					num += 8;
					if (obj != null)
					{
						hslAddressProperty.Buffer = byteTransform.TransByte((double)propertyInfo.GetValue(obj, null));
					}
				}
				else if (propertyType == typeof(double[]))
				{
					num += 8 * ((hslDeviceAddressAttribute.Length <= 0) ? 1 : hslDeviceAddressAttribute.Length);
					if (obj != null)
					{
						hslAddressProperty.Buffer = byteTransform.TransByte((double[])propertyInfo.GetValue(obj, null));
					}
				}
				else if (propertyType == typeof(string))
				{
					num += ((hslDeviceAddressAttribute.Length <= 0) ? 1 : hslDeviceAddressAttribute.Length);
					if (obj != null)
					{
						hslAddressProperty.Buffer = byteTransform.TransByte((string)propertyInfo.GetValue(obj, null), Encoding.ASCII);
					}
				}
				else if (propertyType == typeof(byte[]))
				{
					num += ((hslDeviceAddressAttribute.Length <= 0) ? 1 : hslDeviceAddressAttribute.Length);
					if (obj != null)
					{
						hslAddressProperty.Buffer = (byte[])propertyInfo.GetValue(obj, null);
					}
				}
				else if (propertyType == typeof(bool))
				{
					num++;
					if (obj != null)
					{
						hslAddressProperty.Buffer = ((!(bool)propertyInfo.GetValue(obj, null)) ? new byte[1] : new byte[1] { 1 });
					}
				}
				else if (propertyType == typeof(bool[]))
				{
					num += ((hslDeviceAddressAttribute.Length <= 0) ? 1 : hslDeviceAddressAttribute.Length);
					if (obj != null)
					{
						hslAddressProperty.Buffer = ((bool[])propertyInfo.GetValue(obj, null)).Select((bool m) => (byte)(m ? 1 : 0)).ToArray();
					}
				}
				hslAddressProperty.ByteLength = num - hslAddressProperty.ByteOffset;
				list.Add(hslAddressProperty);
			}
			return list;
		}

		/// <summary>
		/// 根据地址列表信息，数据缓存，自动解析基础类型的数据，赋值到自定义的对象上去
		/// </summary>
		/// <param name="byteTransform">数据解析对象</param>
		/// <param name="obj">数据对象信息</param>
		/// <param name="properties">地址属性列表</param>
		/// <param name="buffer">缓存数据信息</param>
		public static void SetPropertyValueFrom(IByteTransform byteTransform, object obj, List<HslAddressProperty> properties, byte[] buffer)
		{
			foreach (HslAddressProperty property in properties)
			{
				Type propertyType = property.PropertyInfo.PropertyType;
				object obj2 = null;
				if (propertyType == typeof(byte))
				{
					obj2 = buffer[property.ByteOffset];
				}
				else if (propertyType == typeof(short))
				{
					obj2 = byteTransform.TransInt16(buffer, property.ByteOffset);
				}
				else if (propertyType == typeof(short[]))
				{
					obj2 = byteTransform.TransInt16(buffer, property.ByteOffset, property.DeviceAddressAttribute.GetDataLength());
				}
				else if (propertyType == typeof(ushort))
				{
					obj2 = byteTransform.TransUInt16(buffer, property.ByteOffset);
				}
				else if (propertyType == typeof(ushort[]))
				{
					obj2 = byteTransform.TransUInt16(buffer, property.ByteOffset, property.DeviceAddressAttribute.GetDataLength());
				}
				else if (propertyType == typeof(int))
				{
					obj2 = byteTransform.TransInt32(buffer, property.ByteOffset);
				}
				else if (propertyType == typeof(int[]))
				{
					obj2 = byteTransform.TransInt32(buffer, property.ByteOffset, property.DeviceAddressAttribute.GetDataLength());
				}
				else if (propertyType == typeof(uint))
				{
					obj2 = byteTransform.TransUInt32(buffer, property.ByteOffset);
				}
				else if (propertyType == typeof(uint[]))
				{
					obj2 = byteTransform.TransUInt32(buffer, property.ByteOffset, property.DeviceAddressAttribute.GetDataLength());
				}
				else if (propertyType == typeof(long))
				{
					obj2 = byteTransform.TransInt64(buffer, property.ByteOffset);
				}
				else if (propertyType == typeof(long[]))
				{
					obj2 = byteTransform.TransInt64(buffer, property.ByteOffset, property.DeviceAddressAttribute.GetDataLength());
				}
				else if (propertyType == typeof(ulong))
				{
					obj2 = byteTransform.TransUInt64(buffer, property.ByteOffset);
				}
				else if (propertyType == typeof(ulong[]))
				{
					obj2 = byteTransform.TransUInt64(buffer, property.ByteOffset, property.DeviceAddressAttribute.GetDataLength());
				}
				else if (propertyType == typeof(float))
				{
					obj2 = byteTransform.TransSingle(buffer, property.ByteOffset);
				}
				else if (propertyType == typeof(float[]))
				{
					obj2 = byteTransform.TransSingle(buffer, property.ByteOffset, property.DeviceAddressAttribute.GetDataLength());
				}
				else if (propertyType == typeof(double))
				{
					obj2 = byteTransform.TransDouble(buffer, property.ByteOffset);
				}
				else if (propertyType == typeof(double[]))
				{
					obj2 = byteTransform.TransDouble(buffer, property.ByteOffset, property.DeviceAddressAttribute.GetDataLength());
				}
				else if (propertyType == typeof(string))
				{
					obj2 = Encoding.ASCII.GetString(buffer, property.ByteOffset, property.DeviceAddressAttribute.GetDataLength());
				}
				else if (propertyType == typeof(byte[]))
				{
					obj2 = buffer.SelectMiddle(property.ByteOffset, property.DeviceAddressAttribute.GetDataLength());
				}
				else if (propertyType == typeof(bool))
				{
					obj2 = buffer[property.ByteOffset] != 0;
				}
				else if (propertyType == typeof(bool[]))
				{
					obj2 = (from m in buffer.SelectMiddle(property.ByteOffset, property.DeviceAddressAttribute.GetDataLength())
						select m != 0).ToArray();
				}
				if (obj2 != null)
				{
					property.PropertyInfo.SetValue(obj, obj2, null);
				}
			}
		}
	}
}
