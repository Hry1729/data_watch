/**
 * useModuleRates — 二期速率/趋势轮询（backend module-monitor/rates）
 *
 * 一个接口同时返回 collector / writer 两侧的 current + series，供采集页、入库页、
 * 总览页复用。轮询策略与 useModuleMetrics 同款：5s、document.hidden 暂停、
 * 401→logout（logoutRef 防陈旧闭包）、effect 级 active 标志防在途竞态。
 */

import { useCallback, useEffect, useRef, useState } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { usePageRefresh } from "@/contexts/PageRefreshContext";
import { backendClient } from "@/lib/backend-client";
import type { AppError, ModuleRate, ModuleRates, TrendChartState } from "@/types";

const POLL_INTERVAL = 5_000;

// ─── 速率/趋势展示辅助（采集页/入库页/总览页共用）────────────────────────────────

/** 把某侧 rate 映射为 TrendChart 四态 */
export function rateTrendState(
  rate: ModuleRate | undefined,
  _loading: boolean,
  error: string | null
): TrendChartState {
  if (error && !rate) return "error";
  if (!rate) return "loading";
  return rate.series.length ? "ready" : "empty";
}

/** 速率卡显示值（与趋势最新点同源）；无值显示 — */
export function rateCardValue(rate: ModuleRate | undefined): string {
  return rate && rate.current != null ? `${rate.current} ${rate.unit}` : "—";
}

export interface UseModuleRatesResult {
  data: ModuleRates | null;
  loading: boolean;
  error: string | null;
  refresh: () => void;
}

export function useModuleRates(): UseModuleRatesResult {
  const { auth, logout } = useAuth();
  const { refreshVersion } = usePageRefresh();
  const backendToken = auth?.backendToken ?? null;

  const logoutRef = useRef(logout);
  useEffect(() => {
    logoutRef.current = logout;
  }, [logout]);

  const [data, setData] = useState<ModuleRates | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [tick, setTick] = useState(0);

  const refresh = useCallback(() => setTick((t) => t + 1), []);

  useEffect(() => {
    if (!backendToken) return;
    const token = backendToken;

    let active = true;
    let timer: ReturnType<typeof setInterval> | null = null;

    async function fetchOnce() {
      try {
        const result = await backendClient.moduleRates(token);
        if (!active) return;
        setData(result);
        setError(null);
      } catch (err) {
        if (!active) return;
        const e = err as AppError;
        if (e?.type === "unauthorized") {
          logoutRef.current();
          return;
        }
        setError(e?.type === "network_error" ? "无法连接到后端服务" : "获取速率失败");
      } finally {
        if (active) setLoading(false);
      }
    }

    function startTimer() {
      if (timer) return;
      timer = setInterval(fetchOnce, POLL_INTERVAL);
    }

    function stopTimer() {
      if (timer) {
        clearInterval(timer);
        timer = null;
      }
    }

    function onVisibilityChange() {
      if (document.hidden) {
        stopTimer();
      } else {
        fetchOnce();
        startTimer();
      }
    }

    if (!document.hidden) {
      fetchOnce();
      startTimer();
    }
    document.addEventListener("visibilitychange", onVisibilityChange);

    return () => {
      active = false;
      stopTimer();
      document.removeEventListener("visibilitychange", onVisibilityChange);
    };
  }, [backendToken, refreshVersion, tick]);

  return { data, loading, error, refresh };
}
