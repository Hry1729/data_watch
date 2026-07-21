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

  const doFetch = useCallback(async () => {
    try {
      const health = await agentClient.getConnectionHealth();
      if (!mountedRef.current) return;
      setData(health);
      setError(null);
    } catch (err) {
      if (!mountedRef.current) return;
      const e = err as AppError;
      if (e?.type === "unauthorized") {
        logout();
        return;
      }
      setError(e?.type === "agent_unreachable" ? "Agent 不可达" : "获取连接状态失败");
    } finally {
      if (mountedRef.current) setLoading(false);
    }
  }, [logout]);

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
    document.addEventListener("visibilitychange", onVisibilityChange);

    return () => {
      mountedRef.current = false;
      stopTimer();
      document.removeEventListener("visibilitychange", onVisibilityChange);
    };
  }, [doFetch, refreshVersion]);

  return { data, loading, error, refresh: doFetch };
}
