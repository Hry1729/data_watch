// 审查 R2：OPC DA 代理令牌校验回归检查（Windows / .NET Framework 4.8 x86）。
// 与服务源码一起编译（StartupObject 指向本类），退出码 = 失败条数。
// 用法见 tests/README.md。
using System;
using System.IO;
using System.Net;
using System.Text;

namespace OpcDaProxyService
{
    internal static class TokenAuthCheck
    {
        private static int _fails;

        private static void Check(string name, bool ok)
        {
            Console.WriteLine((ok ? "PASS " : "FAIL ") + name);
            if (!ok)
            {
                _fails++;
            }
        }

        private static bool Throws(Action action)
        {
            try
            {
                action();
                return false;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }

        private static string LoadToken(string json)
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, json, Encoding.UTF8);
                return ProxyConfig.Load(path).AuthToken;
            }
            finally
            {
                File.Delete(path);
            }
        }

        public static int Main(string[] args)
        {
            // 请求校验
            Check("未配置令牌：放行（仅允许回环监听）", Program.IsTokenAuthorized(null, null));
            Check("配置为纯空白：视同未配置", Program.IsTokenAuthorized("   ", null));
            Check("已配置，请求缺令牌：拒绝", !Program.IsTokenAuthorized("s3cret", null));
            Check("已配置，请求空令牌：拒绝", !Program.IsTokenAuthorized("s3cret", ""));
            Check("已配置，请求纯空白令牌：拒绝", !Program.IsTokenAuthorized("s3cret", "   "));
            Check("已配置，错误令牌：拒绝", !Program.IsTokenAuthorized("s3cret", "s3creX"));
            Check("已配置，前缀：拒绝", !Program.IsTokenAuthorized("s3cret", "s3cre"));
            Check("已配置，更长：拒绝", !Program.IsTokenAuthorized("s3cret", "s3cret1"));
            Check("已配置，匹配：放行", Program.IsTokenAuthorized("s3cret", "s3cret"));
            Check("首尾空白不影响匹配", Program.IsTokenAuthorized(" s3cret\n", "\ts3cret "));

            // 配置加载
            Check("Load：纯空白 authToken -> 空", LoadToken("{\"authToken\":\"   \"}") == "");
            Check("Load：authToken 去首尾空白", LoadToken("{\"authToken\":\" abc \"}") == "abc");
            Check("Load：缺省 authToken -> 空", LoadToken("{}") == "");

            // 启动校验
            Check("非回环 + 空令牌：拒绝启动",
                Throws(delegate { Program.ValidateListenSecurity(IPAddress.Any, ""); }));
            Check("非回环 + 纯空白令牌：拒绝启动",
                Throws(delegate { Program.ValidateListenSecurity(IPAddress.Any, "  \t"); }));
            Check("非回环 + null 令牌：拒绝启动",
                Throws(delegate { Program.ValidateListenSecurity(IPAddress.Parse("10.0.0.5"), null); }));
            Check("非回环 + 有效令牌：允许",
                !Throws(delegate { Program.ValidateListenSecurity(IPAddress.Any, "s3cret"); }));
            Check("回环 + 空令牌：允许",
                !Throws(delegate { Program.ValidateListenSecurity(IPAddress.Loopback, ""); }));

            Console.WriteLine(_fails == 0 ? "ALL PASSED" : (_fails + " FAILED"));
            return _fails;
        }
    }
}
