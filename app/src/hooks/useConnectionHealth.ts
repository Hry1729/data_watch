/**
 * useConnectionHealth — 顶部 Redis/MySQL/TDengine 连接状态轮询
 *
 * 轮询策略：
 *   - 每 10 秒调用 Agent GET /viewer/health
 *   - document.hidden 时暂停，切回时立即重取
 *   - 401 → 调用 logout，不继续轮询
 */

import { useCallback, useEffect, useRef, useState } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { usePageRefresh } from "@/contexts/PageRefreshContext";
import { agentClient } from "@/lib/agent-client";
import type { AppError, ConnectionHealth } from "@/types";

const POLL_INTERVAL = 10_000;
export const CONNECTION_HEALTH_REFRESH_EVENT = "datawatch:connection-health-refresh";

export interface UseConnectionHealthResult {
  data: ConnectionHealth | null;
  loading: boolean;
  error: string | null;
  refresh: () => void;
}

export function useConnectionHealth(): UseConnectionHealthResult {
  const { logout } = useAuth();
  const { refreshVersion } = usePageRefresh();

  const [data, setData] = useState<ConnectionHealth | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const mountedRef = useRef(true);
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const requestRef = useRef<AbortController | null>(null);

  const doFetch = useCallback(async () => {
    if (requestRef.current) return;
    const controller = new AbortController();
    requestRef.current = controller;
    try {
      const health = await agentClient.getConnectionHealth(controller.signal);
      if (!mountedRef.current || controller.signal.aborted) return;
      setData(health);
      setError(null);
    } catch (err) {
      if (!mountedRef.current || controller.signal.aborted) return;
      const e = err as AppError;
      if (e?.type === "unauthorized") {
        logout();
        return;
      }
      setError(e?.type === "agent_unreachable" ? "Agent 不可达" : "获取连接状态失败");
    } finally {
      if (requestRef.current === controller) requestRef.current = null;
      if (mountedRef.current && !controller.signal.aborted) setLoading(false);
    }
  }, [logout]);

  const refresh = useCallback(() => {
    requestRef.current?.abort();
    requestRef.current = null;
    void doFetch();
  }, [doFetch]);

  useEffect(() => {
    mountedRef.current = true;

    function startTimer() {
      if (timerRef.current) return;
      timerRef.current = setInterval(doFetch, POLL_INTERVAL);
    }

    function stopTimer() {
      if (timerRef.current) {
        clearInterval(timerRef.current);
        timerRef.current = null;
      }
    }

    function onVisibilityChange() {
      if (document.hidden) {
        stopTimer();
      } else {
        doFetch();
        startTimer();
      }
    }

    if (!document.hidden) {
      doFetch();
      startTimer();
    }
    const onRequestedRefresh = refresh;
    document.addEventListener("visibilitychange", onVisibilityChange);
    window.addEventListener(CONNECTION_HEALTH_REFRESH_EVENT, onRequestedRefresh);

    return () => {
      mountedRef.current = false;
      requestRef.current?.abort();
      requestRef.current = null;
      stopTimer();
      document.removeEventListener("visibilitychange", onVisibilityChange);
      window.removeEventListener(CONNECTION_HEALTH_REFRESH_EVENT, onRequestedRefresh);
    };
  }, [doFetch, refresh, refreshVersion]);

  return { data, loading, error, refresh };
}
