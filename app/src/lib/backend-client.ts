import type {
  AppError,
  CollectorModuleMetrics,
  ModuleRates,
  WriterLogRow,
  WriterModuleMetrics,
} from "@/types";

// ─── 接口 ─────────────────────────────────────────────────────────────────────

/** /log/list 查询参数（camelCase，与 backend 路由对齐） */
export interface LogListQuery {
  serviceName?: string;
  kind?: string;
  type?: string;
  pageNo?: number;
  pageSize?: number;
  level?: string;
  source?: string;
  startTime?: string;
  endTime?: string;
}

export interface BackendClient {
  login(userName: string, password: string): Promise<{
    token: string;
    refreshToken: string;
  }>;
  /** 查询 DB system_log（需 backend token 鉴权），返回行数组 */
  logList(token: string, query: LogListQuery): Promise<WriterLogRow[]>;
  /** 采集页一期即时指标 */
  collectorMetrics(token: string): Promise<CollectorModuleMetrics>;
  /** 入库页一期即时指标 */
  writerMetrics(token: string): Promise<WriterModuleMetrics>;
  /** 二期：采集/入库速率与趋势（同源，一个接口出两侧） */
  moduleRates(token: string): Promise<ModuleRates>;
}

// ─── fetch 封装（realBackend 专用）───────────────────────────────────────────

async function backendFetch<T>(
  method: string,
  path: string,
  body?: object,
  token?: string
): Promise<T> {
  const url = `${import.meta.env.VITE_BACKEND_URL}${path}`;
  const headers: Record<string, string> = { "Content-Type": "application/json" };
  // backend 登录返回的 token 已带 "Bearer " 前缀，规范化避免出现 "Bearer Bearer ..."
  if (token) {
    headers["Authorization"] = token.startsWith("Bearer ") ? token : `Bearer ${token}`;
  }
  let resp: Response;
  try {
    resp = await fetch(url, {
      method,
      headers,
      body: body ? JSON.stringify(body) : undefined,
    });
  } catch {
    throw {
      type: "network_error",
      message: "无法连接到后端服务",
    } satisfies AppError;
  }
  if (resp.status === 401) throw { type: "unauthorized" } satisfies AppError;
  if (resp.status >= 500)
    throw {
      type: "api_error",
      status: resp.status,
      message: "后端服务异常",
    } satisfies AppError;

  const json = await resp.json();
  return json.data as T; // 后端统一格式：{ code, msg, data }
}

// ─── realBackend 实现 ─────────────────────────────────────────────────────────

const realBackend: BackendClient = {
  login(userName, password) {
    return backendFetch<{ token: string; refreshToken: string }>(
      "POST",
      "/api/auth/login",
      { userName, password }
    );
  },

  async logList(token, query) {
    const qs = new URLSearchParams();
    for (const [k, v] of Object.entries(query)) {
      if (v !== undefined && v !== null && v !== "") qs.append(k, String(v));
    }
    const page = await backendFetch<{ list: WriterLogRow[] } | null>(
      "GET",
      `/api/device-monitor/log/list?${qs.toString()}`,
      undefined,
      token
    );
    return page?.list ?? [];
  },

  collectorMetrics(token) {
    return backendFetch<CollectorModuleMetrics>(
      "GET",
      "/api/device-monitor/module-monitor/collector",
      undefined,
      token
    );
  },

  writerMetrics(token) {
    return backendFetch<WriterModuleMetrics>(
      "GET",
      "/api/device-monitor/module-monitor/writer",
      undefined,
      token
    );
  },

  moduleRates(token) {
    return backendFetch<ModuleRates>(
      "GET",
      "/api/device-monitor/module-monitor/rates",
      undefined,
      token
    );
  },
};

// ─── mock / real 选择（动态 import 确保 mock-agent 不进入生产包）─────────────

const _useMock =
  import.meta.env.MODE !== "production" &&
  import.meta.env.VITE_USE_MOCK === "true";

let _implPromise: Promise<BackendClient>;
if (_useMock) {
  _implPromise = import("./mock-agent").then((m) => m.mockBackend);
} else {
  _implPromise = Promise.resolve(realBackend);
}

export const backendClient: BackendClient = {
  async login(userName, password) {
    const c = await _implPromise;
    return c.login(userName, password);
  },
  async logList(token, query) {
    const c = await _implPromise;
    return c.logList(token, query);
  },
  async collectorMetrics(token) {
    const c = await _implPromise;
    return c.collectorMetrics(token);
  },
  async writerMetrics(token) {
    const c = await _implPromise;
    return c.writerMetrics(token);
  },
  async moduleRates(token) {
    const c = await _implPromise;
    return c.moduleRates(token);
  },
};
