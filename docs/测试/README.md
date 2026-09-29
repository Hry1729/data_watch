# 桥吊100演示数据

用于没有真实 PLC / Collector 的本机演示。仅写桥吊100（`qc100`），默认每 **100ms 一帧**。当前805个启用点中，182个数值点在0～100间往返，623个布尔点在0/1间切换。

## 一键启动（推荐）

先启动 Docker 数据库和后端服务，然后双击本目录的 **启动桥吊100模拟.cmd** 即可持续运行。按 **Ctrl+C** 停止；若出现终止批处理的询问，输入 `Y` 并回车。不要直接关闭窗口。报错或正常结束后，窗口保留供查看提示。

先启动现有 Docker 数据库及 `data-watch-backend` 容器，再在 PowerShell 执行：

```powershell
powershell -ExecutionPolicy Bypass -File "D:\python\device_alarm\data_watch\docs\测试\启动桥吊100模拟.ps1"
```

脚本持续运行，按 **Ctrl+C** 停止；停止后历史演示数据保留。保持终端开启，然后在网页中选择桥吊100查看实时值或最近一段时间的历史曲线。

只检查连接/映射/结构，不写数据：

```powershell
powershell -ExecutionPolicy Bypass -File "D:\python\device_alarm\data_watch\docs\测试\启动桥吊100模拟.ps1" -DryRun
```

限时演示5分钟，约8秒一个基础周期，增加扰动：

```powershell
powershell -ExecutionPolicy Bypass -File "D:\python\device_alarm\data_watch\docs\测试\启动桥吊100模拟.ps1" -Duration 300 -Period 8 -Noise 20
```

默认基础周期12秒、扰动12。各点的周期和相位不同；叠加多个频率的正弦波，最终夹在0～100内。整数点取整，布尔点保持0/1；质量码为0（GOOD）。`Noise`范围0～50。

## 写入范围和运行方式

- 从MySQL读取 `qc100` 当前启用且未删除的永久映射，不更改MySQL配置，也不创建/修改TDengine表结构。
- 写入TDengine `qc100` 值表和 `qc100_q` 质量表，仅存储启用存储的点。
- TDengine写入成功后更新Redis `rt:latest:qc100:<group>`，发布 `rt:change`，让实时页与设备状态看到新数据。
- 不依赖Collector或Writer，不写Redis Stream，也不伪造它们的心跳。因此进程链路健康仍可能显示离线。
- 请只启动一份脚本，演示期间不要同时运行写入qc100的真实采集。停止脚本不会删除Redis最后值；设备状态会按现有超时规则转为离线。
- 按单调时钟安排100ms周期；数据库写入过慢时跳过错过的周期并打印计数，不堆积补写任务。它是演示工具，不是严格实时采集器。
- 数据库报错时停止并打印错误。TDengine和Redis不是跨库事务；发生部分写入错误时已成功的数据会保留。

启动脚本将Python文件复制到正在运行的后端容器 `/tmp/simulate_qc100.py`，复用容器已有依赖与连接配置，不修改镜像或在脚本中存储密码。PowerShell使用交互终端，把Ctrl+C传递给Python。

也可在配置好 `backend/.env` 和Python依赖（`httpx`、`redis`、`pymysql`）的本机直接运行：

```powershell
python "D:\python\device_alarm\data_watch\docs\测试\simulate_qc100.py" --duration 60
```

## 验收记录（2026-09-29）

- 离线测试4项通过：波动幅度及点间差异、布尔/整数类型、存储开关与时间戳一致性、SQL标识符检查。
- 真实容器环境限时运行12秒：120帧、96,600条质量记录、805个最新值，慢写跳过0周期。
- 对比验证qc101及qc101_q行数未变。限时验证进程已退出，保留12秒演示数据。
- 工作台真实趋势服务已读到这120帧；PowerShell启动入口的只读预检也已通过。

离线测试命令（不会连接数据库）：

```powershell
python -m unittest discover -s "D:\python\device_alarm\data_watch\docs\测试" -p test_simulate_qc100.py
```
