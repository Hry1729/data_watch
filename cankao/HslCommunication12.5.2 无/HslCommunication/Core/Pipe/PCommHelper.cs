using System.Runtime.InteropServices;

namespace HslCommunication.Core.Pipe
{
	internal class PCommHelper
	{
		/// <summary>
		/// 波特率 50Bd
		/// </summary>
		public const int B50 = 0;

		/// <summary>
		/// 波特率 75Bd
		/// </summary>
		public const int B75 = 1;

		/// <summary>
		/// 波特率 110Bd
		/// </summary>
		public const int B110 = 2;

		/// <summary>
		/// 波特率 134Bd
		/// </summary>
		public const int B134 = 3;

		/// <summary>
		/// 波特率 150Bd
		/// </summary>
		public const int B150 = 4;

		/// <summary>
		/// 波特率 300Bd
		/// </summary>
		public const int B300 = 5;

		/// <summary>
		/// 波特率 600Bd
		/// </summary>
		public const int B600 = 6;

		/// <summary>
		/// 波特率 1200Bd
		/// </summary>
		public const int B1200 = 7;

		/// <summary>
		/// 波特率 1800Bd
		/// </summary>
		public const int B1800 = 8;

		/// <summary>
		/// 波特率 240Bd
		/// </summary>
		public const int B2400 = 9;

		/// <summary>
		/// 波特率 4800Bd
		/// </summary>
		public const int B4800 = 10;

		/// <summary>
		/// 波特率 7200Bd
		/// </summary>
		public const int B7200 = 11;

		/// <summary>
		/// 波特率 9600Bd
		/// </summary>
		public const int B9600 = 12;

		/// <summary>
		/// 波特率 19200Bd
		/// </summary>
		public const int B19200 = 13;

		/// <summary>
		/// 波特率 38400Bd
		/// </summary>
		public const int B38400 = 14;

		/// <summary>
		/// 波特率 57600Bd
		/// </summary>
		public const int B57600 = 15;

		/// <summary>
		/// 波特率 115200Bd
		/// </summary>
		public const int B115200 = 16;

		/// <summary>
		/// 波特率 230400Bd
		/// </summary>
		public const int B230400 = 17;

		/// <summary>
		/// 波特率 460800Bd
		/// </summary>
		public const int B460800 = 18;

		/// <summary>
		/// 波特率 921600Bd
		/// </summary>
		public const int B921600 = 19;

		/// <summary>
		/// 数据位 BIT_5
		/// </summary>
		public const int BIT_5 = 0;

		/// <summary>
		/// 数据位 BIT_6
		/// </summary>
		public const int BIT_6 = 1;

		/// <summary>
		/// 数据位 BIT_7
		/// </summary>
		public const int BIT_7 = 2;

		/// <summary>
		/// 数据位 BIT_8
		/// </summary>
		public const int BIT_8 = 3;

		/// <summary>
		/// 停止位 STOP_1
		/// </summary>
		public const int STOP_1 = 0;

		/// <summary>
		/// 停止位 STOP_2
		/// </summary>
		public const int STOP_2 = 4;

		/// <summary>
		/// 校验位 无校验 P_NONE
		/// </summary>
		public const int P_NONE = 0;

		/// <summary>
		/// 校验位 偶校验 P_EVEN
		/// </summary>
		public const int P_EVEN = 24;

		/// <summary>
		/// 校验位 奇校验 P_ODD
		/// </summary>
		public const int P_ODD = 8;

		/// <summary>
		/// 校验位 标记位 P_MRK
		/// </summary>
		public const int P_MRK = 40;

		/// <summary>
		/// 校验位 空白位 P_SPC
		/// </summary>
		public const int P_SPC = 56;

		/// <summary>
		/// 返回值 成功
		/// </summary>
		public const int SIO_OK = 0;

		/// <summary>
		/// 返回值 失败 串口号无效
		/// </summary>
		public const int SIO_BADPORT = -1;

		/// <summary>
		/// 返回值 失败 该板卡不是与MOXA兼容的智能板卡
		/// </summary>
		public const int SIO_OUTCONTROL = -2;

		/// <summary>
		/// 返回值 失败 没有要读取的数据
		/// </summary>
		public const int SIO_NODATA = -4;

		/// <summary>
		/// 返回值 失败 没有该端口或该端口被其他程序占用
		/// </summary>
		public const int SIO_OPENFAIL = -5;

		/// <summary>
		/// 返回值 失败 无法控制该端口，因为它被sio_flowctrl设置为自动H/W流量控制
		/// </summary>
		public const int SIO_RTS_BY_HW = -6;

		/// <summary>
		/// 返回值 失败 参数错误
		/// </summary>
		public const int SIO_BADPARM = -7;

		/// <summary>
		/// 返回值 失败 调用Win32函数失败，可尝试调用GetLastError获取错误码
		/// </summary>
		public const int SIO_WIN32FAIL = -8;

		/// <summary>
		/// 返回值 失败 该端口不支持该函数
		/// </summary>
		public const int SIO_BOARDNOTSUPPORT = -9;

		/// <summary>
		/// 返回值 失败 用户中止阻塞的写入
		/// </summary>
		public const int SIO_ABORT_WRITE = -11;

		/// <summary>
		/// 返回值 失败 写入超时
		/// </summary>
		public const int SIO_WRITETIMEOUT = -12;

		/// <summary>
		/// 打开串口
		/// </summary>
		/// <param name="port">串口号</param>
		/// <returns>详见PComm.chm</returns>
		[DllImport("PComm.dll")]
		public static extern int sio_open(int port);

