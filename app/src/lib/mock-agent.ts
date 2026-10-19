/**
 * mock-agent.ts — 仅用于开发/测试环境。
 * 生产构建时由 agent-client / backend-client 的动态 import 分支消除，
 * 不会进入生产包。
 */

import type { AgentClient } from "./agent-client";
import type { BackendClient } from "./backend-client";
import type {
  ConnectionConfig,
  ConnectionHealth,
  DbConfig,
  LogLine,
  LogLevel,
  ModuleStatus,
  ModuleStatusValue,
} from "@/types";
import type { MenuTreeItem, RoleGrant, RoleListItem, UserListItem } from "@/types/system-management";
import type { DriverListItem } from "@/types/device-monitor";
import type { DistributedNode } from "@/types/distributed";
import { MOCK_DEVICES, MOCK_POINTS, mockDeviceRows, mockHistoryChart, mockHistoryMatrix, mockHistoryPoints, mockPointGroups, mockRealtimeItems } from "./mock-device-data";

// ─── 辅助 ─────────────────────────────────────────────────────────────────────

const delay = (ms = Math.random() * 500 + 300) =>
  new Promise<void>((r) => setTimeout(r, ms));

// ─── 可变内部状态 ─────────────────────────────────────────────────────────────

let _inBootstrap = true;
let _edgeBootstrap = {
  nodeUuid: "",
  nodeName: "",
  centerUrl: "",
  registered: false,
  registrationStatus: "not_registered",
  lastSyncError: "",
};

let _edgeLocalSettings = {
  max_offline_retention_days: 7,
  outbox_maintenance_seconds: 60,
};
const _processStatus: Record<string, ModuleStatusValue> = {
  collector: "running",
  ingest:    "running",
  writer:    "running",
  uploader:  "running",
  storage:   "running",
};

// ─── 监控页示例数据（仅开发用：确定性伪随机，按 5s 桶滑动，刷新不跳变）────────────

function mockNoise(seed: number) {
  const x = Math.sin(seed * 12.9898 + 78.233) * 43758.5453;
  return x - Math.floor(x);
}

function mockPad(n: number, w = 2) {
  return String(n).padStart(w, "0");
}

function mockHms(d: Date, ms = false) {
  const base = `${mockPad(d.getHours())}:${mockPad(d.getMinutes())}:${mockPad(d.getSeconds())}`;
  return ms ? `${base}.${mockPad(d.getMilliseconds(), 3)}` : base;
}

function mockLocalIso(d: Date) {
  return `${d.getFullYear()}-${mockPad(d.getMonth() + 1)}-${mockPad(d.getDate())}T${mockHms(d, true)}`;
}

/** 近 ~10 分钟速率序列：每 5s 一点，126 点 */
function mockRateSeries(key: number, base: number, vol: number) {
  const bucket = Math.floor(Date.now() / 5000);
  const series: { t: string; v: number }[] = [];
  // 从更早处起算的指数平滑随机游走（与概念稿一致的平滑度），只保留最后 126 点
  let v = base;
  for (let i = 200; i >= 0; i -= 1) {
    const k = bucket - i;
    const target = base + vol * 1.9 * Math.sin(k * 0.09 + key) + vol * 1.1 * Math.sin(k * 0.031 + key * 1.7);
    v += (target - v) * 0.32 + (mockNoise(k * 7 + key) - 0.5) * vol * 1.1;
    if (mockNoise(k * 13 + key) < 0.025) v -= vol * 2.2 * mockNoise(k + key);
    v = Math.max(0, v);
    if (i <= 125) series.push({ t: mockHms(new Date(k * 5000)), v: Math.round(v) });
  }
  return series;
}

// password 字段省略：模拟真实 Agent 行为（GET /admin/config 不返回密码）
let _config: ConnectionConfig = {
  redis:     { host: "127.0.0.1", port: 6379, user: "",     database: "0" },
  mysql:     { host: "127.0.0.1", port: 3306, user: "root", database: "device_watch" },
  tdengine:  { host: "127.0.0.1", port: 6041, user: "root", database: "device_data" },
  collector: { executable: "collector/data-watch-collector.exe", working_dir: "D:/data_watch_deploy/data-watch-collector" },
  writer:    { executable: "writer/data-watch-writer.exe",       working_dir: "D:/data_watch_deploy/data-watch-writer" },
};

// 内部存储的"真实"密码（模拟 Agent 内部状态；不会从 getConfig 返回）
const _passwords: Record<"redis" | "mysql" | "tdengine", string> = {
  redis:    "mock-redis-pass",
  mysql:    "mock-mysql-pass",
  tdengine: "mock-tdengine-pass",
};

// ─── mockAgent 模拟代理 ────────────────────────────────────────────────────────────

export const mockAgent: AgentClient = {

  async getBootstrapStatus() {
    await delay();
    return { in_bootstrap: _inBootstrap };
  },

  async getRuntimeConfig() {
    await delay();
    return { backend_base_url: "http://127.0.0.1:8000" };
  },

  async putBootstrapConfig(_cfg) {
    await delay();
    // 接受配置但不做校验（Bootstrap 流程由 page 状态机控制）
  },

  async postBootstrapTestConnection(_req) {
    await delay(800);
    return { success: true };
  },

  async postBootstrapFinish() {
    await delay();
    _inBootstrap = false;
  },

  async getEdgeBootstrapStatus() {
    await delay();
    return { ..._edgeBootstrap };
  },

  async registerEdge(req) {
    await delay(800);
    _edgeBootstrap = {
      nodeUuid: _edgeBootstrap.nodeUuid || "mock-edge-node-01",
      nodeName: req.node_name,
      centerUrl: req.center_url,
      registered: true,
      registrationStatus: "pending",
      lastSyncError: "",
    };
    return { nodeUuid: _edgeBootstrap.nodeUuid, status: "pending" };
  },

  async agentLogin(u, _p) {
    await delay();
    // 只要 username 非空即成功（mock 不验证密码）
    if (!u) throw { type: "unauthorized" as const };
    return { token: "mock-agent-jwt-dev", roles: ["R_ADMIN"] };
  },

  async getViewerStatus() {
    await delay();
    return { agent: "running" };
  },

  async getLocalDevices() {
    await delay(120);
    return {
      config: { config_revision: 12, checksum: "mock", read_timeout_ms: null, applied_at: Date.now() },
      fastest_point_scan_rate_ms: 100,
      devices: [
        { device_code: "QC01", device_type: "QC", protocol: "s7", endpoint: "10.0.0.11:102", scan_rate_ms: 100, config_version: 8, assignment_epoch: 2, enabled: 1, status: 1, message: "connected", status_updated_at: Date.now() },
        { device_code: "QC02", device_type: "QC", protocol: "s7", endpoint: "10.0.0.12:102", scan_rate_ms: 100, config_version: 8, assignment_epoch: 1, enabled: 1, status: 1, message: "connected", status_updated_at: Date.now() },
      ],
    };
  },

  async getLocalCollector() {
    await delay(100);
    return { name: "collector", status: "running", pid: 4820, started_at: "2026-08-01 08:00:00", uptime_seconds: 3600, memory_mb: 128, cpu_percent: 2.1, restart_count: 0 };
  },

  async getLocalOutbox() {
    await delay(100);
    return { counts: { pending: 3, sent: 42190 }, backlog_count: 3, oldest_backlog_at: Date.now() - 300, upload_session_id: "mock-session", last_seq: 42193, upload_interval_ms: 100, session_switch_pending: false, database_bytes: 2_621_440, dropped_records: 0, sent_records_total: 42190, disk_total_bytes: 512_000_000_000, disk_free_bytes: 220_000_000_000, disk_used_percent: 57.03, watermark: "normal" as const };
  },

  async getLocalUploader() {
    await delay(100);
    return { name: "uploader", status: "running", pid: 4932, started_at: "2026-08-01 08:00:00", uptime_seconds: 3600, memory_mb: 42, cpu_percent: 0.4, restart_count: 0, outbox: await this.getLocalOutbox() };
  },

  async getLocalStorage() {
    await delay(100);
    return {
      name: "storage",
      status: "running",
      pid: 4876,
      started_at: "2026-08-01 08:00:00",
      uptime_seconds: 3600,
      memory_mb: 36,
      cpu_percent: 0.3,
      restart_count: 0,
      storage: {
        ...(await this.getLocalOutbox()),
        writer_queue_depth: 0,
        writer_queue_capacity: 2048,
        commands_total: 84621,
        command_counts: { "frame.commit": 42193, "outbox.complete": 42190, "config.apply": 1 },
        errors_total: 0,
        commit_p99_ms: 2.4,
        frames_per_second: 10,
        points_per_second: 100,
        storage_uptime_seconds: 3600,
      },
    };
  },

  async getLocalSettings() {
    await delay(100);
    return {
      node_uuid: _edgeBootstrap.nodeUuid,
      node_name: _edgeBootstrap.nodeName,
      center_url: _edgeBootstrap.centerUrl,
      registered: _edgeBootstrap.registered,
      registration_status: _edgeBootstrap.registrationStatus,
      last_sync_error: _edgeBootstrap.lastSyncError,
      applied_revision: 12,
      ..._edgeLocalSettings,
    };
  },

  async updateLocalSettings(settings) {
    await delay(200);
    _edgeLocalSettings = { ...settings };
    return this.getLocalSettings();
  },
  async getLocalMonitorScope() {
    return (await mockBackend.deviceList("mock", { pageSize: 100 })).list.map(({ id, deviceCode, deviceType }) => ({ id, deviceCode, deviceType }));
  },
  async getLocalMonitorDevices(query) {
    return { ...(await mockBackend.deviceList("mock", query)), source: "local" as const };
  },

  async getLocalMonitorPoints(query) {
    return { ...(await mockBackend.pointList("mock", query)), source: "local" as const };
  },

  async getLocalRealtimeGroups(deviceCode) {
    return mockBackend.realtimePointGroups("mock", deviceCode);
  },

  async getLocalRealtime(deviceId, groupCode) {
    const result = await mockBackend.realtimeInit("mock", deviceId, groupCode);
    return { ...result, redisConnected: false, degraded: true };
  },

  async getLocalRealtimeLatest(deviceCode, groupCode) {
    const deviceId = MOCK_DEVICES.find((device) => device.deviceCode === deviceCode)?.id ?? 1;
    const result = await mockBackend.realtimeInit("mock", deviceId, groupCode);
    return {
      values: Object.fromEntries(result.data.map((point) => [String(point.pointId), {
        v: point.value,
        t: Date.now(),
        q: point.quality ?? 192,
      }])),
      deviceOffline: false,
    };
  },

  async getLocalHistoryDevices() {
    return mockBackend.historyDevices("mock");
  },

  async getLocalHistoryDeviceTypes() {
    return mockBackend.historyDeviceTypes("mock");
  },

  async getLocalHistoryPoints(deviceId) {
    return mockBackend.historyPoints("mock", deviceId);
  },

  async getLocalHistoryChart(query) {
    return mockBackend.historyChart("mock", query);
  },

  async getLocalHistoryMatrix(query) {
    return mockBackend.historyRawMatrix("mock", query);
  },
  async rotateEdgeIdentity() {
    await delay(400);
    return { keyVersion: 2, uploaderRestarted: true };
  },

  async getConnectionHealth() {
    await delay(300);
    const checkedAt = new Date().toISOString();
    return {
      redis:    { status: "connected", latencyMs: 3, checkedAt },
      mysql:    { status: "connected", latencyMs: 6, checkedAt },
      tdengine: { status: "connected", latencyMs: 9, checkedAt },
    } satisfies ConnectionHealth;
  },

  async getProcessMetrics(name) {
    await delay();
    const status = _processStatus[name] ?? "unknown";
    const running  = status === "running";
    return {
      name,
      status,
      pid:            running ? ({ collector: 4820, ingest: 18342, writer: 4932 }[name as string] ?? 5010) : null,
      uptime_seconds: running ? ({ collector: 268512, ingest: 186190, writer: 268470 }[name as string] ?? 13338) : 0,
      memory_mb:      running ? ({ collector: 278, ingest: 315.3, writer: 419 }[name as string] ?? 142) : 0,
      cpu_percent:    running ? ({ collector: 10.7, ingest: 17.9, writer: 19.6 }[name as string] ?? 1.1) : 0,
      started_at:     running ? "2026-05-25T06:18:00Z" : null,
      restart_count:  0,
    } satisfies ModuleStatus;
  },

  async getProcessStatus(name) {
    await delay();
    return { status: _processStatus[name] ?? "unknown" };
  },

  async startProcess(name) {
    await delay(800);
    _processStatus[name] = "running";
  },

  async stopProcess(name) {
    await delay(800);
    _processStatus[name] = "stopped";
  },

  async restartProcess(name) {
    _processStatus[name] = "unknown";   // 过渡状态
    await delay(1200);
    _processStatus[name] = "running";
  },

  async getConfig() {
    await delay();
    return { ..._config };            // 返回副本，防止直接篡改
  },

  async putConfig(cfg) {
    await delay();
    // partial update 语义：仅当请求体包含 password 时才覆盖；缺失则保留 _passwords[db]
    (["redis", "mysql", "tdengine"] as const).forEach((db) => {
      const section = cfg[db];
      if (!section) return;
      if ("password" in section && section.password !== undefined) {
        _passwords[db] = section.password;
      }
      // 合并非密码字段（剔除 password 后浅合并到 _config[db]）
      const rest: Partial<DbConfig> = { ...section };
      delete rest.password;
      _config[db] = { ..._config[db], ...rest };
    });
    if (cfg.collector) _config.collector = { ..._config.collector, ...cfg.collector };
    if (cfg.writer)    _config.writer    = { ..._config.writer,    ...cfg.writer    };
  },

  async checkHealth(_target, _config) {
    await delay(600);
    return { success: true };
  },

  async listDrivers() {
    await delay();
    // 仅开发用：覆盖 已加载/未使用/未注册/加载失败/未安装/包缺失 等状态
    const d = (id: string, name: string, description: string, version: string, rw: string, source: string, status: string, extra: Record<string, unknown> = {}) => ({
      id, protocol: id, name, description, version, sha256: "abc", capabilities: ["poll", "read"], readWriteType: rw, aliases: [id, name.toLowerCase().replace(/[\s-]+/g, "_")], source,
      protected: source === "bundled", status, error: null, ...extra,
    });
    return [
      d("siemens_s7", "Siemens S7", "西门子 S7 TCP 驱动（S7-300/400/1200/1500）", "2.4.1", "读写", "bundled", "loaded"),
      d("opc_ua", "OPC UA", "OPC UA 客户端驱动，支持订阅与轮询", "1.9.3", "读写", "official", "loaded"),
      d("modbus_tcp", "Modbus TCP", "Modbus TCP 通用驱动", "2.1.0", "读写", "bundled", "loaded"),
      d("opc_da", "OPC DA", "OPC DA 驱动（经 DCOM 网关桥接）", "1.3.7", "只读", "installed", "loaded"),
      d("ge_srtp", "GE SRTP", "GE PLC 通讯驱动（RX3i / 90-30）", "1.2.0", "只读", "official", "loaded"),
      d("modbus_rtu", "Modbus RTU", "串口 Modbus RTU 驱动（RS-485）", "1.0.4", "读写", "installed", "loaded"),
      d("omron_fins", "", "", "0.9.2", "只读", "installed", "loaded", { protocol: "Omron FINS", updated_at: "2026-10-16 17:30:02" }),
      d("ethernet_ip", "EtherNet/IP", "罗克韦尔 CIP 驱动（ControlLogix）", "1.1.0", "读写", "installed", "load_failed", { runtime_status: "load_failed", error: "manifest ABI 版本 3 与 Collector ABI 4 不兼容，已回滚至旧包" }),
      d("mitsubishi_mc", "Mitsubishi MC", "三菱 MC 协议 3E 帧驱动", "1.4.2", "读写", "official", "not_installed", { install_status: "not_installed" }),
      d("bacnet_ip", "BACnet/IP", "冷箱区空调 BACnet/IP 驱动", "0.8.0", "只读", "official", "missing", { install_status: "missing", error: "驱动目录缺少 bacnet-0.8.0，Collector 无法加载" }),
    ];
  },

  async previewDriver(input) {
    return { driver: { id: input.restore_id || "demo", name: "Mock Driver", version: "1.0.0", sha256: "abc" } };
  },

  async restoreDriver(driverId) {
    await delay(800);
    return { driver: { id: driverId, protocol: driverId, name: driverId,
      version: "1.0.0", description: "Mock restored", sha256: "abc", status: "installed", source: "official",
      install_status: "installed", runtime_status: "collector_stopped" } };
  },

  async installDriver(_packageBase64) {
    await delay(800);
    return {
      driver: {
        id: "modbus_tcp",
        protocol: "modbus_tcp",
        name: "Modbus TCP",
        description: "Mock 安装成功",
        version: "1.0.0",
        sha256: "abc",
        status: "loaded",
        source: "installed",
      },
    };
  },

  async uninstallDriver(driverId) {
    await delay(500);
    return { driver: { id: driverId, status: "uninstalled" } };
  },
};

