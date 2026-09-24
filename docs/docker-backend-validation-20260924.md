# 本机 Docker 后端回归验证

> 后续状态已更新：请优先阅读 [本机 Docker 优化与验证续报](performance-optimization-validation-20260924.md)。本文件保留当时的阶段记录。


日期：2026-09-24。

## 结论

本机 Docker Desktop 的 Linux 引擎已经可用。已有后端镜像可以加载当前工作区后端代码，上一轮受 Windows Python ABI 限制的后端测试可以在容器中运行。

- 后端完整测试：175 passed，6 subtests passed，1 warning。
- 包含上一轮未能收集的 WebSocket 撤权、管线健康等测试；聚焦组合先验证36项通过。
- 实际应用 `create_app()` 加载成功，类型为 EnhancedASGITurboAPI。
- 对实际应用ASGI入口发起未登录请求，受保护Writer监控接口返回HTTP 200 + JSON业务码401，数据为空，符合该项目业务码包装契约。
- 未启动数据库相关lifespan，未连接现有MySQL、Redis或TDengine，不将这次结果称为完整服务启动或全链路集成验收。

## 测试环境

基础镜像：data-watch-backend:latest。
镜像ID：sha256:935b17f7aba6e7cf4464bbda8925452ac68c90eaba9e1b8b6fad65146cd31e1a。
Python：3.14.4 free-threading，Linux amd64。
临时容器只读挂载当前 backend/app 及 data_watch_shared；不是只测试镜像内的旧源码。
仅在临时容器内安装 pytest==9.1.1、pytest-asyncio==1.4.0；安装后断开网络，再运行测试。没有挂载现有数据库卷，没有替换现有后端容器或镜像。

测试命令（临时容器内）：

```text
TZ=Asia/Shanghai python3.14t -m pytest -p no:cacheprovider app/tests -q
```

首次未设置TZ时为174 passed / 1 failed，失败断言相差8小时。按现有 deploy/docker-compose.yml 对齐 TZ=Asia/Shanghai 后175项全部通过；没有修改源码或测试断言来规避该失败。

## 新确认的性能问题

初始解释器 `sys._is_gil_enabled()` 为False。导入SQLAlchemy的 `sqlalchemy.cyextension.collections` 时，Python报告该扩展未声明无GIL兼容，随后 `sys._is_gil_enabled()` 为True；完整应用加载后也确认这一状态。

因此，当前镜像虽然使用free-threading Python，不能据此认定整个后端实际保持无GIL运行。此次没有强制设置PYTHON_GIL=0，也没有更换生产依赖。后续应验证依赖兼容性及CPU性能，再决定修复方式。

## 仍未完成

- 真实Redis Lua/WAITAOF、Writer到TDengine、浏览器端到端延迟和稳态吞吐测试。
- 目标机部署镜像与本机镜像是否一致，尚未核实。
- receipt压缩迁移、多历史批次claim/lease、多进程扩容及部分监控/公平恢复调度项目，仍按原交付报告列为待办。

本轮未修改产品源码或现有容器运行配置。临时测试容器将在记录结果后移除。
