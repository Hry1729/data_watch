/**
 * useWebSocketLog — 实时日志 WebSocket hook
 *
 * 连接策略：
 *   - real 模式：new WebSocket('ws://127.0.0.1:7070/viewer/logs/{name}/tail')
 *   - mock 模式：createMockWebSocket(name)（动态 import，生产包 tree-shaked）
 *   - onopen 后立即发送 first-frame auth：{ "type": "auth", "token": "<agentToken>" }
 *   - token **不**出现在 URL query 参数中
 *
 * 重连策略：
 *   - 非主动关闭时 5 秒后重连
 *   - 页面卸载或 enabled=false 时主动 close + 取消重连 timer
 *
 * 行数限制：
 *   - 最多保留 MAX_LINES(5000) 行：setLines(prev => [...prev, line].slice(-5000))
 */

import { useCallback, useEffect, useState } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { usePageRefresh } from "@/contexts/PageRefreshContext";
import type { LogLevel, LogLine, WsStatus } from "@/types";

// ─── WS 工厂（动态 import 确保 mock 不进入生产包）────────────────────────────

const _useMock =
  import.meta.env.MODE !== "production" &&
  import.meta.env.VITE_USE_MOCK === "true";

type WsFactory = (name: string) => WebSocket;
let _factoryPromise: Promise<WsFactory> | null = null;

function getWsFactory(): Promise<WsFactory> {
  if (_factoryPromise) return _factoryPromise;
  if (_useMock) {
    // 生产构建：此分支被 Vite 消除，mock-agent 不进入 dist
    _factoryPromise = import("@/lib/mock-agent").then(
      (m) => m.createMockWebSocket
    );
  } else {
    _factoryPromise = Promise.resolve(
      (name: string) =>
        new WebSocket(`ws://127.0.0.1:7070/viewer/logs/${name}/tail`)
    );
  }
  return _factoryPromise;
}

// ─── LogLine 解析 ─────────────────────────────────────────────────────────────

const VALID_LEVELS = new Set<string>(["DEBUG", "INFO", "WARN", "ERROR"]);

function parseLogLine(raw: string, sourceFallback: string): LogLine {
  try {
    const parsed = JSON.parse(raw) as Record<string, unknown>;
    const level: LogLevel =
      typeof parsed.level === "string" && VALID_LEVELS.has(parsed.level)
        ? (parsed.level as LogLevel)
        : "INFO";
    return {
      timestamp:
        typeof parsed.timestamp === "string"
          ? parsed.timestamp
          : new Date().toISOString(),
      level,
      source:
        typeof parsed.source === "string" ? parsed.source : sourceFallback,
      message:
        typeof parsed.message === "string" ? parsed.message : String(raw),
    };
  } catch {
    // 纯文本或非标准 JSON → 降级为 INFO
    return {
      timestamp: new Date().toISOString(),
      level: "INFO",
      source: sourceFallback,
      message: raw,
    };
  }
}

// ─── 常量 ─────────────────────────────────────────────────────────────────────

/** 最大保留行数（超出时从头部截断）；实时只保留最近几十条以省内存，更多走「历史」DB 视图 */
const MAX_LINES = 50;

/** 非主动断开后的重连等待时间（ms） */
const RECONNECT_DELAY_MS = 5000;

// ─── 钩子 Hook ─────────────────────────────────────────────────────────────────

export interface UseWebSocketLogResult {
  lines: LogLine[];
  wsStatus: WsStatus;
  /** 清空前端日志缓存，不调用 API */
  clear: () => void;
}

export function useWebSocketLog(
  name: "collector" | "writer",
  enabled = true
): UseWebSocketLogResult {
  const { auth } = useAuth();
  const { refreshVersion } = usePageRefresh();
  const agentToken = auth?.agentToken ?? null;

  const [lines,    setLines]    = useState<LogLine[]>([]);
  const [wsStatus, setWsStatus] = useState<WsStatus>("connecting");

  const clear = useCallback(() => setLines([]), []);

  useEffect(() => {
    // 未启用或未登录 → 不建立连接
    if (!enabled || !agentToken) {
      setWsStatus("disconnected");
      return;
    }

    setWsStatus("connecting");

    let ws: WebSocket | null = null;
    let reconnectTimer: ReturnType<typeof setTimeout> | null = null;
    // 主动关闭标志：true 时禁止重连
    let intentionallyClosed = false;

    function connect(factory: WsFactory) {
      if (intentionallyClosed) return;

      ws = factory(name);

      ws.onopen = () => {
        if (intentionallyClosed) { ws?.close(); return; }
        setWsStatus("connected");
        // first-frame auth：token 不放进 URL，只在首帧发送
        ws!.send(JSON.stringify({ type: "auth", token: agentToken }));
      };

      ws.onmessage = (e: MessageEvent) => {
        if (intentionallyClosed) return;
        const line = parseLogLine(String(e.data), name);
        // 5000 行上限：超出时从头部截断
        setLines((prev) => [...prev, line].slice(-MAX_LINES));
      };

      ws.onclose = () => {
        if (intentionallyClosed) return;
        setWsStatus("reconnecting");
        // 5 秒后重连
        reconnectTimer = setTimeout(() => {
          if (intentionallyClosed) return;
          connect(factory);
        }, RECONNECT_DELAY_MS);
      };

      ws.onerror = () => {
        // onerror 后 onclose 紧随其后，由 onclose 处理重连，此处不做额外处理
      };
    }

    // 获取工厂（首次为微任务，后续命中缓存为同步）
    getWsFactory().then((factory) => {
      if (intentionallyClosed) return;
      connect(factory);
    });

    // ── 清理：页面卸载或 deps 变更时调用 ──────────────────────────────────
    return () => {
      intentionallyClosed = true;
      // 取消待执行的重连 timer
      if (reconnectTimer) {
        clearTimeout(reconnectTimer);
        reconnectTimer = null;
      }
      // 主动关闭 WebSocket（onclose 因 intentionallyClosed=true 不会触发重连）
      if (ws && ws.readyState !== WebSocket.CLOSED) {
        ws.close();
      }
      ws = null;
    };
  }, [name, agentToken, enabled, refreshVersion]); // agentToken 变化时重建连接

  return { lines, wsStatus, clear };
}