// ─── mockBackend 模拟后端 ──────────────────────────────────────────────────────────

let _mockDebugUntil = 0;

const MOCK_LOG_SAMPLES = [
  { level: "ERROR", type: "collect", source: "collector", serviceName: "data-watch-collector", deviceCode: "QC01", content: "S7 读取超时：DB100.DBD24 连续 3 次超时（timeout=500ms），驱动进入重连", detail: { driver: "S7", host: "10.20.3.11", area: "DB100", offset: 24, retries: 3, timeoutMs: 500 }, exceptionStack: "s7: read DB100.DBD24: i/o timeout\n  at driver/s7.(*Conn).ReadArea (conn.go:214)" },
  { level: "WARN", type: "storage", source: "writer", serviceName: "data-watch-writer", content: "TDengine 写入批次耗时 1,284 ms，超过阈值 1,000 ms（batch_count=200）", detail: { stable: "sdqc", rows: 200, costMs: 1284, thresholdMs: 1000 } },
  { level: "INFO", type: "operation", source: "web-admin", serviceName: "data-watch-server", deviceCode: "RTG07", operatorName: "admin", moduleName: "点位管理", actionName: "update", resultStatus: "success", content: "修改点位 RTG07.Hoist.Speed 采集周期 200ms → 100ms", detail: { module: "点位管理", action: "update", durationMs: 42 } },
  { level: "INFO", type: "auth", source: "auth", serviceName: "data-watch-server", operatorName: "zhangwei", content: "用户 zhangwei 登录成功（192.168.10.23）", detail: { ip: "192.168.10.23", client: "desktop 2.4.1" } },
  { level: "CRITICAL", type: "alarm", source: "alarm-engine", serviceName: "data-watch-server", deviceCode: "ASC03", content: "ASC03 大车急停触发，PLC 状态字 0x8004，已推送告警", detail: { device: "ASC03", protocol: "OPC UA", node: "ns=3;s=ASC03.Gantry.EStop", value: true, statusWord: "0x8004" } },
  { level: "WARN", type: "collect", source: "collector", serviceName: "data-watch-collector", deviceCode: "AGV-118", content: "Modbus TCP 重连：192.168.31.118:502 第 2 次", detail: { driver: "Modbus TCP", host: "192.168.31.118", port: 502, attempt: 2 } },
  { level: "DEBUG", type: "collect", source: "collector", serviceName: "data-watch-collector", deviceCode: "QC01", content: "OPC UA 订阅回调 batch=64 queue=3 latency=18ms", detail: { subscription: 12, batch: 64, queue: 3 } },
  { level: "INFO", type: "system", source: "ingest", serviceName: "data-watch-ingest", content: "Redis Stream rt:stream:qc:main:0 消费组 ingest-g1 rebalance 完成", detail: { stream: "rt:stream:qc:main:0", group: "ingest-g1", consumers: 4 } },
  { level: "INFO", type: "storage", source: "writer", serviceName: "data-watch-writer", content: "TDengine 批量写入完成 rows=200 cost=86ms", detail: { stable: "sdqc", rows: 200, costMs: 86 } },
  { level: "INFO", type: "security", source: "auth", serviceName: "data-watch-server", operatorName: "admin", content: "用户 liuyang 的临时密码已重置", detail: { target: "liuyang" } },
  { level: "INFO", type: "frontend", source: "desktop", serviceName: "data-watch-app", operatorName: "lina", content: "实时监控页面订阅 128 个点位", detail: { points: 128 } },
  { level: "INFO", type: "system", source: "backend", serviceName: "data-watch-server", content: "服务运行正常，周期任务执行完成", detail: { job: "cleanup-check" } },
] as const;

const MOCK_SYSTEM_LOGS = Array.from({ length: 36 }, (_, index) => {
  const sample = MOCK_LOG_SAMPLES[index % MOCK_LOG_SAMPLES.length] as (typeof MOCK_LOG_SAMPLES)[number] & { deviceCode?: string; operatorName?: string; moduleName?: string; actionName?: string; resultStatus?: string; exceptionStack?: string };
  return {
    id: 1000 - index,
    kind: sample.type === "operation" ? "operation" as const : "system" as const,
    time: new Date(Date.now() - index * 83_000 - 12_000).toISOString(),
    level: sample.level,
    type: sample.type,
    source: sample.source,
    serviceName: sample.serviceName,
    moduleName: sample.moduleName,
    actionName: sample.actionName,
    operatorName: sample.operatorName,
    deviceCode: sample.deviceCode,
    content: sample.content,
    resultStatus: sample.resultStatus,
    durationMs: 18 + index * 3,
    requestId: `req-${(0x7f3a9c21 - index * 977).toString(16)}`,
    traceId: index % 4 === 0 ? `trace-${1000 - index}` : undefined,
    detail: { ...sample.detail },
    exceptionStack: sample.exceptionStack,
  };
});

