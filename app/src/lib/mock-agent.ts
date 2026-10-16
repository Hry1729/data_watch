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
  writer:    "stopped",
  uploader:  "running",
  storage:   "running",
};

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
    const deviceId = deviceCode === "METER-02" ? 2 : 1;
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
      pid:            running ? (name === "collector" ? 4820 : 4932) : null,
      uptime_seconds: running ? 13338 : 0,
      memory_mb:      running ? (name === "collector" ? 186  : 142)  : 0,
      cpu_percent:    running ? (name === "collector" ? 2.4  : 1.1)  : 0,
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
    return [
      {
        id: "modbus_tcp",
        protocol: "modbus_tcp",
        name: "Modbus TCP",
        description: "安装包默认内置的 Modbus TCP 只读驱动",
        version: "1.0.0",
        sha256: "abc",
        capabilities: ["poll", "read"],
        readWriteType: "只读",
        aliases: ["modbus_tcp"],
        source: "bundled",
        protected: true,
        status: "loaded",
        error: null,
      },
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

const MOCK_SYSTEM_LOGS = Array.from({ length: 36 }, (_, index) => {
  const levels = ["INFO", "INFO", "WARN", "INFO", "ERROR"] as const;
  const types = ["system", "collect", "storage", "operation", "auth"] as const;
  const level = levels[index % levels.length];
  const type = types[index % types.length];
  return {
    id: 1000 - index,
    kind: type === "operation" ? "operation" as const : "system" as const,
    time: new Date(Date.now() - index * 12 * 60_000).toISOString(),
    level,
    type,
    source: type === "collect" ? "collector" : type === "storage" ? "writer" : "backend",
    serviceName: type === "collect" ? "collector" : type === "storage" ? "writer" : "backend",
    moduleName: type === "operation" ? "设备管理" : undefined,
    actionName: type === "operation" ? "更新设备" : undefined,
    operatorName: type === "operation" ? "admin" : undefined,
    deviceCode: index % 3 === 0 ? "PLC-S7-01" : undefined,
    content: level === "ERROR" ? "TDengine batch write failed, retry scheduled" : level === "WARN" ? "采集周期出现短时抖动" : "服务运行正常，周期任务执行完成",
    resultStatus: type === "operation" ? "success" : undefined,
    durationMs: 18 + index * 3,
    requestId: `req-${1000 - index}`,
    traceId: index % 4 === 0 ? `trace-${1000 - index}` : undefined,
    detail: { sample: true, sequence: index },
    exceptionStack: level === "ERROR" ? "MockError: write timeout\n    at batch_writer.flush" : undefined,
  };
});

let MOCK_USERS: UserListItem[] = [
  { id: 1, avatar: "", status: "1", userName: "admin", userGender: "male", nickName: "系统管理员", userPhone: "13800000001", userEmail: "admin@example.com", userRoles: ["R_SUPER"], createBy: "system", createTime: "2026-01-01 08:00:00", updateBy: "system", updateTime: "2026-07-17 10:00:00" },
  { id: 2, avatar: "", status: "1", userName: "operator", userGender: "female", nickName: "值班员", userPhone: "13800000002", userEmail: "operator@example.com", userRoles: ["R_OPERATOR"], createBy: "admin", createTime: "2026-03-12 09:30:00", updateBy: "admin", updateTime: "2026-07-16 16:20:00" },
  { id: 3, avatar: "", status: "2", userName: "viewer", userGender: "unknown", nickName: "访客账号", userPhone: "", userEmail: "viewer@example.com", userRoles: ["R_USER"], createBy: "admin", createTime: "2026-05-20 14:12:00", updateBy: "admin", updateTime: "2026-07-10 11:40:00" },
];

let MOCK_ROLES: RoleListItem[] = [
  { roleId: 1, roleName: "超级管理员", roleCode: "R_SUPER", description: "拥有全部系统权限", enabled: true, createTime: "2026-01-01 08:00:00" },
  { roleId: 2, roleName: "运维人员", roleCode: "R_OPERATOR", description: "设备运维与日志查看", enabled: true, createTime: "2026-02-10 10:00:00" },
  { roleId: 3, roleName: "普通用户", roleCode: "R_USER", description: "只读访问", enabled: true, createTime: "2026-02-10 10:05:00" },
];

let MOCK_MENUS: MenuTreeItem[] = [
  { id: 10, path: "/dashboard", name: "Dashboard", component: "", menuType: "directory", sort: 1, enabled: true, meta: { title: "仪表盘", icon: "LayoutDashboard", authList: [] }, children: [
    { id: 11, parentId: 10, path: "workbench", name: "Workbench", component: "/dashboard/workbench", menuType: "menu", sort: 1, enabled: true, meta: { title: "工作台", keepAlive: true, authList: [{ id: 101, title: "查看", authMark: "view", permissionCode: "dashboard:workbench:view", permissionType: "action", enabled: true, sort: 1 }] } },
  ] },
  { id: 30, path: "/device-monitor", name: "DeviceMonitor", component: "", menuType: "directory", sort: 2, enabled: true, meta: { title: "设备监控", authList: [] }, children: [
    { id: 31, parentId: 30, path: "monitor", name: "DeviceMonitorCard", component: "/device_monitor/monitor/index", menuType: "menu", sort: 1, enabled: true, meta: { title: "设备通讯", authList: [] } },
    { id: 32, parentId: 30, path: "driver", name: "DriverManage", component: "/device_monitor/driver/index", menuType: "menu", sort: 2, enabled: true, meta: { title: "驱动管理", authList: [] } },
    { id: 33, parentId: 30, path: "device", name: "DeviceManage", component: "/device_monitor/device/index", menuType: "menu", sort: 3, enabled: true, meta: { title: "设备管理", authList: [] } },
    { id: 34, parentId: 30, path: "point", name: "PointManage", component: "/device_monitor/point/index", menuType: "menu", sort: 4, enabled: true, meta: { title: "点位管理", authList: [] } },
    { id: 35, parentId: 30, path: "realtime", name: "RealtimeMonitor", component: "/device_monitor/realtime/index", menuType: "menu", sort: 5, enabled: true, meta: { title: "实时监控", authList: [] } },
    { id: 36, parentId: 30, path: "history", name: "HistoryData", component: "/device_monitor/history/index", menuType: "menu", sort: 6, enabled: true, meta: { title: "历史数据", authList: [] } },
    { id: 37, parentId: 30, path: "log", name: "SystemLog", component: "/device_monitor/log/index", menuType: "menu", sort: 7, enabled: true, meta: { title: "系统日志", authList: [] } },
  ] },
  { id: 20, path: "/system", name: "System", component: "", menuType: "directory", sort: 9, enabled: true, meta: { title: "系统管理", icon: "Settings2", authList: [] }, children: [
    { id: 21, parentId: 20, path: "users", name: "Users", component: "/system/user/index", menuType: "menu", sort: 1, enabled: true, meta: { title: "用户管理", authList: [{ id: 201, title: "新增", authMark: "add", permissionCode: "system:user:create", permissionType: "action", enabled: true, sort: 1 }, { id: 202, title: "编辑", authMark: "edit", permissionCode: "system:user:update", permissionType: "action", enabled: true, sort: 2 }] } },
    { id: 22, parentId: 20, path: "roles", name: "Roles", component: "/system/role/index", menuType: "menu", sort: 2, enabled: true, meta: { title: "角色管理", authList: [] } },
    { id: 23, parentId: 20, path: "menu", name: "Menus", component: "/system/menu/index", menuType: "menu", sort: 3, enabled: true, meta: { title: "菜单管理", authList: [] } },
  ] },
];

const MOCK_ROLE_GRANTS = new Map<number, RoleGrant>([[1, { menuIds: [10, 11, 20, 21, 22], permissionIds: [101, 201, 202], dataScopes: [] }], [2, { menuIds: [10, 11, 20, 21], permissionIds: [101], dataScopes: [] }], [3, { menuIds: [10, 11], permissionIds: [101], dataScopes: [] }]]);

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
  async logList(_token, _query) {
    await delay();
    return [];
  },
  async collectorMetrics(_token) {
    await delay();
    return { scanRateMs: null, onlineDevices: 0, totalDevices: 0, pointTotal: 0, tasks: [] };
  },
  async writerMetrics(_token) {
    await delay();
    return { streamLagTotal: 0, pelPendingTotal: 0, dlqTotal: 0, streams: [] };
  },
  async moduleRates(_token) {
    await delay();
    return {
      collector: { unit: "批次/min", current: null, series: [] },
      writer: { unit: "条/min", current: null, series: [] },
      collectorPoints: { unit: "点/min", current: null, series: [] },
      writerLanded: { unit: "rows/min", current: null, series: [] },
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
      deviceTotal: 42,
      onlineDeviceTotal: 36,
      offlineDeviceTotal: 4,
      riskDeviceTotal: 2,
      pointTotal: 2864,
      systemAlertTotal: 18,
      systemAlertComparePrev: { countDiff: -5, ratioDiff: -21.7 },
      generatedAt: new Date().toISOString(),
    };
  },
  async dashboardTrend(_token, rangeType) {
    await delay(220);
    const isWeek = rangeType === "last7d";
    const timeAxis = isWeek
      ? ["07-11", "07-12", "07-13", "07-14", "07-15", "07-16", "07-17"]
      : ["00:00", "04:00", "08:00", "12:00", "16:00", "20:00", "24:00"];
    return {
      query: { rangeType, granularity: isWeek ? "1d" : "1h" },
      timeAxis,
      series: [
        { metricKey: "collectCount", metricName: "采集量", unit: "点", data: [1820, 2060, 3180, 3460, 3290, 3720, 3510] },
        { metricKey: "alarmCount", metricName: "告警数", unit: "条", data: [2, 1, 4, 3, 5, 2, 1] },
        { metricKey: "onlineDeviceCount", metricName: "在线设备数", unit: "台", data: [34, 35, 36, 35, 37, 36, 36] },
      ],
      summary: { collectCountTotal: 21040, alarmCountTotal: 18, onlineRateAvg: 94.3 },
    };
  },
  async dashboardDeviceTypeDistribution(_token) {
    await delay(180);
    return {
      total: 42,
      groups: [
        { typeCode: "PLC", typeName: "PLC", deviceCount: 14, onlineCount: 13, ratio: 33.3 },
        { typeCode: "METER", typeName: "仪表", deviceCount: 10, onlineCount: 9, ratio: 23.8 },
        { typeCode: "ROBOT", typeName: "机器人", deviceCount: 8, onlineCount: 7, ratio: 19.0 },
        { typeCode: "SENSOR", typeName: "传感器", deviceCount: 6, onlineCount: 5, ratio: 14.3 },
        { typeCode: "OTHER", typeName: "其他", deviceCount: 4, onlineCount: 2, ratio: 9.6 },
      ],
    };
  },
  async dashboardConnectionHealth(_token) {
    await delay(200);
    return {
      query: { statRange: "last24h", topN: 8, includeOffline: true },
      overview: { onlineDevices: 36, offlineDevices: 4, riskDevices: 2 },
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
      { id: 1, driverName: "Siemens S7", driverDesc: "西门子 S7 TCP 驱动", readWriteType: "只读", updateTime: "2026-07-16 14:20:00", status: "available", boundDeviceCount: 14, source: "bundled", protected: true },
      { id: 2, driverName: "Modbus TCP", driverDesc: "Modbus TCP 通用驱动", readWriteType: "只读", updateTime: "2026-07-15 09:12:00", status: "available", boundDeviceCount: 10, source: "bundled", protected: true },
      { id: 3, driverName: "OPC UA", driverDesc: "OPC UA 客户端驱动", readWriteType: "只读", updateTime: "2026-07-13 18:35:00", status: "available", boundDeviceCount: 8, source: "bundled", protected: true },
      { id: 4, driverName: "GE SRTP", driverDesc: "GE PLC 通讯驱动", readWriteType: "只读", updateTime: "2026-07-10 10:05:00", status: "disabled", boundDeviceCount: 0, source: "bundled", protected: true },
    ] satisfies DriverListItem[]).filter((item) => !query.keyword || item.driverName.toLowerCase().includes(query.keyword.toLowerCase()));
    return { list, total: list.length, pageNo: 1, pageSize: 20, availableCount: 3 };
  },
  async driverOptions(_token) {
    await delay(100);
    return [
      { driverName: "Siemens S7", driverDesc: "西门子 S7 TCP 驱动" },
      { driverName: "Modbus TCP", driverDesc: "Modbus TCP 通用驱动" },
      { driverName: "OPC UA", driverDesc: "OPC UA 客户端驱动" },
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
    const rows = [
      { id: 1, deviceType: "PLC", deviceCode: "PLC-S7-01", deviceName: "一号线主控PLC", driverName: "Siemens S7", endpoint: "192.168.1.10:102", endpointBackup: "192.168.1.11:102", deviceStatus: "enabled" as const, connectStatus: "online" as const, connectUpdateTime: "2026-07-17 15:30:00", dataStatus: "normal" as const, dataUpdateTime: "2026-07-17 15:30:00", description: "主线控制器" },
      { id: 2, deviceType: "仪表", deviceCode: "METER-02", deviceName: "二号电力仪表", driverName: "Modbus TCP", endpoint: "192.168.1.20:502", endpointBackup: "", deviceStatus: "enabled" as const, connectStatus: "unstable" as const, connectUpdateTime: "2026-07-17 15:29:41", dataStatus: "abnormal" as const, dataUpdateTime: "2026-07-17 15:29:40", description: "能耗计量" },
      { id: 3, deviceType: "机器人", deviceCode: "ROBOT-07", deviceName: "七号装配机器人", driverName: "OPC UA", endpoint: "192.168.1.30:4840", endpointBackup: "", deviceStatus: "enabled" as const, connectStatus: "offline" as const, connectUpdateTime: "2026-07-17 14:52:00", dataStatus: "nodata" as const, dataUpdateTime: "2026-07-17 14:51:55", description: "装配工位" },
      { id: 4, deviceType: "PLC", deviceCode: "PLC-S7-03", deviceName: "三号线辅控PLC", driverName: "Siemens S7", endpoint: "192.168.1.40:102", endpointBackup: "", deviceStatus: "disabled" as const, connectStatus: "offline" as const, connectUpdateTime: "2026-07-16 11:10:00", dataStatus: "nodata" as const, dataUpdateTime: "2026-07-16 11:10:00", description: "维护中" },
    ];
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
    return [...new Set((await this.deviceList(token, { ...query, pageSize: 100 })).list.map((device) => device.deviceType))];
  },
  async deviceSummary(token, query) {
    const { list } = await this.deviceList(token, { ...query, pageSize: 100 });
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
    const rows = [
      { id: 11, deviceId: 1, deviceCode: "PLC-S7-01", varSeq: 1, pointName: "运行状态", ioAddress: "DB1.DBX0.0", dataType: "BOOL" as const, description: "设备运行标志", isReadonly: true, driverName: "Siemens S7", pointStatus: "enabled" as const, updateTime: "2026-07-17 15:30:00" },
      { id: 12, deviceId: 1, deviceCode: "PLC-S7-01", varSeq: 2, pointName: "主轴转速", ioAddress: "DB1.DBD4", dataType: "REAL" as const, description: "rpm", isReadonly: true, driverName: "Siemens S7", pointStatus: "enabled" as const, updateTime: "2026-07-17 15:30:00" },
      { id: 21, deviceId: 2, deviceCode: "METER-02", varSeq: 1, pointName: "A相电压", ioAddress: "40001", dataType: "REAL" as const, description: "V", isReadonly: true, driverName: "Modbus TCP", pointStatus: "disabled" as const, updateTime: "2026-07-17 15:29:40" },
    ];
    const allowedDevices = query.deviceType || query.connectStatus
      ? (await this.deviceList(_token, { deviceTypeExact: query.deviceType, connectStatus: query.connectStatus, deviceIds: query.deviceIds, pageSize: 100 })).list.map((device) => device.id)
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
  async realtimeInit(_token, deviceId, _groupCode = "g001") {
    await delay(120);
    const now = new Date().toISOString();
    return { deviceId, deviceCode: deviceId === 2 ? "METER-02" : "PLC-S7-01", deviceName: deviceId === 2 ? "二号电力仪表" : "一号线主控PLC", driverName: deviceId === 2 ? "Modbus TCP" : "Siemens S7", connectStatus: "online" as const, lastCollectTime: now, redisConnected: true, degraded: false, data: [
      { pointId: 11, varSeq: 1, pointName: "运行状态", ioAddress: "DB1.DBX0.0", value: true, updateTime: now, quality: 192, dataType: "BOOL" },
      { pointId: 12, varSeq: 2, pointName: "主轴转速", ioAddress: "DB1.DBD4", value: 1480 + Math.round(Math.random() * 20), updateTime: now, quality: 192, dataType: "REAL" },
      { pointId: 13, varSeq: 3, pointName: "轴承温度", ioAddress: "DB1.DBD8", value: 46.2 + Math.random(), updateTime: now, quality: 192, dataType: "REAL" },
    ] };
  },
  async realtimePointGroups(_token, _deviceCode) {
    await delay(80);
    return [{ group_code: "g001", group_name: "默认分组", active_point_count: 3, max_point_count: 3072, status: "active" }];
  },
  async historyDevices(_token) { await delay(100); return [{ id: 1, name: "PLC-S7-01 · 一号线主控PLC", deviceCode: "PLC-S7-01", deviceName: "一号线主控PLC", typeCode: "PLC", typeName: "PLC" }, { id: 2, name: "METER-02 · 二号电力仪表", deviceCode: "METER-02", deviceName: "二号电力仪表", typeCode: "METER", typeName: "仪表" }]; },
  async historyDeviceTypes(_token) { await delay(80); return [{ typeCode: "PLC", typeName: "PLC", deviceCount: 1 }, { typeCode: "METER", typeName: "仪表", deviceCount: 1 }]; },
  async historyPoints(_token, deviceId) { await delay(80); return deviceId === 2 ? [{ varSeq: 1, pointName: "A相电压", ioAddress: "40001" }] : [{ varSeq: 1, pointName: "运行状态", ioAddress: "DB1.DBX0.0" }, { varSeq: 2, pointName: "主轴转速", ioAddress: "DB1.DBD4" }, { varSeq: 3, pointName: "轴承温度", ioAddress: "DB1.DBD8" }]; },
  async historyChart(_token, query) {
    await delay(180);
    const times = Array.from({ length: 12 }, (_, i) => new Date(Date.now() - (11 - i) * 300_000).toISOString());
    return { interval: query.interval ?? "5m", series: query.varSeqs.map((varSeq) => ({ varSeq, name: varSeq === 2 ? "主轴转速" : varSeq === 3 ? "轴承温度" : "运行状态", unit: varSeq === 2 ? "rpm" : varSeq === 3 ? "°C" : "", dataType: "REAL", data: times.map((time, index) => [time, varSeq === 2 ? 1450 + index * 4 : 42 + index * 0.5]) })) };
  },
  async historyRawMatrix(_token, query) {
    await delay(180);
    const columns = [{ prop: "ts", label: "采样时间" }, ...query.varSeqs.map((varSeq) => ({ prop: `v${varSeq}`, label: `点位 ${varSeq}`, varSeq }))];
    const rows = Array.from({ length: 8 }, (_, index) => Object.fromEntries([["ts", new Date(Date.now() - index * 60_000).toISOString()], ...query.varSeqs.map((varSeq) => [`v${varSeq}`, (varSeq * 100 + index).toFixed(2)])]));
    return { columns, rows, nextCursor: null, meta: { alignGranularity: query.alignGranularity ?? "1s", fillMode: "none" as const, pageSize: query.pageSize ?? 100 } };
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
      todayTotal: 1842,
      yesterdayTotal: 1728,
      todayWarnTotal: 34,
      todayErrorTotal: 7,
      todayCriticalTotal: 1,
      debugEnabled: debugRemainingSeconds > 0,
      debugRemainingSeconds,
      totalSizeMb: 286.4,
      maxTotalMb: 1024,
      lastCleanupTime: "2026-07-17 03:00:00",
      lastCleanupDeletedTotal: 6240,
      levelTrend: Array.from({ length: 12 }, (_, index) => ({ time: `${String(index * 2).padStart(2, "0")}:00`, info: 90 + index * 7, warn: 8 + index % 5, error: index % 4, critical: index === 8 ? 1 : 0, debug: debugRemainingSeconds > 0 ? 20 + index * 2 : 0 })),
      typeRatio: [{ type: "system", count: 620, ratio: 33.7 }, { type: "collect", count: 486, ratio: 26.4 }, { type: "storage", count: 358, ratio: 19.4 }, { type: "operation", count: 241, ratio: 13.1 }, { type: "auth", count: 137, ratio: 7.4 }],
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
      buttons: ["view", "add", "edit", "delete"],
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
  { level: "INFO",  message: "Device AGV-01 polled. points=384 quality=Good" },
  { level: "INFO",  message: "XADD rt:stream:agv:main:0 ok. id=1716890012345-0" },
  { level: "INFO",  message: "Device PLC-S7-02 polled. points=620 quality=Good" },
  { level: "INFO",  message: "Subscribe Redis stream: rt:stream:agv:main:*" },
  { level: "INFO",  message: "Device QC-100 connected. protocol=OPC UA" },
  { level: "WARN",  message: "Retry opcda tag read once. device=QC-100 tag=Temp_01" },
  { level: "WARN",  message: "Stream write lag detected. stream=rt:stream:agv:main:0 lag=18" },
  { level: "ERROR", message: "opcda read failed. device=METER-01 tag=Voltage_A" },
  { level: "DEBUG", message: "Batch flush: 384 pts → redis stream" },
  { level: "INFO",  message: "Device METER-01 reconnected. protocol=Modbus TCP" },
];

const WRITER_MSGS: { level: LogLevel; message: string }[] = [
  { level: "INFO",  message: "XREADGROUP streams=8 consumer=writer-1" },
  { level: "INFO",  message: "batch insert TDengine rows=512 table=AGV-01" },
  { level: "INFO",  message: "XACK success count=512" },
  { level: "INFO",  message: "map var_id to td_column_name success" },
  { level: "INFO",  message: "DLQ check: 0 items" },
  { level: "INFO",  message: "flush timer triggered. interval=500ms" },
  { level: "WARN",  message: "pending drain claimed=2" },
  { level: "WARN",  message: "TDengine write slow. elapsed=820ms" },
  { level: "ERROR", message: "tdengine write failed. retry later" },
  { level: "DEBUG", message: "Writer heartbeat ok. uptime=13338s" },
];

/**
 * 创建与真实 WebSocket 接口兼容的 mock 对象。
 * - 50ms 后触发 onopen（模拟握手延迟）
 * - 之后每 400ms 推一条随机 LogLine
 * - close() 后立即停止推送，不泄漏定时器
 * - send() 接受 auth 首帧但不做校验（简化 mock）
 */
export function createMockWebSocket(name: string): WebSocket {
  let closed = false;
  let openTimer:  ReturnType<typeof setTimeout>  | null = null;
  let pushTimer:  ReturnType<typeof setInterval> | null = null;

  const pool = name === "writer" ? WRITER_MSGS : COLLECTOR_MSGS;

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
    pushTimer = setInterval(() => {
      if (closed) { clearInterval(pushTimer!); pushTimer = null; return; }
      const { level, message } = pool[i % pool.length];
      i++;
      const line: LogLine = {
        timestamp: new Date().toISOString(),
        level,
        source: name,
        message,
      };
      _onmessage?.({
        type: "message",
        data: JSON.stringify(line),
      } as MessageEvent);
    }, 400);
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
