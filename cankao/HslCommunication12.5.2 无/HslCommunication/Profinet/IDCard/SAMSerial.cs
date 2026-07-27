using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using HslCommunication.BasicFramework;
using HslCommunication.Core.IMessage;
using HslCommunication.Reflection;
using HslCommunication.Serial;

namespace HslCommunication.Profinet.IDCard
{
	/// <summary>
	/// 基于SAM协议的串口通信类，支持读取身份证的数据信息，详细参见API文档<br />
	/// Network class implemented by Tcp based on the SAM protocol, which supports reading ID card data information, 
	/// see API documentation for details
	/// </summary>
	/// <example>
	/// 在使用之前需要实例化当前的对象，然后根据实际的情况填写好串口的信息，否则连接不上去。
	/// <code lang="cs" source="HslCommunication_Net45.Test\Documentation\Samples\Profinet\SAMSerialSample.cs" region="Sample1" title="实例化操作" />
	/// 在实际的读取，我们一般放在后台进行循环扫描的操作，参见下面的代码
	/// <code lang="cs" source="HslCommunication_Net45.Test\Documentation\Samples\Profinet\SAMSerialSample.cs" region="Sample2" title="基本的读取操作" />
	/// </example>
	public class SAMSerial : SerialBase
	{
		[CompilerGenerated]
		private sealed class _003CGetNationEnumerator_003Ed__15 : IEnumerator<string>, IDisposable, IEnumerator
		{
			private int _003C_003E1__state;

			private string _003C_003E2__current;

			private int _003Ci_003E5__1;

			string IEnumerator<string>.Current
			{
				[DebuggerHidden]
				get
				{
					return _003C_003E2__current;
				}
			}

			object IEnumerator.Current
			{
				[DebuggerHidden]
				get
				{
					return _003C_003E2__current;
				}
			}

			[DebuggerHidden]
			public _003CGetNationEnumerator_003Ed__15(int _003C_003E1__state)
			{
				this._003C_003E1__state = _003C_003E1__state;
			}

			[DebuggerHidden]
			void IDisposable.Dispose()
			{
				_003C_003E1__state = -2;
			}

			private bool MoveNext()
			{
				switch (_003C_003E1__state)
				{
				default:
					return false;
				case 0:
					_003C_003E1__state = -1;
					_003Ci_003E5__1 = 1;
					break;
				case 1:
					_003C_003E1__state = -1;
					_003Ci_003E5__1++;
					break;
				}
				if (_003Ci_003E5__1 < 57)
				{
					_003C_003E2__current = GetNationText(_003Ci_003E5__1);
					_003C_003E1__state = 1;
					return true;
				}
				return false;
			}

			bool IEnumerator.MoveNext()
			{
				//ILSpy generated this explicit interface implementation from .override directive in MoveNext
				return this.MoveNext();
			}

			[DebuggerHidden]
			void IEnumerator.Reset()
			{
				throw new NotSupportedException();
			}
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.IDCard.SAMTcpNet.#ctor" />
		public SAMSerial()
		{
		}

