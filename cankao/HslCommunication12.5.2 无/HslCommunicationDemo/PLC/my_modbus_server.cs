using System;
using HslCommunication.ModBus;
using HslCommunication.Profinet.Knx;

namespace HslCommunicationDemo.PLC
{
	public class my_modbus_server : ModbusTcpServer
	{
		private byte[] modbus_buffer;

		private KnxUdp knx_Connect;

		public my_modbus_server(KnxUdp In_knx_Connect)
		{
			knx_Connect = In_knx_Connect;
			base.OnDataReceived += My_modbus_server_OnDataReceived;
		}

		private void My_modbus_server_OnDataReceived(object sender, object source, byte[] data)
		{
			byte[] array = new byte[2];
			if (modbus_buffer == null)
			{
				return;
			}
			switch (modbus_buffer[1])
			{
			case 6:
			{
				byte[] array3 = new byte[1];
				array[0] = modbus_buffer[3];
				array[1] = modbus_buffer[2];
				short addr2 = (short)BitConverter.ToUInt16(array, 0);
				if ((modbus_buffer[5] == 1) | (modbus_buffer[5] == 0))
				{
					array3[0] = modbus_buffer[5];
					knx_Connect.SetKnxData(addr2, 1, array3);
				}
				break;
			}
			case 16:
			{
				byte[] array2 = new byte[1];
				array[0] = modbus_buffer[3];
				array[1] = modbus_buffer[2];
				short addr = (short)BitConverter.ToUInt16(array, 0);
				if ((modbus_buffer[8] == 1) | (modbus_buffer[8] == 0))
				{
					array2[0] = modbus_buffer[8];
					knx_Connect.SetKnxData(addr, 1, array2);
				}
				break;
			}
			}
			modbus_buffer = null;
		}

		protected override byte[] ReadFromModbusCore(byte[] modbusCore)
		{
			byte[] result = base.ReadFromModbusCore(modbusCore);
			switch (modbusCore[1])
			{
			case 6:
				modbus_buffer = modbusCore;
				break;
			case 16:
				modbus_buffer = modbusCore;
				break;
			}
			return result;
		}
	}
}
