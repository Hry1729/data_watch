using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using HslCommunication.Core;
using HslCommunication.Core.Net;
using HslCommunication.Reflection;
using Newtonsoft.Json.Linq;

namespace HslCommunication.Enthernet
{
	/// <summary>
	/// 一个终极文件管理服务器，可以实现对所有的文件分类管理，本服务器支持读写分离，支持同名文件，
	/// 客户端使用<see cref="T:HslCommunication.Enthernet.IntegrationFileClient" />进行访问，支持上传，下载，删除，请求文件列表，校验文件是否存在操作。<br />
	/// An ultimate file management server, which can realize classified management of all files. This server supports read-write separation, 
	/// supports files with the same name, and the client uses <see cref="T:HslCommunication.Enthernet.IntegrationFileClient" /> to access, 
	/// supports upload, download, delete, and request files List, check whether the file exists operation.
	/// </summary>
	/// <remarks>
	/// 本文件的服务器支持存储文件携带上传人的信息，备注信息，文件名被映射成了新的名称，无法在服务器直接查看文件信息。
	/// </remarks>
	/// <example>
	/// 以下的示例来自Demo项目，创建了一个简单的服务器对象。
	/// <code lang="cs" source="TestProject\FileNetServer\FormFileServer.cs" region="Ultimate Server" title="UltimateFileServer示例" />
	/// </example>
	public class UltimateFileServer : NetworkFileServerBase
	{
		/// <summary>
		/// 所有文件组操作的词典锁
		/// </summary>
		internal Dictionary<string, GroupFileContainer> m_dictionary_group_marks = new Dictionary<string, GroupFileContainer>();

		/// <summary>
		/// 词典的锁
		/// </summary>
		private SimpleHybirdLock hybirdLock = new SimpleHybirdLock();

		/// <summary>
		/// 实例化一个默认的对象<br />
		/// Instantiate a default object
		/// </summary>
		public UltimateFileServer()
		{
		}

		/// <summary>
		/// 获取当前的针对文件夹的文件管理容器的数量<br />
		/// Get the current number of file management containers for the folder
		/// </summary>
		[HslMqttApi(Description = "Get the current number of file management containers for the folder")]
		public int GroupFileContainerCount()
		{
			return m_dictionary_group_marks.Count;
		}

		/// <summary>
		/// 获取当前目录的文件列表管理容器，如果没有会自动创建，通过该容器可以实现对当前目录的文件进行访问<br />
		/// Get the file list management container of the current directory. If not, it will be created automatically. 
		/// Through this container, you can access files in the current directory.
		/// </summary>
		/// <param name="filePath">路径信息</param>
		/// <returns>文件管理容器信息</returns>
		public GroupFileContainer GetGroupFromFilePath(string filePath)
		{
			GroupFileContainer groupFileContainer = null;
			filePath = filePath.ToUpper();
			hybirdLock.Enter();
			if (m_dictionary_group_marks.ContainsKey(filePath))
			{
				groupFileContainer = m_dictionary_group_marks[filePath];
			}
			else
			{
				groupFileContainer = new GroupFileContainer(base.LogNet, filePath);
				m_dictionary_group_marks.Add(filePath, groupFileContainer);
			}
			hybirdLock.Leave();
			return groupFileContainer;
		}

		/// <summary>
		/// 清除系统中所有空的路径信息
		/// </summary>
		public void DeleteGroupFile(GroupFileContainer groupFile)
		{
			hybirdLock.Enter();
			if (m_dictionary_group_marks.ContainsKey(groupFile.DirectoryPath))
			{
				m_dictionary_group_marks.Remove(groupFile.DirectoryPath);
			}
			try
			{
				Directory.Delete(groupFile.DirectoryPath, true);
			}
			catch
			{
			}
			hybirdLock.Leave();
		}

