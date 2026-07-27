using System;

namespace HslCommunication.Core.Address
{
	/// <summary>
	/// 横河PLC的地址表示类<br />
	/// Yokogawa PLC address display class
	/// </summary>
	public class YokogawaLinkAddress : DeviceAddressDataBase
	{
		/// <summary>
		/// 获取或设置等待读取的数据的代码<br />
		/// Get or set the code of the data waiting to be read
		/// </summary>
		public int DataCode { get; set; }

		/// <summary>
		/// 获取当前横河PLC的地址的二进制表述方式<br />
		/// Obtain the binary representation of the current Yokogawa PLC address
		/// </summary>
		/// <returns>二进制数据信息</returns>
		public byte[] GetAddressBinaryContent()
		{
			return new byte[6]
			{
				BitConverter.GetBytes(DataCode)[1],
				BitConverter.GetBytes(DataCode)[0],
				BitConverter.GetBytes(base.AddressStart)[3],
				BitConverter.GetBytes(base.AddressStart)[2],
				BitConverter.GetBytes(base.AddressStart)[1],
				BitConverter.GetBytes(base.AddressStart)[0]
			};
		}

		/// <inheritdoc />
		public override void Parse(string address, ushort length)
		{
			OperateResult<YokogawaLinkAddress> operateResult = ParseFrom(address, length);
			if (operateResult.IsSuccess)
			{
				base.AddressStart = operateResult.Content.AddressStart;
				base.Length = operateResult.Content.Length;
				DataCode = operateResult.Content.DataCode;
			}
		}

		/// <inheritdoc />
		public override string ToString()
		{
			switch (DataCode)
			{
			case 49:
				return "CN" + base.AddressStart;
			case 33:
				return "TN" + base.AddressStart;
			case 24:
				return "X" + base.AddressStart;
			case 25:
				return "Y" + base.AddressStart;
			case 9:
				return "I" + base.AddressStart;
			case 5:
				return "E" + base.AddressStart;
			case 13:
				return "M" + base.AddressStart;
			case 20:
				return "T" + base.AddressStart;
			case 3:
				return "C" + base.AddressStart;
			case 12:
				return "L" + base.AddressStart;
			case 4:
				return "D" + base.AddressStart;
			case 2:
				return "B" + base.AddressStart;
			case 6:
				return "F" + base.AddressStart;
			case 18:
				return "R" + base.AddressStart;
			case 22:
				return "V" + base.AddressStart;
			case 26:
				return "Z" + base.AddressStart;
			case 23:
				return "W" + base.AddressStart;
			default:
				return base.AddressStart.ToString();
			}
		}

		/// <summary>
		/// 从普通的PLC的地址转换为HSL标准的地址信息
		/// </summary>
		/// <param name="address">地址信息</param>
		/// <param name="length">数据长度</param>
		/// <returns>是否成功的地址结果</returns>
		public static OperateResult<YokogawaLinkAddress> ParseFrom(string address, ushort length)
		{
			try
			{
				int num = 0;
				int num2 = 0;
				if (address.StartsWith("CN") || address.StartsWith("cn"))
				{
					num = 49;
					num2 = int.Parse(address.Substring(2));
				}
				else if (address.StartsWith("TN") || address.StartsWith("tn"))
				{
					num = 33;
					num2 = int.Parse(address.Substring(2));
				}
				else if (address.StartsWith("X") || address.StartsWith("x"))
				{
					num = 24;
					num2 = int.Parse(address.Substring(1));
				}
				else if (address.StartsWith("Y") || address.StartsWith("y"))
				{
					num = 25;
					num2 = int.Parse(address.Substring(1));
				}
				else if (address.StartsWith("I") || address.StartsWith("i"))
				{
					num = 9;
					num2 = int.Parse(address.Substring(1));
				}
				else if (address.StartsWith("E") || address.StartsWith("e"))
				{
					num = 5;
					num2 = int.Parse(address.Substring(1));
				}
				else if (address.StartsWith("M") || address.StartsWith("m"))
				{
					num = 13;
					num2 = int.Parse(address.Substring(1));
				}
				else if (address.StartsWith("T") || address.StartsWith("t"))
				{
					num = 20;
					num2 = int.Parse(address.Substring(1));
				}
				else if (address.StartsWith("C") || address.StartsWith("c"))
				{
					num = 3;
					num2 = int.Parse(address.Substring(1));
				}
				else if (address.StartsWith("L") || address.StartsWith("l"))
				{
					num = 12;
					num2 = int.Parse(address.Substring(1));
				}
				else if (address.StartsWith("D") || address.StartsWith("d"))
				{
					num = 4;
					num2 = int.Parse(address.Substring(1));
				}
				else if (address.StartsWith("B") || address.StartsWith("b"))
				{
					num = 2;
					num2 = int.Parse(address.Substring(1));
				}
				else if (address.StartsWith("F") || address.StartsWith("f"))
				{
					num = 6;
					num2 = int.Parse(address.Substring(1));
				}
				else if (address.StartsWith("R") || address.StartsWith("r"))
				{
					num = 18;
					num2 = int.Parse(address.Substring(1));
				}
				else if (address.StartsWith("V") || address.StartsWith("v"))
				{
					num = 22;
					num2 = int.Parse(address.Substring(1));
				}
				else if (address.StartsWith("Z") || address.StartsWith("z"))
				{
					num = 26;
					num2 = int.Parse(address.Substring(1));
				}
				else
				{
					if (!address.StartsWith("W") && !address.StartsWith("w"))
					{
						throw new Exception(StringResources.Language.NotSupportedDataType);
					}
					num = 23;
					num2 = int.Parse(address.Substring(1));
				}
				return OperateResult.CreateSuccessResult(new YokogawaLinkAddress
				{
					DataCode = num,
					AddressStart = num2,
					Length = length
				});
			}
			catch (Exception ex)
			{
				return new OperateResult<YokogawaLinkAddress>(DeviceAddressDataBase.GetUnsupportedAddressInfo(address, ex));
			}
		}
	}
}
