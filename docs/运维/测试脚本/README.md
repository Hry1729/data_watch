# 测试与压测脚本

本目录收录开发/压测阶段使用的辅助脚本，不属于生产代码。

运行环境：`collector/.venv-pack`（`pip install -r collector/requirements.txt`）

---

## redis_mock_writer.py

**用途**：向 Redis 写入模拟数据（v0.6 key schema），用于验证后端实时推送和 writer→TDengine 消费链路。设备列表和 var_id 从 MySQL 加载，key 格式与真实 collector 完全一致。

```powershell
cd D:\python\device_alarm\data_watch

# 仅写 latest/window（验证前端实时显示）
collector\.venv-pack\Scripts\python.exe "docs\运维\测试脚本\redis_mock_writer.py"

# 同时写 Stream（验证 writer → TDengine 链路）
collector\.venv-pack\Scripts\python.exe "docs\运维\测试脚本\redis_mock_writer.py" --enable-stream

# 持续运行直到手动停止
collector\.venv-pack\Scripts\python.exe "docs\运维\测试脚本\redis_mock_writer.py" --enable-stream --duration-seconds 0
```

**依赖**：MySQL 和 Redis 必须运行，配置自动读取 `writer/config/settings.toml`。

---

## redis_to_tdengine_writer.py

**用途**：早期实验性脚本，直接从 Redis 读取数据写入 TDengine，不经过 Stream/writer 流程。已被 `writer/` 模块取代，保留作参考。

---

## tdengine_write_benchmark.py

**用途**：TDengine 批量写入压力测试，测试不同 batch size / 并发数下的写入吞吐量。

```powershell
cd D:\python\device_alarm\data_watch
collector\.venv-pack\Scripts\python.exe docs\运维\测试脚本\tdengine_write_benchmark.py --help
```

---

## init_tdengine_benchmark_tables.py

**用途**：为压测准备 TDengine 基准测试超级表和子表（独立于生产库 `device_data`）。压测前先运行此脚本创建测试库。

---

## drop_tdengine_benchmark_databases.py

**用途**：清理 `init_tdengine_benchmark_tables.py` 创建的压测数据库，压测结束后执行。

---

## 旧版/

存档目录，保存上述脚本的旧版本供对比参考，不需要单独运行。
