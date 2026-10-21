# OPC DA 代理回归检查

`TokenAuthCheck.cs` 检查令牌校验（去空白、拒绝空令牌、常量时间比较）、配置加载和
“非回环监听必须配置非空令牌”的启动校验。它与服务源码一起编译成单独的 exe，不进入正式构建
（`build.ps1` 只编译服务源码）。

在 Windows（已装 .NET Framework 4.8 开发包）上，于 `collector/sidecars/opcda_proxy_service` 目录执行：

```powershell
$csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
New-Item -ItemType Directory -Force bin\tests | Out-Null
& $csc /nologo /target:exe /platform:x86 /main:OpcDaProxyService.TokenAuthCheck `
  /out:bin\tests\TokenAuthCheck.exe `
  /reference:System.dll /reference:System.Core.dll /reference:System.Web.Extensions.dll `
  OpcDaProxyService.cs OpcConnectionManager.cs tests\TokenAuthCheck.cs
bin\tests\TokenAuthCheck.exe; "exit=$LASTEXITCODE"
```

期望输出最后一行为 `ALL PASSED`，退出码 0。
