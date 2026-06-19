# 采集模块 TODO / 技术债

> 本文记录采集器（collector）已知的后续增强与技术债。每条须注明性质（**后续增强** / **缺陷**），避免把"有意保持的旧语义"误读为漏修。

---

## TODO: 支持运行期连接参数热更新

**性质：后续增强 / 非本轮 #6 缺陷**

当前 `_config_reload_loop` 由 `variable_config_version` 触发，热重载范围保持为点位 / 采集任务变更；设备 IP、endpoint、protocol、rack、slot、opc_server 等连接参数变更不会在运行期自动断开并重连。该行为继承旧 `_reload_device` 语义，不属于 #6（运行期设备拓扑热管理）的回归。

> 边界说明：#6 处理的是设备的**新增 / 删除 / 禁用 / 点位版本变更**；连接参数热切换是另一类变更，触发字段（`variable_config_version`）语义上也不代表连接变更，因此本轮有意不处理。

后续如需要连接参数热生效，应单独设计：

- 明确连接参数版本来源或变更检测字段（不要复用 `variable_config_version`）。
- 判断哪些字段需要重连、哪些只需更新内存配置。
- 处理 center 协议端口映射、reconnect task、已有调度任务的切换。
- 增加独立 mock 测试和端到端验证。

相关代码：`collector/main.py` 的 `_config_reload_loop` / `_apply_device_variables`，`collector/device/manager.py` 的 `rebuild_device_variables` / `_setup_scan_tasks`。
