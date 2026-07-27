using System;
using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.Core;
using HslCommunication.Core.Device;
using HslCommunication.Core.Net;
using HslCommunication.Core.Pipe;
using HslCommunication.Instrument.CJT;
using HslCommunication.Instrument.IEC;
using HslCommunication.MQTT;
using HslCommunication.Profinet.AllenBradley;
using HslCommunication.Profinet.Beckhoff;
using HslCommunication.Profinet.Siemens;
using HslCommunication.Robot.ABB;

namespace HslCommunicationDemo.DemoControl
{
	public class CodeExampleControl : UserControl
	{
		private string deviceName = string.Empty;

		private string deviceCreateCode = string.Empty;

		private string deviceMethodCode = string.Empty;

		private IContainer components = null;

		private TextBox textBox1;

		private TextBox textBox2;

		public CodeExampleControl()
		{
			InitializeComponent();
			base.SizeChanged += CodeExampleControl_SizeChanged;
		}

		private void CodeExampleControl_SizeChanged(object sender, EventArgs e)
		{
			int num = base.Width;
			if (num > 50)
			{
				int num2 = num * 3 / 5;
				if (num2 > 200)
				{
					num2 -= 43;
				}
				int num3 = num - num2;
				textBox1.Width = num2 - 1;
				textBox2.Location = new Point(num2 + 1, 0);
				textBox2.Width = num3 - 1;
			}
		}

		public static string GetTitle()
		{
			return (Program.Language == 1) ? "代码示例" : "Code Example";
		}

		public void SetCodeText(MqttSyncClient network, params string[] props)
		{
			deviceName = "rpc";
			StringBuilder stringBuilder = CreateStringBulider(network, deviceName);
			SetPropties(deviceName, stringBuilder, network, props);
			RenderExampleCode(stringBuilder);
		}

		private static object GetObject(object obj, string names)
		{
			int num = names.IndexOf('.');
			if (num < 0)
			{
				return obj;
			}
			string name = names.Substring(0, num);
			PropertyInfo property = obj.GetType().GetProperty(name);
			if (property == null)
			{
				return null;
			}
			return GetObject(property.GetValue(obj), names.Substring(num + 1));
		}

		private static string GetPropertyName(string name)
		{
			if (name.IndexOf('.') < 0)
			{
				return name;
			}
			return name.Substring(name.LastIndexOf('.') + 1);
		}

