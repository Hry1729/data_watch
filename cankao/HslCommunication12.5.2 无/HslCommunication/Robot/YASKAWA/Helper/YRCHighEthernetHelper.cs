using System;
using System.IO;
using System.Text;

namespace HslCommunication.Robot.YASKAWA.Helper
{
	/// <summary>
	/// 安川机器人的高速以太网的辅助方法
	/// </summary>
	public class YRCHighEthernetHelper
	{
		/// <summary>
		/// 构建完整的读取指令
		/// </summary>
		/// <param name="handle">处理分区，1:机器人控制 2:文件控制</param>
		/// <param name="requestID">请求ID， 客户端每次命令输出的时请增量</param>
		/// <param name="command">命令编号，相当于CIP通信的CLASS</param>
		/// <param name="dataAddress">数据队列编号，相当于CIP通信的Instance</param>
		/// <param name="dataAttribute">单元编号，相当于CIP通信协议的Attribute</param>
		/// <param name="dataHandle">处理请求，定义数据的请方法</param>
		/// <param name="dataPart">数据部分的内容</param>
		/// <returns>构建结果</returns>
		public static byte[] BuildCommand(byte handle, byte requestID, ushort command, ushort dataAddress, byte dataAttribute, byte dataHandle, byte[] dataPart)
		{
			MemoryStream memoryStream = new MemoryStream();
			memoryStream.Write(Encoding.ASCII.GetBytes("YERC"));
			memoryStream.Write(new byte[4] { 32, 0, 0, 0 });
			memoryStream.Write(new byte[4] { 3, handle, 0, requestID });
			memoryStream.Write(new byte[4]);
			memoryStream.Write(Encoding.ASCII.GetBytes("99999999"));
			memoryStream.Write(BitConverter.GetBytes(command));
			memoryStream.Write(BitConverter.GetBytes(dataAddress));
			memoryStream.Write(new byte[4] { dataAttribute, dataHandle, 0, 0 });
			if (dataPart != null)
			{
				memoryStream.Write(dataPart);
			}
			byte[] array = memoryStream.ToArray();
			array[6] = BitConverter.GetBytes(array.Length - 32)[0];
			array[7] = BitConverter.GetBytes(array.Length - 32)[1];
			return array;
		}

		/// <summary>
		/// 检查当前的机器人反馈的数据是否正确
		/// </summary>
		/// <param name="response">从机器人反馈的数据</param>
		/// <returns>是否检查正确</returns>
		public static OperateResult CheckResponseContent(byte[] response)
		{
			if (response[25] != 0)
			{
				byte b = response[25];
				int affix = 0;
				if (b == 31)
				{
					affix = ((response[26] != 1) ? BitConverter.ToUInt16(response, 28) : response[28]);
				}
				return new OperateResult(b, GetErrorText(b, affix));
			}
			return OperateResult.CreateSuccessResult();
		}