		/// <inheritdoc />
		protected override INetMessage GetNewNetMessage()
		{
			return new SAMMessage();
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.IDCard.SAMTcpNet.ReadSafeModuleNumber" />
		[HslMqttApi]
		public OperateResult<string> ReadSafeModuleNumber()
		{
			OperateResult<byte[]> operateResult = ReadFromCoreServer(PackToSAMCommand(BuildReadCommand(18, byte.MaxValue, null)));
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<string>(operateResult);
			}
			OperateResult operateResult2 = CheckADSCommandAndSum(operateResult.Content);
			if (!operateResult2.IsSuccess)
			{
				return OperateResult.CreateFailedResult<string>(operateResult2);
			}
			return ExtractSafeModuleNumber(operateResult.Content);
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.IDCard.SAMTcpNet.CheckSafeModuleStatus" />
		[HslMqttApi]
		public OperateResult CheckSafeModuleStatus()
		{
			OperateResult<byte[]> operateResult = ReadFromCoreServer(PackToSAMCommand(BuildReadCommand(18, byte.MaxValue, null)));
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<string>(operateResult);
			}
			OperateResult operateResult2 = CheckADSCommandAndSum(operateResult.Content);
			if (!operateResult2.IsSuccess)
			{
				return OperateResult.CreateFailedResult<string>(operateResult2);
			}
			if (operateResult.Content[9] != 144)
			{
				return new OperateResult(GetErrorDescription(operateResult.Content[9]));
			}
			return OperateResult.CreateSuccessResult();
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.IDCard.SAMTcpNet.SearchCard" />
		[HslMqttApi]
		public OperateResult SearchCard()
		{
			OperateResult<byte[]> operateResult = ReadFromCoreServer(PackToSAMCommand(BuildReadCommand(32, 1, null)));
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<string>(operateResult);
			}
			OperateResult operateResult2 = CheckADSCommandAndSum(operateResult.Content);
			if (!operateResult2.IsSuccess)
			{
				return OperateResult.CreateFailedResult<string>(operateResult2);
			}
			if (operateResult.Content[9] != 159)
			{
				return new OperateResult(GetErrorDescription(operateResult.Content[9]));
			}
			return OperateResult.CreateSuccessResult();
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.IDCard.SAMTcpNet.SelectCard" />
		[HslMqttApi]
		public OperateResult SelectCard()
		{
			OperateResult<byte[]> operateResult = ReadFromCoreServer(PackToSAMCommand(BuildReadCommand(32, 2, null)));
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<string>(operateResult);
			}
			OperateResult operateResult2 = CheckADSCommandAndSum(operateResult.Content);
			if (!operateResult2.IsSuccess)
			{
				return OperateResult.CreateFailedResult<string>(operateResult2);
			}
			if (operateResult.Content[9] != 144)
			{
				return new OperateResult(GetErrorDescription(operateResult.Content[9]));
			}
			return OperateResult.CreateSuccessResult();
		}

		/// <inheritdoc cref="M:HslCommunication.Profinet.IDCard.SAMTcpNet.ReadCard" />
		[HslMqttApi]
		public OperateResult<IdentityCard> ReadCard()
		{
			OperateResult<byte[]> operateResult = ReadFromCoreServer(PackToSAMCommand(BuildReadCommand(48, 1, null)));
			if (!operateResult.IsSuccess)
			{
				return OperateResult.CreateFailedResult<IdentityCard>(operateResult);
			}
			OperateResult operateResult2 = CheckADSCommandAndSum(operateResult.Content);
			if (!operateResult2.IsSuccess)
			{
				return OperateResult.CreateFailedResult<IdentityCard>(operateResult2);
			}
			return ExtractIdentityCard(operateResult.Content);
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return $"SAMSerial[{base.PortName}:{base.BaudRate}]";
		}

		/// <summary>
		/// 将指令进行打包成可以发送的数据对象
		/// </summary>
		/// <param name="command">命令信息</param>
		/// <returns>字节数组</returns>
		public static byte[] PackToSAMCommand(byte[] command)
		{
			byte[] array = new byte[command.Length + 8];
			array[0] = 170;
			array[1] = 170;
			array[2] = 170;
			array[3] = 150;
			array[4] = 105;
			array[5] = BitConverter.GetBytes(array.Length - 7)[1];
			array[6] = BitConverter.GetBytes(array.Length - 7)[0];
			command.CopyTo(array, 7);
			int num = 0;
			for (int i = 5; i < array.Length - 1; i++)
			{
				num ^= array[i];
			}
			array[array.Length - 1] = (byte)num;
			return array;
		}

		/// <summary>
		/// 根据SAM的实际的指令，来生成实际的指令信息
		/// </summary>
		/// <param name="cmd">命令码</param>
		/// <param name="para">参数信息</param>
		/// <param name="data">数据内容</param>
		/// <returns>字符串的结果信息</returns>
		public static byte[] BuildReadCommand(byte cmd, byte para, byte[] data)
		{
			if (data == null)
			{
				data = new byte[0];
			}
			byte[] array = new byte[2 + data.Length];
			array[0] = cmd;
			array[1] = para;
			data.CopyTo(array, 2);
			return array;
		}

		/// <summary>
		/// 检查当前的接收数据信息是否一条完整的数据信息
		/// </summary>
		/// <param name="input">输入的信息</param>
		/// <returns>是否接收完成</returns>
		public static bool CheckADSCommandCompletion(List<byte> input)
		{
			if (input != null && input.Count < 8)
			{
				return false;
			}
			if (input[5] * 256 + input[6] > input.Count - 7)
			{
				return false;
			}
			return true;
		}

