// 认证状态（仅内存，不持久化）
export interface AuthUserInfo {
  userId: number;
  userName: string;
  email?: string;
  avatar?: string;
  roles: string[];
  buttons: string[];
  mustChangePassword?: boolean;
}

export interface AuthStore {
  agentToken: string;
  backendToken: string;
  backendRefreshToken: string;
  /** Backend marks a newly reset account until the user changes the temporary password. */
  mustChangePassword?: boolean;
  /** 当前用户资料（角色 / 按钮权限） */
  user?: AuthUserInfo;
  /** 侧栏与路由可见 path；含桌面专有页 */
  allowedPaths?: string[];
}

// 模块运行状态
export type ModuleName = 'collector' | 'writer' | 'ingest' | 'uploader' | 'storage';
export type ModuleStatusValue = 'running' | 'stopped' | 'crashed' | 'unknown';
export interface ModuleStatus {
  name: ModuleName;
  status: ModuleStatusValue;
  pid: number | null;
  uptime_seconds: number;
  memory_mb: number;
  cpu_percent: number;
  started_at: string | null;
  restart_count: number;
}

// WS 日志行
export type LogLevel = 'DEBUG' | 'INFO' | 'WARN' | 'ERROR';
export interface LogLine {
  timestamp: string;   // ISO 8601
  level: LogLevel;
  source: string;      // 保留字段；单模块页不渲染
  message: string;
}

// DB system_log 行（backend /log/list 返回，仅取 UI 用到的字段）
export interface WriterLogRow {
  id: number;
  time: string;        // 'YYYY-MM-DD HH:mm:ss'
  level: string;       // DEBUG/INFO/WARN/ERROR/CRITICAL
  source: string;
  content: string;
}

// module-monitor 接口返回（采集/入库页一期即时指标）
export interface CollectorTaskRow {
  deviceCode: string;
  deviceName: string;
  groupName: string;
  protocol: string;
  scanRateMs: number | null;
  pointCount: number;
  lastCollectTime: string; // '' 表示无采集时间
  connectStatus: 'online' | 'unstable' | 'offline';
}
export interface CollectorModuleMetrics {
  scanRateMs: number | null;
  onlineDevices: number;
  totalDevices: number;
  pointTotal: number;
  tasks: CollectorTaskRow[];
}
export interface WriterStreamRow {
  streamKey: string;
  lag: number;
  pel: number;
  length: number;
  status: 'normal' | 'backlog';
}
export interface WriterModuleMetrics {
  streamLagTotal: number;
  pelPendingTotal: number;
  dlqTotal: number;
  streams: WriterStreamRow[];
}

// module-monitor 速率/趋势（二期：读端采样，内存滚动缓冲，~10min 窗口）
export interface RatePoint {
  t: string; // 'HH:MM:SS'
  v: number;
}
export interface ModuleRate {
  unit: string; // collector:'批次/min' writer:'条/min' collectorPoints:'点/min' writerLanded:'rows/min'
  current: number | null; // 最新点；缓冲为空时 null
  series: RatePoint[];
}
export interface ModuleRates {
  collector: ModuleRate;
  writer: ModuleRate;
  collectorPoints: ModuleRate;
  writerLanded: ModuleRate;
}

// collector 运行参数配置（app 端可配，存 MySQL collector_setting，重启 collector 生效）
export interface CollectorConfig {
  streamMaxlen: number;
}

// 顶部连接状态（Agent /viewer/health 聚合快照）
export type ConnectionHealthStatus = 'connected' | 'checking' | 'disconnected' | 'unknown';
export interface ServiceHealth {
  status: ConnectionHealthStatus;
  latencyMs?: number | null;
  message?: string | null;
  checkedAt?: string | null;
}
export interface ConnectionHealth {
  redis: ServiceHealth;
  mysql: ServiceHealth;
  tdengine: ServiceHealth;
}

// 连接配置（partial update 语义：未修改时不发送 password 字段）
export interface DbConfig {
  host: string;
  port: number;
  user: string;
  // password 缺失 = 保留原密码；本阶段不支持清空密码
  password?: string;
  database?: string;
}
export interface ProcessConfig {
  executable: string;
  working_dir: string;
}
export interface ConnectionConfig {
  redis: DbConfig;
  mysql: DbConfig;
  tdengine: DbConfig;
  collector?: ProcessConfig;
  writer?: ProcessConfig;
  ingest?: ProcessConfig;
}

// 统一应用错误
export type AppError =
  | { type: 'agent_unreachable' }
  | { type: 'unauthorized' }
  | { type: 'api_error'; status: number; message: string }
  | { type: 'network_error'; message: string };

// TrendChart 渲染状态
export type TrendChartState = 'empty' | 'loading' | 'error' | 'ready';

// WS 连接状态
export type WsStatus = 'connecting' | 'connected' | 'disconnected' | 'reconnecting';

// 工作台仪表盘
export interface DashboardOverview {
  deviceTotal: number;
  onlineDeviceTotal: number;
  offlineDeviceTotal: number;
  riskDeviceTotal: number;
  pointTotal: number;
  systemAlertTotal: number;
  systemAlertComparePrev: { countDiff: number; ratioDiff: number };
  generatedAt: string;
}

export interface DashboardDeviceTypeDistributionGroup {
  typeCode: string;
  typeName: string;
  deviceCount: number;
  onlineCount: number;
  ratio: number;
}

export interface DashboardDeviceTypeDistribution {
  total: number;
  groups: DashboardDeviceTypeDistributionGroup[];
}

export interface DashboardRecentEvent {
  eventId: string;
  eventTime: string;
  deviceId: number;
  deviceCode: string;
  deviceName: string;
  eventType: 'alarm' | 'collect' | 'connection' | string;
  eventLevel: string;
  content: string;
  status: string;
}

export interface DashboardRecentEventList {
  list: DashboardRecentEvent[];
}

export interface DashboardTrendSeries {
  metricKey: string;
  metricName: string;
  unit: string;
  data: number[];
}

export interface DashboardTrend {
  query: { rangeType: string; granularity: string };
  timeAxis: string[];
  series: DashboardTrendSeries[];
  summary: {
    collectCountTotal: number;
    alarmCountTotal: number;
    onlineRateAvg: number;
  };
}

export interface DashboardConnectionHealthItem {
  deviceId: number;
  deviceCode: string;
  deviceName: string;
  deviceType: string;
  connectStatus: 'online' | 'unstable' | 'offline';
  exceptionCount: number;
  timeoutCount: number;
  reconnectCount: number;
  successRate: number;
  lastHeartbeatTime: string;
}

export interface DashboardConnectionHealth {
  query: { statRange: string; topN: number; includeOffline: boolean };
  overview: { onlineDevices: number; offlineDevices: number; riskDevices: number };
  list: DashboardConnectionHealthItem[];
}

export type PipelineHealthStatus = "healthy" | "warning" | "critical" | "unknown" | "inactive";

export interface DashboardEdgePipelineModuleHealth {
  status: Exclude<PipelineHealthStatus, "inactive">;
  totalNodes: number;
  healthyNodes: number;
  warningNodes: number;
  criticalNodes: number;
}

export interface DashboardEdgePipelineHealth {
  heartbeatStaleSeconds: number;
  collector: DashboardEdgePipelineModuleHealth;
  uploader: DashboardEdgePipelineModuleHealth;
  generatedAt: string;
}
