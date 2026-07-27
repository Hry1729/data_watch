using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using HslCommunication.BasicFramework;
using HslCommunication.Core;
using HslCommunication.Core.Net;
using HslCommunication.LogNet;

namespace HslCommunication.Enthernet
{
	/// <summary>
	/// 用于服务器支持软件全自动更新升级的类<br />
	/// Class for server support software full automatic update and upgrade
	/// </summary>
	/// <remarks>
	/// 目前的更新机制是全部文件的更新，没有进行差异化的比较
	/// </remarks>
	public sealed class NetSoftUpdateServer : NetworkServerBase
	{
		private string m_FilePath = "C:\\HslCommunication";

		private string updateExeFileName;

		private List<AppSession> sessions = new List<AppSession>();

		private object lockSessions = new object();

		private object lockMd5 = new object();

		private Dictionary<string, FileInfoExtension> fileMd5 = new Dictionary<string, FileInfoExtension>();

		private long downloadSize = 0L;

		/// <summary>
		/// 系统升级时客户端所在的目录，默认为C:\HslCommunication
		/// </summary>
		public string FileUpdatePath
		{
			get
			{
				return m_FilePath;
			}
			set
			{
				m_FilePath = value;
			}
		}

		/// <summary>
		/// 获取当前在线的客户端数量信息，一般是正在下载中的会话客户端数量。<br />
		/// Get information about the number of currently online clients, generally the number of session clients that are being downloaded.
		/// </summary>
		public int OnlineSessions => sessions.Count;

		/// <summary>
		/// 实例化一个默认的对象<br />
		/// Instantiate a default object
		/// </summary>
		/// <param name="updateExeFileName">更新程序的名称</param>
		public NetSoftUpdateServer(string updateExeFileName = "软件自动更新.exe")
		{
			this.updateExeFileName = updateExeFileName;
		}

		/// <summary>
		/// 获取当前处理的所有的字节信息，并且重置当前的字节数量<br />
		/// Get all byte information currently processed and reset the current number of bytes
		/// </summary>
		/// <remarks>
		/// 如果每秒调用一次本方法，就可以显示每秒的下载网速总和，如果每分钟调用一次本方法，就可以显示每分钟的下载网络总和<br />
		/// If this method is called once per second, the total download speed per second can be displayed. If this method is called once per minute, the total download network speed per minute can be displayed.
		/// </remarks>
		/// <returns>当前处理的字节数量</returns>
		public long GetDealSizeAndReset()
		{
			return Interlocked.Exchange(ref downloadSize, 0L);
		}

		private void RemoveAndCloseSession(AppSession session)
		{
			lock (lockSessions)
			{
				if (sessions.Remove(session))
				{
					session.WorkSocket?.Close();
				}
			}
		}

		private string RemoveHeadPathChar(string path)
		{
			if (path.StartsWith("\\"))
			{
				return path.Substring(1);
			}
			return path;
		}