		/// <summary>
		/// 检查当前的指令是否是正确的
		/// </summary>
		/// <param name="input">输入的指令信息</param>
		/// <returns>是否校验成功</returns>
		public static OperateResult CheckADSCommandAndSum(byte[] input)
		{
			if (input != null && input.Length < 8)
			{
				return new OperateResult(StringResources.Language.SAMReceiveLengthMustLargerThan8);
			}
			if (input[0] != 170 || input[1] != 170 || input[2] != 170 || input[3] != 150 || input[4] != 105)
			{
				return new OperateResult(StringResources.Language.SAMHeadCheckFailed);
			}
			if (input[5] * 256 + input[6] != input.Length - 7)
			{
				return new OperateResult(StringResources.Language.SAMLengthCheckFailed);
			}
			int num = 0;
			for (int i = 5; i < input.Length - 1; i++)
			{
				num ^= input[i];
			}
			if (num != input[input.Length - 1])
			{
				return new OperateResult(StringResources.Language.SAMSumCheckFailed);
			}
			return OperateResult.CreateSuccessResult();
		}

		/// <summary>
		/// 提炼安全的模块数据信息
		/// </summary>
		/// <param name="data">数据</param>
		/// <returns>结果对象</returns>
		public static OperateResult<string> ExtractSafeModuleNumber(byte[] data)
		{
			try
			{
				if (data[9] != 144)
				{
					return new OperateResult<string>(GetErrorDescription(data[9]));
				}
				StringBuilder stringBuilder = new StringBuilder();
				stringBuilder.Append(data[10].ToString("D2"));
				stringBuilder.Append(".");
				stringBuilder.Append(data[12].ToString("D2"));
				stringBuilder.Append("-");
				stringBuilder.Append(BitConverter.ToInt32(data, 14).ToString());
				stringBuilder.Append("-");
				stringBuilder.Append(BitConverter.ToInt32(data, 18).ToString("D9"));
				stringBuilder.Append("-");
				stringBuilder.Append(BitConverter.ToInt32(data, 22).ToString("D9"));
				return OperateResult.CreateSuccessResult(stringBuilder.ToString());
			}
			catch (Exception ex)
			{
				return new OperateResult<string>("Error:" + ex.Message + "  Source Data: " + SoftBasic.ByteToHexString(data));
			}
		}

		/// <summary>
		/// 从数据中提取出真实的身份证信息
		/// </summary>
		/// <param name="data">原始数据内容</param>
		/// <returns>包含结果对象的身份证数据</returns>
		public static OperateResult<IdentityCard> ExtractIdentityCard(byte[] data)
		{
			try
			{
				if (data[9] != 144)
				{
					return new OperateResult<IdentityCard>(GetErrorDescription(data[9]));
				}
				string @string = Encoding.Unicode.GetString(data, 14, 256);
				byte[] portrait = SoftBasic.ArraySelectMiddle(data, 270, 1024);
				IdentityCard identityCard = new IdentityCard();
				identityCard.Name = @string.Substring(0, 15);
				identityCard.Sex = ((@string.Substring(15, 1) == "1") ? "男" : ((@string.Substring(15, 1) == "2") ? "女" : "未知"));
				identityCard.Nation = GetNationText(Convert.ToInt32(@string.Substring(16, 2)));
				identityCard.Birthday = new DateTime(int.Parse(@string.Substring(18, 4)), int.Parse(@string.Substring(22, 2)), int.Parse(@string.Substring(24, 2)));
				identityCard.Address = @string.Substring(26, 35);
				identityCard.Id = @string.Substring(61, 18);
				identityCard.Organ = @string.Substring(79, 15);
				identityCard.ValidityStartDate = new DateTime(int.Parse(@string.Substring(94, 4)), int.Parse(@string.Substring(98, 2)), int.Parse(@string.Substring(100, 2)));
				identityCard.ValidityEndDate = new DateTime(int.Parse(@string.Substring(102, 4)), int.Parse(@string.Substring(106, 2)), int.Parse(@string.Substring(108, 2)));
				identityCard.Portrait = portrait;
				return OperateResult.CreateSuccessResult(identityCard);
			}
			catch (Exception ex)
			{
				return new OperateResult<IdentityCard>(ex.Message);
			}
		}

