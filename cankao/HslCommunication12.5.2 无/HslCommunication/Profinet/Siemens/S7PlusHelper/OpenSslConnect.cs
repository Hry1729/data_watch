using System;
using System.Collections.Generic;
using HslCommunication.Core.Security;
using HslCommunication.LogNet;

namespace HslCommunication.Profinet.Siemens.S7PlusHelper
{
	/// <summary>
	/// OpenSsl的连接对象，控制当前的
	/// </summary>
	public class OpenSslConnect : IDisposable
	{
		private bool skip_bio_should_retry = false;

		private LinkedList<byte[]> m_pendingReadList = new LinkedList<byte[]>();

		private LinkedList<byte[]> m_pendingWriteList = new LinkedList<byte[]>();

		private bool m_readRequired;

		private readonly IntPtr m_ssl;

		private readonly IntPtr m_pBioIn;

		private readonly IntPtr m_pBioOut;

		private IntPtr m_ptr_ssl_method;

		private IntPtr m_ptr_ctx;

		private Func<byte[], OperateResult> sendDataFunc;

		private Action<byte[]> receiveByte;

		private bool disposedValue;

		/// <summary>
		/// 获取或设置当前的日志对象
		/// </summary>
		public ILogNet LogNet { get; set; }

		/// <summary>
		/// 指定收发数据实例化一个对象
		/// </summary>
		/// <param name="sendDataFunc">发送数据的委托方法</param>
		/// <param name="receiveByte">接收数据的委托方法</param>
		public OpenSslConnect(Func<byte[], OperateResult> sendDataFunc, Action<byte[]> receiveByte)
		{
			if (OpenSslNative.OPENSSL_init_ssl(0uL, IntPtr.Zero) != 1)
			{
				throw new Exception("errOpenSSL");
			}
			m_ptr_ssl_method = OpenSslNative.ExpectNonNull(OpenSslNative.TLS_client_method());
			m_ptr_ctx = OpenSslNative.ExpectNonNull(OpenSslNative.SSL_CTX_new(m_ptr_ssl_method));
			OpenSslNative.SSL_CTX_ctrl(m_ptr_ctx, 123, 772L, IntPtr.Zero);
			if (OpenSslNative.SSL_CTX_set_ciphersuites(m_ptr_ctx, "TLS_AES_256_GCM_SHA384:TLS_AES_128_GCM_SHA256") != 1)
			{
				throw new Exception("errOpenSSL");
			}
			m_readRequired = false;
			m_ssl = OpenSslNative.SSL_new(m_ptr_ctx);
			m_pBioIn = OpenSslNative.BIO_new(OpenSslNative.BIO_s_mem());
			m_pBioOut = OpenSslNative.BIO_new(OpenSslNative.BIO_s_mem());
			OpenSslNative.SSL_set_bio(m_ssl, m_pBioIn, m_pBioOut);
			this.sendDataFunc = sendDataFunc;
			this.receiveByte = receiveByte;
		}

		/// <summary>
		/// 连接
		/// </summary>
		public void SSLConnect()
		{
			OpenSslNative.SSL_set_connect_state(m_ssl);
		}

		/// <summary>
		/// 将PDU数据写入到当前的信息中
		/// </summary>
		/// <param name="value">原始数据信息</param>
		public OperateResult Write(byte[] value)
		{
			return Write(new List<byte[]> { value });
		}

		/// <summary>
		/// 将PDU数据写入到当前的信息中
		/// </summary>
		/// <param name="values">等待发送的字节列表</param>
		public OperateResult Write(List<byte[]> values)
		{
			try
			{
				for (int i = 0; i < values.Count; i++)
				{
					LogNet?.WriteDebug(ToString(), StringResources.Language.Send + " : " + values[i].ToHexString(' '));
					m_pendingWriteList.AddLast(values[i]);
				}
				RunSSL();
				return OperateResult.CreateSuccessResult();
			}
			catch (Exception ex)
			{
				return new OperateResult(ex.Message);
			}
		}

		/// <summary>
		/// 收到了加密数据，写入到ssl里进行解密
		/// </summary>
		/// <param name="pData"></param>
		public void ReadCompleted(byte[] pData)
		{
			m_pendingReadList.AddLast(pData);
			RunSSL();
		}

		/// <summary>
		/// 初始化方法
		/// </summary>
		public void SSLInit()
		{
			OpenSslNative.SSL_in_init(m_ssl);
			if (OpenSslNative.BIO_ctrl_pending(m_pBioOut) != 0)
			{
				SendPendingData();
			}
		}