		private static void SetPropties(string iniName, StringBuilder stringBuilder, object obj, params string[] props)
		{
			if (props != null && props.Length != 0)
			{
				foreach (string text in props)
				{
					if (text.StartsWith("HslCommunication.ModBus.ModbusMappingAddress."))
					{
						stringBuilder.Append(iniName + ".RegisteredAddressMapping( " + text + " );    // 重新指定地址映射，这一行很关键");
						stringBuilder.AppendLine();
						continue;
					}
					string propertyName = GetPropertyName(text);
					object @object = GetObject(obj, text);
					if (@object == null)
					{
						continue;
					}
					stringBuilder.Append(iniName + ".");
					stringBuilder.Append(text);
					stringBuilder.Append(" = ");
					PropertyInfo property = @object.GetType().GetProperty(propertyName);
					if (property.PropertyType == typeof(string))
					{
						object value = property.GetValue(@object);
						if (value == null)
						{
							stringBuilder.Append("null");
						}
						else
						{
							stringBuilder.Append("\"");
							stringBuilder.Append(value.ToString());
							stringBuilder.Append("\"");
						}
					}
					else if (property.PropertyType.IsSubclassOf(typeof(Enum)))
					{
						stringBuilder.Append(property.PropertyType.FullName);
						stringBuilder.Append(".");
						stringBuilder.Append(property.GetValue(@object).ToString());
					}
					else if (property.PropertyType == typeof(bool))
					{
						stringBuilder.Append(property.GetValue(@object).ToString().ToLower());
					}
					else if (property.PropertyType == typeof(MessageRouter))
					{
						stringBuilder.Append("new HslCommunication.Profinet.AllenBradley.MessageRouter( \"" + ((MessageRouter)property.GetValue(@object)).GetRouter().ToHexString(' ') + "\".ToHexBytes( ) )");
					}
					else if (property.PropertyType == typeof(Encoding))
					{
						Encoding encoding = (Encoding)property.GetValue(@object);
						if (encoding == Encoding.ASCII)
						{
							stringBuilder.Append("System.Text.Encoding.ASCII");
						}
						else if (encoding == Encoding.Default)
						{
							stringBuilder.Append("System.Text.Encoding.Default");
						}
						else if (encoding == Encoding.UTF8)
						{
							stringBuilder.Append("System.Text.Encoding.UTF8");
						}
						else if (encoding == Encoding.Unicode)
						{
							stringBuilder.Append("System.Text.Encoding.Unicode");
						}
						else if (encoding == Encoding.BigEndianUnicode)
						{
							stringBuilder.Append("System.Text.Encoding.BigEndianUnicode");
						}
						else
						{
							stringBuilder.Append("System.Text.Encoding.Default");
						}
					}
					else if (property.PropertyType == typeof(TimeSpan))
					{
						TimeSpan timeSpan = (TimeSpan)property.GetValue(@object);
						if (timeSpan.TotalSeconds >= 1.0)
						{
							stringBuilder.Append($"TimeSpan.FromSeconds( {timeSpan.TotalSeconds} )");
						}
						else
						{
							stringBuilder.Append($"TimeSpan.FromMilliseconds( {timeSpan.TotalMilliseconds} )");
						}
					}
					else if (property.PropertyType == typeof(MqttCredential))
					{
						if (!(property.GetValue(@object) is MqttCredential mqttCredential))
						{
							stringBuilder.Append("null");
						}
						else
						{
							stringBuilder.Append("new " + property.PropertyType.FullName + "( \"" + mqttCredential.UserName + "\",\"" + mqttCredential.Password + "\" )");
						}
					}
					else if (property.PropertyType == typeof(MqttApplicationMessage))
					{
						if (property.GetValue(@object) is MqttApplicationMessage obj2)
						{
							stringBuilder.Append("new " + property.PropertyType.FullName + "( );");
							stringBuilder.AppendLine();
							SetPropties(iniName + "." + text, stringBuilder, obj2, "Topic", "Payload", "Retain");
							return;
						}
						stringBuilder.Append("null");
					}
					else
					{
						if (property.PropertyType == typeof(IByteTransform))
						{
							stringBuilder.Append("new " + (property.GetValue(@object) as IByteTransform).GetType().FullName + "( );");
							stringBuilder.AppendLine();
							SetPropties(iniName, stringBuilder, obj, text + ".DataFormat", text + ".IsStringReverseByteWord");
							continue;
						}
						if (property.PropertyType == typeof(byte[]))
						{
							if (!(property.GetValue(@object) is byte[] inBytes))
							{
								stringBuilder.Append("null");
							}
							else
							{
								stringBuilder.Append("\"" + inBytes.ToHexString() + "\".ToHexBytes( )");
							}
						}
						else if (property.PropertyType == typeof(int[]))
						{
							stringBuilder.Append(ConvertArrayToString(property.GetValue(@object) as int[]));
						}
						else if (property.PropertyType == typeof(short[]))
						{
							stringBuilder.Append(ConvertArrayToString(property.GetValue(@object) as short[]));
						}
						else if (property.PropertyType == typeof(ushort[]))
						{
							stringBuilder.Append(ConvertArrayToString(property.GetValue(@object) as ushort[]));
						}
						else if (property.PropertyType == typeof(uint[]))
						{
							stringBuilder.Append(ConvertArrayToString(property.GetValue(@object) as uint[]));
						}
						else
						{
							stringBuilder.Append(property.GetValue(@object).ToString());
						}
					}
					stringBuilder.Append(";");
					stringBuilder.AppendLine();
				}
			}
			if (obj.GetType() == typeof(BeckhoffAdsNet) && obj is BeckhoffAdsNet beckhoffAdsNet && !beckhoffAdsNet.UseAutoAmsNetID)
			{
				stringBuilder.Append("plc.SetSenderAMSNetId( \"");
				stringBuilder.Append(beckhoffAdsNet.GetSenderAMSNetId());
				stringBuilder.Append("\" );");
				stringBuilder.AppendLine();
				stringBuilder.Append("plc.SetTargetAMSNetId( \"");
				stringBuilder.Append(beckhoffAdsNet.GetTargetAMSNetId());
				stringBuilder.Append("\" );");
				stringBuilder.AppendLine();
			}
		}