		/// <summary>
		/// 根据民族的代号来获取到民族的文本描述信息
		/// </summary>
		/// <param name="nation">民族代码</param>
		/// <returns>民族的文本信息</returns>
		public static string GetNationText(int nation)
		{
			switch (nation)
			{
			case 1:
				return "汉";
			case 2:
				return "蒙古";
			case 3:
				return "回";
			case 4:
				return "藏";
			case 5:
				return "维吾尔";
			case 6:
				return "苗";
			case 7:
				return "彝";
			case 8:
				return "壮";
			case 9:
				return "布依";
			case 10:
				return "朝鲜";
			case 11:
				return "满";
			case 12:
				return "侗";
			case 13:
				return "瑶";
			case 14:
				return "白";
			case 15:
				return "土家";
			case 16:
				return "哈尼";
			case 17:
				return "哈萨克";
			case 18:
				return "傣";
			case 19:
				return "黎";
			case 20:
				return "傈僳";
			case 21:
				return "佤";
			case 22:
				return "畲";
			case 23:
				return "高山";
			case 24:
				return "拉祜";
			case 25:
				return "水";
			case 26:
				return "东乡";
			case 27:
				return "纳西";
			case 28:
				return "景颇";
			case 29:
				return "柯尔克孜";
			case 30:
				return "土";
			case 31:
				return "达斡尔";
			case 32:
				return "仫佬";
			case 33:
				return "羌";
			case 34:
				return "布朗";
			case 35:
				return "撒拉";
			case 36:
				return "毛南";
			case 37:
				return "仡佬";
			case 38:
				return "锡伯";
			case 39:
				return "阿昌";
			case 40:
				return "普米";
			case 41:
				return "塔吉克";
			case 42:
				return "怒";
			case 43:
				return "乌孜别克";
			case 44:
				return "俄罗斯";
			case 45:
				return "鄂温克";
			case 46:
				return "德昂";
			case 47:
				return "保安";
			case 48:
				return "裕固";
			case 49:
				return "京";
			case 50:
				return "塔塔尔";
			case 51:
				return "独龙";
			case 52:
				return "鄂伦春";
			case 53:
				return "赫哲";
			case 54:
				return "门巴";
			case 55:
				return "珞巴";
			case 56:
				return "基诺";
			case 97:
				return "其他";
			case 98:
				return "外国血统中国籍人士";
			default:
				return string.Empty;
			}
		}

		/// <summary>
		/// 枚举当前的所有的民族信息，共计五十六个民族
		/// </summary>
		/// <returns>枚举信息</returns>
		[IteratorStateMachine(typeof(_003CGetNationEnumerator_003Ed__15))]
		public static IEnumerator<string> GetNationEnumerator()
		{
			//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
			return new _003CGetNationEnumerator_003Ed__15(0);
		}

		/// <summary>
		/// 获取错误的文本信息
		/// </summary>
		/// <param name="err">错误号</param>
		/// <returns>错误信息</returns>
		public static string GetErrorDescription(int err)
		{
			switch (err)
			{
			case 145:
				return StringResources.Language.SAMStatus91;
			case 16:
				return StringResources.Language.SAMStatus10;
			case 17:
				return StringResources.Language.SAMStatus11;
			case 33:
				return StringResources.Language.SAMStatus21;
			case 35:
				return StringResources.Language.SAMStatus23;
			case 36:
				return StringResources.Language.SAMStatus24;
			case 49:
				return StringResources.Language.SAMStatus31;
			case 50:
				return StringResources.Language.SAMStatus32;
			case 51:
				return StringResources.Language.SAMStatus33;
			case 64:
				return StringResources.Language.SAMStatus40;
			case 65:
				return StringResources.Language.SAMStatus41;
			case 71:
				return StringResources.Language.SAMStatus47;
			case 96:
				return StringResources.Language.SAMStatus60;
			case 102:
				return StringResources.Language.SAMStatus66;
			case 128:
				return StringResources.Language.SAMStatus80;
			case 129:
				return StringResources.Language.SAMStatus81;
			default:
				return StringResources.Language.UnknownError;
			}
		}
	}
}
