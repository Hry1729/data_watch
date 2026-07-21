import { invoke } from "@tauri-apps/api/core";
import { getAgentAuthToken } from "@/lib/agent-auth-token";
import type {
  AppError,
  ConnectionConfig,
  ConnectionHealth,
  DbConfig,
  ModuleStatus,
} from "@/types";

export { setAgentAuthToken } from "@/lib/agent-auth-token";

// ─── 引导页载荷类型 ───────────────────────────────────────────────────────────

export type BootstrapTarget = "backend" | "mysql" | "redis" | "tdengine";

export interface BootstrapDbInput {
  host: string;
  port: number;
  user?: string;
  password?: string;
  database?: string;
}

export interface BootstrapConfigPayload {
  backend?: { base_url: string };
  mysql?: BootstrapDbInput;
  redis?: BootstrapDbInput;
  tdengine?: BootstrapDbInput;
}

export interface BootstrapTestPayload {
  target: BootstrapTarget;
  base_url?: string;
  host?: string;
  port?: number;
  user?: string;
  password?: string;
  database?: string;
}

// ─── 接口 ─────────────────────────────────────────────────────────────────────

export interface AgentClient {
  getBootstrapStatus():    Promise<{ in_bootstrap: boolean }>;
  getRuntimeConfig():      Promise<{ backend_base_url: string }>;
  putBootstrapConfig(cfg: BootstrapConfigPayload): Promise<void>;
  postBootstrapTestConnection(req: BootstrapTestPayload): Promise<{ success: boolean; message?: string }>;
  postBootstrapFinish():   Promise<void>;
  agentLogin(u: string, p: string): Promise<{ token: string }>;
  getViewerStatus():       Promise<{ agent: string }>;
  getConnectionHealth():    Promise<ConnectionHealth>;
  getProcessMetrics(name: "collector" | "writer"): Promise<ModuleStatus>;
  getProcessStatus(name:  "collector" | "writer"): Promise<{ status: string }>;
  startProcess(name:   "collector" | "writer"): Promise<void>;
  stopProcess(name:    "collector" | "writer"): Promise<void>;
  restartProcess(name: "collector" | "writer"): Promise<void>;
  getConfig():   Promise<ConnectionConfig>;
  putConfig(cfg: Partial<ConnectionConfig>): Promise<void>;
  checkHealth(
    target: "redis" | "mysql" | "tdengine",
    config?: Partial<DbConfig>
  ): Promise<{ success: boolean; message?: string }>;
}

// ─── 日志脱敏辅助 ─────────────────────────────────────────────────────────────

function sanitize(body: object): string {
  return JSON.stringify(body).replace(
    /"password(_enc)?":"[^"]*"/g,
    '"password":"[FILTERED]"'
  );
}

function getApiErrorMessage(text: string, fallback: string): string {
  try {
    const payload = JSON.parse(text) as { detail?: unknown; message?: unknown };
    if (typeof payload.detail === "string") return payload.detail;
    if (typeof payload.message === "string") return payload.message;
  } catch {
    // 忽略无效/非 JSON 的错误响应体
  }
  return fallback;
}

// ─── invoke 封装（realAgent 专用）────────────────────────────────────────────

async function agentFetch<T>(
  method: string,
  path: string,
  body?: object,
  token?: string
): Promise<T> {
  // 序列化原始 body 用于实际请求；脱敏版本仅用于开发日志，不发送给 Agent
  const bodyStr = body ? JSON.stringify(body) : undefined;
  if (import.meta.env.DEV) {
    // 脱敏：日志中不打印 password 明文；不打印 token 值
    console.debug(`[agent] ${method} ${path}`, body ? sanitize(body) : "");
  }

  let result: [number, string];
  try {
    result = await invoke<[number, string]>("agent_request", {
      method,
      path,
      body: bodyStr,
      token: token ?? getAgentAuthToken() ?? undefined,
    });
  } catch {
    throw { type: "agent_unreachable" } satisfies AppError;
  }

  const [status, text] = result;
  if (status === 401) throw { type: "unauthorized" } satisfies AppError;
  if (status >= 400) {
    throw {
      type: "api_error",
      status,
      message: getApiErrorMessage(text, status >= 500 ? "服务异常" : "请求失败"),
    } satisfies AppError;
  }

  return JSON.parse(text) as T;
}

