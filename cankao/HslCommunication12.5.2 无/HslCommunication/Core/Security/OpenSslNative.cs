using System;
using System.Runtime.InteropServices;
using System.Text;

namespace HslCommunication.Core.Security
{
	/// <summary>
	/// SSL相关的类方法名称，本代码来源 https://github.com/thomas-v2/S7CommPlusDriver
	/// </summary>
	public class OpenSslNative
	{
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void SSL_CTX_keylog_cb_func(IntPtr ssl, string line);

		private const string DLLNAME = "libcrypto-3-x64.dll";

		private const string SSLDLLNAME = "libssl-3-x64.dll";

		public const int SSL_NOTHING = 1;

		public const int SSL_WRITING = 2;

		public const int SSL_READING = 3;

		public const int SSL_X509_LOOKUP = 4;

		public const int SSL_ASYNC_PAUSED = 5;

		public const int SSL_ASYNC_NO_JOBS = 6;

		public const int SSL_CLIENT_HELLO_CB = 7;

		public const int SSL_RETRY_VERIFY = 8;

		public const int SSL_CTRL_GET_CLIENT_CERT_REQUEST = 9;

		public const int SSL_CTRL_GET_NUM_RENEGOTIATIONS = 10;

		public const int SSL_CTRL_CLEAR_NUM_RENEGOTIATIONS = 11;

		public const int SSL_CTRL_GET_TOTAL_RENEGOTIATIONS = 12;

		public const int SSL_CTRL_GET_FLAGS = 13;

		public const int SSL_CTRL_EXTRA_CHAIN_CERT = 14;

		public const int SSL_CTRL_SET_MSG_CALLBACK = 15;

		public const int SSL_CTRL_SET_MSG_CALLBACK_ARG = 16;

		public const int SSL_CTRL_SET_MTU = 17;

		public const int SSL_CTRL_SESS_NUMBER = 20;

		public const int SSL_CTRL_SESS_CONNECT = 21;

		public const int SSL_CTRL_SESS_CONNECT_GOOD = 22;

		public const int SSL_CTRL_SESS_CONNECT_RENEGOTIATE = 23;

		public const int SSL_CTRL_SESS_ACCEPT = 24;

		public const int SSL_CTRL_SESS_ACCEPT_GOOD = 25;

		public const int SSL_CTRL_SESS_ACCEPT_RENEGOTIATE = 26;

		public const int SSL_CTRL_SESS_HIT = 27;

		public const int SSL_CTRL_SESS_CB_HIT = 28;

		public const int SSL_CTRL_SESS_MISSES = 29;

		public const int SSL_CTRL_SESS_TIMEOUTS = 30;

		public const int SSL_CTRL_SESS_CACHE_FULL = 31;

		public const int SSL_CTRL_MODE = 33;

		public const int SSL_CTRL_GET_READ_AHEAD = 40;

		public const int SSL_CTRL_SET_READ_AHEAD = 41;

		public const int SSL_CTRL_SET_SESS_CACHE_SIZE = 42;

		public const int SSL_CTRL_GET_SESS_CACHE_SIZE = 43;

		public const int SSL_CTRL_SET_SESS_CACHE_MODE = 44;

		public const int SSL_CTRL_GET_SESS_CACHE_MODE = 45;

		public const int SSL_CTRL_GET_MAX_CERT_LIST = 50;

		public const int SSL_CTRL_SET_MAX_CERT_LIST = 51;

		public const int SSL_CTRL_SET_MAX_SEND_FRAGMENT = 52;

		public const int SSL_CTRL_SET_TLSEXT_SERVERNAME_CB = 53;

		public const int SSL_CTRL_SET_TLSEXT_SERVERNAME_ARG = 54;

		public const int SSL_CTRL_SET_TLSEXT_HOSTNAME = 55;

		public const int SSL_CTRL_SET_TLSEXT_DEBUG_CB = 56;

		public const int SSL_CTRL_SET_TLSEXT_DEBUG_ARG = 57;

		public const int SSL_CTRL_GET_TLSEXT_TICKET_KEYS = 58;

		public const int SSL_CTRL_SET_TLSEXT_TICKET_KEYS = 59;

		public const int SSL_CTRL_SET_TLSEXT_STATUS_REQ_CB = 63;

		public const int SSL_CTRL_SET_TLSEXT_STATUS_REQ_CB_ARG = 64;

		public const int SSL_CTRL_SET_TLSEXT_STATUS_REQ_TYPE = 65;

		public const int SSL_CTRL_GET_TLSEXT_STATUS_REQ_EXTS = 66;

		public const int SSL_CTRL_SET_TLSEXT_STATUS_REQ_EXTS = 67;

		public const int SSL_CTRL_GET_TLSEXT_STATUS_REQ_IDS = 68;

		public const int SSL_CTRL_SET_TLSEXT_STATUS_REQ_IDS = 69;

		public const int SSL_CTRL_GET_TLSEXT_STATUS_REQ_OCSP_RESP = 70;

		public const int SSL_CTRL_SET_TLSEXT_STATUS_REQ_OCSP_RESP = 71;

