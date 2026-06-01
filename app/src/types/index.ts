// 认证状态（仅内存，不持久化）
export interface AuthStore {
  agentToken: string;
  backendToken: string;
  backendRefreshToken: string;
}

// 模块运行状态
export type ModuleStatusValue = 'running' | 'stopped' | 'crashed' | 'unknown';
export interface ModuleStatus {
  name: 'collector' | 'writer';
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
  collector: ProcessConfig;
  writer: ProcessConfig;
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