let MOCK_USERS: UserListItem[] = [
  { id: 1, avatar: "", status: "1", userName: "admin", userGender: "male", nickName: "系统管理员", userPhone: "13800000001", userEmail: "admin@example.com", userRoles: ["R_SUPER"], createBy: "system", createTime: "2026-01-01 08:00:00", updateBy: "system", updateTime: "2026-07-17 10:00:00" },
  { id: 2, avatar: "", status: "1", userName: "operator", userGender: "female", nickName: "值班员", userPhone: "13800000002", userEmail: "operator@example.com", userRoles: ["R_OPERATOR"], createBy: "admin", createTime: "2026-03-12 09:30:00", updateBy: "admin", updateTime: "2026-07-16 16:20:00" },
  { id: 3, avatar: "", status: "2", userName: "viewer", userGender: "unknown", nickName: "访客账号", userPhone: "", userEmail: "viewer@example.com", userRoles: ["R_USER"], createBy: "admin", createTime: "2026-05-20 14:12:00", updateBy: "admin", updateTime: "2026-07-10 11:40:00" },
  { id: 4, avatar: "", status: "1", userName: "zhangwei", userGender: "male", nickName: "张伟 · 岸桥班组", userPhone: "13916620418", userEmail: "zhangwei@port-ops.cn", userRoles: ["R_OPERATOR"], createBy: "admin", createTime: "2026-03-18 10:06:41", updateBy: "admin", updateTime: "2026-03-18 10:06:41" },
  { id: 5, avatar: "", status: "1", userName: "lina", userGender: "female", nickName: "李娜", userPhone: "13761205537", userEmail: "lina@port-ops.cn", userRoles: ["R_ENGINEER", "R_OPERATOR"], createBy: "admin", createTime: "2026-03-21 15:42:09", updateBy: "admin", updateTime: "2026-03-21 15:42:09" },
  { id: 6, avatar: "", status: "1", userName: "wangqiang", userGender: "male", nickName: "王强 · PLC 工程师", userPhone: "13817749062", userEmail: "wangqiang@port-ops.cn", userRoles: ["R_ENGINEER"], createBy: "admin", createTime: "2026-04-02 09:15:27", updateBy: "admin", updateTime: "2026-04-02 09:15:27" },
  { id: 7, avatar: "", status: "1", userName: "chenjing", userGender: "female", nickName: "陈静", userPhone: "13524418890", userEmail: "chenjing@port-ops.cn", userRoles: ["R_ADMIN"], createBy: "admin", createTime: "2026-04-08 11:20:53", updateBy: "admin", updateTime: "2026-04-08 11:20:53" },
  { id: 8, avatar: "", status: "1", userName: "liuyang", userGender: "male", nickName: "刘洋 · RTG 值班", userPhone: "13661930275", userEmail: "liuyang@port-ops.cn", userRoles: ["R_OPERATOR"], createBy: "admin", createTime: "2026-04-15 08:47:12", updateBy: "admin", updateTime: "2026-04-15 08:47:12" },
  { id: 9, avatar: "", status: "1", userName: "zhaomin", userGender: "female", nickName: "赵敏", userPhone: "13901872264", userEmail: "zhaomin@port-ops.cn", userRoles: ["R_USER"], createBy: "admin", createTime: "2026-05-06 16:03:38", updateBy: "admin", updateTime: "2026-05-06 16:03:38" },
  { id: 10, avatar: "", status: "2", userName: "sunhao", userGender: "male", nickName: "孙浩", userPhone: "13788051943", userEmail: "sunhao@port-ops.cn", userRoles: ["R_ENGINEER"], createBy: "admin", createTime: "2026-05-28 13:29:05", updateBy: "admin", updateTime: "2026-05-28 13:29:05" },
  { id: 11, avatar: "", status: "1", userName: "rtg_shift_b", userGender: "unknown", nickName: "RTG 夜班", userPhone: "13671126608", userEmail: "", userRoles: ["R_OPERATOR"], createBy: "admin", createTime: "2026-06-11 20:10:44", updateBy: "admin", updateTime: "2026-06-11 20:10:44" },
  { id: 12, avatar: "", status: "1", userName: "audit_view", userGender: "unknown", nickName: "调度只读", userPhone: "", userEmail: "dispatch@port-ops.cn", userRoles: ["R_USER"], createBy: "admin", createTime: "2026-07-03 09:58:16", updateBy: "admin", updateTime: "2026-07-03 09:58:16" },
];

let MOCK_ROLES: RoleListItem[] = [
  { roleId: 1, roleName: "超级管理员", roleCode: "R_SUPER", description: "拥有全部系统权限", enabled: true, createTime: "2026-01-01 08:00:00" },
  { roleId: 2, roleName: "运维人员", roleCode: "R_OPERATOR", description: "设备运维与日志查看", enabled: true, createTime: "2026-02-10 10:00:00" },
  { roleId: 3, roleName: "普通用户", roleCode: "R_USER", description: "只读访问", enabled: true, createTime: "2026-02-10 10:05:00" },
  { roleId: 4, roleName: "系统管理员", roleCode: "R_ADMIN", description: "用户、角色与系统配置管理", enabled: true, createTime: "2026-02-18 09:12:30" },
  { roleId: 5, roleName: "设备工程师", roleCode: "R_ENGINEER", description: "驱动、设备与点位配置", enabled: true, createTime: "2026-03-05 14:26:08" },
  { roleId: 6, roleName: "审计员", roleCode: "R_AUDIT", description: "系统日志与操作审计（只读）", enabled: false, createTime: "2026-06-22 10:41:55" },
];

let MOCK_MENUS: MenuTreeItem[] = [
  { id: 10, path: "/dashboard", name: "Dashboard", component: "", menuType: "directory", sort: 1, enabled: true, meta: { title: "仪表盘", icon: "LayoutDashboard", authList: [] }, children: [
    { id: 11, parentId: 10, path: "workbench", name: "Workbench", component: "/dashboard/workbench", menuType: "menu", sort: 1, enabled: true, meta: { title: "工作台", icon: "BriefcaseBusiness", keepAlive: true, authList: [{ id: 101, title: "查看", authMark: "view", permissionCode: "dashboard:workbench:view", permissionType: "action", enabled: true, sort: 1 }] } },
    { id: 12, parentId: 10, path: "console", name: "Console", component: "/dashboard/console", menuType: "menu", sort: 2, enabled: true, meta: { title: "控制台", icon: "Gauge", authList: [{ id: 102, title: "查看", authMark: "view", permissionCode: "dashboard:console:view", permissionType: "action", enabled: true, sort: 1 }, { id: 103, title: "重启服务", authMark: "restart", permissionCode: "dashboard:console:restart", permissionType: "action", enabled: true, sort: 2 }] } },
  ] },
  { id: 40, path: "/collection", name: "Collection", component: "", menuType: "directory", sort: 2, enabled: true, meta: { title: "数据采集", icon: "Database", authList: [] }, children: [
    { id: 41, parentId: 40, path: "collector", name: "Collector", component: "/collection/collector", menuType: "menu", sort: 1, enabled: true, meta: { title: "采集监控", icon: "Activity", authList: [] } },
    { id: 42, parentId: 40, path: "ingest", name: "Ingest", component: "/collection/ingest", menuType: "menu", sort: 2, enabled: false, meta: { title: "汇聚监控", icon: "Network", authList: [] } },
    { id: 43, parentId: 40, path: "writer", name: "Writer", component: "/collection/writer", menuType: "menu", sort: 3, enabled: true, meta: { title: "入库监控", icon: "HardDriveDownload", authList: [] } },
  ] },
  { id: 30, path: "/device-monitor", name: "DeviceMonitor", component: "", menuType: "directory", sort: 3, enabled: true, meta: { title: "设备监控", icon: "MonitorCog", authList: [] }, children: [
    { id: 31, parentId: 30, path: "monitor", name: "DeviceMonitorCard", component: "/device_monitor/monitor/index", menuType: "menu", sort: 1, enabled: true, meta: { title: "设备通讯", icon: "Cable", authList: [] } },
    { id: 32, parentId: 30, path: "driver", name: "DriverManage", component: "/device_monitor/driver/index", menuType: "menu", sort: 2, enabled: true, meta: { title: "驱动管理", icon: "Puzzle", authList: [{ id: 321, title: "安装", authMark: "install", permissionCode: "device:driver:install", permissionType: "action", enabled: true, sort: 1 }, { id: 322, title: "启停", authMark: "toggle", permissionCode: "device:driver:toggle", permissionType: "action", enabled: true, sort: 2 }] } },
    { id: 33, parentId: 30, path: "device", name: "DeviceManage", component: "/device_monitor/device/index", menuType: "menu", sort: 3, enabled: true, meta: { title: "设备管理", icon: "Cpu", keepAlive: true, authList: [{ id: 331, title: "新增", authMark: "add", permissionCode: "device:device:create", permissionType: "action", enabled: true, sort: 1 }, { id: 332, title: "编辑", authMark: "edit", permissionCode: "device:device:update", permissionType: "action", enabled: true, sort: 2 }, { id: 333, title: "删除", authMark: "delete", permissionCode: "device:device:delete", permissionType: "action", enabled: true, sort: 3 }, { id: 334, title: "导入", authMark: "import", permissionCode: "device:device:import", permissionType: "action", enabled: true, sort: 4 }] } },
    { id: 34, parentId: 30, path: "point", name: "PointManage", component: "/device_monitor/point/index", menuType: "menu", sort: 4, enabled: true, meta: { title: "点位管理", icon: "MapPin", authList: [{ id: 341, title: "新增", authMark: "add", permissionCode: "device:point:create", permissionType: "action", enabled: true, sort: 1 }, { id: 342, title: "导入", authMark: "import", permissionCode: "device:point:import", permissionType: "action", enabled: true, sort: 2 }, { id: 343, title: "导出", authMark: "export", permissionCode: "device:point:export", permissionType: "action", enabled: true, sort: 3 }] } },
    { id: 35, parentId: 30, path: "realtime", name: "RealtimeMonitor", component: "/device_monitor/realtime/index", menuType: "menu", sort: 5, enabled: true, meta: { title: "实时监控", icon: "Radio", authList: [] } },
    { id: 36, parentId: 30, path: "history", name: "HistoryData", component: "/device_monitor/history/index", menuType: "menu", sort: 6, enabled: true, meta: { title: "历史数据", icon: "History", authList: [{ id: 361, title: "导出", authMark: "export", permissionCode: "device:history:export", permissionType: "action", enabled: true, sort: 1 }] } },
  ] },
  { id: 50, path: "/distributed", name: "Distributed", component: "", menuType: "directory", sort: 4, enabled: true, meta: { title: "分布式", icon: "Network", authList: [] }, children: [
    { id: 51, parentId: 50, path: "nodes", name: "DistributedNodes", component: "/distributed/nodes", menuType: "menu", sort: 1, enabled: true, meta: { title: "边缘节点", icon: "Network", authList: [{ id: 511, title: "审批", authMark: "approve", permissionCode: "distributed:node:approve", permissionType: "action", enabled: true, sort: 1 }, { id: 512, title: "下发配置", authMark: "assign", permissionCode: "distributed:node:assign", permissionType: "action", enabled: true, sort: 2 }] } },
  ] },
  { id: 20, path: "/system", name: "System", component: "", menuType: "directory", sort: 9, enabled: true, meta: { title: "系统管理", icon: "Settings2", authList: [] }, children: [
    { id: 37, parentId: 20, path: "log", name: "SystemLog", component: "/device_monitor/log/index", menuType: "menu", sort: 1, enabled: true, meta: { title: "系统日志", icon: "FileText", authList: [{ id: 371, title: "导出", authMark: "export", permissionCode: "system:log:export", permissionType: "action", enabled: true, sort: 1 }] } },
    { id: 21, parentId: 20, path: "users", name: "Users", component: "/system/user/index", menuType: "menu", sort: 2, enabled: true, meta: { title: "用户管理", icon: "Users", authList: [{ id: 201, title: "新增", authMark: "add", permissionCode: "system:user:create", permissionType: "action", enabled: true, sort: 1 }, { id: 202, title: "编辑", authMark: "edit", permissionCode: "system:user:update", permissionType: "action", enabled: true, sort: 2 }, { id: 203, title: "删除", authMark: "delete", permissionCode: "system:user:delete", permissionType: "action", enabled: true, sort: 3 }, { id: 204, title: "重置密码", authMark: "reset", permissionCode: "system:user:reset-password", permissionType: "action", enabled: true, sort: 4 }] } },
    { id: 22, parentId: 20, path: "roles", name: "Roles", component: "/system/role/index", menuType: "menu", sort: 3, enabled: true, meta: { title: "角色管理", icon: "Shield", authList: [{ id: 221, title: "新增", authMark: "add", permissionCode: "system:role:create", permissionType: "action", enabled: true, sort: 1 }, { id: 222, title: "编辑", authMark: "edit", permissionCode: "system:role:update", permissionType: "action", enabled: true, sort: 2 }, { id: 223, title: "菜单权限", authMark: "grant", permissionCode: "system:role:grant", permissionType: "action", enabled: true, sort: 3 }] } },
    { id: 23, parentId: 20, path: "menu", name: "Menus", component: "/system/menu/index", menuType: "menu", sort: 4, enabled: true, meta: { title: "菜单管理", icon: "Menu", authList: [{ id: 231, title: "新增", authMark: "add", permissionCode: "system:menu:create", permissionType: "action", enabled: true, sort: 1 }, { id: 232, title: "编辑", authMark: "edit", permissionCode: "system:menu:update", permissionType: "action", enabled: true, sort: 2 }, { id: 233, title: "删除", authMark: "delete", permissionCode: "system:menu:delete", permissionType: "action", enabled: true, sort: 3 }] } },
  ] },
];