		/// <inheritdoc />
		protected override async void ThreadPoolLogin(Socket socket, IPEndPoint endPoint)
		{
			string fileUpdatePath = FileUpdatePath;
			OperateResult<byte[]> receive = await ReceiveAsync(socket, 4, 10000);
			if (!receive.IsSuccess)
			{
				base.LogNet?.WriteError(ToString(), "Receive Failed: " + receive.Message);
				return;
			}
			int protocol = BitConverter.ToInt32(receive.Content, 0);
			if (!Directory.Exists(fileUpdatePath) || (protocol != 4097 && protocol != 4098 && protocol != 8193))
			{
				await SendAsync(socket, BitConverter.GetBytes(10000f));
				socket?.Close();
				return;
			}
			if (protocol == 8193)
			{
				List<string> files = GetAllFiles(fileUpdatePath, base.LogNet);
				AppSession session = new AppSession(socket);
				lock (lockSessions)
				{
					sessions.Add(session);
				}
				await SendAsync(socket, BitConverter.GetBytes(files.Count));
				foreach (string fileName in files)
				{
					FileInfo finfo = new FileInfo(fileName);
					string fileShortName = RemoveHeadPathChar(finfo.FullName.Replace(fileUpdatePath, ""));
					byte[] buffer = TranslateSourceData(new string[3]
					{
						fileShortName,
						finfo.Length.ToString(),
						GetMD5(finfo)
					});
					Send(socket, BitConverter.GetBytes(buffer.Length));
					Send(socket, buffer);
					OperateResult<byte[]> receiveCheck = await ReceiveAsync(socket, 4, 10000);
					if (!receiveCheck.IsSuccess)
					{
						RemoveAndCloseSession(session);
						return;
					}
					if (BitConverter.ToInt32(receiveCheck.Content, 0) == 1)
					{
						continue;
					}
					long alreadyReceived = 0L;
					using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
					{
						buffer = new byte[40960];
						long sended = 0L;
						while (sended < fs.Length)
						{
							int count = await fs.ReadAsync(buffer, 0, buffer.Length);
							if (!(await SendAsync(socket, buffer, 0, count)).IsSuccess)
							{
								RemoveAndCloseSession(session);
								return;
							}
							sended += count;
							Interlocked.Add(ref downloadSize, count);
							long atLeastReceive = sended - 10485760;
							while (alreadyReceived < atLeastReceive)
							{
								receiveCheck = await ReceiveAsync(socket, 4);
								if (!receiveCheck.IsSuccess)
								{
									RemoveAndCloseSession(session);
									return;
								}
								alreadyReceived = BitConverter.ToInt32(receiveCheck.Content, 0);
							}
						}
					}
					while (alreadyReceived < finfo.Length)
					{
						receiveCheck = await ReceiveAsync(socket, 4);
						if (!receiveCheck.IsSuccess)
						{
							RemoveAndCloseSession(session);
							return;
						}
						alreadyReceived = BitConverter.ToInt32(receiveCheck.Content, 0);
					}
				}
				RemoveAndCloseSession(session);
				return;
			}
			AppSession session2 = new AppSession(socket)
			{
				Tag = HslTimeOut.HandleTimeOutCheck(socket, 1800000)
			};
			lock (lockSessions)
			{
				sessions.Add(session2);
			}
			try
			{
				if (protocol == 4097)
				{
					base.LogNet?.WriteInfo(ToString(), StringResources.Language.SystemInstallOperater + ((IPEndPoint)socket.RemoteEndPoint).Address.ToString());
				}
				else
				{
					base.LogNet?.WriteInfo(ToString(), StringResources.Language.SystemUpdateOperater + ((IPEndPoint)socket.RemoteEndPoint).Address.ToString());
				}
				List<string> Files = GetAllFiles(fileUpdatePath, base.LogNet);
				for (int i = Files.Count - 1; i >= 0; i--)
				{
					FileInfo finfo2 = new FileInfo(Files[i]);
					if (finfo2.Length > 200000000)
					{
						Files.RemoveAt(i);
					}
					if (protocol == 4098 && finfo2.Name == updateExeFileName)
					{
						Files.RemoveAt(i);
					}
				}
				string[] files2 = Files.ToArray();
				socket.BeginReceive(new byte[4], 0, 4, SocketFlags.None, ReceiveCallBack, session2);
				await SendAsync(socket, BitConverter.GetBytes(files2.Length));
				for (int j = 0; j < files2.Length; j++)
				{
					FileInfo finfo3 = new FileInfo(files2[j]);
					if (!(await SendAsync(socket, GetFirstSendFileHead(RemoveHeadPathChar(finfo3.FullName.Replace(fileUpdatePath, "")), (int)finfo3.Length))).IsSuccess)
					{
						RemoveAndCloseSession(session2);
						return;
					}
					HslHelper.ThreadSleep(10);
					using (FileStream fs2 = new FileStream(files2[j], FileMode.Open, FileAccess.Read))
					{
						byte[] buffer2 = new byte[40960];
						int sended2 = 0;
						while (sended2 < fs2.Length)
						{
							int count2 = await fs2.ReadAsync(buffer2, 0, buffer2.Length);
							if (!(await SendAsync(socket, buffer2, 0, count2)).IsSuccess)
							{
								RemoveAndCloseSession(session2);
								return;
							}
							sended2 += count2;
							Interlocked.Add(ref downloadSize, count2);
						}
					}
					HslHelper.ThreadSleep(20);
				}
			}
			catch (Exception ex)
			{
				if (session2.Tag is HslTimeOut hslTimeOut)
				{
					hslTimeOut.IsSuccessful = true;
				}
				RemoveAndCloseSession(session2);
				base.LogNet?.WriteException(ToString(), StringResources.Language.FileSendClientFailed, ex);
			}
		}

