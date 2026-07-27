using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HslCommunication.BasicFramework;
using HslCommunication.Core.Net;
using HslCommunication.Profinet.AllenBradley;
using HslCommunication.Reflection;

namespace HslCommunication.Profinet.Omron
{
	/// <summary>
	/// 欧姆龙PLC的CIP协议的类，支持NJ,NX,NY系列PLC，支持tag名的方式读写数据，假设你读取的是局部变量，那么使用 Program:MainProgram.变量名<br />
	/// Omron PLC's CIP protocol class, support NJ, NX, NY series PLC, support tag name read and write data, assuming you read local variables, then use Program: MainProgram. Variable name
	/// </summary>
	public class OmronCipNet : AllenBradleyNet
	{
		/// <summary>
		/// Instantiate a communication object for a OmronCipNet PLC protocol
		/// </summary>
		public OmronCipNet()
		{
		}

		/// <summary>
		/// Specify the IP address and port to instantiate a communication object for a OmronCipNet PLC protocol
		/// </summary>
		/// <param name="ipAddress">PLC IpAddress</param>
		/// <param name="port">PLC Port</param>
		public OmronCipNet(string ipAddress, int port = 44818)
			: base(ipAddress, port)
		{
		}

		private bool CheckAddressArrayTag(string address)
		{
			return Regex.IsMatch(address, "\\[[0-9]+\\]$");
		}

		/// <inheritdoc />
		protected override bool GetBoolWritePadding()
		{
			return true;
		}

		/// <inheritdoc />
		[HslMqttApi("ReadByteArray", "")]
		public override OperateResult<byte[]> Read(string address, ushort length)
		{
			if (length > 1)
			{
				if (CheckAddressArrayTag(address))
				{
					return Read(new string[1] { address }, new ushort[1] { length });
				}
				return Read(new string[1] { address }, new ushort[1] { 1 });
			}
			return Read(new string[1] { address }, new ushort[1] { length });
		}

		/// <inheritdoc />
		[HslMqttApi("ReadBoolArray", "")]
		public override OperateResult<bool[]> ReadBool(string address, ushort length)
		{
			if (address.StartsWith("i="))
			{
				return base.ReadBool(address, length);
			}
			OperateResult<byte[]> operateResult = Read(address, length);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<bool[]>(operateResult);
			}
			if (CheckAddressArrayTag(address))
			{
				return OperateResult.CreateSuccessResult(operateResult.Content.Select((byte m) => m != 0).Take(length).ToArray());
			}
			return OperateResult.CreateSuccessResult(SoftBasic.ByteToBoolArray(operateResult.Content, length));
		}

		/// <inheritdoc />
		public override OperateResult<string> ReadString(string address, ushort length, Encoding encoding)
		{
			OperateResult<byte[]> operateResult = Read(address, length);
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<string>(operateResult);
			}
			try
			{
				return OperateResult.CreateSuccessResult(encoding.GetString(count: base.ByteTransform.TransUInt16(operateResult.Content, 0), bytes: operateResult.Content, index: 2));
			}
			catch (Exception ex)
			{
				return new OperateResult<string>("Parse failed: " + ex.Message + Environment.NewLine + "Source: " + operateResult.Content.ToHexString(' '));
			}
		}

		/// <inheritdoc />
		protected override int GetWriteValueLength(string address, int length)
		{
			return (!CheckAddressArrayTag(address)) ? 1 : length;
		}

		/// <inheritdoc />
		public override OperateResult<T> ReadStruct<T>(string address, ushort length)
		{
			return ReadWriteNetHelper.ReadStruct<T>(this, address, length, base.ByteTransform, 2);
		}

		/// <inheritdoc />
		public override OperateResult Write(string address, string value, Encoding encoding)
		{
			if (string.IsNullOrEmpty(value))
			{
				value = string.Empty;
			}
			byte[] array = SoftBasic.SpliceArray<byte>(new byte[2], SoftBasic.ArrayExpandToLengthEven(encoding.GetBytes(value)));
			array[0] = BitConverter.GetBytes(array.Length - 2)[0];
			array[1] = BitConverter.GetBytes(array.Length - 2)[1];
			return WriteTag(address, 208, array);
		}

		/// <inheritdoc />
		[HslMqttApi("WriteByte", "")]
		public override OperateResult Write(string address, byte value)
		{
			return WriteTag(address, 209, new byte[1] { value });
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.Omron.OmronCipNet.Read(System.String,System.UInt16)" />
		public override async Task<OperateResult<byte[]>> ReadAsync(string address, ushort length)
		{
			if (length > 1)
			{
				if (CheckAddressArrayTag(address))
				{
					return await ReadAsync(new string[1] { address }, new ushort[1] { length });
				}
				return await ReadAsync(new string[1] { address }, new ushort[1] { 1 });
			}
			return await ReadAsync(new string[1] { address }, new ushort[1] { length });
		}

		/// <inheritdoc />
		public override async Task<OperateResult<bool[]>> ReadBoolAsync(string address, ushort length)
		{
			if (address.StartsWith("i="))
			{
				return await base.ReadBoolAsync(address, length);
			}
			OperateResult<byte[]> read = await ReadAsync(address, length);
			if (!read.IsSuccess)
			{
				return OperateResult.CreateFailedResult<bool[]>(read);
			}
			if (CheckAddressArrayTag(address))
			{
				return OperateResult.CreateSuccessResult(read.Content.Select((byte m) => m != 0).Take(length).ToArray());
			}
			return OperateResult.CreateSuccessResult(SoftBasic.ByteToBoolArray(read.Content, length));
		}

		/// <inheritdoc />
		public override async Task<OperateResult<string>> ReadStringAsync(string address, ushort length, Encoding encoding)
		{
			OperateResult<byte[]> read = await ReadAsync(address, length);
			if (!read.IsSuccess)
			{
				return OperateResult.CreateFailedResult<string>(read);
			}
			try
			{
				return OperateResult.CreateSuccessResult(encoding.GetString(count: base.ByteTransform.TransUInt16(read.Content, 0), bytes: read.Content, index: 2));
			}
			catch (Exception ex)
			{
				return new OperateResult<string>("Parse failed: " + ex.Message + Environment.NewLine + "Source: " + read.Content.ToHexString(' '));
			}
		}

		/// <inheritdoc />
		public override async Task<OperateResult> WriteAsync(string address, string value, Encoding encoding)
		{
			if (string.IsNullOrEmpty(value))
			{
				value = string.Empty;
			}
			byte[] data = SoftBasic.SpliceArray<byte>(new byte[2], SoftBasic.ArrayExpandToLengthEven(encoding.GetBytes(value)));
			data[0] = BitConverter.GetBytes(data.Length - 2)[0];
			data[1] = BitConverter.GetBytes(data.Length - 2)[1];
			return await WriteTagAsync(address, 208, data);
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.Omron.OmronCipNet.Write(System.String,System.Byte)" />
		public override async Task<OperateResult> WriteAsync(string address, byte value)
		{
			return await WriteTagAsync(address, 209, new byte[1] { value });
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return $"OmronCipNet[{IpAddress}:{Port}]";
		}
	}
}