const MOCK_ROLE_GRANTS = new Map<number, RoleGrant>([[1, { menuIds: [10, 11, 12, 40, 41, 42, 43, 30, 31, 32, 33, 34, 35, 36, 50, 51, 20, 37, 21, 22, 23], permissionIds: [101, 102, 103, 321, 322, 331, 332, 333, 334, 341, 342, 343, 361, 511, 512, 371, 201, 202, 203, 204, 221, 222, 223, 231, 232, 233], dataScopes: [] }], [2, { menuIds: [10, 11, 12, 40, 41, 42, 43, 30, 31, 33, 34, 35, 36, 20, 37], permissionIds: [101, 102, 341, 343, 361, 371], dataScopes: [] }], [3, { menuIds: [10, 11, 30, 35, 36], permissionIds: [101], dataScopes: [] }], [4, { menuIds: [10, 11, 12, 20, 37, 21, 22, 23, 50, 51], permissionIds: [101, 102, 103, 201, 202, 203, 204, 221, 222, 223, 231, 232, 233, 371], dataScopes: [] }], [5, { menuIds: [10, 11, 30, 31, 32, 33, 34, 35, 36, 40, 41], permissionIds: [101, 321, 322, 331, 332, 333, 334, 341, 342, 343, 361], dataScopes: [] }], [6, { menuIds: [20, 37], permissionIds: [371], dataScopes: [] }]]);

const MOCK_DISTRIBUTED_NODES: DistributedNode[] = [
  {
    nodeUuid: "3911959d-3e0d-45a4-880a-d3c7df3a2d65",
    nodeName: "一号产线边缘机",
    status: "approved",
    configVersion: 12,
    appliedRevision: 12,
    outboxBacklog: 3,
    outboxDatabaseBytes: 2_621_440,
    outboxDiskUsedPercent: 57.03,
    outboxWatermark: "normal",
    outboxDroppedRecords: 0,
    keyVersion: 1,
    collectorStatus: "running",
    storageStatus: "running",
    uploaderStatus: "running",
    storageQueueDepth: 0,
    storageCommitP99Ms: 2.4,
    storageErrorsTotal: 0,
    clockSkewMs: 120,
    clockAnomaly: false,
    assignmentCount: 2,
    readTimeoutMs: null,
    lastSeenAt: new Date().toISOString(),
    createdAt: new Date(Date.now() - 86_400_000).toISOString(),
    updatedAt: new Date().toISOString(),
  },
  { nodeUuid: "b21d5e08-93fa-4b67-8c1d-5f2e7a9c0d44", nodeName: "东区岸桥边缘-02", status: "approved", configVersion: 42, appliedRevision: 41, outboxBacklog: 1206, outboxDatabaseBytes: 2469888, outboxDiskUsedPercent: 41.6, outboxWatermark: "warning", outboxDroppedRecords: 0, keyVersion: 1, collectorStatus: "running", storageStatus: "running", uploaderStatus: "unknown", storageQueueDepth: 0, storageCommitP99Ms: 2.1, storageErrorsTotal: 0, clockSkewMs: 80, clockAnomaly: false, assignmentCount: 16, readTimeoutMs: 1500, lastSeenAt: new Date(Date.now() - 5000).toISOString(), createdAt: new Date(Date.now() - 86_400_000 * 30).toISOString(), updatedAt: new Date().toISOString() },
  { nodeUuid: "0c8e4f6a-21b7-4d93-a5e0-7b3c9d1f2e65", nodeName: "堆场 RTG 边缘-A", status: "approved", configVersion: 42, appliedRevision: 42, outboxBacklog: 38, outboxDatabaseBytes: 77824, outboxDiskUsedPercent: 2.1, outboxWatermark: "normal", outboxDroppedRecords: 0, keyVersion: 1, collectorStatus: "running", storageStatus: "running", uploaderStatus: "running", storageQueueDepth: 0, storageCommitP99Ms: 2.1, storageErrorsTotal: 0, clockSkewMs: 80, clockAnomaly: false, assignmentCount: 24, readTimeoutMs: null, lastSeenAt: new Date(Date.now() - 3000).toISOString(), createdAt: new Date(Date.now() - 86_400_000 * 30).toISOString(), updatedAt: new Date().toISOString() },
  { nodeUuid: "e5a7b3c1-6d2f-4e98-b0a4-3c1e8f7d9b02", nodeName: "自动化堆场 ASC 边缘", status: "approved", configVersion: 42, appliedRevision: 42, outboxBacklog: 0, outboxDatabaseBytes: 0, outboxDiskUsedPercent: 5.8, outboxWatermark: "normal", outboxDroppedRecords: 0, keyVersion: 1, collectorStatus: "running", storageStatus: "running", uploaderStatus: "running", storageQueueDepth: 0, storageCommitP99Ms: 2.1, storageErrorsTotal: 0, clockSkewMs: 4200, clockAnomaly: true, assignmentCount: 32, readTimeoutMs: 2000, lastSeenAt: new Date(Date.now() - 8000).toISOString(), createdAt: new Date(Date.now() - 86_400_000 * 30).toISOString(), updatedAt: new Date().toISOString() },
  { nodeUuid: "9d4b2e7f-0a3c-4f15-8e6d-2b9a5c7e1f83", nodeName: "AGV 调度边缘", status: "approved", configVersion: 42, appliedRevision: 40, outboxBacklog: 0, outboxDatabaseBytes: 0, outboxDiskUsedPercent: 0.4, outboxWatermark: "normal", outboxDroppedRecords: 0, keyVersion: 1, collectorStatus: "stopped", storageStatus: "stopped", uploaderStatus: "stopped", storageQueueDepth: 0, storageCommitP99Ms: 2.1, storageErrorsTotal: 0, clockSkewMs: 80, clockAnomaly: false, assignmentCount: 12, readTimeoutMs: null, lastSeenAt: new Date(Date.now() - 1560000).toISOString(), createdAt: new Date(Date.now() - 86_400_000 * 30).toISOString(), updatedAt: new Date().toISOString() },
  { nodeUuid: "3a6f1c9e-7b52-4d08-9f3a-6e0d4b2c8a17", nodeName: "闸口边缘节点", status: "pending", configVersion: 0, appliedRevision: 0, outboxBacklog: 0, outboxDatabaseBytes: 0, outboxDiskUsedPercent: 0, outboxWatermark: "normal", outboxDroppedRecords: 0, keyVersion: 1, collectorStatus: "unknown", storageStatus: "unknown", uploaderStatus: "unknown", storageQueueDepth: 0, storageCommitP99Ms: 2.1, storageErrorsTotal: 0, clockSkewMs: 80, clockAnomaly: false, assignmentCount: 0, readTimeoutMs: null, lastSeenAt: null, createdAt: new Date(Date.now() - 86_400_000 * 30).toISOString(), updatedAt: new Date().toISOString() },
];

const MOCK_NODE_ASSIGNMENTS = new Map<string, number[]>([
  ["3911959d-3e0d-45a4-880a-d3c7df3a2d65", [1, 2]],
]);