		/// <summary>
		/// 配置串口通讯参数
		/// </summary>
		/// <param name="port">串口号</param>
		/// <param name="baud">波特率，详见PComm.chm</param>
		/// <param name="mode">模式，如：数据位、停止位和校验位。详见PComm.chm</param>
		/// <returns>详见PComm.chm</returns>
		[DllImport("PComm.dll")]
		public static extern int sio_ioctl(int port, int baud, int mode);

		/// <summary>
		/// 设置硬件和/或软件流控制。
		/// </summary>
		/// <param name="port">串口号</param>
		/// <param name="mode">模式,如：硬件和/或软件流控制。详见PComm.chm</param>
		/// <returns></returns>
		[DllImport("PComm.dll")]
		public static extern int sio_flowctrl(int port, int mode);

		/// <summary>
		/// 设置DTR和RTS状态。
		/// </summary>
		/// <param name="port">串口号</param>
		/// <param name="mode">模式,如：设置DTR和RTS控制。详见PComm.chm</param>
		/// <returns></returns>
		[DllImport("PComm.dll")]
		public static extern int sio_lctrl(int port, int mode);

		/// <summary>
		/// 获取在输入缓冲区中积累的数据的长度
		/// </summary>
		/// <param name="port">串口号</param>
		/// <returns>详见PComm.chm</returns>
		[DllImport("PComm.dll")]
		public static extern int sio_iqueue(int port);

		/// <summary>
		/// 设置数据终端准备好 DTR（Data Terminal Ready）
		/// </summary>
		/// <param name="port">串口</param>
		/// <param name="mode">模式 0-DTR关闭 1-DTR启用</param>
		/// <returns>详见PComm.chm</returns>
		[DllImport("PComm.dll")]
		public static extern int sio_DTR(int port, int mode);

		/// <summary>
		/// 设置请求发送 RTS（Request To Send）
		/// </summary>
		/// <param name="port">串口</param>
		/// <param name="mode">模式 0-RTS关闭 1-RTS启用</param>
		/// <returns>详见PComm.chm</returns>
		[DllImport("PComm.dll")]
		public static extern int sio_RTS(int port, int mode);

		/// <summary>
		/// 刷新驱动程序的输入/输出缓冲区中的任何数据
		/// </summary>
		/// <param name="port">串口号</param>
		/// <param name="func">模式 0-刷新输入缓冲区 1-刷新输出缓冲区 2-刷新输入和输出缓冲区</param>
		/// <returns>详见PComm.chm</returns>
		[DllImport("PComm.dll")]
		public static extern int sio_flush(int port, int func);

		/// <summary>
		/// 关闭串口
		/// </summary>
		/// <param name="port">串口号</param>
		/// <returns>详见PComm.chm</returns>
		[DllImport("PComm.dll")]
		public static extern int sio_close(int port);

		/// <summary>
		/// 读取驱动程序的输入缓冲区数据
		/// </summary>
		/// <param name="port">串口号</param>
		/// <param name="buf">接收缓冲区指针</param>
		/// <param name="length">每次要读取的数据的长度</param>
		/// <returns>详见PComm.chm</returns>
		[DllImport("PComm.dll")]
		public static extern int sio_read(int port, ref byte buf, int length);

		/// <summary>
		/// 设置sio_read和sio_getch的超时值。
		/// </summary>
		/// <param name="port">串口号</param>
		/// <param name="TotalTimeouts">总计超时时间(ms)</param>
		/// <param name="IntervalTimeouts">超时间隔时间(ms),默认:0</param>
		/// <returns></returns>
		[DllImport("PComm.dll")]
		public static extern int sio_SetReadTimeouts(int port, int TotalTimeouts, int IntervalTimeouts);

		/// <summary>
		/// 获取COM端口的读取超时值
		/// </summary>
		/// <param name="port">串口号</param>
		/// <param name="TotalTimeouts">总计超时时间(ms)</param>
		/// <param name="IntervalTimeouts">超时间隔时间(ms)</param>
		/// <returns></returns>
		[DllImport("PComm.dll")]
		public static extern int sio_GetReadTimeouts(int port, out int TotalTimeouts, out int IntervalTimeouts);

		/// <summary>
		/// 将一个数据块放到驱动程序的输出缓冲区中
		/// </summary>
		/// <param name="port">串口号</param>
		/// <param name="buf">传输缓冲区指针</param>
		/// <param name="length">传输缓冲区长度 </param>
		/// <returns>详见PComm.chm</returns>
		[DllImport("PComm.dll")]
		public static extern int sio_write(int port, byte[] buf, int length);

		/// <summary>
		/// 将一个数据块放入Tx缓冲区。首先打开RTS，并将数据放入Tx缓冲区，然后关闭RTS。此函数与sio_putb_x（）相同，不同之处在于延迟时间单位为毫秒
		/// </summary>
		/// <param name="port">串口号</param>
		/// <param name="buf">传输缓冲区指针</param>
		/// <param name="length">传输缓冲区长度</param>
		/// <param name="timer">超时时间,ms</param>
		/// <returns></returns>
		[DllImport("PComm.dll")]
		public static extern int sio_putb_x_ex(int port, byte[] buf, int length, int timer);

		/// <summary>
		/// 将一个字符写入驱动程序的输出缓冲区
		/// </summary>
		/// <param name="port">串口号</param>
		/// <param name="term">字符(0 - 255)</param>
		/// <returns>详见PComm.chm</returns>
		[DllImport("PComm.dll")]
		public static extern int sio_putch(int port, int term);

		/// <summary>
		/// 从驱动程序的输入缓冲区中读取一个字符
		/// </summary>
		/// <param name="port">串口号</param>
		/// <returns>详见PComm.chm</returns>
		[DllImport("PComm.dll")]
		public static extern int sio_getch(int port);
	}
}
