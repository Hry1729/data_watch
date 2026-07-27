using System.ComponentModel;

namespace HslCommunicationDemo.DemoControl
{
	public class SettingMqtt
	{
		[Description("Mqtt账户的密码，如果需要账户验证的话\r\nPassword for Mqtt account, if account verification is required")]
		[DefaultValue("")]
		public string Password { get; set; } = "";

		[Description("Mqtt的客户端ID信息，根据实际情况输入\r\nClient ID information of Mqtt. Set this parameter based on the actual situation")]
		[DefaultValue("")]
		public string ClientID { get; set; } = "";

		[Description("订阅该topic时，当服务器收到设备发送的报文时，将返回该报文。\r\nWhen you subscribe to this topic, the server returns the packet when it receives the packet sent by the device.")]
		[DisplayName("Device->Mqtt Topic")]
		[DefaultValue("DeviceToMqtt")]
		public string Device2Mqtt { get; set; } = "DeviceToMqtt";

		[Description("发布该topic时，将由服务器把报文写入到真实的PLC里面。\r\nWhen this topic is published, the server writes the message into the real PLC.")]
		[DisplayName("Mqtt->Device Topic")]
		[DefaultValue("MqttToDevice")]
		public string Mqtt2Device { get; set; } = "MqttToDevice";

		[Description("当连接hslcommunication构建的MQTTServer时，支持rsa加密通信操作\r\nrsa encrypted communication operations are supported when connecting to MQTTServer built by hslcommunication")]
		[DefaultValue(false)]
		public bool UseRSAProvider { get; set; }

		public SettingMqtt()
		{
		}

		public SettingMqtt(SettingMqtt other)
		{
			Password = other.Password;
			ClientID = other.ClientID;
			Device2Mqtt = other.Device2Mqtt;
			Mqtt2Device = other.Mqtt2Device;
			UseRSAProvider = other.UseRSAProvider;
		}

		public override string ToString()
		{
			return "Mqtt Setting [MQTT额外参数配置]";
		}
	}
}