export const mockBackend: BackendClient = {
  async login(u, _p) {
    await delay();
    if (!u) throw { type: "unauthorized" as const };
    return {
      token:        "mock-backend-token-dev",
      refreshToken: "mock-refresh-dev",
    };
  },
  async logList(_token, query) {
    await delay();
    const pool: [string, string, string][] = query.serviceName === "writer"
      ? [
          ["INFO", "writer", "批量写入完成，周期任务执行正常"], ["INFO", "writer.td", "TDengine 连接池就绪 size=8"],
          ["WARN", "writer.td", "TDengine 写入耗时偏高 elapsed=820ms"], ["INFO", "writer.redis", "消费组 writer 位点已同步"],
          ["WARN", "writer.redis", "pending drain claimed=3 stream=rt:stream:qc:g001:0"], ["ERROR", "writer.td", "tdengine write failed，已进入重试"],
          ["INFO", "writer", "配置版本更新 config_version=8"], ["DEBUG", "writer", "心跳上报完成 uptime=268470s"],
        ]
      : [
          ["INFO", "collector", "服务运行正常，周期任务执行完成"], ["WARN", "collector", "采集周期出现短时抖动"],
          ["INFO", "collector.s7", "QC101 连接建立 10.0.0.11:102"], ["WARN", "collector.opcua", "ASC0092 会话超时，已自动重连"],
          ["ERROR", "collector.modbus", "AGV012 读超时，连续 3 次失败，标记离线"], ["INFO", "collector.redis", "Redis Stream 连接恢复"],
          ["INFO", "collector", "配置版本更新 config_version=8，重新分配采集任务"], ["DEBUG", "collector", "心跳上报完成 uptime=268512s"],
        ];
    let t = Date.now() - 40_000;
    return Array.from({ length: 34 }, (_, i) => {
      const [level, source, content] = pool[Math.floor(mockNoise(i + 3) * pool.length)];
      const d = new Date(t);
      t -= 60_000 * (1 + mockNoise(i + 11) * 14);
      return { id: i + 1, time: `${d.getFullYear()}-${mockPad(d.getMonth() + 1)}-${mockPad(d.getDate())} ${mockHms(d)}`, level, source, content };
    });
  },
  async collectorMetrics(_token) {
    await delay();
    const rows: [string, string, string, number, number, "online" | "unstable" | "offline"][] = [
      ["QC101", "桥吊一组", "S7", 100, 426, "online"], ["QC102", "桥吊一组", "S7", 100, 422, "online"], ["QC103", "桥吊一组", "S7", 100, 422, "online"],
      ["QC107", "桥吊二组", "S7", 100, 422, "unstable"], ["QC110", "桥吊二组", "S7", 100, 422, "online"], ["QC115", "桥吊三组", "S7", 100, 422, "offline"],
      ["ASC0011", "轨道吊一组", "OPC UA", 200, 64, "online"], ["ASC0012", "轨道吊一组", "OPC UA", 200, 64, "online"], ["ASC0021", "轨道吊一组", "OPC UA", 200, 64, "online"],
      ["ASC0092", "轨道吊三组", "OPC UA", 200, 64, "unstable"], ["ASC0171", "轨道吊六组", "OPC UA", 200, 64, "offline"],
      ["AGV001", "AGV 主组", "Modbus TCP", 500, 18, "online"], ["AGV002", "AGV 主组", "Modbus TCP", 500, 18, "online"], ["AGV012", "AGV 主组", "Modbus TCP", 500, 18, "offline"],
      ["AGV023", "AGV 主组", "Modbus TCP", 500, 18, "unstable"], ["AGV047", "AGV 二组", "Modbus TCP", 500, 18, "unstable"], ["AGV048", "AGV 二组", "Modbus TCP", 500, 18, "online"],
    ];
    const now = Date.now();
    const tasks = rows.map(([deviceCode, groupName, protocol, scanRateMs, pointCount, connectStatus], i) => {
      const r = mockNoise(i + 5);
      const at = connectStatus === "offline" ? now - 960_000 - Math.floor(r * 300_000) : connectStatus === "unstable" ? now - 2000 - Math.floor(r * 6000) : now - Math.floor(r * scanRateMs);
      return { deviceCode, deviceName: deviceCode, groupName, protocol, scanRateMs, pointCount, lastCollectTime: mockHms(new Date(at), true), connectStatus };
    });
    return { scanRateMs: 100, onlineDevices: 138, totalDevices: 146, pointTotal: 12480, tasks };
  },
  async writerMetrics(_token) {
    await delay();
    const streams = [
      ["rt:stream:qc:g001:0", 2940, 256], ["rt:stream:qc:g001:1", 486, 64], ["rt:stream:asc:g001:0", 132, 24], ["rt:stream:asc:g002:0", 88, 16],
      ["rt:stream:agv:main:0", 141, 32], ["rt:stream:agv:main:1", 37, 8], ["rt:stream:agv:g002:0", 26, 8], ["rt:stream:agv:g002:1", 12, 4],
    ].map(([streamKey, lag, pel]) => ({ streamKey: streamKey as string, lag: lag as number, pel: pel as number, length: 50000, status: (lag as number) >= 1000 ? "backlog" as const : "normal" as const }));
    return { streamLagTotal: 3862, pelPendingTotal: 412, dlqTotal: 3, streams };
  },
  async moduleRates(_token) {
    await delay();
    const collector = mockRateSeries(1, 2148, 46);
    const writer = mockRateSeries(2, 2146, 44);
    const collectorPoints = mockRateSeries(3, 2141280, 42000);
    const writerLanded = mockRateSeries(4, 35640, 760);
    const last = (series: { v: number }[]) => series[series.length - 1]?.v ?? null;
    return {
      collector: { unit: "批次/min", current: last(collector), series: collector },
      writer: { unit: "条/min", current: last(writer), series: writer },
      collectorPoints: { unit: "点/min", current: last(collectorPoints), series: collectorPoints },
      writerLanded: { unit: "rows/min", current: last(writerLanded), series: writerLanded },
    };
  },
  async getCollectorConfig(_token) {
    await delay();
    return { streamMaxlen: 50000 };
  },
  async updateCollectorConfig(_token, streamMaxlen) {
    await delay();
    return { streamMaxlen };
  },
  async dashboardOverview(_token) {
    await delay(180);
    return {
      deviceTotal: 146,
      onlineDeviceTotal: 138,
      offlineDeviceTotal: 8,
      riskDeviceTotal: 5,
      pointTotal: 12480,
      systemAlertTotal: 23,
      systemAlertComparePrev: { countDiff: 6, ratioDiff: 35.3 },
      generatedAt: new Date().toISOString(),
    };
  },
  async dashboardTrend(_token, rangeType) {
    await delay(220);
    const isWeek = rangeType === "last7d";
    // 仅开发用：确定性伪随机，半小时一个桶（近7天按天）
    let seed = isWeek ? 7 : 11;
    const rnd = () => ((seed = (seed * 9301 + 49297) % 233280) / 233280);
    const count = isWeek ? 7 : 49;
    const timeAxis = isWeek
      ? ["07-11", "07-12", "07-13", "07-14", "07-15", "07-16", "07-17"]
      : Array.from({ length: count }, (_, i) => `${String(Math.floor(i / 2)).padStart(2, "0")}:${i % 2 ? "30" : "00"}`);
    const collect: number[] = [];
    const alarm: number[] = [];
    const online: number[] = [];
    let level = 5.6;
    for (let i = 0; i < count; i += 1) {
      level = Math.max(4.6, Math.min(7.6, level + (rnd() - 0.48) * 0.9));
      collect.push(Math.round(level * 1_000_000 * (isWeek ? 48 : 1)));
      online.push(Math.round(134 + rnd() * 6 - (i > 30 && i < 36 ? 4 : 0)));
      alarm.push(rnd() < 0.35 ? Math.ceil(rnd() * 3) : 0);
    }
    return {
      query: { rangeType, granularity: isWeek ? "1d" : "30m" },
      timeAxis,
      series: [
        { metricKey: "collectCount", metricName: "采集量", unit: "点", data: collect },
        { metricKey: "alarmCount", metricName: "告警数", unit: "条", data: alarm },
        { metricKey: "onlineDeviceCount", metricName: "在线设备数", unit: "台", data: online },
      ],
      summary: { collectCountTotal: collect.reduce((a, b) => a + b, 0), alarmCountTotal: alarm.reduce((a, b) => a + b, 0), onlineRateAvg: 94.5 },
    };
  },
  async dashboardDeviceTypeDistribution(_token) {
    await delay(180);
    return {
      total: 146,
      groups: [
        { typeCode: "S7", typeName: "S7", deviceCount: 48, onlineCount: 46, ratio: 32.9 },
        { typeCode: "OPCUA", typeName: "OPC UA", deviceCount: 35, onlineCount: 34, ratio: 24.0 },
        { typeCode: "MODBUS", typeName: "Modbus", deviceCount: 31, onlineCount: 28, ratio: 21.2 },
        { typeCode: "OPCDA", typeName: "OPC DA", deviceCount: 19, onlineCount: 18, ratio: 13.0 },
        { typeCode: "GESRTP", typeName: "GE SRTP", deviceCount: 13, onlineCount: 12, ratio: 8.9 },
      ],
    };
  },
  async dashboardConnectionHealth(_token) {
    await delay(200);
    return {
      query: { statRange: "last24h", topN: 8, includeOffline: true },
      overview: { onlineDevices: 133, offlineDevices: 8, riskDevices: 5 },
      list: [
        { deviceId: 1, deviceCode: "PLC-S7-01", deviceName: "一号线主控PLC", deviceType: "PLC", connectStatus: "online" as const, exceptionCount: 0, timeoutCount: 0, reconnectCount: 1, successRate: 99.8, lastHeartbeatTime: new Date().toISOString() },
        { deviceId: 2, deviceCode: "METER-02", deviceName: "二号电力仪表", deviceType: "METER", connectStatus: "unstable" as const, exceptionCount: 3, timeoutCount: 2, reconnectCount: 4, successRate: 94.2, lastHeartbeatTime: new Date().toISOString() },
      ],
    };
  },
  async dashboardEdgePipelineHealth(_token) {
    await delay(120);
    return {
      heartbeatStaleSeconds: 30,
      collector: { status: "healthy" as const, totalNodes: 3, healthyNodes: 3, warningNodes: 0, criticalNodes: 0 },
      uploader: { status: "warning" as const, totalNodes: 3, healthyNodes: 2, warningNodes: 1, criticalNodes: 0 },
      generatedAt: new Date().toISOString(),
    };
  },

  async dashboardRecentEvents(_token) {
    await delay(200);
    const now = Date.now();
    return {
      list: [
        { eventId: "evt-101", eventTime: new Date(now - 4 * 60_000).toISOString(), deviceId: 2, deviceCode: "METER-02", deviceName: "二号电力仪表", eventType: "alarm", eventLevel: "WARN", content: "通讯延迟超过阈值，已自动重试", status: "active" },
        { eventId: "evt-102", eventTime: new Date(now - 18 * 60_000).toISOString(), deviceId: 7, deviceCode: "ROBOT-07", deviceName: "七号装配机器人", eventType: "alarm", eventLevel: "ERROR", content: "设备连接中断，等待现场确认", status: "active" },
        { eventId: "evt-103", eventTime: new Date(now - 43 * 60_000).toISOString(), deviceId: 12, deviceCode: "PLC-S7-03", deviceName: "三号线辅控PLC", eventType: "alarm", eventLevel: "WARN", content: "采集周期波动，当前已恢复", status: "resolved" },
        { eventId: "evt-104", eventTime: new Date(now - 74 * 60_000).toISOString(), deviceId: 15, deviceCode: "SENSOR-15", deviceName: "环境温度传感器", eventType: "alarm", eventLevel: "INFO", content: "点位数据质量由 Good 变为 Uncertain", status: "resolved" },
      ],
    };
  },
  async distributedNodes(_token) {
    await delay(120);
    return MOCK_DISTRIBUTED_NODES.map((item) => ({ ...item }));
  },
  async syncDistributedNodeDeviceStatuses(_token) {
    await delay(120);
    const approvedNodes = MOCK_DISTRIBUTED_NODES.filter((node) => node.status === "approved");
    for (const node of approvedNodes) node.configVersion += 1;
    return { updatedNodeCount: approvedNodes.length };
  },
  async createNodeRegistrationCode(_token, nodeName, expiresMinutes) {
    await delay(120);
    return {
      registrationCode: "mock-registration-code",
      nodeName,
      expiresAt: new Date(Date.now() + expiresMinutes * 60_000).toISOString(),
    };
  },
  async updateDistributedNodeStatus(_token, nodeUuid, status) {
    await delay(120);
    const node = MOCK_DISTRIBUTED_NODES.find((item) => item.nodeUuid === nodeUuid);
    if (node) node.status = status;
    return {};
  },
  async updateDistributedNodeSettings(_token, nodeUuid, readTimeoutMs) {
    await delay(120);
    const node = MOCK_DISTRIBUTED_NODES.find((item) => item.nodeUuid === nodeUuid);
    if (node) {
      node.readTimeoutMs = readTimeoutMs;
      node.configVersion += 1;
    }
    return {};
  },
  async distributedNodeAssignments(_token, nodeUuid) {
    await delay(120);
    const ids = MOCK_NODE_ASSIGNMENTS.get(nodeUuid) ?? [];
    return ids.map((deviceId) => ({
      deviceId,
      deviceCode: `DEVICE-${deviceId}`,
      deviceName: `设备 ${deviceId}`,
      assignmentEpoch: 1,
      status: "active",
      effectiveAt: new Date().toISOString(),
      cutoverAt: null,
      graceUntil: null,
    }));
  },
  async distributedAssignmentOccupancy(_token) {
    await delay(120);
    return [...MOCK_NODE_ASSIGNMENTS.entries()].flatMap(([nodeUuid, deviceIds]) => {
      const node = MOCK_DISTRIBUTED_NODES.find((item) => item.nodeUuid === nodeUuid);
      return deviceIds.map((deviceId) => ({
        deviceId,
        nodeUuid,
        nodeName: node?.nodeName ?? nodeUuid,
      }));
    });
  },
  async assignDistributedNodeDevices(_token, nodeUuid, deviceIds) {
    await delay(120);
    MOCK_NODE_ASSIGNMENTS.set(nodeUuid, [...deviceIds]);
    const node = MOCK_DISTRIBUTED_NODES.find((item) => item.nodeUuid === nodeUuid);
    if (node) node.assignmentCount = deviceIds.length;
    return {
      nodeUuid,
      deviceIds: [...deviceIds],
      migratedDeviceIds: [],
      removedDeviceIds: [],
      cutoverAt: null,
    };
  },
  async driverList(_token, query) {
    await delay(160);
    const list = ([
      { id: 1, driverName: "Siemens S7", driverDesc: "西门子 S7 TCP 驱动（S7-300/400/1200/1500）", readWriteType: "读写", updateTime: "2026-10-12 14:20:08", status: "available", boundDeviceCount: 54, source: "bundled", protected: true },
      { id: 2, driverName: "OPC UA", driverDesc: "OPC UA 客户端驱动，支持订阅与轮询", readWriteType: "读写", updateTime: "2026-10-11 09:41:52", status: "available", boundDeviceCount: 35, source: "installed" },
      { id: 3, driverName: "Modbus TCP", driverDesc: "Modbus TCP 通用驱动", readWriteType: "读写", updateTime: "2026-10-09 16:05:31", status: "available", boundDeviceCount: 31, source: "bundled", protected: true },
      { id: 4, driverName: "OPC DA", driverDesc: "OPC DA 驱动（经 DCOM 网关桥接）", readWriteType: "只读", updateTime: "2026-09-28 11:12:44", status: "available", boundDeviceCount: 19, source: "installed" },
      { id: 5, driverName: "GE SRTP", driverDesc: "GE PLC 通讯驱动（RX3i / 90-30）", readWriteType: "只读", updateTime: "2026-09-21 10:05:17", status: "available", boundDeviceCount: 15, source: "installed" },
      { id: 6, driverName: "Modbus RTU", driverDesc: "串口 Modbus RTU 驱动（RS-485）", readWriteType: "读写", updateTime: "2026-10-15 08:47:20", status: "available", boundDeviceCount: 0, source: "installed" },
      { id: 7, driverName: "EtherNet/IP", driverDesc: "罗克韦尔 CIP 驱动（ControlLogix）", readWriteType: "读写", updateTime: "2026-10-18 21:14:56", status: "available", boundDeviceCount: 0, source: "installed" },
      { id: 8, driverName: "Mitsubishi MC", driverDesc: "三菱 MC 协议 3E 帧驱动", readWriteType: "读写", updateTime: "2026-08-30 09:00:00", status: "available", boundDeviceCount: 0, source: "installed" },
      { id: 9, driverName: "BACnet/IP", driverDesc: "冷箱区空调 BACnet/IP 驱动", readWriteType: "只读", updateTime: "2026-07-02 15:22:10", status: "available", boundDeviceCount: 0, source: "installed" },
    ] satisfies DriverListItem[]).filter((item) => !query.keyword || item.driverName.toLowerCase().includes(query.keyword.toLowerCase()));
    return { list, total: list.length, pageNo: 1, pageSize: 20, availableCount: 7 };
  },
  async driverOptions(_token) {
    await delay(100);
    return [
      { driverName: "Siemens S7", driverDesc: "西门子 S7 TCP 驱动" },
      { driverName: "Modbus TCP", driverDesc: "Modbus TCP 通用驱动" },
      { driverName: "OPC UA", driverDesc: "OPC UA 客户端驱动" },
      { driverName: "OPC DA", driverDesc: "OPC DA 驱动" },
      { driverName: "GE SRTP", driverDesc: "GE PLC 通讯驱动" },
    ];
  },
  async registerDriver(_token, payload) {
    await delay(120);
    return {
      id: 99,
      protocol: payload.protocol,
      description: payload.description || payload.name || payload.protocol,
      created: true,
    };
  },
  async uninstallDriver(_token, payload) {
    await delay(200);
    return { protocol: payload.protocol, status: "uninstalled", catalogDeleted: true };
  },
  async deviceGroupTree(_token) {
    await delay(120);
    return [
      { key: "line-a", name: "一号产线", count: 3, children: [{ key: "device-1", name: "一号线主控PLC", nodeType: "device" as const, deviceId: 1, deviceCode: "PLC-S7-01", connectStatus: "online" as const }] },
      { key: "line-b", name: "二号产线", count: 2, children: [{ key: "device-2", name: "二号电力仪表", nodeType: "device" as const, deviceId: 2, deviceCode: "METER-02", connectStatus: "unstable" as const }] },
    ];
  },
  async deviceList(_token, query) {
    await delay(160);
    const rows = mockDeviceRows();
    const list = rows.filter((item) => (!query.keyword || `${item.deviceCode} ${item.deviceName}`.toLowerCase().includes(query.keyword.toLowerCase()))
      && (!query.connectStatus || item.connectStatus === query.connectStatus)
      && (!query.deviceStatus || item.deviceStatus === query.deviceStatus)
      && (query.deviceIds === undefined || query.deviceIds.includes(item.id))
      && (!query.deviceType || item.deviceType.includes(query.deviceType))
      && (query.deviceTypeExact === undefined || item.deviceType === query.deviceTypeExact)
      && (query.deviceCodeExact === undefined || item.deviceCode === query.deviceCodeExact));
    const pageNo = query.pageNo ?? 1; const pageSize = query.pageSize ?? 20;
    return { list: list.slice((pageNo - 1) * pageSize, pageNo * pageSize), total: list.length, pageNo, pageSize };
  },
  async deviceTypes(token, query = {}) {
    return [...new Set((await this.deviceList(token, { ...query, pageSize: 1000 })).list.map((device) => device.deviceType))];
  },
  async deviceSummary(token, query) {
    const { list } = await this.deviceList(token, { ...query, pageSize: 1000 });
    return {
      total: list.length,
      onlineCount: list.filter((device) => device.connectStatus === "online").length,
      offlineCount: list.filter((device) => device.connectStatus === "offline").length,
      unstableCount: list.filter((device) => device.connectStatus === "unstable").length,
      disabledCount: list.filter((device) => device.deviceStatus === "disabled").length,
      enabledOnlineCount: list.filter((device) => device.deviceStatus === "enabled" && device.connectStatus === "online").length,
      enabledOfflineCount: list.filter((device) => device.deviceStatus === "enabled" && device.connectStatus === "offline").length,
    };
  },
  async createDevice(_token, _payload) { await delay(120); return { id: Date.now() }; },
  async updateDevice(_token, _id, _payload) { await delay(120); return {}; },
  async updateDeviceStatus(_token, _id, enabled) { await delay(120); return { enabled }; },
  async deleteDevice(_token, _id) { await delay(120); return {}; },
  async batchDeleteDevices(_token, ids) { await delay(120); return { count: ids.length }; },
  async exportDevices(_token, _query, template = false) { return { bytes: Array.from(new TextEncoder().encode("mock workbook")), fileName: template ? "device-import-template.xlsx" : "device-export.xlsx" }; },
  async importDevices(_token, rows) { return { success: rows.length, fail: 0, errors: [] }; },
  async pointDeviceTree(_token) { return this.deviceGroupTree(_token); },
  async pointList(_token, query) {
    await delay(160);
    const rows = MOCK_POINTS.map(({ unit: _unit, group: _group, ...point }) => point);
    const allowedDevices = query.deviceType || query.connectStatus
      ? (await this.deviceList(_token, { deviceTypeExact: query.deviceType, connectStatus: query.connectStatus, deviceIds: query.deviceIds, pageSize: 1000 })).list.map((device) => device.id)
      : query.deviceIds;
    const list = rows.filter((item) =>
      (!query.keyword || `${item.pointName} ${item.ioAddress}`.toLowerCase().includes(query.keyword.toLowerCase()))
      && (!query.deviceId || item.deviceId === query.deviceId)
      && (allowedDevices === undefined || allowedDevices.includes(item.deviceId))
      && (!query.deviceCode || item.deviceCode.toLowerCase().includes(query.deviceCode.toLowerCase()))
      && (!query.dataType || item.dataType === query.dataType)
    );
    const pageNo = query.pageNo ?? 1; const pageSize = query.pageSize ?? 20;
    return { list: list.slice((pageNo - 1) * pageSize, pageNo * pageSize), total: list.length, pageNo, pageSize };
  },
  async createPoint(_token, _payload) { await delay(120); return { id: Date.now() }; },
  async updatePoint(_token, _id, _payload) { await delay(120); return {}; },
  async updatePointStatus(_token, _id, enabled, _locator) { await delay(120); return { enabled }; },
  async deletePoint(_token, _id) { await delay(120); return {}; },
  async batchDeletePoints(_token, ids) { await delay(120); return { count: ids.length }; },
  async exportPoints(_token, _query, template = false) { return { bytes: Array.from(new TextEncoder().encode("mock workbook")), fileName: template ? "point-import-template.xlsx" : "point-export.xlsx" }; },
  async importPoints(_token, rows) { return { success: rows.length, fail: 0, errors: [] }; },
  async importPointsFromTemplate(_token, _deviceId) { return { success: 3, fail: 0, errors: [] }; },
  async realtimeInit(_token, deviceId, groupCode = "g001") {
    await delay(120);
    const { device, data } = mockRealtimeItems(deviceId, groupCode);
    return { deviceId: device.id, deviceCode: device.deviceCode, deviceName: device.deviceName, driverName: device.driverName, connectStatus: device.connectStatus, lastCollectTime: new Date().toISOString(), redisConnected: true, degraded: false, data };
  },
  async realtimePointGroups(_token, deviceCode) {
    await delay(80);
    return mockPointGroups(deviceCode);
  },
  async historyDevices(_token) { await delay(100); return MOCK_DEVICES.map((device) => ({ id: device.id, name: `${device.deviceCode} · ${device.deviceName}`, deviceCode: device.deviceCode, deviceName: device.deviceName, typeCode: device.deviceType, typeName: device.deviceType })); },
  async historyDeviceTypes(_token) { await delay(80); return [...new Set(MOCK_DEVICES.map((device) => device.deviceType))].map((type) => ({ typeCode: type, typeName: type, deviceCount: MOCK_DEVICES.filter((device) => device.deviceType === type).length })); },
  async historyPoints(_token, deviceId) { await delay(80); return mockHistoryPoints(deviceId); },
  async historyChart(_token, query) {
    await delay(180);
    return mockHistoryChart(query);
  },
  async historyRawMatrix(_token, query) {
    await delay(180);
    return mockHistoryMatrix(query);
  },
  async historyExport(_token, _payload) { await delay(120); return { taskId: "mock-history-export" }; },
  async historyExportStatus(_token, _taskId) { await delay(120); return { status: "completed" as const, progress: 100, createdAt: new Date().toISOString() }; },
  async historyExportDownload(_token, _taskId) {
    await delay(80);
    const csv = "采样时间,点位 1\n2026-07-17 12:00:00,100.00\n";
    return { bytes: Array.from(new TextEncoder().encode(csv)), fileName: "历史数据_mock.csv" };
  },
  async systemLogList(_token, query) {
    await delay(120);
    const keyword = query.keyword?.toLowerCase();
    const rows = MOCK_SYSTEM_LOGS.filter((item) =>
      (!keyword || `${item.content} ${item.source} ${item.deviceCode ?? ""}`.toLowerCase().includes(keyword)) &&
      (!query.level || item.level === query.level) &&
      (!query.type || item.type === query.type) &&
      (!query.kind || query.kind === "all" || item.kind === query.kind) &&
      (!query.serviceName || item.serviceName === query.serviceName) &&
      (!query.deviceCode || item.deviceCode?.includes(query.deviceCode)) &&
      (!query.operatorName || item.operatorName?.includes(query.operatorName)) &&
      (!query.traceId || item.traceId?.includes(query.traceId))
    );
    const pageNo = query.pageNo ?? 1;
    const pageSize = query.pageSize ?? 20;
    return { list: rows.slice((pageNo - 1) * pageSize, pageNo * pageSize), total: rows.length, pageNo, pageSize };
  },
  async logStats(_token) {
    await delay(100);
    const debugRemainingSeconds = Math.max(0, Math.floor((_mockDebugUntil - Date.now()) / 1000));
    return {
      todayTotal: 18426,
      yesterdayTotal: 17113,
      todayWarnTotal: 312,
      todayErrorTotal: 47,
      todayCriticalTotal: 3,
      debugEnabled: debugRemainingSeconds > 0,
      debugRemainingSeconds,
      totalSizeMb: 1284.2,
      maxTotalMb: 4096,
      lastCleanupTime: "2026-10-18 03:00:12",
      lastCleanupDeletedTotal: 126530,
      // 24 小时：白班（07–19）高、夜班低，08/20 交接班尖峰；WARN/ERROR 与 INFO 不同量级（双轴）
      levelTrend: Array.from({ length: 24 }, (_, index) => {
        const hour = (new Date().getHours() + 1 + index) % 24;
        const shift = hour >= 7 && hour <= 19 ? 1 : 0.62;
        const wave = Math.sin(index * 1.7) * 0.5 + 0.5;
        return { time: `${String(hour).padStart(2, "0")}:00`, info: Math.round((640 + wave * 220) * shift + (hour === 8 || hour === 20 ? 260 : 0)), warn: Math.round((12 + wave * 14) * shift + (hour === 15 ? 18 : 0)), error: Math.round(2 + Math.cos(index * 2.3) * 2 + (hour === 15 ? 8 : 0)) + 1, critical: index === 5 || index === 17 || index === 23 ? 1 : 0, debug: debugRemainingSeconds > 0 && index === 23 ? 420 : 0 };
      }),
      typeRatio: [{ type: "collect", count: 7558, ratio: 41 }, { type: "system", count: 4054, ratio: 22 }, { type: "operation", count: 2211, ratio: 12 }, { type: "storage", count: 1658, ratio: 9 }, { type: "auth", count: 1105, ratio: 6 }, { type: "alarm", count: 921, ratio: 5 }, { type: "security", count: 553, ratio: 3 }, { type: "frontend", count: 366, ratio: 2 }],
      sourceRatio: [],
    };
  },
  async toggleLogDebug(_token, payload) {
    await delay(100);
    _mockDebugUntil = payload.enabled ? Date.now() + (payload.durationMinutes ?? 60) * 60_000 : 0;
    return {};
  },
  async cleanupLogs(_token, payload) {
    await delay(180);
    return { deletedOperationLogs: 128, deletedSystemLogs: 1840, sizeBeforeMb: 286.4, sizeAfterMb: 241.8, triggeredBy: "admin", triggerMode: payload.mode, force: Boolean(payload.force) };
  },
  async tdengineLogFiles(_token) {
    await delay(100);
    return [{ name: "taosdlog.0", sizeMb: 18.6, modifiedAt: new Date().toISOString() }, { name: "taosdlog.1", sizeMb: 12.2, modifiedAt: new Date(Date.now() - 86_400_000).toISOString() }];
  },
  async tdengineLogTail(_token, file, lines = 300) {
    await delay(100);
    return { file, lines: Array.from({ length: Math.min(lines, 40) }, (_, index) => `07/17 16:${String(index).padStart(2, "0")}:12.036 INFO  TSC query worker completed, rows=${128 + index}`) };
  },
  async userList(_token, query) {
    await delay(100);
    const rows = MOCK_USERS.filter((user) => (!query.userName || user.userName.includes(query.userName)) && (!query.userGender || user.userGender === query.userGender) && (!query.userPhone || user.userPhone.includes(query.userPhone)) && (!query.userEmail || user.userEmail.includes(query.userEmail)) && (!query.status || user.status === query.status));
    const pageNo = query.current ?? 1; const pageSize = query.size ?? 20;
    return { records: rows.slice((pageNo - 1) * pageSize, pageNo * pageSize), total: rows.length, current: pageNo, size: pageSize };
  },
  async userInfo(_token) {
    await delay(80);
    return {
      userId: 1,
      userName: "admin",
      email: "admin@example.com",
      avatar: "",
      roles: ["R_SUPER"],
      buttons: ["view", "add", "edit", "delete", "device_monitor:driver:manage"],
    };
  },
  async roleOptions(_token) { await delay(80); return MOCK_ROLES.map(({ roleId, roleName, roleCode, enabled }) => ({ roleId, roleName, roleCode, enabled })); },
  async createUser(_token, payload) { const id = Date.now(); MOCK_USERS = [...MOCK_USERS, { id, avatar: payload.avatar ?? "", status: payload.status ?? "1", userName: payload.userName, userGender: payload.userGender ?? "unknown", nickName: payload.nickName ?? "", userPhone: payload.userPhone ?? "", userEmail: payload.userEmail ?? "", userRoles: payload.userRoles ?? ["R_USER"], createBy: "admin", createTime: new Date().toISOString(), updateBy: "admin", updateTime: new Date().toISOString() }]; return { id, userId: id, temporaryPassword: "mock-temporary-password", mustChangePassword: true as const, tokenVersion: 0 }; },
  async updateUser(_token, id, payload) { MOCK_USERS = MOCK_USERS.map((user) => user.id === id ? { ...user, ...payload, updateTime: new Date().toISOString() } : user); return { id }; },
  async updateUserStatus(_token, id, status) { MOCK_USERS = MOCK_USERS.map((user) => user.id === id ? { ...user, status } : user); return { id }; },
  async resetUserPassword(_token, id) {
    await delay(80);
    return { id, userId: id, temporaryPassword: "mock-temporary-password", mustChangePassword: true as const, tokenVersion: 1 };
  },
  async changePassword(_token, _oldPassword, _newPassword) { await delay(80); return true; },
  async deleteUser(_token, id) { MOCK_USERS = MOCK_USERS.filter((user) => user.id !== id); return { id }; },
  async roleList(_token, query) { await delay(100); const rows = MOCK_ROLES.filter((role) => (!query.roleName || role.roleName.includes(query.roleName)) && (!query.roleCode || role.roleCode.includes(query.roleCode)) && (!query.description || role.description.includes(query.description)) && (query.enabled === "" || query.enabled === undefined || role.enabled === query.enabled)); const pageNo = query.current ?? 1; const pageSize = query.size ?? 20; return { records: rows.slice((pageNo - 1) * pageSize, pageNo * pageSize), total: rows.length, current: pageNo, size: pageSize }; },
  async createRole(_token, payload) { const id = Date.now(); MOCK_ROLES = [...MOCK_ROLES, { roleId: id, roleName: payload.roleName, roleCode: payload.roleCode, description: payload.description ?? "", enabled: payload.enabled ?? true, createTime: new Date().toISOString() }]; return { id }; },
  async updateRole(_token, id, payload) { MOCK_ROLES = MOCK_ROLES.map((role) => role.roleId === id ? { ...role, ...payload } : role); return { id }; },
  async updateRoleStatus(_token, id, enabled) { MOCK_ROLES = MOCK_ROLES.map((role) => role.roleId === id ? { ...role, enabled } : role); return { id }; },
  async deleteRole(_token, id) { MOCK_ROLES = MOCK_ROLES.filter((role) => role.roleId !== id); return { id }; },
  async roleGrant(_token, id) { return MOCK_ROLE_GRANTS.get(id) ?? { menuIds: [], permissionIds: [], dataScopes: [] }; },
  async updateRoleGrant(_token, id, payload) { MOCK_ROLE_GRANTS.set(id, payload); return { id }; },
  async adminMenus(_token) { await delay(100); return MOCK_MENUS; },
  async currentUserMenus(_token) { await delay(100); return MOCK_MENUS; },
  async createMenu(_token, payload) { const id = Date.now(); const item = { id, parentId: payload.parentId, path: payload.path, name: payload.name, component: payload.component, menuType: payload.menuType, sort: payload.sort, enabled: payload.enabled, meta: { title: payload.titleKey, icon: payload.icon, keepAlive: payload.keepAlive, isHide: payload.isHide, isHideTab: payload.isHideTab, authList: [] } }; if (payload.parentId) { MOCK_MENUS = MOCK_MENUS.map((menu) => menu.id === payload.parentId ? { ...menu, children: [...(menu.children ?? []), item] } : menu); } else MOCK_MENUS = [...MOCK_MENUS, item]; return { id }; },
  async updateMenu(_token, id, payload) { const update = (items: MenuTreeItem[]): MenuTreeItem[] => items.map((menu) => menu.id === id ? { ...menu, path: payload.path, name: payload.name, component: payload.component, menuType: payload.menuType, sort: payload.sort, enabled: payload.enabled, meta: { ...menu.meta, title: payload.titleKey, icon: payload.icon, keepAlive: payload.keepAlive, isHide: payload.isHide, isHideTab: payload.isHideTab } } : { ...menu, children: menu.children ? update(menu.children) : undefined }); MOCK_MENUS = update(MOCK_MENUS); return { id }; },
  async deleteMenu(_token, id) { const remove = (items: MenuTreeItem[]): MenuTreeItem[] => items.filter((menu) => menu.id !== id).map((menu) => ({ ...menu, children: menu.children ? remove(menu.children) : undefined })); MOCK_MENUS = remove(MOCK_MENUS); return { id }; },
  async menuPermissions(_token, menuId) { const find = (items: MenuTreeItem[]): MenuTreeItem | undefined => { for (const menu of items) { if (menu.id === menuId) return menu; const nested = menu.children ? find(menu.children) : undefined; if (nested) return nested; } }; return find(MOCK_MENUS)?.meta.authList ?? []; },
  async createMenuPermission(_token, menuId, payload) { const id = Date.now(); const permission = { id, title: payload.permissionName, authMark: payload.authMark ?? payload.permissionCode, enabled: payload.enabled, permissionCode: payload.permissionCode, permissionType: payload.permissionType, sort: payload.sort }; const add = (items: MenuTreeItem[]): MenuTreeItem[] => items.map((menu) => menu.id === menuId ? { ...menu, meta: { ...menu.meta, authList: [...(menu.meta.authList ?? []), permission] } } : { ...menu, children: menu.children ? add(menu.children) : undefined }); MOCK_MENUS = add(MOCK_MENUS); return { id }; },
  async updateMenuPermission(_token, menuId, permissionId, payload) { const update = (items: MenuTreeItem[]): MenuTreeItem[] => items.map((menu) => menu.id === menuId ? { ...menu, meta: { ...menu.meta, authList: (menu.meta.authList ?? []).map((permission) => permission.id === permissionId ? { ...permission, title: payload.permissionName, authMark: payload.authMark ?? payload.permissionCode, enabled: payload.enabled, permissionType: payload.permissionType, sort: payload.sort } : permission) } } : { ...menu, children: menu.children ? update(menu.children) : undefined }); MOCK_MENUS = update(MOCK_MENUS); return { id: permissionId }; },
  async deleteMenuPermission(_token, menuId, permissionId) { const remove = (items: MenuTreeItem[]): MenuTreeItem[] => items.map((menu) => menu.id === menuId ? { ...menu, meta: { ...menu.meta, authList: (menu.meta.authList ?? []).filter((permission) => permission.id !== permissionId) } } : { ...menu, children: menu.children ? remove(menu.children) : undefined }); MOCK_MENUS = remove(MOCK_MENUS); return { id: permissionId }; },
};

