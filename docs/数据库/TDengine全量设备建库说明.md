# TDengine 全量设备建库说明

> 更新时间：2026-05-19
> 对应脚本：`docs/数据库/tdengine_full_device_schema.sql`

---

## 一、脚本目标

本次脚本用于一次性完成以下 TDengine 初始化动作：

- 新建 `device_data` 数据库
- 创建 3 个设备类型超级表：`qc`、`asc`、`agv`
- 按 MySQL `device` 为每台设备创建一个 TDengine 子表
- 按 MySQL `device_variable.var_seq` 为对应设备类型超级表动态维护点位值宽列
- TDengine 历史表只保存点位数值，不保存 `quality`

---

## 二、覆盖范围

- `QC`：`qc100` ~ `qc128`，共 29 台
- `ASC`：按 121 台口径生成，名单为 `asc7011` + `asc7021/asc7022` 一直到 `asc7611/asc7612`
- `AGV`：`agv801` ~ `agv955`，共 155 台

---

## 三、旧测试子表样本

以下样本属于 2026-04-14 的旧“每点位一个子表”口径，仅作为历史记录；新模型不再创建这些点位子表：

- QC：`qc100_1`、`qc100_11901`、`qc101_1`、`qc101_11901`
- ASC：`asc7011_85`、`asc7011_4200`、`asc7021_85`、`asc7021_4200`
- AGV：`agv801_100`、`agv801_200`、`agv802_100`、`agv802_200`

---

## 四、使用建议

- 该脚本适合作为 TDengine 新模型初始化脚本
- 若后续要补充实际测试样本，可继续复用现有 `fake_data_seed_tdengine.sql` 或新的 collector 写入链路
- 若你确认 ASC 编码需要保留 `asc7012`，则应先重新确认 121 台总量，再统一调整生成名单