		/// <summary>
		/// 从套接字接收文件并保存，更新文件列表
		/// </summary>
		/// <param name="socket">套接字</param>
		/// <param name="savename">保存的文件名</param>
		/// <returns>是否成功的结果对象</returns>
		private OperateResult<FileBaseInfo> ReceiveFileFromSocketAndUpdateGroup(Socket socket, string savename)
		{
			FileInfo fileInfo = new FileInfo(savename);
			string text = CreateRandomFileName();
			string text2 = Path.Combine(fileInfo.DirectoryName, text);
			OperateResult<FileBaseInfo> operateResult = ReceiveFileFromSocket(socket, text2, null);
			if (!operateResult.IsSuccess)
			{
				DeleteFileByName(text2);
				return operateResult;
			}
			DeleteExsistingFile(fileName: GetGroupFromFilePath(fileInfo.DirectoryName).UpdateFileMappingName(fileInfo.Name, operateResult.Content.Size, text, operateResult.Content.Upload, operateResult.Content.Tag), path: fileInfo.DirectoryName);
			OperateResult operateResult2 = SendStringAndCheckReceive(socket, 1, StringResources.Language.SuccessText);
			if (!operateResult2.IsSuccess)
			{
				return OperateResult.CreateFailedResult<FileBaseInfo>(operateResult2);
			}
			return OperateResult.CreateSuccessResult(operateResult.Content);
		}

		/// <summary>
		/// 从套接字接收文件并保存，更新文件列表
		/// </summary>
		/// <param name="socket">套接字</param>
		/// <param name="savename">保存的文件名</param>
		/// <returns>是否成功的结果对象</returns>
		private async Task<OperateResult<FileBaseInfo>> ReceiveFileFromSocketAndUpdateGroupAsync(Socket socket, string savename)
		{
			FileInfo info = new FileInfo(savename);
			string guidName = CreateRandomFileName();
			string fileName = Path.Combine(info.DirectoryName, guidName);
			OperateResult<FileBaseInfo> receive = await ReceiveFileFromSocketAsync(socket, fileName, null);
			if (!receive.IsSuccess)
			{
				DeleteFileByName(fileName);
				return receive;
			}
			DeleteExsistingFile(fileName: GetGroupFromFilePath(info.DirectoryName).UpdateFileMappingName(info.Name, receive.Content.Size, guidName, receive.Content.Upload, receive.Content.Tag), path: info.DirectoryName);
			OperateResult sendBack = await SendStringAndCheckReceiveAsync(socket, 1, StringResources.Language.SuccessText);
			if (!sendBack.IsSuccess)
			{
				return OperateResult.CreateFailedResult<FileBaseInfo>(sendBack);
			}
			return OperateResult.CreateSuccessResult(receive.Content);
		}

		/// <summary>
		/// 根据文件的显示名称转化为真实存储的名称，例如 123.txt 获取到在文件服务器里映射的文件名称，例如返回 b35a11ec533147ca80c7f7d1713f015b7909
		/// </summary>
		/// <param name="factory">第一大类</param>
		/// <param name="group">第二大类</param>
		/// <param name="id">第三大类</param>
		/// <param name="fileName">文件显示名称</param>
		/// <returns>是否成功的结果对象</returns>
		private string TransformFactFileName(string factory, string group, string id, string fileName)
		{
			return GetGroupFromFilePath(ReturnAbsoluteFilePath(factory, group, id)).GetCurrentFileMappingName(fileName);
		}

		/// <summary>
		/// 删除已经存在的文件信息，文件的名称需要是guid名称，例如 b35a11ec533147ca80c7f7d1713f015b7909
		/// </summary>
		/// <param name="path">文件的路径</param>
		/// <param name="fileName">文件的guid名称，例如 b35a11ec533147ca80c7f7d1713f015b7909</param>
		private void DeleteExsistingFile(string path, string fileName)
		{
			DeleteExsistingFile(path, new List<string> { fileName });
		}

		/// <summary>
		/// 删除已经存在的文件信息，文件的名称需要是guid名称，例如 b35a11ec533147ca80c7f7d1713f015b7909
		/// </summary>
		/// <param name="path">文件的路径</param>
		/// <param name="fileNames">文件的guid名称，例如 b35a11ec533147ca80c7f7d1713f015b7909</param>
		private void DeleteExsistingFile(string path, List<string> fileNames)
		{
			foreach (string fileName in fileNames)
			{
				if (string.IsNullOrEmpty(fileName))
				{
					continue;
				}
				string fileUltimatePath = Path.Combine(path, fileName);
				GetFileMarksFromDictionaryWithFileName(fileName).AddOperation(delegate
				{
					if (!DeleteFileByName(fileUltimatePath))
					{
						base.LogNet?.WriteInfo(ToString(), StringResources.Language.FileDeleteFailed + fileUltimatePath);
					}
					else
					{
						base.LogNet?.WriteInfo(ToString(), StringResources.Language.FileDeleteSuccess + fileUltimatePath);
					}
				});
			}
		}

