# TDengine 宽表压测包

这个包用于在目标机测试当前推荐模型：

- 数据库：`device_data`
- 超级表：`qc`、`asc`、`agv`
- 子表：从 MySQL `device` 表读取，每台设备一张子表
- 每行：`ts` + `1..1000` 个信号值列
- 默认压测：每 `1000ms` 批量写一次，每台设备一次写 `10` 行

## 1. 离线运行前提

这个包已经内置 `vendor` 离线依赖，不需要目标机联网 `pip install`。

目标机仍然需要有 Windows Python。当前 vendor 依赖来自本机：

```text
Python 3.14.4 64-bit
```

先检查目标机能否直接加载离线依赖：

```cmd
check_offline.cmd
```

如果提示找不到 Python，可以指定 Python 路径：

```cmd
set PYTHON=C:\Python314\python.exe
check_offline.cmd
```

如果目标机 Python 版本不兼容，`taosws` 或 `greenlet` 这类二进制包可能无法加载，需要用目标机相同 Python 版本重新制作 vendor 包。

`requirements.txt` 仅作为依赖清单保留；离线运行时不用安装它。

## 2. 配置连接

包内已经带了 `config\settings.toml` 模板，直接修改 TDengine / MySQL 地址、账号和密码：

```powershell
notepad config\settings.toml
```

如果配置被改乱，可以用 `config\settings.toml.example` 重新复制一份。

脚本会从 MySQL `device` 和 `device_type` 表读取设备清单，只覆盖设备类型为 `qc`、`asc`、`agv` 的设备。

## 3. 初始化数据库和表

先 dry-run 看计划：

```powershell
run_init.cmd --dry-run
```

确认后建库建表：

```powershell
run_init.cmd
```

初始化脚本不会删除已存在数据库。若 `device_data` 已存在，数据库级参数不会被 `CREATE DATABASE IF NOT EXISTS` 改写。

## 4. 运行压测

先 dry-run 确认目标设备数量：

```powershell
run_benchmark.cmd --dry-run
```

推荐用当前较稳的 1 秒批量写入：

```powershell
run_benchmark.cmd --interval-ms 1000 --rows-per-device 10
```

如果要改“多少时间存储一次”，调整 `--interval-ms`：

```powershell
run_benchmark.cmd --interval-ms 500 --rows-per-device 5
run_benchmark.cmd --interval-ms 100 --rows-per-device 1
```

保持等效采样频率时，通常需要满足：

```text
rows_per_device / interval_seconds = 10 rows/second
```

例如 `1000ms x 10行`、`500ms x 5行`、`100ms x 1行` 都是每台设备每秒 10 行。

## 5. 常用参数

```powershell
run_benchmark.cmd --help
run_init.cmd --help
```

压测重点关注：

- `--interval-ms`：每隔多少毫秒提交一批
- `--rows-per-device`：每台设备每批写多少行
- `--duration-seconds`：压测持续多久
- `--points-per-device`：每行信号列数量，默认 `1000`
- `--workers-per-stable`：每个超级表写入线程数，默认 `1`
