using System;
using System.Text;
using System.Text.RegularExpressions;
using HslCommunication.BasicFramework;
using HslCommunication.Core.Net;
using HslCommunication.Profinet.AllenBradley;

namespace HslCommunication.Profinet.Omron
{
	/// <summary>
	/// 欧姆龙的CIP虚拟服务器
	/// </summary>
	public class OmronCipServer : AllenBradleyServer
	{
		/// <inheritdoc />
		public override void AddTagValue(string key, string value, int maxLength)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(value);
			AddTagValue(key, new AllenBradleyItemValue
			{
				IsArray = false,
				Buffer = SoftBasic.ArrayExpandToLength(SoftBasic.SpliceArray<byte>(BitConverter.GetBytes((ushort)bytes.Length), Encoding.UTF8.GetBytes(value)), maxLength),
				TypeLength = maxLength,
				TypeCode = 208
			});
		}

		/// <inheritdoc />
		public override void AddTagValue(string key, string[] value, int maxLength)
		{
			byte[] array = new byte[maxLength * value.Length];
			for (int i = 0; i < value.Length; i++)
			{
				byte[] bytes = Encoding.UTF8.GetBytes(value[i]);
				BitConverter.GetBytes((ushort)bytes.Length).CopyTo(array, maxLength * i);
				bytes.CopyTo(array, maxLength * i + 2);
			}
			AddTagValue(key, new AllenBradleyItemValue
			{
				IsArray = true,
				Buffer = array,
				TypeLength = maxLength,
				TypeCode = 208
			});
		}