		private void ReceiveCallBack(IAsyncResult ir)
		{
			if (!(ir.AsyncState is AppSession appSession))
			{
				return;
			}
			try
			{
				appSession.WorkSocket.EndReceive(ir);
			}
			catch (Exception ex)
			{
				base.LogNet?.WriteException(ToString(), ex);
			}
			finally
			{
				if (appSession.Tag is HslTimeOut hslTimeOut)
				{
					hslTimeOut.IsSuccessful = true;
				}
				RemoveAndCloseSession(appSession);
			}
		}

		private byte[] GetFirstSendFileHead(string relativeFileName, int fileLength)
		{
			byte[] bytes = Encoding.Unicode.GetBytes(relativeFileName);
			byte[] array = new byte[8 + bytes.Length];
			Array.Copy(BitConverter.GetBytes(array.Length), 0, array, 0, 4);
			Array.Copy(BitConverter.GetBytes(fileLength), 0, array, 4, 4);
			Array.Copy(bytes, 0, array, 8, bytes.Length);
			return array;
		}

		private byte[] TranslateSourceData(string[] parameters)
		{
			if (parameters == null)
			{
				return new byte[0];
			}
			MemoryStream memoryStream = new MemoryStream();
			foreach (string text in parameters)
			{
				byte[] array = (string.IsNullOrEmpty(text) ? new byte[0] : Encoding.UTF8.GetBytes(text));
				memoryStream.Write(BitConverter.GetBytes(array.Length), 0, 4);
				if (array.Length != 0)
				{
					memoryStream.Write(array, 0, array.Length);
				}
			}
			return memoryStream.ToArray();
		}

		private string[] TranslateFromSourceData(byte[] source)
		{
			if (source == null)
			{
				return new string[0];
			}
			List<string> list = new List<string>();
			int num = 0;
			while (num < source.Length)
			{
				try
				{
					int num2 = BitConverter.ToInt32(source, num);
					num += 4;
					string item = ((num2 > 0) ? Encoding.UTF8.GetString(source, num, num2) : string.Empty);
					num += num2;
					list.Add(item);
				}
				catch
				{
					return list.ToArray();
				}
			}
			return list.ToArray();
		}

		private string GetMD5(FileInfo fileInfo)
		{
			lock (lockMd5)
			{
				if (fileMd5.ContainsKey(fileInfo.FullName))
				{
					if (fileInfo.LastWriteTime == fileMd5[fileInfo.FullName].ModifiTime)
					{
						return fileMd5[fileInfo.FullName].MD5;
					}
					fileMd5[fileInfo.FullName].MD5 = SoftBasic.CalculateFileMD5(fileInfo.FullName);
					return fileMd5[fileInfo.FullName].MD5;
				}
				FileInfoExtension fileInfoExtension = new FileInfoExtension();
				fileInfoExtension.FullName = fileInfo.FullName;
				fileInfoExtension.ModifiTime = fileInfo.LastWriteTime;
				fileInfoExtension.MD5 = SoftBasic.CalculateFileMD5(fileInfo.FullName);
				fileMd5.Add(fileInfoExtension.FullName, fileInfoExtension);
				return fileInfoExtension.MD5;
			}
		}

		/// <summary>
		/// 获取所有的文件信息，包括所有的子目录的文件信息<br />
		/// Get all file information, including file information of all subdirectories
		/// </summary>
		/// <param name="dircPath">目标路径</param>
		/// <param name="logNet">日志信息</param>
		/// <returns>文件名的列表</returns>
		public static List<string> GetAllFiles(string dircPath, ILogNet logNet)
		{
			List<string> list = new List<string>();
			try
			{
				list.AddRange(Directory.GetFiles(dircPath));
			}
			catch (Exception ex)
			{
				logNet?.WriteWarn("GetAllFiles", ex.Message);
			}
			string[] directories = Directory.GetDirectories(dircPath);
			for (int i = 0; i < directories.Length; i++)
			{
				list.AddRange(GetAllFiles(directories[i], logNet));
			}
			return list;
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return $"NetSoftUpdateServer[{base.Port}]";
		}
	}
}
