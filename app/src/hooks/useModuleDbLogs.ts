/**
 * useModuleDbLogs — 拉取某模块的 DB system_log（backend /log/list）
 *
 * 最小可用版：挂载时拉一次 + 手动 refresh()，不做轮询/分页/过滤。
 * 固定查询：serviceName=<模块> & kind=system（+ 可选 logType，writer 用 storage，
 * collector 不锁 type 以涵盖其多种 log_type）。
 * 401（backendToken 过期）→ logout()，与 Agent hooks 行为一致。
 */

import { useCallback, useEffect, useRef, useState } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { usePageRefresh } from "@/contexts/PageRefreshContext";
import { backendClient, type LogListQuery } from "@/lib/backend-client";
import type { AppError, WriterLogRow } from "@/types";

export interface UseModuleDbLogsResult {
  rows: WriterLogRow[];
  loading: boolean;
  error: string | null;
  refresh: () => void;
}

export function useModuleDbLogs(
  serviceName: "writer" | "collector",
  logType?: string,
  enabled = true
): UseModuleDbLogsResult {
  const { auth, logout } = useAuth();
  const { refreshVersion } = usePageRefresh();
  const backendToken = auth?.backendToken ?? null;

  // 保存最新 logout，避免拉取 effect 捕获到旧闭包（logout 身份每次渲染变化）
  const logoutRef = useRef(logout);
  useEffect(() => {
    logoutRef.current = logout;
  }, [logout]);

  const [rows, setRows] = useState<WriterLogRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [tick, setTick] = useState(0);

  const refresh = useCallback(() => setTick((t) => t + 1), []);

  useEffect(() => {
    if (!enabled || !backendToken) return;

    const query: LogListQuery = {
      serviceName,
      kind: "system",
      pageNo: 1,
      pageSize: 200,
      // 显式省略 type（不依赖 logList 对空值的过滤）；collector 不传则查所有 log_type
      ...(logType ? { type: logType } : {}),
    };

    let cancelled = false;
    setLoading(true);
    setError(null);

    backendClient
      .logList(backendToken, query)
      .then((data) => {
        if (!cancelled) setRows(data);
      })
      .catch((e: AppError) => {
        if (cancelled) return;
        if (e?.type === "unauthorized") {
          logoutRef.current();
          return;
        }
        setError(
          e?.type === "network_error" ? "无法连接到后端服务" : "加载历史日志失败"
        );
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
    // logout 经 ref 调用，故意不入依赖
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [enabled, backendToken, serviceName, logType, refreshVersion, tick]);

  return { rows, loading, error, refresh };
}