		/// <summary>
		/// 根据状态信息及附加状态信息来获取错误的文本描述信息
		/// </summary>
		/// <param name="status">状态信息</param>
		/// <param name="affix">附加状态信息</param>
		/// <returns>错误的文本描述信息</returns>
		public static string GetErrorText(byte status, int affix)
		{
			switch (status)
			{
			case 8:
				return "未定义被要求的命令。";
			case 9:
				return "检出无效的数据单元编号。";
			case 40:
				return "请求的数据排列编号不存在指定的命令。";
			case 31:
				switch (affix)
				{
				case 4112:
					return "命令异常";
				case 4113:
					return "命令操作数异常";
				case 4114:
					return "命令操作值超出范围";
				case 4115:
					return "命令操作长异常";
				case 4128:
					return "设备的文件数太多。";
				case 8208:
					return "机器人动作中";
				case 8224:
					return "示教编程器HOLD停止中";
				case 8240:
					return "再线盒HOLD停止中";
				case 8256:
					return "外部HOLD中";
				case 8272:
					return "命令HOLD中";
				case 8288:
					return "发生错误报警中";
				case 8304:
					return "伺服ON中";
				case 8320:
					return "模式不同";
				case 8336:
					return "访问其他功能的文件中";
				case 8448:
					return "没有命令模式设定";
				case 8464:
					return "此数据不能访问";
				case 8480:
					return "此数据不能读取";
				case 8496:
					return "编辑中";
				case 8528:
					return "执行坐标变换功能中";
				case 12304:
					return "请接通伺服电源";
				case 12352:
					return "请确认原点位置";
				case 12368:
					return "请进行位置确认";
				case 12400:
					return "无法生成现在值";
				case 12832:
					return "收到锁定面板模式／循环禁止信号";
				case 12848:
					return "面板锁定 收到禁止启动信号";
				case 13136:
					return "没有示教用户坐标";
				case 13152:
					return "用户坐标文件被损坏";
				case 13168:
					return "控制轴组不同";
				case 13184:
					return "基座轴数据不同";
				case 13200:
					return "不可变换相对JOB ";
				case 13312:
					return "禁止调用主程序 （参数）";
				case 13328:
					return "禁止调用主程序 （动作中灯亮）";
				case 13344:
					return "禁止调用主程序 （示教锁定）";
				case 13360:
					return "未定义机器人间的校准";
				case 13392:
					return "不能接通伺服电源";
				case 13408:
					return "不能设定坐标系";
				case 16400:
					return "内存容量不足 （程序登录内存）";
				case 16402:
					return "内存容量不足 （ 变位机数据登录内存）";
				case 16416:
					return "禁止编辑程序";
				case 16432:
					return "存在相同名称的程序";
				case 16448:
					return "没有指定的程序";
				case 16480:
					return "请设定执行的程序";
				case 16672:
					return "位置数据被损坏";
				case 16688:
					return "位置数据部存在";
				case 16704:
					return "位置变量类型不同";
				case 16720:
					return "不是主程序的程序 END命令";
				case 16752:
					return "命令数据被损坏";
				case 16784:
					return "程序名中存在不合适的字符";
				case 16896:
					return "标签名中存在不合适的字符";
				case 16944:
					return "本系统中存在不能使用的命令";
				case 17440:
					return "转换的程序没有步骤";
				case 17456:
					return "此程序已全被转换";
				case 17536:
					return "请示教用户坐标";
				case 17552:
					return "相对JOB／ 独立控制功能未被许可";
				case 20752:
					return "语法错误 （命令的语法）";
				case 20768:
					return "变位机数据异常";
				case 20784:
					return "缺少NOP或者 END命令";
				case 20848:
					return "格式错误（违背写法）";
				case 20864:
					return "数据数不恰当";
				case 20992:
					return "超出数据范围";
				case 21264:
					return "语法错误 （ 命令以外）";
				case 21312:
					return "模拟命令指定错误";
				case 21360:
					return "存在条件数据记录错误";
				case 21392:
					return "存在程序数据记录错误";
				case 21552:
					return "系统数据不一致";
				case 21632:
					return "焊接机类型不一致";
				case 24592:
					return "机器人或工装轴动作中";
				case 24608:
					return "指定设备容量不足";
				case 24624:
					return "指定设备无法访问";
				case 24640:
					return "预想外自动备份要求";
				case 24656:
					return "CMOS 大小在RAM 区域超出";
				case 24672:
					return "电源接通时，无法确保内存";
				case 24688:
					return "备份文件信息访问异常";
				case 24704:
					return "备份文件排序（删除）失败";
				case 24720:
					return "备份文件排序（重命名）失败";
				case 24832:
					return "驱动名称超出规定值";
				case 24848:
					return "设备不同";
				case 24864:
					return "系统错误";
				case 24880:
					return "不可设定自动备份";
				case 24896:
					return "自动备份中不可手动备份";
				case 40960:
					return "未定义命令";
				case 40961:
					return "数据排列编号（ Instance） 异常";
				case 40962:
					return "单元编号（ Attribute） 异常";
				case 41216:
					return "响应数据部大小( 硬件限制值) 异常";
				case 41218:
					return "响应数据部大小(软件限制值)异常";
				case 45057:
					return "未定义位置变量";
				case 45058:
					return "禁止使用数据";
				case 45059:
					return "请求数据大小异常";
				case 45060:
					return "数据范围以外";
				case 45061:
					return "未设定数据";
				case 45062:
					return "未登录指定的用途";
				case 45063:
					return "未登录指定的机种";
				case 45064:
					return "控制轴组设定异常";
				case 45065:
					return "速度设定异常";
				case 45066:
					return "未设定动作速度";
				case 45067:
					return "动作坐标系设定异常";
				case 45068:
					return "形态设定异常";
				case 45069:
					return "工具编号设定异常";
				case 45070:
					return "用户编号设定异常";
				default:
					return StringResources.Language.UnknownError;
				}
			default:
				return StringResources.Language.UnknownError;
			}
		}
	}
}
