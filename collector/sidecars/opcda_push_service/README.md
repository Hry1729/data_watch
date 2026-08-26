# OPC DA Push Service

这是 AGV 32 位 OPC DA 到中心 collector 的推送型边车服务基线。

设计目标：

- 在 OPC DA 所在 Windows 虚拟机本地运行。
- 使用 x86 .NET Framework 进程读取 32 位 OPC DA COM。
- 配置多台 AGV、多组 group、具体 tag 点位。
- 批量读取后 HTTP 推送到中心 collector。
- 推送载荷只带本地 `tag`，不要求 sidecar 知道中心 `variable_id`。

## 推荐部署方式

每个 OPC 虚拟机部署一个 sidecar：

```text
172.27.18.221
  OpcDaPushService.exe
  负责 AGV801-AGV810

172.27.18.222
  OpcDaPushService.exe
  负责 AGV811-AGV820

中心 collector
  接收 HTTP tag batch
  统一完成 device_id + group + tag -> variable_id / var_seq 映射
```

不推荐中心机器远程 DCOM 读取所有 VM；除非现场无法在 VM 上运行 exe，并且已经确认 DCOM 权限、防火墙、RPC 动态端口与服务账号策略。

## 构建

```powershell
cd D:\python\device_alarm\data_watch\collector\sidecars\opcda_push_service
.\build.ps1
```

构建产物：

```text
bin\OpcDaPushService.exe
```

构建目标固定为 `x86`，不要改成 AnyCPU 或 x64。

## 配置

复制示例配置：

```powershell
copy opcda_push_config.example.json opcda_push_config.json
```

核心字段：

```json
{
  "serverProgId": "ZPMC.OPCServer",
  "comHost": "",
  "channel": "OPD_Server",
  "itemFormat": "{channel}.{device}.{group}.{tag}",
  "readMode": "poll",
  "postEnabled": true,
  "collectorUrl": "http://中心collector地址:8090/ingest/opcda-tags",
  "source": "cache",
  "readIntervalMs": 1000,
  "pushMode": "changed_only",
  "fullSnapshotIntervalMs": 60000,
  "reportIntervalMs": 1000,
  "includeBadQuality": true,
  "devices": [
    {
      "deviceId": "agv801",
      "opcDevice": "AGV801",
      "groups": [
        {
          "name": "ViewMain",
          "csv": "../../../data/AGV_OPC/agv/ViewMain.csv",
          "skip": 0,
          "limit": 20,
          "points": []
        }
      ]
    }
  ]
}
```

说明：

- `deviceId`：推送给中心的设备编码，建议小写，例如 `agv801`。
- `opcDevice`：OPC ItemID 中的设备名，例如 `AGV801`。
- `group.name`：OPC group 名，例如 `ViewMain`。
- `group.csv`：点位模板 CSV，适合所有 AGV 点位一致的场景。
- `group.skip/group.limit`：从 CSV 中截取点位，便于先用少量点测试；生产全量读取时可把 `limit` 设为 `0`。
- `group.points`：额外指定点位，可用于只读少量测试点或补充 CSV 外点位。
- `itemFormat`：默认生成 `OPD_Server.AGV801.ViewMain.Tag30041800`。
- `readMode`：`poll` 为原同步轮询读；`subscription` 使用 OPC DA `IOPCDataCallback` 订阅回调。
- `postEnabled`：为 `false` 时只做本地读取/订阅性能测试，不要求 `collectorUrl`。
- `pushMode`：订阅缓存推送模式。默认 `changed_only` 只发送自上次成功选取后发生回调更新的点；`full` 每个推送周期发送完整缓存，适合校准但开销较大。
- `fullSnapshotIntervalMs`：`changed_only` 模式的周期性全量校准间隔，默认 `60000`；全量推送失败会把缓存点标记为 dirty，下一周期重试。sidecar 没有本地持久失败队列，进程崩溃仍可能丢失尚未成功 HTTP 提交的数据。
- `reportIntervalMs`：订阅压测统计日志间隔，例如 `500` 表示每 500ms 输出一行 tick。
- `collectorUrl`：中心接收 tag 批量数据的接口。当前主 collector `/ingest` 仍要求 `variable_id`，所以中心需要新增或适配一个 tag 映射入口。

## 运行

```powershell
.\run.bat
```

或者：

```powershell
.\bin\OpcDaPushService.exe --config .\opcda_push_config.json
```