// ─── realAgent 实现 ───────────────────────────────────────────────────────────

function makeRealAgent(): AgentClient {
  return {
    getBootstrapStatus: () =>
      agentFetch<{ in_bootstrap: boolean }>("GET", "/bootstrap/status"),

    getRuntimeConfig: () =>
      agentFetch<{ backend_base_url: string }>("GET", "/bootstrap/runtime-config"),

    putBootstrapConfig: (cfg) =>
      agentFetch<void>("PUT", "/bootstrap/config", cfg),

    postBootstrapTestConnection: (req) =>
      agentFetch<{ success: boolean; message?: string }>(
        "POST", "/bootstrap/test-connection", req
      ),

    postBootstrapFinish: () =>
      agentFetch<void>("POST", "/bootstrap/finish"),

    agentLogin: (u, p) =>
      agentFetch<{ token: string }>("POST", "/auth/login", {
        username: u,
        password: p,
      }),

    getViewerStatus: () =>
      agentFetch<{ agent: string }>("GET", "/viewer/status"),

    getConnectionHealth: () =>
      agentFetch<ConnectionHealth>("GET", "/viewer/health"),

    getProcessMetrics: (name) =>
      agentFetch<ModuleStatus>("GET", `/viewer/processes/${name}/metrics`),

    getProcessStatus: (name) =>
      agentFetch<{ status: string }>("GET", `/viewer/processes/${name}/status`),

    startProcess:   (name) => agentFetch<void>("POST", `/admin/processes/${name}/start`),
    stopProcess:    (name) => agentFetch<void>("POST", `/admin/processes/${name}/stop`),
    restartProcess: (name) => agentFetch<void>("POST", `/admin/processes/${name}/restart`),

    getConfig: () =>
      agentFetch<ConnectionConfig>("GET", "/admin/config"),

    putConfig: (cfg) =>
      agentFetch<void>("PUT", "/admin/config", cfg),

    checkHealth: (target, config) =>
      agentFetch<{ success: boolean; message?: string }>(
        "POST", "/admin/health/check", config ? { target, config } : { target }
      ),
  };
}

const realAgent: AgentClient = makeRealAgent();

// ─── mock / real 选择（动态 import 确保 mock-agent 不进入生产包）─────────────

const _useMock =
  import.meta.env.MODE !== "production" &&
  import.meta.env.VITE_USE_MOCK === "true";

// 生产构建：_useMock 被 Vite 替换为 false，if 分支成为死代码被 Rollup 消除，
// import("./mock-agent") 不会出现在生产 dist 中。
let _implPromise: Promise<AgentClient>;
if (_useMock) {
  _implPromise = import("./mock-agent").then((m) => m.mockAgent);
} else {
  _implPromise = Promise.resolve(realAgent);
}

// 代理转发：对业务层完全透明，调用方无需关心 mock/real 切换
async function via<T>(fn: (c: AgentClient) => Promise<T>): Promise<T> {
  return fn(await _implPromise);
}

export const agentClient: AgentClient = {
  getBootstrapStatus:          ()        => via((c) => c.getBootstrapStatus()),
  getRuntimeConfig:            ()        => via((c) => c.getRuntimeConfig()),
  putBootstrapConfig:          (cfg)     => via((c) => c.putBootstrapConfig(cfg)),
  postBootstrapTestConnection: (req)     => via((c) => c.postBootstrapTestConnection(req)),
  postBootstrapFinish:         ()        => via((c) => c.postBootstrapFinish()),
  agentLogin:                  (u, p)    => via((c) => c.agentLogin(u, p)),
  getViewerStatus:             ()        => via((c) => c.getViewerStatus()),
  getConnectionHealth:         ()        => via((c) => c.getConnectionHealth()),
  getProcessMetrics:           (name)    => via((c) => c.getProcessMetrics(name)),
  getProcessStatus:            (name)    => via((c) => c.getProcessStatus(name)),
  startProcess:                (name)    => via((c) => c.startProcess(name)),
  stopProcess:                 (name)    => via((c) => c.stopProcess(name)),
  restartProcess:              (name)    => via((c) => c.restartProcess(name)),
  getConfig:                   ()        => via((c) => c.getConfig()),
  putConfig:                   (cfg)     => via((c) => c.putConfig(cfg)),
  checkHealth:                 (target, config) => via((c) => c.checkHealth(target, config)),
};
