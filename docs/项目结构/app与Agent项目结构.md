# app 与 Agent 项目结构

> 范围：`app/` 与 `agent/`。`app` 是桌面控制台，`agent` 是本机进程管理与日志服务。`node_modules`、`target`、`.venv` 等依赖/构建目录不展开。

## app

```text
app
├── .vscode                 # app 推荐 VS Code 配置
│   └── extensions.json     # 推荐扩展
├── node_modules            # 前端依赖目录，按依赖处理，不展开
├── public                  # 静态资源
│   ├── tauri.svg           # Tauri 标识资源
│   └── vite.svg            # Vite 标识资源
├── src                     # React 前端源码
│   ├── assets              # 前端静态资源
│   │   └── react.svg       # React 标识资源
│   ├── components          # 页面通用组件
│   │   ├── ui              # 基础 UI 组件封装
│   │   │   ├── alert-dialog.tsx  # 弹窗确认组件
│   │   │   ├── badge.tsx         # 标签组件
│   │   │   ├── button.tsx        # 按钮组件
│   │   │   ├── card.tsx          # 卡片组件
│   │   │   ├── dialog.tsx        # 对话框组件
│   │   │   ├── input.tsx         # 输入框组件
│   │   │   ├── label.tsx         # 标签文本组件
│   │   │   ├── separator.tsx     # 分隔线组件
│   │   │   ├── skeleton.tsx      # 骨架屏组件
│   │   │   ├── sonner.tsx        # Toast 消息组件
│   │   │   ├── switch.tsx        # 开关组件
│   │   │   ├── table.tsx         # 表格组件
│   │   │   └── tabs.tsx          # 标签页组件
│   │   ├── AppLayout.tsx         # 应用主布局
│   │   ├── ConnectionStatusBar.tsx # 顶部连接状态栏
│   │   ├── LogPanel.tsx          # Agent 实时日志面板
│   │   ├── ModuleDbLogPanel.tsx  # 后端数据库日志面板
│   │   ├── ModuleSummaryCard.tsx # 模块指标卡片
│   │   ├── ThemeToggle.tsx       # 主题切换按钮
│   │   └── TrendChart.tsx        # 速率趋势图组件
│   ├── contexts            # React Context
│   │   ├── AuthContext.tsx       # 登录态、Token 和用户上下文
│   │   └── ThemeContext.tsx      # 主题上下文
│   ├── hooks               # 页面数据与状态 Hooks
│   │   ├── useChartColors.ts     # 图表颜色 Hook
│   │   ├── useConnectionHealth.ts # 连接健康检查 Hook
│   │   ├── useModuleDbLogs.ts    # 后端模块日志查询 Hook
│   │   ├── useModuleMetrics.ts   # collector/writer 指标 Hook
│   │   ├── useModuleRates.ts     # 采集/入库速率 Hook
│   │   ├── useModuleStatus.ts    # Agent 进程状态 Hook
│   │   └── useWebSocketLog.ts    # Agent 日志 WebSocket Hook
│   ├── lib                 # 客户端与工具层
│   │   ├── agent-client.ts       # 通过 Tauri invoke 调用本机 Agent
│   │   ├── backend-client.ts     # 调用后端业务 API
│   │   ├── mock-agent.ts         # Agent mock 数据与降级逻辑
│   │   └── utils.ts              # 通用工具函数
│   ├── pages               # 页面组件
│   │   ├── BootstrapPage.tsx     # 初始配置/引导页
│   │   ├── CollectorPage.tsx     # 采集模块监控页
│   │   ├── LoginPage.tsx         # 登录页
│   │   ├── OverviewPage.tsx      # 总览页
│   │   ├── SettingsPage.tsx      # 设置页
│   │   └── WriterPage.tsx        # 入库模块监控页
│   ├── types               # TypeScript 类型
│   │   └── index.ts              # app 共享类型定义
│   ├── App.tsx             # React 根组件
│   ├── index.css           # 全局样式
│   ├── main.tsx            # React 入口
│   └── vite-env.d.ts       # Vite 类型声明
├── src-tauri               # Tauri/Rust 桌面壳
│   ├── capabilities        # Tauri 权限能力配置
│   │   └── default.json    # 默认能力声明
│   ├── gen                 # Tauri 生成文件
│   │   └── schemas         # Tauri schema 文件
│   ├── icons               # 桌面应用图标资源
│   ├── src                 # Rust 源码
│   │   ├── http_proxy.rs   # Agent HTTP/WS 代理调用逻辑
│   │   ├── lib.rs          # Tauri 命令注册与应用初始化
│   │   ├── main.rs         # Rust 程序入口
│   │   └── tray.rs         # 系统托盘逻辑
│   ├── target              # Rust 构建产物，按构建输出处理
│   ├── build.rs            # Tauri build 脚本
│   ├── Cargo.lock          # Rust 依赖锁定文件
│   ├── Cargo.toml          # Rust 包与依赖配置
│   └── tauri.conf.json     # Tauri 应用配置
├── .env.development        # app 开发环境变量
├── components.json         # UI 组件生成/配置文件
├── index.html              # Vite HTML 入口
├── package.json            # app 前端依赖与脚本
├── pnpm-lock.yaml          # pnpm 锁文件
├── README.md               # app 说明
├── tsconfig.json           # TypeScript 配置
├── tsconfig.node.json      # Node/Vite TS 配置
└── vite.config.ts          # Vite 配置
```

## agent

```text
agent
├── .venv                   # Agent Python 虚拟环境，按依赖处理
├── app                     # Agent FastAPI 应用源码
│   ├── core                # Agent 核心基础能力
│   │   ├── __init__.py
│   │   ├── config.py              # 配置文件读写、DPAPI 加密字段处理
│   │   ├── deps.py                # 登录鉴权依赖
│   │   ├── exceptions.py          # 异常处理
│   │   └── security.py            # Token、密码、安全工具
│   ├── routers             # Agent 路由
│   │   ├── __init__.py
│   │   ├── admin.py               # 启停 collector/writer、更新配置等管理接口
│   │   ├── auth.py                # Agent 登录接口
│   │   ├── bootstrap.py           # 首次配置、连接测试、完成初始化接口
│   │   └── viewer.py              # 状态、健康、日志查看和日志 WebSocket 接口
│   ├── schemas             # Agent 请求/响应模型
│   │   ├── __init__.py
│   │   └── models.py              # Agent 通用 Pydantic 模型
│   ├── services            # Agent 服务层
│   │   ├── __init__.py
│   │   ├── health_checker.py      # MySQL/Redis/TDengine 健康检查
│   │   ├── log_manager.py         # collector/writer 日志 tail 管理
│   │   └── process_manager.py     # collector/writer 子进程管理
│   ├── __init__.py
│   └── factory.py          # FastAPI app 创建与生命周期管理
├── logs                    # Agent 管理的运行日志目录
├── service                 # Windows 服务安装辅助
│   └── install_service.py  # Agent Windows 服务安装脚本
├── config.toml             # Agent 本机实际配置
├── config.toml.example     # Agent 配置示例
├── main.py                 # Agent 启动入口
└── requirements.txt        # Agent Python 依赖列表
```
