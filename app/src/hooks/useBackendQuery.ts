import { useCallback, useEffect, useRef, useState } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { BUILD_ROLE } from "@/config/build-role";
import { usePageRefresh } from "@/contexts/PageRefreshContext";
import type { AppError } from "@/types";

export function useBackendQuery<T>(
  load: (token: string, signal: AbortSignal) => Promise<T>,
  options: { enabled?: boolean; intervalMs?: number } = {},
) {
  const { auth, logout } = useAuth();
  const { refreshVersion } = usePageRefresh();
  const token = auth?.backendToken || (BUILD_ROLE === "edge" ? auth?.agentToken ?? null : null);
  const logoutRef = useRef(logout);
  const [data, setData] = useState<T | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [version, setVersion] = useState(0);
  const enabled = options.enabled ?? true;

  useEffect(() => {
    logoutRef.current = logout;
  }, [logout]);

  const refresh = useCallback(() => setVersion((value) => value + 1), []);

  useEffect(() => {
    if (!token || !enabled) {
      setLoading(false);
      return;
    }
    const activeToken = token;
    let active = true;
    let inFlight = false;
    const controller = new AbortController();

    async function fetchOnce() {
      if (inFlight) return;
      inFlight = true;
      try {
        const result = await load(activeToken, controller.signal);
        if (!active || controller.signal.aborted) return;
        setData(result);
        setError(null);
      } catch (reason) {
        if (!active) return;
        const appError = reason as AppError;
        if (appError?.type === "unauthorized") {
          logoutRef.current();
          return;
        }
        setError(appError?.type === "network_error" ? "无法连接到后端服务" : "数据加载失败");
      } finally {
        inFlight = false;
        if (active) setLoading(false);
      }
    }

    setLoading(true);
    let timer: ReturnType<typeof setInterval> | null = null;
    function stopTimer() {
      if (timer) clearInterval(timer);
      timer = null;
    }
    function onVisibilityChange() {
      stopTimer();
      if (!document.hidden) {
        void fetchOnce();
        if (options.intervalMs) timer = setInterval(() => void fetchOnce(), options.intervalMs);
      }
    }
    // One-shot queries still run immediately; only periodic refreshes follow visibility.
    if (options.intervalMs) {
      onVisibilityChange();
      document.addEventListener("visibilitychange", onVisibilityChange);
    } else {
      void fetchOnce();
    }
    return () => {
      active = false;
      controller.abort();
      stopTimer();
      document.removeEventListener("visibilitychange", onVisibilityChange);
    };
  }, [enabled, load, options.intervalMs, refreshVersion, token, version]);

  return { data, loading, error, refresh, setData };
}
