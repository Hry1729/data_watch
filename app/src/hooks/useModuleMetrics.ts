/**
 * useModuleMetrics — 采集/入库页一期即时指标轮询（backend module-monitor）
 *
 * 轮询策略（比进程状态 3s 轻）：
 *   - 每 5 秒拉一次 collector / writer 指标
 *   - document.hidden 时暂停（含挂载时已隐藏的情形），切回时立即补一次
 *   - 401（backendToken 过期）→ logout()，与 useModuleDbLogs 行为一致
 *     （logout 经 ref 调用，避免轮询闭包捕获旧身份）
 *   - effect 级 active 标志：token/name 变化或卸载后，旧的在途请求不再写状态、
 *     不再误触发 logout（避免 token 刷新后被旧请求登出）
 */

import { useCallback, useEffect, useRef, useState } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { backendClient } from "@/lib/backend-client";
import type { AppError, CollectorModuleMetrics, WriterModuleMetrics } from "@/types";

const POLL_INTERVAL = 5_000; // 5 秒

export interface UseModuleMetricsResult<T> {
  data: T | null;
  loading: boolean;
  error: string | null;
  refresh: () => void;
}

export function useModuleMetrics(name: "collector"): UseModuleMetricsResult<CollectorModuleMetrics>;
export function useModuleMetrics(name: "writer"): UseModuleMetricsResult<WriterModuleMetrics>;
export function useModuleMetrics(
  name: "collector" | "writer"
): UseModuleMetricsResult<CollectorModuleMetrics | WriterModuleMetrics> {
  const { auth, logout } = useAuth();
  const backendToken = auth?.backendToken ?? null;

  // 保存最新 logout，避免轮询 effect 捕获旧闭包（logout 身份每次渲染变化）
  const logoutRef = useRef(logout);
  useEffect(() => {
    logoutRef.current = logout;
  }, [logout]);

  const [data, setData] = useState<CollectorModuleMetrics | WriterModuleMetrics | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [tick, setTick] = useState(0);

  const refresh = useCallback(() => setTick((t) => t + 1), []);

  useEffect(() => {
    if (!backendToken) return;
    const token = backendToken; // 收窄为 string，供下方异步闭包使用

    let active = true;
    let timer: ReturnType<typeof setInterval> | null = null;

    async function fetchOnce() {
      try {
        const result =
          name === "collector"
            ? await backendClient.collectorMetrics(token)
            : await backendClient.writerMetrics(token);
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
        setError(e?.type === "network_error" ? "无法连接到后端服务" : "获取指标失败");
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
        fetchOnce(); // 切回时立即补一次
        startTimer();
      }
    }

    // 挂载时若页面已隐藏则不启动，等可见事件再拉
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
  }, [name, backendToken, tick]);

  return { data, loading, error, refresh };
}
