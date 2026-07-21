import { useCallback, useEffect, useRef, useState } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { usePageRefresh } from "@/contexts/PageRefreshContext";
import type { AppError } from "@/types";

export function useBackendQuery<T>(
  load: (token: string) => Promise<T>,
  options: { enabled?: boolean; intervalMs?: number } = {},
) {
  const { auth, logout } = useAuth();
  const { refreshVersion } = usePageRefresh();
  const token = auth?.backendToken ?? null;
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

    async function fetchOnce() {
      try {
        const result = await load(activeToken);
        if (!active) return;
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
        if (active) setLoading(false);
      }
    }

    setLoading(true);
    void fetchOnce();
    const timer = options.intervalMs ? setInterval(() => void fetchOnce(), options.intervalMs) : null;
    return () => {
      active = false;
      if (timer) clearInterval(timer);
    };
  }, [enabled, load, options.intervalMs, refreshVersion, token, version]);

  return { data, loading, error, refresh, setData };
}