		/// <inheritdoc />
		protected override async void ThreadPoolLogin(Socket socket, IPEndPoint endPoint)
		{
			string IpAddress = endPoint.Address.ToString();
			OperateResult<FileGroupInfo> infoResult = await ReceiveInformationHeadAsync(socket);
			if (!infoResult.IsSuccess)
			{
				return;
			}
			int customer = infoResult.Content.Command;
			string Factory = infoResult.Content.Factory;
			string Group = infoResult.Content.Group;
			string Identify = infoResult.Content.Identify;
			string fileName = infoResult.Content.FileName;
			string relativeName = GetRelativeFileName(Factory, Group, Identify, fileName);
			switch (customer)
			{
			case 2001:
			{
				string guidName = TransformFactFileName(Factory, Group, Identify, fileName);
				FileMarkId fileMarkId = GetFileMarksFromDictionaryWithFileName(guidName);
				fileMarkId.EnterReadOperator();
				OperateResult send = await SendFileAndCheckReceiveAsync(socket, ReturnAbsoluteFileName(Factory, Group, Identify, guidName), fileName, "", "");
				if (!send.IsSuccess)
				{
					fileMarkId.LeaveReadOperator();
					base.LogNet?.WriteError(ToString(), StringResources.Language.FileDownloadFailed + " : " + send.Message + " :" + relativeName + " ip:" + IpAddress);
				}
				else
				{
					base.LogNet?.WriteInfo(ToString(), StringResources.Language.FileDownloadSuccess + ":" + relativeName);
					fileMarkId.LeaveReadOperator();
					socket?.Close();
				}
				break;
			}
			case 2002:
			{
				string fullFileName = ReturnAbsoluteFileName(Factory, Group, Identify, fileName);
				CheckFolderAndCreate();
				FileInfo info2 = new FileInfo(fullFileName);
				try
				{
					if (!Directory.Exists(info2.DirectoryName))
					{
						Directory.CreateDirectory(info2.DirectoryName);
					}
				}
				catch (Exception ex)
				{
					base.LogNet?.WriteException(ToString(), StringResources.Language.FilePathCreateFailed + fullFileName, ex);
					socket?.Close();
					break;
				}
				OperateResult<FileBaseInfo> receive = await ReceiveFileFromSocketAndUpdateGroupAsync(socket, fullFileName);
				if (receive.IsSuccess)
				{
					socket?.Close();
					OnFileUpload(new FileServerInfo
					{
						ActualFileFullName = fullFileName,
						Name = receive.Content.Name,
						Size = receive.Content.Size,
						Tag = receive.Content.Tag,
						Upload = receive.Content.Upload
					});
					base.LogNet?.WriteInfo(ToString(), StringResources.Language.FileUploadSuccess + ":" + relativeName);
				}
				else
				{
					base.LogNet?.WriteInfo(ToString(), StringResources.Language.FileUploadFailed + ":" + relativeName);
				}
				break;
			}
			case 2003:
			{
				FileInfo info3 = new FileInfo(ReturnAbsoluteFileName(Factory, Group, Identify, fileName));
				DeleteExsistingFile(fileName: GetGroupFromFilePath(info3.DirectoryName).DeleteFile(info3.Name), path: info3.DirectoryName);
				if ((await SendStringAndCheckReceiveAsync(socket, 1, "success")).IsSuccess)
				{
					socket?.Close();
				}
				base.LogNet?.WriteInfo(ToString(), StringResources.Language.FileDeleteSuccess + ":" + relativeName);
				break;
			}
			case 2011:
			{
				string[] fileNames = infoResult.Content.FileNames;
				for (int i = 0; i < fileNames.Length; i++)
				{
					FileInfo info = new FileInfo(ReturnAbsoluteFileName(Factory, Group, Identify, fileNames[i]));
					DeleteExsistingFile(fileName: GetGroupFromFilePath(info.DirectoryName).DeleteFile(info.Name), path: info.DirectoryName);
					relativeName = GetRelativeFileName(Factory, Group, Identify, fileName);
					base.LogNet?.WriteInfo(ToString(), StringResources.Language.FileDeleteSuccess + ":" + relativeName);
				}
				if ((await SendStringAndCheckReceiveAsync(socket, 1, "success")).IsSuccess)
				{
					socket?.Close();
				}
				break;
			}
			case 2012:
				DeleteGroupFile(GetGroupFromFilePath(new FileInfo(ReturnAbsoluteFileName(Factory, Group, Identify, "123.txt")).DirectoryName));
				if ((await SendStringAndCheckReceiveAsync(socket, 1, "success")).IsSuccess)
				{
					socket?.Close();
				}
				base.LogNet?.WriteInfo(ToString(), "FolderDelete : " + relativeName);
				break;
			case 2014:
			{
				string[] directories2 = GetDirectories(Factory, Group, Identify);
				for (int k = 0; k < directories2.Length; k++)
				{
					GroupFileContainer fileManagment = GetGroupFromFilePath(new DirectoryInfo(directories2[k]).FullName);
					if (fileManagment.FileCount == 0)
					{
						DeleteGroupFile(fileManagment);
					}
				}
				if ((await SendStringAndCheckReceiveAsync(socket, 1, "success")).IsSuccess)
				{
					socket?.Close();
				}
				base.LogNet?.WriteInfo(ToString(), "FolderEmptyDelete : " + relativeName);
				break;
			}
			case 2007:
				if ((await SendStringAndCheckReceiveAsync(socket, 2007, GetGroupFromFilePath(ReturnAbsoluteFilePath(Factory, Group, Identify)).JsonArrayContent)).IsSuccess)
				{
					socket?.Close();
				}
				break;
			case 2015:
				if ((await SendStringAndCheckReceiveAsync(socket, 2007, GetGroupFromFilePath(ReturnAbsoluteFilePath(Factory, Group, Identify)).GetGroupFileInfo().ToJsonString())).IsSuccess)
				{
					socket?.Close();
				}
				break;
			case 2016:
			{
				List<GroupFileInfo> folders = new List<GroupFileInfo>();
				string[] directories = GetDirectories(Factory, Group, Identify);
				for (int j = 0; j < directories.Length; j++)
				{
					DirectoryInfo directory = new DirectoryInfo(directories[j]);
					if (string.IsNullOrEmpty(Factory))
					{
						GroupFileInfo groupFileInfo = GetGroupFromFilePath(ReturnAbsoluteFilePath(directory.Name, string.Empty, string.Empty)).GetGroupFileInfo();
						groupFileInfo.PathName = directory.Name;
						folders.Add(groupFileInfo);
					}
					else if (string.IsNullOrEmpty(Group))
					{
						GroupFileInfo groupFileInfo2 = GetGroupFromFilePath(ReturnAbsoluteFilePath(Factory, directory.Name, string.Empty)).GetGroupFileInfo();
						groupFileInfo2.PathName = directory.Name;
						folders.Add(groupFileInfo2);
					}
					else
					{
						GroupFileInfo groupFileInfo3 = GetGroupFromFilePath(ReturnAbsoluteFilePath(Factory, Group, directory.Name)).GetGroupFileInfo();
						groupFileInfo3.PathName = directory.Name;
						folders.Add(groupFileInfo3);
					}
				}
				if ((await SendStringAndCheckReceiveAsync(socket, 2016, folders.ToJsonString())).IsSuccess)
				{
					socket?.Close();
				}
				break;
			}
			case 2008:
			{
				List<string> folders2 = new List<string>();
				string[] directories3 = GetDirectories(Factory, Group, Identify);
				for (int l = 0; l < directories3.Length; l++)
				{
					folders2.Add(new DirectoryInfo(directories3[l]).Name);
				}
				if ((await SendStringAndCheckReceiveAsync(socket, 2015, JArray.FromObject(folders2.ToArray()).ToString())).IsSuccess)
				{
					socket?.Close();
				}
				break;
			}
			case 2013:
				if ((await SendStringAndCheckReceiveAsync(socket, GetGroupFromFilePath(ReturnAbsoluteFilePath(Factory, Group, Identify)).FileExists(fileName) ? 1 : 0, StringResources.Language.FileNotExist)).IsSuccess)
				{
					socket?.Close();
				}
				break;
			default:
				socket?.Close();
				break;
			}
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return $"UltimateFileServer[{base.Port}]";
		}
	}
}
