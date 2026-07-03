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

// ─── 辅助 ─────────────────────────────────────────────────────────────────────

const delay = (ms = Math.random() * 500 + 300) =>
  new Promise<void>((r) => setTimeout(r, ms));

// ─── 可变内部状态 ─────────────────────────────────────────────────────────────

let _inBootstrap = true;

const _processStatus: Record<string, ModuleStatusValue> = {
  collector: "running",
  writer:    "stopped",
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

// ─── mockAgent ────────────────────────────────────────────────────────────────

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

  async agentLogin(u, _p) {
    await delay();
    // 只要 username 非空即成功（mock 不验证密码）
    if (!u) throw { type: "unauthorized" as const };
    return { token: "mock-agent-jwt-dev" };
  },

  async getViewerStatus() {
    await delay();
    return { agent: "running" };
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
};

// ─── mockBackend ──────────────────────────────────────────────────────────────

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
};

// ─── createMockWebSocket ──────────────────────────────────────────────────────

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