		/// <summary>
		/// 运行当前的SSL方法
		/// </summary>
		private void RunSSL()
		{
			bool dataToWrite = false;
			bool dataToRead = false;
			GetPendingOperations(ref dataToRead, ref dataToWrite);
			while ((!m_readRequired && dataToWrite) || dataToRead)
			{
				if (OpenSslNative.SSL_in_init(m_ssl) != 0)
				{
				}
				if (dataToRead)
				{
					PerformRead();
				}
				if (!m_readRequired && dataToWrite)
				{
					PerformWrite();
				}
				if (OpenSslNative.BIO_ctrl_pending(m_pBioOut) != 0)
				{
					SendPendingData();
				}
				GetPendingOperations(ref dataToRead, ref dataToWrite);
			}
		}

		private void GetPendingOperations(ref bool dataToRead, ref bool dataToWrite)
		{
			dataToRead = m_pendingReadList.Count > 0;
			dataToWrite = m_pendingWriteList.Count > 0;
		}

		private void PerformRead()
		{
			byte[] nextBuffer = GetNextBuffer(m_pendingReadList);
			if (nextBuffer != null)
			{
				DataToRead(nextBuffer, nextBuffer.Length);
			}
		}

		private void PerformWrite()
		{
			byte[] nextBuffer = GetNextBuffer(m_pendingWriteList);
			if (nextBuffer != null)
			{
				int num = DataToWrite(nextBuffer, nextBuffer.Length);
				if (num == 0)
				{
					m_pendingWriteList.AddLast(nextBuffer);
				}
				else if (num < nextBuffer.Length)
				{
					nextBuffer.RemoveBegin(num);
					m_pendingWriteList.AddLast(nextBuffer);
				}
			}
		}

		private void DataToRead(byte[] pData, int dataLength)
		{
			m_readRequired = false;
			int num = 0;
			while (num < dataLength)
			{
				byte[] array = new byte[dataLength - num];
				Array.Copy(pData, num, array, 0, array.Length);
				num += OpenSslNative.BIO_write(m_pBioIn, array, array.Length);
				int num2 = 0;
				do
				{
					byte[] array2 = new byte[4096];
					num2 = OpenSslNative.SSL_read(m_ssl, array2, array2.Length);
					if (num2 > 0)
					{
						receiveByte(array2.SelectBegin(num2));
					}
					if (num2 < 0)
					{
					}
				}
				while (num2 > 0);
			}
		}

		private void SendPendingData()
		{
			while (OpenSslNative.BIO_ctrl_pending(m_pBioOut) != 0)
			{
				byte[] array = new byte[4096];
				int num = OpenSslNative.BIO_read(m_pBioOut, array, array.Length);
				if (num > 0)
				{
					Array.Resize(ref array, num);
					OperateResult operateResult = sendDataFunc(array);
					if (!operateResult.IsSuccess)
					{
						LogNet?.WriteDebug("SendPendingData failed: " + operateResult.Message);
					}
				}
				if (num > 0 || skip_bio_should_retry)
				{
					continue;
				}
				try
				{
					if (OpenSslNative.BIO_should_retry(m_pBioOut) == 0)
					{
					}
				}
				catch (EntryPointNotFoundException)
				{
					skip_bio_should_retry = true;
				}
			}
		}

		private int DataToWrite(byte[] pData, int dataLength)
		{
			int result = 0;
			int num = OpenSslNative.SSL_write(m_ssl, pData, dataLength);
			if (num < 0)
			{
				LogNet?.WriteDebug("OpenSSL DataToWrite: Error = " + OpenSslNative.SSL_get_error(m_ssl, num));
			}
			else
			{
				result = num;
			}
			if (OpenSslNative.SSL_want(m_ssl) == 3)
			{
				m_readRequired = true;
			}
			return result;
		}

		private void HandleError(int result)
		{
			if (result <= 0)
			{
				int num = OpenSslNative.SSL_get_error(m_ssl, result);
				if (num != 0)
				{
					LogNet?.WriteDebug("OpenSSL HandleError: Error = " + num);
				}
				
			}
		}

		private byte[] GetNextBuffer(LinkedList<byte[]> list)
		{
			byte[] result = null;
			if (list.Count > 0)
			{
				result = list.First.Value;
				list.RemoveFirst();
			}
			return result;
		}

		/// <inheritdoc cref="M:System.IDisposable.Dispose" />
		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
				}
				OpenSslNative.SSL_free(m_ssl);
				disposedValue = true;
			}
		}

		/// <inheritdoc cref="M:System.IDisposable.Dispose" />
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
	}
}
