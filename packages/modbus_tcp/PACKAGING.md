# Modbus TCP 外置驱动包

通用制作说明见：[外置驱动包制作指南](../../docs/02-模块设计/collector/外置驱动包制作指南.md)。

目录布局即 Agent 安装 ZIP 的包根内容：

```text
modbus_tcp/
  driver.toml
  modbus_tcp/
    __init__.py
    driver.py
    ...
  vendor/
  vendor_meta.toml
```

## 打包（PowerShell）

在仓库根执行，生成可供驱动管理页安装的 ZIP（包根目录名必须为 `modbus_tcp`）：

```powershell
$src = "D:\python\device_alarm\data_watch\packages\modbus_tcp"
$out = "D:\python\device_alarm\data_watch\packages\modbus_tcp-1.0.0.zip"
if (Test-Path $out) { Remove-Item $out -Force }
Compress-Archive -Path $src -DestinationPath $out
```

注意：`Compress-Archive` 会把文件夹名 `modbus_tcp` 作为 ZIP 内唯一包根，符合 Agent 校验要求。

## 安装

1. App「驱动管理」→「安装/升级 ZIP」选择上述 ZIP。
2. Agent 静态校验 → 激活到 `%DATAWATCH_DATA_ROOT%\drivers\modbus_tcp` → 重启 collector。
3. Backend 自动注册 `comm_protocol.protocol = modbus_tcp`（若不存在）。
