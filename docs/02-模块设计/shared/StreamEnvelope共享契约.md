# StreamEnvelope 共享契约

## 1. 目标

`data_watch_shared.stream_envelope` 是 Collector、Central Ingest 和边缘 Uploader 共用的 Redis 数据契约实现。键名、分片和 JSON 编码只在该模块定义，业务组件不得复制规则。

## 2. 已实现边界

- `shard_number`：`crc32(device_code) % shard_count`。
- `stream_key`：生成 `rt:stream:{device_type}:{group_code}:{shard_no}`。
- `latest_key`：生成 `rt:latest:{device_code}:{group_code}`。
- `build_stream_fields`：生成 Writer 兼容的 v1/v2 独立 Stream 字段。
- `validate_envelope`：校验版本、必填标识和 v2 values/qualities 键集合。
- `build_latest_updates`：生成逐点 `{v,t,q,e}` 更新。
- `decode_latest_value`：兼容旧 Collector 的 `{v,t,q}`，缺失 epoch 时按 `e=0` 解码。

## 3. 组件接入

中心式 Collector 已复用键生成和编码函数，外部 Redis 格式不变。Central Ingest 应在业务校验后调用 `validate_envelope`，并将同一 envelope 交给原子 Lua；Uploader 应把完整 envelope 原样持久化到 SQLite outbox，不重新解释 Redis 字段。

共享模块属于运行时契约，不包含 MySQL 查询、HTTP 鉴权、重试或进程角色判断。
