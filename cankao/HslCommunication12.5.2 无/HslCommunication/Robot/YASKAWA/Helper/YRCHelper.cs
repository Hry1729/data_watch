using System;
using System.Text.RegularExpressions;

namespace HslCommunication.Robot.YASKAWA.Helper
{
	/// <summary>
	/// 安川机器人的静态辅助方法
	/// </summary>
	public class YRCHelper
	{
		/// <summary>
		/// 根据错误信息获取安川机器人的错误信息文本<br />
		/// Obtain the error message text of the Yaskawa robot according to the error message
		/// </summary>
		/// <param name="err">错误号</param>
		/// <returns>错误文本信息</returns>
		public static string GetErrorMessage(int err)
		{
			switch (err)
			{
			case 1010:
				return StringResources.Language.YRC1010;
			case 1011:
				return StringResources.Language.YRC1011;
			case 1012:
				return StringResources.Language.YRC1012;
			case 1013:
				return StringResources.Language.YRC1013;
			case 1020:
				return StringResources.Language.YRC1020;
			case 2010:
				return StringResources.Language.YRC2010;
			case 2020:
				return StringResources.Language.YRC2020;
			case 2030:
				return StringResources.Language.YRC2030;
			case 2040:
				return StringResources.Language.YRC2040;
			case 2050:
				return StringResources.Language.YRC2050;
			case 2060:
				return StringResources.Language.YRC2060;
			case 2070:
				return StringResources.Language.YRC2070;
			case 2080:
				return StringResources.Language.YRC2080;
			case 2090:
				return StringResources.Language.YRC2090;
			case 2100:
				return StringResources.Language.YRC2100;
			case 2110:
				return StringResources.Language.YRC2110;
			case 2120:
				return StringResources.Language.YRC2120;
			case 2130:
				return StringResources.Language.YRC2130;
			case 2150:
				return StringResources.Language.YRC2150;
			case 3010:
				return StringResources.Language.YRC3010;
			case 3040:
				return StringResources.Language.YRC3040;
			case 3050:
				return StringResources.Language.YRC3050;
			case 3070:
				return StringResources.Language.YRC3070;
			case 3220:
				return StringResources.Language.YRC3220;
			case 3230:
				return StringResources.Language.YRC3230;
			case 3350:
				return StringResources.Language.YRC3350;
			case 3360:
				return StringResources.Language.YRC3360;
			case 3370:
				return StringResources.Language.YRC3370;
			case 3380:
				return StringResources.Language.YRC3380;
			case 3390:
				return StringResources.Language.YRC3390;
			case 3400:
				return StringResources.Language.YRC3400;
			case 3410:
				return StringResources.Language.YRC3410;
			case 3420:
				return StringResources.Language.YRC3420;
			case 3430:
				return StringResources.Language.YRC3430;
			case 3450:
				return StringResources.Language.YRC3450;
			case 3460:
				return StringResources.Language.YRC3460;
			case 4010:
				return StringResources.Language.YRC4010;
			case 4012:
				return StringResources.Language.YRC4012;
			case 4020:
				return StringResources.Language.YRC4020;
			case 4030:
				return StringResources.Language.YRC4030;
			case 4040:
				return StringResources.Language.YRC4040;
			case 4060:
				return StringResources.Language.YRC4060;
			case 4120:
				return StringResources.Language.YRC4120;
			case 4130:
				return StringResources.Language.YRC4130;
			case 4140:
				return StringResources.Language.YRC4140;
			case 4150:
				return StringResources.Language.YRC4150;
			case 4170:
				return StringResources.Language.YRC4170;
			case 4190:
				return StringResources.Language.YRC4190;
			case 4200:
				return StringResources.Language.YRC4200;
			case 4230:
				return StringResources.Language.YRC4230;
			case 4420:
				return StringResources.Language.YRC4420;
			case 4430:
				return StringResources.Language.YRC4430;
			case 4480:
				return StringResources.Language.YRC4480;
			case 4490:
				return StringResources.Language.YRC4490;
			case 5110:
				return StringResources.Language.YRC5110;
			case 5120:
				return StringResources.Language.YRC5120;
			case 5130:
				return StringResources.Language.YRC5130;
			case 5170:
				return StringResources.Language.YRC5170;
			case 5180:
				return StringResources.Language.YRC5180;
			case 5200:
				return StringResources.Language.YRC5200;
			case 5310:
				return StringResources.Language.YRC5310;
			case 5340:
				return StringResources.Language.YRC5340;
			case 5370:
				return StringResources.Language.YRC5370;
			case 5390:
				return StringResources.Language.YRC5390;
			case 5430:
				return StringResources.Language.YRC5430;
			case 5480:
				return StringResources.Language.YRC5480;
			default:
				return StringResources.Language.UnknownError;
			}
		}

		/// <summary>
		/// 当机器人返回ERROR的错误指令后，检测消息里面是否有相关的错误码数据，如果存在，就解析出错误对应的文本<br />
		/// When the robot returns the error instruction of ERROR, it checks whether there is related error code data in the message, 
		/// and if it exists, it parses out the text corresponding to the error
		/// </summary>
		/// <param name="errText">返回的完整的报文</param>
		/// <returns>带有错误文本的数据信息</returns>
		public static OperateResult<string> ExtraErrorMessage(string errText)
		{
			Match match = Regex.Match(errText, "\\([0-9]+\\)\\.$");
			if (match.Success)
			{
				if (int.TryParse(match.Value.Substring(1, match.Value.Length - 3), out var result))
				{
					return new OperateResult<string>(errText + Environment.NewLine + GetErrorMessage(result));
				}
				return new OperateResult<string>(errText);
			}
			return new OperateResult<string>(errText);
		}
	}
}