		public const int SSL_CTRL_SET_TLS_EXT_SRP_USERNAME_CB = 75;

		public const int SSL_CTRL_SET_SRP_VERIFY_PARAM_CB = 76;

		public const int SSL_CTRL_SET_SRP_GIVE_CLIENT_PWD_CB = 77;

		public const int SSL_CTRL_SET_SRP_ARG = 78;

		public const int SSL_CTRL_SET_TLS_EXT_SRP_USERNAME = 79;

		public const int SSL_CTRL_SET_TLS_EXT_SRP_STRENGTH = 80;

		public const int SSL_CTRL_SET_TLS_EXT_SRP_PASSWORD = 81;

		public const int DTLS_CTRL_GET_TIMEOUT = 73;

		public const int DTLS_CTRL_HANDLE_TIMEOUT = 74;

		public const int SSL_CTRL_GET_RI_SUPPORT = 76;

		public const int SSL_CTRL_CLEAR_MODE = 78;

		public const int SSL_CTRL_SET_NOT_RESUMABLE_SESS_CB = 79;

		public const int SSL_CTRL_GET_EXTRA_CHAIN_CERTS = 82;

		public const int SSL_CTRL_CLEAR_EXTRA_CHAIN_CERTS = 83;

		public const int SSL_CTRL_CHAIN = 88;

		public const int SSL_CTRL_CHAIN_CERT = 89;

		public const int SSL_CTRL_GET_GROUPS = 90;

		public const int SSL_CTRL_SET_GROUPS = 91;

		public const int SSL_CTRL_SET_GROUPS_LIST = 92;

		public const int SSL_CTRL_GET_SHARED_GROUP = 93;

		public const int SSL_CTRL_SET_SIGALGS = 97;

		public const int SSL_CTRL_SET_SIGALGS_LIST = 98;

		public const int SSL_CTRL_CERT_FLAGS = 99;

		public const int SSL_CTRL_CLEAR_CERT_FLAGS = 100;

		public const int SSL_CTRL_SET_CLIENT_SIGALGS = 101;

		public const int SSL_CTRL_SET_CLIENT_SIGALGS_LIST = 102;

		public const int SSL_CTRL_GET_CLIENT_CERT_TYPES = 103;

		public const int SSL_CTRL_SET_CLIENT_CERT_TYPES = 104;

		public const int SSL_CTRL_BUILD_CERT_CHAIN = 105;

		public const int SSL_CTRL_SET_VERIFY_CERT_STORE = 106;

		public const int SSL_CTRL_SET_CHAIN_CERT_STORE = 107;

		public const int SSL_CTRL_GET_PEER_SIGNATURE_NID = 108;

		public const int SSL_CTRL_GET_PEER_TMP_KEY = 109;

		public const int SSL_CTRL_GET_RAW_CIPHERLIST = 110;

		public const int SSL_CTRL_GET_EC_POINT_FORMATS = 111;

		public const int SSL_CTRL_GET_CHAIN_CERTS = 115;

		public const int SSL_CTRL_SELECT_CURRENT_CERT = 116;

		public const int SSL_CTRL_SET_CURRENT_CERT = 117;

		public const int SSL_CTRL_SET_DH_AUTO = 118;

		public const int DTLS_CTRL_SET_LINK_MTU = 120;

		public const int DTLS_CTRL_GET_LINK_MIN_MTU = 121;

		public const int SSL_CTRL_GET_EXTMS_SUPPORT = 122;

		public const int SSL_CTRL_SET_MIN_PROTO_VERSION = 123;

		public const int SSL_CTRL_SET_MAX_PROTO_VERSION = 124;

		public const int SSL_CTRL_SET_SPLIT_SEND_FRAGMENT = 125;

		public const int SSL_CTRL_SET_MAX_PIPELINES = 126;

		public const int SSL_CTRL_GET_TLSEXT_STATUS_REQ_TYPE = 127;

		public const int SSL_CTRL_GET_TLSEXT_STATUS_REQ_CB = 128;

		public const int SSL_CTRL_GET_TLSEXT_STATUS_REQ_CB_ARG = 129;

		public const int SSL_CTRL_GET_MIN_PROTO_VERSION = 130;

		public const int SSL_CTRL_GET_MAX_PROTO_VERSION = 131;

		public const int SSL_CTRL_GET_SIGNATURE_NID = 132;

		public const int SSL_CTRL_GET_TMP_KEY = 133;

		public const int SSL_CTRL_GET_NEGOTIATED_GROUP = 134;

		public const int SSL_CTRL_SET_RETRY_VERIFY = 136;

		public const int SSL_CTRL_GET_VERIFY_CERT_STORE = 137;

		public const int SSL_CTRL_GET_CHAIN_CERT_STORE = 138;

		public const int TLS1_VERSION = 769;

		public const int TLS1_1_VERSION = 770;

		public const int TLS1_2_VERSION = 771;

		public const int TLS1_3_VERSION = 772;

		private const int BIO_C_SET_BUF_MEM_EOF_RETURN = 130;

		public const int SSL_ERROR_NONE = 0;

		public const int SSL_ERROR_SSL = 1;