// ─── createMockWebSocket 创建模拟 WebSocket ──────────────────────────────────────

// 随机日志消息池
const COLLECTOR_MSGS: { level: LogLevel; message: string }[] = [
  { level: "INFO",  message: "Device QC101 polled. points=426 quality=Good" },
  { level: "INFO",  message: "XADD rt:stream:qc:g001:0 ok. entries=7" },
  { level: "INFO",  message: "Device ASC0011 polled. points=64 quality=Good" },
  { level: "DEBUG", message: "Scan cycle done. group=qc:g001 elapsed=43ms" },
  { level: "INFO",  message: "XADD rt:stream:agv:main:0 ok. entries=4" },
  { level: "INFO",  message: "Device AGV001 polled. points=18 quality=Good" },
  { level: "DEBUG", message: "Batch flush: 467 pts → redis stream" },
  { level: "INFO",  message: "XADD rt:stream:asc:g001:0 ok. entries=2" },
  { level: "WARN",  message: "Device QC107 response slow. protocol=S7 rtt=406ms" },
  { level: "INFO",  message: "XADD rt:stream:qc:g001:0 ok. entries=9" },
  { level: "INFO",  message: "Device QC101 polled. points=426 quality=Good" },
  { level: "DEBUG", message: "Scan cycle done. group=qc:g001 elapsed=57ms" },
  { level: "INFO",  message: "Device ASC0011 polled. points=64 quality=Good" },
  { level: "WARN",  message: "Retry opcua node read once. device=ASC0092 node=ns=2;s=Hoist.Load" },
  { level: "INFO",  message: "XADD rt:stream:qc:g001:0 ok. entries=6" },
  { level: "ERROR", message: "modbus read timeout. device=AGV012 unit=1 addr=40001" },
  { level: "INFO",  message: "Device AGV023 reconnected. protocol=Modbus TCP" },
];