		private static string ConvertArrayToString<T>(T[] values)
		{
			if (values == null)
			{
				return "null";
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("new " + typeof(T).FullName + "[] { ");
			for (int i = 0; i < values.Length; i++)
			{
				stringBuilder.Append(values[i].ToString());
				if (i < values.Length - 1)
				{
					stringBuilder.Append(", ");
				}
			}
			stringBuilder.Append(" }");
			return stringBuilder.ToString();
		}

		public void SetCodeText(NetworkDoubleBase network, params string[] props)
		{
			deviceName = DemoUtils.PlcDeviceName;
			SetCodeText(deviceName, network, props);
		}

		public void SetCodeText(DeviceTcpNet network, params string[] props)
		{
			deviceName = DemoUtils.PlcDeviceName;
			SetCodeText(deviceName, network, props);
		}

		public void SetCodeText(DeviceUdpNet network, params string[] props)
		{
			deviceName = DemoUtils.PlcDeviceName;
			SetCodeText(deviceName, network, props);
		}

		public void SetCodeText(string deviceName, string com, NetworkServerBase serverBase, params string[] props)
		{
			this.deviceName = deviceName;
			StringBuilder stringBuilder = CreateFromObject(serverBase, deviceName);
			SetPropties(deviceName, stringBuilder, serverBase, props);
			stringBuilder.Append(deviceName + ".ServerStart( " + serverBase.Port + " );");
			stringBuilder.AppendLine();
			if (!string.IsNullOrEmpty(com))
			{
				stringBuilder.Append(deviceName + ".StartSerialSlave( \"" + com + "\" );");
				stringBuilder.AppendLine();
			}
			RenderExampleCode(stringBuilder);
		}

		public void SetCodeText(string deviceName, string com, CommunicationServer serverBase, params string[] props)
		{
			this.deviceName = deviceName;
			StringBuilder stringBuilder = CreateFromObject(serverBase, deviceName);
			SetPropties(deviceName, stringBuilder, serverBase, props);
			stringBuilder.Append(deviceName + ".ServerStart( " + serverBase.Port + " );");
			stringBuilder.AppendLine();
			if (!string.IsNullOrEmpty(com))
			{
				stringBuilder.Append(deviceName + ".SerialReceiveAtleastTime = " + serverBase.SerialReceiveAtleastTime + ";");
				stringBuilder.AppendLine();
				stringBuilder.Append(deviceName + ".StartSerialSlave( \"" + com + "\" );");
				stringBuilder.AppendLine();
			}
			RenderExampleCode(stringBuilder);
		}

		public void SetCodeText(string deviceName, string com, DeviceServer serverBase, params string[] props)
		{
			SetCodeText(deviceName, com, serverBase, null, props);
		}

		public void SetCodeText(string deviceName, string com, DeviceServer serverBase, SslServerControl sslServerControl, params string[] props)
		{
			this.deviceName = deviceName;
			StringBuilder stringBuilder = CreateFromObject(serverBase, deviceName);
			SetPropties(deviceName, stringBuilder, serverBase, props);
			stringBuilder.Append(deviceName + ".ActiveTimeSpan = TimeSpan.Parse( \"" + serverBase.ActiveTimeSpan.ToString() + "\" );");
			stringBuilder.AppendLine();
			if (serverBase.LocalAddress != null)
			{
				stringBuilder.Append(deviceName + ".LocalAddress = System.Net.IPAddress.Parse( \"" + serverBase.LocalAddress.ToString() + "\" );");
			}
			stringBuilder.AppendLine();
			stringBuilder.Append(deviceName + ".EnableIPv6 = " + serverBase.EnableIPv6.ToString().ToLower() + ";");
			stringBuilder.AppendLine();
			sslServerControl?.AddServerCode(stringBuilder);
			if (serverBase.ServerMode == 0)
			{
				stringBuilder.Append(deviceName + ".ServerStart( " + serverBase.Port + " );");
			}
			else if (serverBase.ServerMode == 1)
			{
				stringBuilder.Append(deviceName + ".ServerStart( " + serverBase.Port + ", false );");
			}
			else
			{
				stringBuilder.Append(deviceName + ".ServerStart( " + serverBase.Port + ", " + serverBase.BothModeUdpPort + " );");
			}
			stringBuilder.AppendLine();
			if (!string.IsNullOrEmpty(com))
			{
				stringBuilder.Append(deviceName + ".SerialReceiveAtleastTime = " + serverBase.SerialReceiveAtleastTime + ";");
				stringBuilder.AppendLine();
				stringBuilder.Append(deviceName + ".StartSerialSlave( \"" + com + "\" );");
				stringBuilder.AppendLine();
			}
			RenderExampleCode(stringBuilder);
		}

		public void SetCodeText(string deviceName, NetworkDoubleBase network, params string[] props)
		{
			this.deviceName = deviceName;
			StringBuilder stringBuilder = CreateStringBulider(network, deviceName);
			SetPropties(deviceName, stringBuilder, network, props);
			RenderExampleCode(stringBuilder);
		}

		public void SetCodeText(string deviceName, DeviceTcpNet device, params string[] props)
		{
			this.deviceName = deviceName;
			StringBuilder stringBuilder = CreateStringBulider(device, deviceName);
			SetPropties(deviceName, stringBuilder, device, props);
			CreateCommunicationPipe(stringBuilder, device.CommunicationPipe, deviceName);
			if (device.GetType() == typeof(IEC104))
			{
				stringBuilder.Append(FormIEC104.Example());
			}
			RenderExampleCode(stringBuilder);
		}

		public void SetCodeText(string deviceName, TcpNetCommunication device, params string[] props)
		{
			this.deviceName = deviceName;
			StringBuilder stringBuilder = CreateStringBulider(device, deviceName);
			SetPropties(deviceName, stringBuilder, device, props);
			RenderExampleCode(stringBuilder);
		}

		public void SetCodeText(string deviceName, DeviceUdpNet device, params string[] props)
		{
			this.deviceName = deviceName;
			StringBuilder stringBuilder = CreateStringBulider(device, deviceName);
			SetPropties(deviceName, stringBuilder, device, props);
			RenderExampleCode(stringBuilder);
		}

		public void SetCodeText(NetworkWebApiBase network, params string[] props)
		{
			deviceName = DemoUtils.PlcDeviceName;
			StringBuilder stringBuilder = CreateStringBulider(network);
			SetPropties(deviceName, stringBuilder, network, props);
			RenderExampleCode(stringBuilder);
		}

		public void SetCodeText(DeviceWebApi network, params string[] props)
		{
			deviceName = DemoUtils.PlcDeviceName;
			StringBuilder stringBuilder = CreateStringBulider(network);
			SetPropties(deviceName, stringBuilder, network, props);
			RenderExampleCode(stringBuilder);
		}

		public void SetCodeText(NetworkUdpBase network, params string[] props)
		{
			SetCodeText(DemoUtils.PlcDeviceName, network, props);
		}

		public void SetCodeText(string deviceName, NetworkUdpBase network, params string[] props)
		{
			this.deviceName = deviceName;
			StringBuilder stringBuilder = CreateStringBulider(network, deviceName);
			SetPropties(deviceName, stringBuilder, network, props);
			RenderExampleCode(stringBuilder);
		}

		public void SetCodeText(DeviceSerialPort network, params string[] props)
		{
			SetCodeText(DemoUtils.PlcDeviceName, network, props);
		}

		public void SetCodeText(string deviceName, DeviceSerialPort network, params string[] props)
		{
			this.deviceName = deviceName;
			StringBuilder stringBuilder = CreateStringBulider(network, deviceName);
			SetPropties(deviceName, stringBuilder, network, props);
			CreateCommunicationPipe(stringBuilder, network.CommunicationPipe, deviceName);
			RenderExampleCode(stringBuilder);
		}

		public static StringBuilder CreateStringBulider(NetworkWebApiBase network)
		{
			if (network == null)
			{
				return new StringBuilder();
			}
			string plcDeviceName = DemoUtils.PlcDeviceName;
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(network.GetType().FullName);
			stringBuilder.Append(" " + plcDeviceName + " = new ");
			stringBuilder.Append(network.GetType().FullName);
			if (network.GetType() == typeof(ABBWebApiClient))
			{
				ABBWebApiClient aBBWebApiClient = (ABBWebApiClient)network;
				stringBuilder.Append($"( \"{aBBWebApiClient.IpAddress}\", {aBBWebApiClient.Port}, \"{aBBWebApiClient.UserName}\", \"{aBBWebApiClient.Password}\" );");
				stringBuilder.AppendLine();
				return stringBuilder;
			}
			stringBuilder.Append("( );");
			stringBuilder.AppendLine();
			stringBuilder.Append(plcDeviceName + ".IpAddress = \"" + network.IpAddress + "\";");
			stringBuilder.AppendLine();
			stringBuilder.Append(plcDeviceName + ".Port = " + network.Port + ";");
			stringBuilder.AppendLine();
			return stringBuilder;
		}

		public static StringBuilder CreateStringBulider(DeviceWebApi network)
		{
			if (network == null)
			{
				return new StringBuilder();
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(network.GetType().FullName);
			stringBuilder.Append(" " + DemoUtils.PlcDeviceName + " = new ");
			stringBuilder.Append(network.GetType().FullName);
			stringBuilder.Append($"( \"{network.IpAddress}\", {network.Port}, \"{network.UserName}\", \"{network.Password}\" );");
			stringBuilder.AppendLine();
			return stringBuilder;
		}

		private static StringBuilder CreateFromObject(object obj, string deviceName)
		{
			string fullName = obj.GetType().FullName;
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(fullName);
			stringBuilder.Append(" " + deviceName + " = new ");
			stringBuilder.Append(fullName);
			if (obj.GetType() == typeof(SiemensS7Net))
			{
				stringBuilder.Append(string.Format("( HslCommunication.Profinet.Siemens.SiemensPLCS.{0} );", (SiemensPLCS)obj.GetType().GetField("CurrentPlc", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj)));
			}
			else if (obj.GetType() == typeof(CJT188))
			{
				stringBuilder.Append("( \"" + ((CJT188)obj).Station + "\" );");
			}
			else if (obj.GetType() == typeof(CJT188OverTcp))
			{
				stringBuilder.Append("( \"" + ((CJT188OverTcp)obj).Station + "\" );");
			}
			else
			{
				stringBuilder.Append("( );");
			}
			stringBuilder.AppendLine();
			return stringBuilder;
		}

		public static StringBuilder CreateStringBulider(MqttSyncClient network, string deviceName)
		{
			if (network == null)
			{
				return new StringBuilder();
			}
			StringBuilder stringBuilder = CreateFromObject(network.ConnectionOptions, "options");
			SetPropties("options", stringBuilder, network.ConnectionOptions, "IpAddress", "Port", "ClientId", "UseRSAProvider", "Credentials", "ConnectTimeout");
			stringBuilder.Append(network.GetType().FullName + " " + deviceName + " = new " + network.GetType().FullName + "( options );");
			stringBuilder.AppendLine();
			return stringBuilder;
		}

		public static StringBuilder CreateStringBulider(MqttClient mqttClient)
		{
			if (mqttClient == null)
			{
				return new StringBuilder();
			}
			StringBuilder stringBuilder = CreateFromObject(mqttClient.ConnectionOptions, "options");
			SetPropties("options", stringBuilder, mqttClient.ConnectionOptions, "IpAddress", "Port", "ClientId", "UseRSAProvider", "Credentials", "CleanSession", "UseSSL", "SSLSecure", "CertificateFile", "KeepAlivePeriod", "KeepAliveSendInterval", "WillMessage");
			stringBuilder.Append(mqttClient.GetType().FullName + " mqtt = new " + mqttClient.GetType().FullName + "( options );");
			stringBuilder.AppendLine();
			return stringBuilder;
		}

		private static void CreateCommunicationPipe(StringBuilder sb, CommunicationPipe pipe, string deviceName)
		{
			if (pipe.GetType() == typeof(PipeTcpNet) || pipe.GetType() == typeof(PipeSslNet) || pipe.GetType() == typeof(PipeUdpNet) || pipe.GetType() == typeof(PipeDebugRemote))
			{
				PipeTcpNet pipeTcpNet = pipe as PipeTcpNet;
				PipeTcpNet pipeTcpNet2 = new PipeTcpNet();
				sb.Append(deviceName + $".CommunicationPipe = new {pipeTcpNet.GetType().FullName}(\"{pipeTcpNet.Host}\", {pipeTcpNet.Port})");
				sb.AppendLine();
				sb.Append("{");
				sb.AppendLine();
				if (pipe.GetType() != typeof(PipeUdpNet))
				{
					sb.Append($"    ConnectTimeOut = {pipeTcpNet.ConnectTimeOut},    // 连接超时时间，单位毫秒");
					sb.AppendLine();
				}
				sb.Append($"    ReceiveTimeOut = {pipeTcpNet.ReceiveTimeOut},    // 接收设备数据反馈的超时时间");
				sb.AppendLine();
				if (pipeTcpNet.SleepTime != pipeTcpNet2.SleepTime)
				{
					sb.Append($"    SleepTime = {pipeTcpNet.SleepTime},");
					sb.AppendLine();
				}
				if (pipeTcpNet.CloseOnRecvTimeOutTick != pipeTcpNet2.CloseOnRecvTimeOutTick)
				{
					sb.Append($"    CloseOnRecvTimeOutTick = {pipeTcpNet.CloseOnRecvTimeOutTick},");
					sb.AppendLine();
				}
				if (pipeTcpNet.SocketKeepAliveTime != pipeTcpNet2.SocketKeepAliveTime)
				{
					sb.Append($"    SocketKeepAliveTime = {pipeTcpNet.SocketKeepAliveTime},");
					sb.AppendLine();
				}
				if (pipeTcpNet.IsPersistentConnection != pipeTcpNet2.IsPersistentConnection)
				{
					sb.Append("    IsPersistentConnection = " + pipeTcpNet.IsPersistentConnection.ToString().ToLower() + ",");
					sb.AppendLine();
				}
				if (pipeTcpNet.UseServerActivePush)
				{
					sb.Append("    UseServerActivePush = " + pipeTcpNet.UseServerActivePush.ToString().ToLower() + ",");
					sb.AppendLine();
				}
				if (pipe.GetType() == typeof(PipeDebugRemote))
				{
					PipeDebugRemote pipeDebugRemote = pipe as PipeDebugRemote;
					if (pipeDebugRemote.ClientKey > 0)
					{
						sb.Append($"    ClientKey = {pipeDebugRemote.ClientKey},");
						sb.AppendLine();
					}
				}
				if (pipeTcpNet.LocalBinding != null)
				{
					sb.Append($"    LocalBinding = new System.Net.IPEndPoint( System.Net.IPAddress.Parse( \"{pipeTcpNet.LocalBinding.Address}\" ), {pipeTcpNet.LocalBinding.Port} ),");
					sb.AppendLine();
				}
				sb.Append("};");
				sb.AppendLine();
			}
			else if (pipe.GetType() == typeof(PipeSerialPort))
			{
				PipeSerialPort pipeSerialPort = pipe as PipeSerialPort;
				sb.Append(pipeSerialPort.GetType().FullName + " pipe =  new " + pipeSerialPort.GetType().FullName + "( );");
				sb.AppendLine();
				sb.Append("pipe.SerialPortInni( \"" + pipeSerialPort.GetPipe().ToFormatString() + "\" );");
				sb.AppendLine();
				sb.Append("pipe.RtsEnable = " + pipeSerialPort.RtsEnable.ToString().ToLower() + ";");
				sb.AppendLine();
				sb.Append("pipe.DtrEnable = " + pipeSerialPort.DtrEnable.ToString().ToLower() + ";");
				sb.AppendLine();
				sb.Append($"pipe.SleepTime = {pipeSerialPort.SleepTime};");
				sb.AppendLine();
				sb.Append($"pipe.ReceiveTimeOut = {pipeSerialPort.ReceiveTimeOut};");
				sb.AppendLine();
				sb.Append(deviceName + ".CommunicationPipe = pipe;");
				sb.AppendLine();
			}
			else if (pipe.GetType() == typeof(PipeMoxa))
			{
				PipeMoxa pipeMoxa = pipe as PipeMoxa;
				sb.Append(pipeMoxa.GetType().FullName + " pipe =  new " + pipeMoxa.GetType().FullName + "( );");
				sb.AppendLine();
				sb.Append("pipe.SerialPortInni( \"" + pipeMoxa.ToFormatString() + "\" );");
				sb.AppendLine();
				sb.Append("pipe.RtsEnable = " + pipeMoxa.RtsEnable.ToString().ToLower() + ";");
				sb.AppendLine();
				sb.Append("pipe.DtrEnable = " + pipeMoxa.DtrEnable.ToString().ToLower() + ";");
				sb.AppendLine();
				sb.Append($"pipe.SleepTime = {pipeMoxa.SleepTime};");
				sb.AppendLine();
				sb.Append($"pipe.ReceiveEmptyDataCount = {pipeMoxa.ReceiveEmptyDataCount};");
				sb.AppendLine();
				sb.Append(deviceName + ".CommunicationPipe = pipe;");
				sb.AppendLine();
			}
			else if (pipe.GetType() == typeof(PipeMqttClient))
			{
				PipeMqttClient pipeMqttClient = pipe as PipeMqttClient;
				sb.Append("HslCommunication.MQTT.MqttClient mqttClient = new HslCommunication.MQTT.MqttClient( new HslCommunication.MQTT.MqttConnectionOptions( ){");
				sb.AppendLine();
				sb.Append("    IpAddress = \"" + pipeMqttClient.MqttClient.ConnectionOptions.HostName + "\",");
				sb.AppendLine();
				sb.Append($"    Port = {pipeMqttClient.MqttClient.ConnectionOptions.Port},");
				sb.AppendLine();
				if (pipeMqttClient.MqttClient.ConnectionOptions.Credentials != null)
				{
					sb.Append("    Credentials = new HslCommunication.MQTT.MqttCredential( \"" + pipeMqttClient.MqttClient.ConnectionOptions.Credentials.UserName + "\", \"" + pipeMqttClient.MqttClient.ConnectionOptions.Credentials.Password + "\" ),");
					sb.AppendLine();
				}
				sb.Append("    ClientId = \"" + pipeMqttClient.MqttClient.ConnectionOptions.ClientId + "\",");
				sb.AppendLine();
				sb.Append("    UseRSAProvider = " + pipeMqttClient.MqttClient.ConnectionOptions.UseRSAProvider.ToString().ToLower() + ",");
				sb.AppendLine();
				sb.Append("} );");
				sb.AppendLine();
				sb.Append(deviceName + ".CommunicationPipe = new " + pipeMqttClient.GetType().FullName + "( mqttClient, \"" + pipeMqttClient.ReadTopic + "\", \"" + pipeMqttClient.WriteTopic + "\" );");
				sb.AppendLine();
			}
			else if (pipe.GetType() == typeof(PipeDtuNet))
			{
				PipeDtuNet pipeDtuNet = pipe as PipeDtuNet;
				sb.AppendLine();
				sb.Append("NetworkAlienClient dtuServer = new NetworkAlienClient( );");
				sb.AppendLine();
				sb.Append("dtuServer.IsResponseAck = " + pipeDtuNet.DtuServer.IsResponseAck.ToString().ToLower() + ";");
				sb.AppendLine();
				if (!pipeDtuNet.DtuServer.UseRegistrationPackage)
				{
					sb.Append("dtuServer.UseRegistrationPackage = " + pipeDtuNet.DtuServer.UseRegistrationPackage.ToString().ToLower() + ";");
					sb.AppendLine();
				}
				if (!string.IsNullOrEmpty(pipeDtuNet.Pwd))
				{
					sb.Append("dtuServer.SetPassword( Encoding.ASCII.GetBytes( \"" + pipeDtuNet.Pwd + "\" ) );");
					sb.AppendLine();
				}
				sb.Append("dtuServer.OnClientConnected += ( PipeDtuNet session ) =>\r\n{\r\n\tif (session.DTU == \"" + pipeDtuNet.DTU + "\")\r\n\t{\r\n\t\tOperateResult connect = " + deviceName + ".SetDtuPipe( session );\r\n\t\tif (connect.IsSuccess)\r\n\t\t{\r\n\t\t\tConsole.WriteLine( \"connect success\" );\r\n\t\t}\r\n\t}\r\n};");
				sb.AppendLine();
				sb.Append($"dtuServer.ServerStart( {pipeDtuNet.DtuServer.Port} );");
				sb.AppendLine();
				sb.AppendLine();
				sb.Append(deviceName + ".SetDtuPipe( new PipeDtuNet( ) );   // 在连接上来之前，先给一个默认的空的DTU会话");
				sb.AppendLine();
			}
		}

		public static StringBuilder CreateStringBulider(NetworkDoubleBase network, string deviceName)
		{
			if (network == null)
			{
				return new StringBuilder();
			}
			StringBuilder stringBuilder = CreateFromObject(network, deviceName);
			stringBuilder.Append(deviceName + ".IpAddress = \"" + network.IpAddress + "\";");
			stringBuilder.AppendLine();
			stringBuilder.Append(deviceName + ".Port = " + network.Port + ";");
			stringBuilder.AppendLine();
			stringBuilder.Append(deviceName + ".ConnectTimeOut = " + network.ConnectTimeOut + ";     // 连接超时，单位毫秒");
			stringBuilder.AppendLine();
			stringBuilder.Append(deviceName + ".ReceiveTimeOut = " + network.ReceiveTimeOut + ";     // 接收超时，单位毫秒");
			stringBuilder.AppendLine();
			return stringBuilder;
		}

		public static StringBuilder CreateStringBulider(DeviceTcpNet device, string deviceName)
		{
			if (device == null)
			{
				return new StringBuilder();
			}
			return CreateFromObject(device, deviceName);
		}

		public static StringBuilder CreateStringBulider(DeviceUdpNet device, string deviceName)
		{
			if (device == null)
			{
				return new StringBuilder();
			}
			StringBuilder stringBuilder = CreateFromObject(device, deviceName);
			CreateCommunicationPipe(stringBuilder, device.CommunicationPipe, deviceName);
			return stringBuilder;
		}

		public static StringBuilder CreateStringBulider(TcpNetCommunication device, string deviceName)
		{
			if (device == null)
			{
				return new StringBuilder();
			}
			StringBuilder stringBuilder = CreateFromObject(device, deviceName);
			CreateCommunicationPipe(stringBuilder, device.CommunicationPipe, deviceName);
			return stringBuilder;
		}

		public static StringBuilder CreateStringBulider(NetworkUdpBase network, string deviceName)
		{
			if (network == null)
			{
				return new StringBuilder();
			}
			StringBuilder stringBuilder = CreateFromObject(network, deviceName);
			stringBuilder.Append(deviceName + ".IpAddress = \"" + network.IpAddress + "\";");
			stringBuilder.AppendLine();
			stringBuilder.Append(deviceName + ".Port = " + network.Port + ";");
			stringBuilder.AppendLine();
			stringBuilder.Append(deviceName + ".ReceiveTimeout = " + network.ReceiveTimeout + ";   // 接收超时，单位毫秒");
			stringBuilder.AppendLine();
			return stringBuilder;
		}

		public static StringBuilder CreateStringBulider(DeviceSerialPort device, string deviceName)
		{
			if (device == null)
			{
				return new StringBuilder();
			}
			return CreateFromObject(device, deviceName);
		}

		private void RenderExampleCode(StringBuilder sb)
		{
			deviceCreateCode = sb.ToString();
			textBox1.Text = deviceCreateCode;
		}

		public void ReaderReadCode(string methodCode)
		{
			if (string.IsNullOrEmpty(deviceName))
			{
				return;
			}
			try
			{
				textBox2.Text = Regex.Replace(methodCode, "@deviceName", deviceName);
			}
			catch
			{
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && components != null)
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		private void InitializeComponent()
		{
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.textBox2 = new System.Windows.Forms.TextBox();
			base.SuspendLayout();
			this.textBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.textBox1.BackColor = System.Drawing.Color.White;
			this.textBox1.Location = new System.Drawing.Point(0, 0);
			this.textBox1.Multiline = true;
			this.textBox1.Name = "textBox1";
			this.textBox1.ReadOnly = true;
			this.textBox1.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox1.Size = new System.Drawing.Size(390, 448);
			this.textBox1.TabIndex = 0;
			this.textBox2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
			this.textBox2.BackColor = System.Drawing.Color.White;
			this.textBox2.Location = new System.Drawing.Point(396, 0);
			this.textBox2.Multiline = true;
			this.textBox2.Name = "textBox2";
			this.textBox2.ReadOnly = true;
			this.textBox2.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
			this.textBox2.Size = new System.Drawing.Size(394, 448);
			this.textBox2.TabIndex = 1;
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.Controls.Add(this.textBox2);
			base.Controls.Add(this.textBox1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "CodeExampleControl";
			base.Size = new System.Drawing.Size(792, 448);
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