		public const int SSL_ERROR_WANT_READ = 2;

		public const int SSL_ERROR_WANT_WRITE = 3;

		public const int SSL_ERROR_WANT_X509_LOOKUP = 4;

		public const int SSL_ERROR_SYSCALL = 5;

		public const int SSL_ERROR_ZERO_RETURN = 6;

		public const int SSL_ERROR_WANT_CONNECT = 7;

		public const int SSL_ERROR_WANT_ACCEPT = 8;

		[DllImport("libssl-3-x64.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern int OPENSSL_init_ssl(ulong opts, IntPtr settings);

		[DllImport("libssl-3-x64.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void OPENSSL_free(IntPtr addr);

		[DllImport("libssl-3-x64.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern int SSL_connect(IntPtr ssl);

		[DllImport("libssl-3-x64.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void SSL_free(IntPtr ssl);

		[DllImport("libssl-3-x64.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern int SSL_get_error(IntPtr ssl, int ret_code);

		[DllImport("libssl-3-x64.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern int SSL_in_init(IntPtr ssl);

		[DllImport("libssl-3-x64.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr SSL_new(IntPtr ctx);

		[DllImport("libssl-3-x64.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern int SSL_read(IntPtr ssl, byte[] buf, int len);

		[DllImport("libssl-3-x64.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void SSL_set_accept_state(IntPtr ssl);

		[DllImport("libssl-3-x64.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void SSL_set_bio(IntPtr ssl, IntPtr read_bio, IntPtr write_bio);

		[DllImport("libssl-3-x64.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void SSL_set_connect_state(IntPtr ssl);

		[DllImport("libssl-3-x64.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr SSL_state_string_long(IntPtr ssl);

		[DllImport("libssl-3-x64.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern int SSL_want(IntPtr ssl);

		[DllImport("libssl-3-x64.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern int SSL_want_write(IntPtr ssl);

		[DllImport("libssl-3-x64.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern int SSL_write(IntPtr ssl, byte[] buf, int len);

		[DllImport("libssl-3-x64.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr TLS_client_method();

		[DllImport("libssl-3-x64.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern long SSL_CTX_ctrl(IntPtr ctx, int cmd, long arg, IntPtr parg);

		[DllImport("libssl-3-x64.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void SSL_CTX_free(IntPtr ctx);

		[DllImport("libssl-3-x64.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr SSL_CTX_new(IntPtr sslMethod);

		[DllImport("libssl-3-x64.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern int SSL_CTX_set_ciphersuites(IntPtr ctx, string str);

		[DllImport("libssl-3-x64.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void SSL_CTX_set_keylog_callback(IntPtr ctx, SSL_CTX_keylog_cb_func cb);

		[DllImport("libcrypto-3-x64.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern long BIO_ctrl(IntPtr bp, int cmd, long larg, IntPtr parg);

		[DllImport("libcrypto-3-x64.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern ulong BIO_ctrl_pending(IntPtr bio);

		[DllImport("libcrypto-3-x64.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void BIO_free_all(IntPtr bio);

		[DllImport("libcrypto-3-x64.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern int BIO_free(IntPtr bio);

		[DllImport("libcrypto-3-x64.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr BIO_new(IntPtr type);

		[DllImport("libcrypto-3-x64.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern int BIO_read(IntPtr b, byte[] buf, int len);

		[DllImport("libcrypto-3-x64.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr BIO_s_mem();

		[DllImport("libcrypto-3-x64.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern int BIO_should_retry(IntPtr b);

		[DllImport("libcrypto-3-x64.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern int BIO_write(IntPtr b, byte[] data, int dlen);

		public static long BIO_set_mem_eof_return(IntPtr b, int v)
		{
			return BIO_ctrl(b, 130, v, IntPtr.Zero);
		}

		[DllImport("libcrypto-3-x64.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern void ERR_error_string_n(ulong e, byte[] buf, ulong len);

		[DllImport("libcrypto-3-x64.dll", CallingConvention = CallingConvention.Cdecl)]
		public static extern ulong ERR_get_error();

		public static string StaticString(IntPtr ptr)
		{
			return Marshal.PtrToStringAnsi(ptr);
		}

		public static string PtrToStringAnsi(IntPtr ptr, bool hasOwnership)
		{
			int num = 0;
			int num2 = 0;
			while (num2 < 1024 && Marshal.ReadByte(ptr, num2) != 0)
			{
				num2++;
				num++;
			}
			if (num == 1024)
			{
				return "Invalid string";
			}
			byte[] array = new byte[num];
			Marshal.Copy(ptr, array, 0, num);
			if (hasOwnership)
			{
				OPENSSL_free(ptr);
			}
			return Encoding.ASCII.GetString(array, 0, num);
		}

		public static IntPtr ExpectNonNull(IntPtr ptr)
		{
			if (ptr == IntPtr.Zero)
			{
				throw new Exception();
			}
			return ptr;
		}

		public static int ExpectSuccess(int ret)
		{
			if (ret <= 0)
			{
				throw new Exception();
			}
			return ret;
		}
	}
}