const INGEST_MSGS: { level: LogLevel; message: string }[] = [
  { level: "INFO",  message: "accept edge batch. node=edge-qc-01 seq=182635 frames=35" },
  { level: "DEBUG", message: "validate batch ok. schema=v2 points=1449" },
  { level: "INFO",  message: "XADD rt:stream:qc:g001:0 ok. entries=7" },
  { level: "INFO",  message: "receipt ack sent. node=edge-qc-02 seq=182225" },
  { level: "INFO",  message: "accept edge batch. node=edge-agv-01 seq=55490 frames=5" },
  { level: "INFO",  message: "accept edge batch. node=edge-asc-02 seq=90475 frames=20" },
  { level: "INFO",  message: "XADD rt:stream:asc:g001:0 ok. entries=3" },
  { level: "INFO",  message: "accept edge batch. node=edge-qc-01 seq=182636 frames=38" },
  { level: "INFO",  message: "XADD rt:stream:qc:g001:0 ok. entries=4" },
  { level: "INFO",  message: "receipt ack sent. node=edge-qc-02 seq=182050" },
  { level: "INFO",  message: "accept edge batch. node=edge-agv-01 seq=55459 frames=13" },
  { level: "INFO",  message: "XADD rt:stream:qc:g001:0 ok. entries=9" },
  { level: "INFO",  message: "accept edge batch. node=edge-asc-02 seq=90502 frames=11" },
  { level: "DEBUG", message: "validate batch ok. schema=v2 points=989" },
  { level: "INFO",  message: "accept edge batch. node=edge-agv-01 seq=55125 frames=14" },
  { level: "WARN",  message: "duplicate batch ignored. node=edge-asc-02 seq=90474" },
];