`run.bat` 会优先读取 `opcda_push_config.json`；如果文件不存在，会临时使用 `opcda_push_config.example.json` 方便本地冒烟。生产部署时仍建议复制并编辑为独立的 `opcda_push_config.json`。

## AGV801-AGV810 订阅压测

用于验证 500ms 级别变化到达，不推送中心、不写 TDengine：

```powershell
cd D:\python\device_alarm\data_watch\collector\sidecars\opcda_push_service
.\run_subscription_benchmark_agv801_810.ps1
```

压测配置：`opcda_subscription_benchmark_agv801_810.json`。

关键行为：

- 一个常驻 x86 `OpcDaPushService.exe` 进程负责 AGV801-AGV810。
- 启动时一次性 `AddGroup/AddItems`，每台 AGV 的 `ViewMain.csv` 全量读取，`limit=0`。
- `readMode=subscription` 会通过 `IConnectionPointContainer.FindConnectionPoint(IOPCDataCallback)` 建立回调，并用 `IOPCAsyncIO2.Refresh2` 触发初始刷新。
- 每 500ms 输出 `tick changed=... callbacks=... cached=... good=... bad=... lastCallbackAgeMs=... maxEventAgeMs=...`。
- 如果 OPC Server 不支持 `IOPCDataCallback` 或 `IOPCAsyncIO2`，进程会失败退出，不会静默退回轮询。

## 推送载荷

sidecar 推送的是 tag batch，不包含 `variable_id`：

```json
{
  "source": "opcda_sidecar",
  "schema": "opcda_tag_batch_v1",
  "server": "ZPMC.OPCServer",
  "com_host": "OPC-VM-01",
  "channel": "OPD_Server",
  "sent_at": "2026-04-26T23:30:00.0000000+08:00",
  "points": [
    {
      "device_id": "agv801",
      "opc_device": "AGV801",
      "group": "ViewMain",
      "tag": "Tag30041800",
      "address": "300418.0",
      "data_type": "Boolean",
      "item_id": "OPD_Server.AGV801.ViewMain.Tag30041800",
      "value": true,
      "quality": 192,
      "quality_hex": "0x00C0",
      "quality_good": true,
      "opc_timestamp": "2026-04-26T23:30:00.0000000+08:00",
      "timestamp": "2026-04-26T23:30:00.0100000+08:00"
    }
  ]
}
```

`batch_id` 由 sidecar 为每个原始批次生成，并在该批次的重试请求中复用，仅用于关联日志和响应，不提供服务端去重或幂等保证。collector 响应中的 `accepted`、`rejected`、`rejected_indices`、`errors` 会被 sidecar 校验：带逐点拒绝索引时只重试失败点；无法定位失败点时保守重试当前剩余批次。重试耗尽后失败点不会被记为成功，但当前 sidecar 没有本地持久失败队列，后续轮询/缓存周期只能尽力重新读取当前值，进程崩溃可能丢失尚未确认的历史值。

协议解析 smoke test（验证 2xx + `errors` 不会被当作整体成功）：

```powershell
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
$out = Join-Path ([System.IO.Path]::GetTempPath()) "OpcDaPushService-response-smoke.exe"
& $csc /nologo /platform:x86 /target:exe /reference:System.Web.Extensions.dll `
  /main:OpcDaPushService.PushClientResponseSmoke "/out:$out" `
  .\OpcDaPushService.cs .\tests\PushClientResponseSmoke.cs
if ($LASTEXITCODE -ne 0) { throw "csc failed" }
& $out
if ($LASTEXITCODE -ne 0) { throw "response smoke test failed" }
```

中心侧建议按以下键映射：

```text
device_id + group + tag -> device_variable.id / var_seq
```

由于每台 AGV 点位模板一致，中心可以只维护一套 `group + tag -> var_seq` 模板，再结合 `device_id` 定位具体设备点位。

## 安全边界

- 只读 OPC DA，不调用写入接口。
- 不向 AGV 下发控制命令。
- 不直接写 TDengine。
- 生产环境建议配置 `apiKey`，并限制只允许 sidecar 主机访问中心接收接口。

## 后续待补

- 增加 Windows Service 或任务计划安装脚本。
- 增加本地失败队列，中心短暂不可用时可落盘补发。
- 增加按 MySQL 拉取 `device/group/tag` 配置的模式。
- 中心 collector 增加 `opcda_tag_batch_v1` 映射入口。