		/// <inheritdoc />
		public override OperateResult<string> ReadString(string address, ushort length, Encoding encoding)
		{
			OperateResult<byte[]> operateResult = Read(address, length);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<string>(operateResult);
			}
			if (operateResult.Content.Length >= 2)
			{
				return OperateResult.CreateSuccessResult(encoding.GetString(count: BitConverter.ToUInt16(operateResult.Content, 0), bytes: operateResult.Content, index: 2));
			}
			return OperateResult.CreateSuccessResult(encoding.GetString(operateResult.Content));
		}

		/// <inheritdoc />
		public override OperateResult Write(string address, string value, Encoding encoding)
		{
			if (IsNeedCreateTag(address))
			{
				AddTagValue(address, value, 1024);
				return OperateResult.CreateSuccessResult();
			}
			bool flag = false;
			int addressIndex = GetAddressIndex(ref address);
			simpleHybird.Enter();
			if (abValues.ContainsKey(address))
			{
				flag = true;
				AllenBradleyItemValue allenBradleyItemValue = abValues[address];
				byte[] buffer = allenBradleyItemValue.Buffer;
				if (buffer != null && buffer.Length >= 2)
				{
					byte[] bytes = encoding.GetBytes(value);
					BitConverter.GetBytes((ushort)bytes.Length).CopyTo(allenBradleyItemValue.Buffer, addressIndex * allenBradleyItemValue.TypeLength);
					if (bytes.Length != 0)
					{
						Array.Copy(bytes, 0, allenBradleyItemValue.Buffer, 2 + addressIndex * allenBradleyItemValue.TypeLength, Math.Min(bytes.Length, allenBradleyItemValue.Buffer.Length - 2));
					}
				}
			}
			simpleHybird.Leave();
			if (!flag)
			{
				return new OperateResult<bool>(StringResources.Language.AllenBradley04);
			}
			return OperateResult.CreateSuccessResult();
		}

		/// <inheritdoc />
		protected override byte[] ReadByCommand(PipeSession session, byte[] cipCore)
		{
			if (session.Tag is string)
			{
				return base.ReadByCommand(session, cipCore);
			}
			byte[] array = base.ByteTransform.TransByte(cipCore, 2, cipCore[1] * 2);
			string text = AllenBradleyHelper.ParseRequestPathCommand(array);
			OperateResult<byte[], ushort> operateResult = null;
			operateResult = ((session.Tag is CipSessionTag cipSessionTag && cipSessionTag.IsConnectedCIP) ? ReadWithType(text, BitConverter.ToUInt16(cipCore, 2 + array.Length)) : ((!Regex.IsMatch(text, "\\[[0-9]+\\]$")) ? ReadWithType(text, -1) : ReadWithType(text, BitConverter.ToUInt16(cipCore, 2 + array.Length))));
			AllenBradleyItemValue addressItemValue = GetAddressItemValue(text);
			byte[] array2;
			if (addressItemValue != null && addressItemValue.TypeCode == 193 && addressItemValue.IsArray)
			{
				array2 = AllenBradleyHelper.PackCommandResponse(SoftBasic.ByteToBoolArray(operateResult.Content1, operateResult.Content1.Length, 1).ToByteArray(), true);
			}
			else if (addressItemValue.TypeCode == 208)
			{
				if (operateResult.Content1.Length >= 2)
				{
					int num = BitConverter.ToUInt16(operateResult.Content1, 0);
					if (num + 2 > operateResult.Content1.Length)
					{
						num = operateResult.Content1.Length - 2;
					}
					array2 = AllenBradleyHelper.PackCommandResponse(operateResult.Content1.SelectBegin(num + 2), true);
				}
				else
				{
					array2 = AllenBradleyHelper.PackCommandResponse(operateResult.Content1, true);
				}
			}
			else
			{
				array2 = AllenBradleyHelper.PackCommandResponse(operateResult.Content1, true);
			}
			if (array2.Length > 6)
			{
				BitConverter.GetBytes(operateResult.Content2).CopyTo(array2, 4);
			}
			return array2;
		}

		/// <inheritdoc />
		protected override byte[] WriteByMessage(byte[] cipCore)
		{
			if (!base.EnableWrite)
			{
				return AllenBradleyHelper.PackCommandResponse(null, false);
			}
			byte[] array = base.ByteTransform.TransByte(cipCore, 2, cipCore[1] * 2);
			string address = AllenBradleyHelper.ParseRequestPathCommand(array);
			ushort num = BitConverter.ToUInt16(cipCore, 2 + array.Length);
			ushort num2 = BitConverter.ToUInt16(cipCore, 4 + array.Length);
			byte[] array2 = base.ByteTransform.TransByte(cipCore, 6 + array.Length, cipCore.Length - 6 - array.Length);
			AllenBradleyItemValue addressItemValue = GetAddressItemValue(address);
			if (addressItemValue != null && addressItemValue.TypeCode == 193 && addressItemValue.IsArray)
			{
				if (Write(address, array2.SelectBegin(addressItemValue.Buffer.Length)).IsSuccess)
				{
					return AllenBradleyHelper.PackCommandResponse(new byte[0], false);
				}
				return AllenBradleyHelper.PackCommandResponse(null, false);
			}
			if (addressItemValue.TypeCode == 208 && num == 208)
			{
				if (array2.Length < 2)
				{
					return AllenBradleyHelper.PackCommandResponse(null, false);
				}
				int num3 = BitConverter.ToUInt16(array2, 0);
				int num4 = 2 + num3 - 1;
				while (num4 >= 2 && array2[num4] == 0)
				{
					num3--;
					num4--;
				}
				if (Write(address, Encoding.UTF8.GetString(array2, 2, num3)).IsSuccess)
				{
					return AllenBradleyHelper.PackCommandResponse(new byte[0], false);
				}
				return AllenBradleyHelper.PackCommandResponse(null, false);
			}
			if (num == 193 && num2 == 1)
			{
				bool value = false;
				if (array2.Length == 2 && array2[0] == byte.MaxValue && array2[1] == byte.MaxValue)
				{
					value = true;
				}
				if (Write(address, value).IsSuccess)
				{
					return AllenBradleyHelper.PackCommandResponse(new byte[0], false);
				}
				return AllenBradleyHelper.PackCommandResponse(null, false);
			}
			if (Write(address, array2).IsSuccess)
			{
				return AllenBradleyHelper.PackCommandResponse(new byte[0], false);
			}
			return AllenBradleyHelper.PackCommandResponse(null, false);
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return $"OmronCipServer[{base.Port}]";
		}
	}
}
