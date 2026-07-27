using System;
using System.Threading.Tasks;

namespace HslCommunication.Profinet.Delta.Helper
{
	/// <summary>
	/// 台达的想关的辅助类
	/// </summary>
	public class DeltaHelper
	{
		internal static OperateResult<string> TranslateToModbusAddress(IDelta delta, string address, byte modbusCode)
		{
			switch (delta.Series)
			{
			case DeltaSeries.Dvp:
				return DeltaDvpHelper.ParseDeltaDvpAddress(address, modbusCode);
			case DeltaSeries.AS:
				return DeltaASHelper.ParseDeltaASAddress(address, modbusCode);
			default:
				return new OperateResult<string>(StringResources.Language.NotSupportedDataType);
			}
		}

		internal static OperateResult<bool[]> ReadBool(IDelta delta, Func<string, ushort, OperateResult<bool[]>> readBoolFunc, string address, ushort length)
		{
			switch (delta.Series)
			{
			case DeltaSeries.Dvp:
				return DeltaDvpHelper.ReadBool(readBoolFunc, address, length);
			case DeltaSeries.AS:
				return readBoolFunc(address, length);
			default:
				return new OperateResult<bool[]>(StringResources.Language.NotSupportedDataType);
			}
		}

		internal static OperateResult Write(IDelta delta, Func<string, bool[], OperateResult> writeBoolFunc, string address, bool[] values)
		{
			switch (delta.Series)
			{
			case DeltaSeries.Dvp:
				return DeltaDvpHelper.Write(writeBoolFunc, address, values);
			case DeltaSeries.AS:
				return writeBoolFunc(address, values);
			default:
				return new OperateResult(StringResources.Language.NotSupportedDataType);
			}
		}

		internal static OperateResult<byte[]> Read(IDelta delta, Func<string, ushort, OperateResult<byte[]>> readFunc, string address, ushort length)
		{
			switch (delta.Series)
			{
			case DeltaSeries.Dvp:
				return DeltaDvpHelper.Read(readFunc, address, length);
			case DeltaSeries.AS:
				return readFunc(address, length);
			default:
				return new OperateResult<byte[]>(StringResources.Language.NotSupportedDataType);
			}
		}

		internal static OperateResult Write(IDelta delta, Func<string, byte[], OperateResult> writeFunc, string address, byte[] value)
		{
			switch (delta.Series)
			{
			case DeltaSeries.Dvp:
				return DeltaDvpHelper.Write(writeFunc, address, value);
			case DeltaSeries.AS:
				return writeFunc(address, value);
			default:
				return new OperateResult(StringResources.Language.NotSupportedDataType);
			}
		}

		internal static async Task<OperateResult<bool[]>> ReadBoolAsync(IDelta delta, Func<string, ushort, Task<OperateResult<bool[]>>> readBoolFunc, string address, ushort length)
		{
			switch (delta.Series)
			{
			case DeltaSeries.Dvp:
				return await DeltaDvpHelper.ReadBoolAsync(readBoolFunc, address, length);
			case DeltaSeries.AS:
				return await readBoolFunc(address, length);
			default:
				return new OperateResult<bool[]>(StringResources.Language.NotSupportedDataType);
			}
		}

		internal static async Task<OperateResult> WriteAsync(IDelta delta, Func<string, bool[], Task<OperateResult>> writeBoolFunc, string address, bool[] values)
		{
			switch (delta.Series)
			{
			case DeltaSeries.Dvp:
				return await DeltaDvpHelper.WriteAsync(writeBoolFunc, address, values);
			case DeltaSeries.AS:
				return await writeBoolFunc(address, values);
			default:
				return new OperateResult(StringResources.Language.NotSupportedDataType);
			}
		}

		internal static async Task<OperateResult<byte[]>> ReadAsync(IDelta delta, Func<string, ushort, Task<OperateResult<byte[]>>> readFunc, string address, ushort length)
		{
			switch (delta.Series)
			{
			case DeltaSeries.Dvp:
				return await DeltaDvpHelper.ReadAsync(readFunc, address, length);
			case DeltaSeries.AS:
				return await readFunc(address, length);
			default:
				return new OperateResult<byte[]>(StringResources.Language.NotSupportedDataType);
			}
		}

		internal static async Task<OperateResult> WriteAsync(IDelta delta, Func<string, byte[], Task<OperateResult>> writeFunc, string address, byte[] value)
		{
			switch (delta.Series)
			{
			case DeltaSeries.Dvp:
				return await DeltaDvpHelper.WriteAsync(writeFunc, address, value);
			case DeltaSeries.AS:
				return await writeFunc(address, value);
			default:
				return new OperateResult(StringResources.Language.NotSupportedDataType);
			}
		}
	}
}