const WRITER_MSGS: { level: LogLevel; message: string }[] = [
  { level: "INFO",  message: "XACK success count=551" },
  { level: "INFO",  message: "XREADGROUP streams=8 consumer=writer-1 count=433" },
  { level: "DEBUG", message: "flush timer triggered. interval=500ms" },
  { level: "INFO",  message: "map var_id to td_column_name success" },
  { level: "INFO",  message: "batch insert TDengine rows=512 table=qc101" },
  { level: "INFO",  message: "XACK success count=533" },
  { level: "DEBUG", message: "flush timer triggered. interval=500ms" },
  { level: "INFO",  message: "XREADGROUP streams=8 consumer=writer-1 count=465" },
  { level: "INFO",  message: "batch insert TDengine rows=88 table=asc0011" },
  { level: "INFO",  message: "XACK success count=498" },
  { level: "WARN",  message: "pending drain claimed=3 stream=rt:stream:qc:g001:0" },
  { level: "INFO",  message: "DLQ check: 0 items" },
  { level: "DEBUG", message: "Writer heartbeat ok. uptime=268470s" },
  { level: "INFO",  message: "XREADGROUP streams=8 consumer=writer-1 count=427" },
  { level: "INFO",  message: "XACK success count=526" },
  { level: "ERROR", message: "tdengine write failed. retry later" },
];

/**
 * 创建与真实 WebSocket 接口兼容的 mock 对象。
 * - 50ms 后触发 onopen（模拟握手延迟）
 * - 首屏回填 40 条，之后每 1200ms 推一条随机 LogLine
 * - close() 后立即停止推送，不泄漏定时器
 * - send() 接受 auth 首帧但不做校验（简化 mock）
 */
export function createMockWebSocket(name: string): WebSocket {
  let closed = false;
  let openTimer:  ReturnType<typeof setTimeout>  | null = null;
  let pushTimer:  ReturnType<typeof setInterval> | null = null;

  const pool = name === "writer" ? WRITER_MSGS : name === "ingest" ? INGEST_MSGS : COLLECTOR_MSGS;

  // 事件处理器存储（用 get/set 代理到外部赋值）
  let _onopen:    ((e: Event)        => void) | null = null;
  let _onmessage: ((e: MessageEvent) => void) | null = null;
  let _onclose:   ((e: CloseEvent)   => void) | null = null;
  let _onerror:   ((e: Event)        => void) | null = null;

  function cleanup() {
    closed = true;
    if (openTimer) { clearTimeout(openTimer);   openTimer = null; }
    if (pushTimer) { clearInterval(pushTimer);  pushTimer = null; }
  }

  // 50ms 后 open，再开始定时推日志
  openTimer = setTimeout(() => {
    if (closed) return;
    ws.readyState = WebSocket.OPEN;
    _onopen?.({ type: "open" } as Event);

    let i = Math.floor(Math.random() * pool.length); // 随机起始，避免每次开头相同
    // 首屏回填近 40 条（本地时间戳），让预览日志面板有内容
    const backfillStart = Date.now() - 40 * 900;
    for (let k = 0; k < 40; k += 1) {
      const { level, message } = pool[i % pool.length];
      i++;
      _onmessage?.({
        type: "message",
        data: JSON.stringify({ timestamp: mockLocalIso(new Date(backfillStart + k * 900 + Math.floor(mockNoise(k) * 400))), level, source: name, message }),
      } as MessageEvent);
    }
    pushTimer = setInterval(() => {
      if (closed) { clearInterval(pushTimer!); pushTimer = null; return; }
      const { level, message } = pool[i % pool.length];
      i++;
      const line: LogLine = {
        timestamp: mockLocalIso(new Date()),
        level,
        source: name,
        message,
      };
      _onmessage?.({
        type: "message",
        data: JSON.stringify(line),
      } as MessageEvent);
    }, 1200);
  }, 50);

  const ws = {
    readyState: WebSocket.CONNECTING as number,

    // getter/setter 确保外部 ws.onmessage = fn 赋值生效
    get onopen()    { return _onopen;    },
    set onopen(fn)  { _onopen    = fn;   },
    get onmessage() { return _onmessage; },
    set onmessage(fn) { _onmessage = fn; },
    get onclose()   { return _onclose;   },
    set onclose(fn) { _onclose   = fn;   },
    get onerror()   { return _onerror;   },
    set onerror(fn) { _onerror   = fn;   },

    // WebSocket 静态常量
    CONNECTING: 0 as const,
    OPEN:       1 as const,
    CLOSING:    2 as const,
    CLOSED:     3 as const,

    // 元信息
    url:            `ws://mock/${name}/logs`,
    protocol:       "",
    extensions:     "",
    bufferedAmount: 0,
    binaryType:     "blob" as BinaryType,

    close() {
      if (closed) return;
      cleanup();
      ws.readyState = WebSocket.CLOSED;
      _onclose?.({
        type:      "close",
        code:      1000,
        reason:    "mock closed",
        wasClean:  true,
      } as unknown as CloseEvent);
    },

    // send 用于接收首帧 auth，mock 直接忽略
    send(_data: string | ArrayBufferLike | Blob | ArrayBufferView) {},

    dispatchEvent(_event: Event): boolean { return true; },
    addEventListener()    {},
    removeEventListener() {},
  };

  return ws as unknown as WebSocket;
}
