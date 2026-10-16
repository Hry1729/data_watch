/**
 * useModuleStatus — 进程状态轮询 + useProcessControl — 进程控制
 *
 * 轮询策略：
 *   - 每 3 秒调用 getProcessMetrics(name)
 *   - document.hidden 时暂停，切回时立即重取
 *   - 401 → 调用 logout，不继续轮询
 */

import { useCallback, useEffect, useRef, useState } from "react";
import { toast } from "sonner";
import { useAuth } from "@/contexts/AuthContext";
import { usePageRefresh } from "@/contexts/PageRefreshContext";
import { agentClient } from "@/lib/agent-client";
import type { AppError, ModuleName, ModuleStatus } from "@/types";

const POLL_INTERVAL = 3_000; // 3 秒

// ─── useModuleStatus 模块状态 ──────────────────────────────────────────────────────

export interface UseModuleStatusResult {
  data: ModuleStatus | null;
  loading: boolean;
  error: string | null;
  refresh: () => void;
}

export function useModuleStatus(
  name: ModuleName
): UseModuleStatusResult {
  const { logout } = useAuth();
  const { refreshVersion } = usePageRefresh();

  const [data,    setData]    = useState<ModuleStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [error,   setError]   = useState<string | null>(null);

  const mountedRef = useRef(true);
  const timerRef   = useRef<ReturnType<typeof setInterval> | null>(null);
  const requestRef = useRef<AbortController | null>(null);

  // 单次拉取（内部）
  const doFetch = useCallback(async () => {
    if (requestRef.current) return;
    const controller = new AbortController();
    requestRef.current = controller;
    try {
      const metrics = await agentClient.getProcessMetrics(name, controller.signal);
      if (!mountedRef.current || controller.signal.aborted) return;
      setData(metrics);
      setError(null);
    } catch (err) {
      if (!mountedRef.current || controller.signal.aborted) return;
      const e = err as AppError;
      if (e?.type === "unauthorized") { logout(); return; }
      setError(e?.type === "agent_unreachable" ? "Agent 不可达" : "获取状态失败");
    } finally {
      if (requestRef.current === controller) requestRef.current = null;
      if (mountedRef.current && !controller.signal.aborted) setLoading(false);
    }
  }, [name, logout]);

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
        doFetch();   // 切回时立即补一次
        startTimer();
      }
    }

    doFetch();
    startTimer();
    document.addEventListener("visibilitychange", onVisibilityChange);

    return () => {
      mountedRef.current = false;
      requestRef.current?.abort();
      requestRef.current = null;
      stopTimer();
      document.removeEventListener("visibilitychange", onVisibilityChange);
    };
  }, [doFetch, refreshVersion]);

  return { data, loading, error, refresh };
}

// ─── useProcessControl 进程控制 ────────────────────────────────────────────────────

/** 需要确认的操作 */
export type ControlAction = "stop" | "restart";

export interface UseProcessControlResult {
  operating: boolean;
  /** 非 null 时显示确认 AlertDialog */
  pendingAction: ControlAction | null;
  onStart:   () => void;
  onStop:    () => void;
  onRestart: () => void;
  /** AlertDialog 确认按钮 */
  onConfirm: () => void;
  /** AlertDialog 取消 / onOpenChange(false) */
  onCancel:  () => void;
}

export function useProcessControl(
  name: ModuleName,
  refresh: () => void
): UseProcessControlResult {
  const { logout } = useAuth();

  const [operating,     setOperating]     = useState(false);
  const [pendingAction, setPendingAction] = useState<ControlAction | null>(null);

  async function execute(action: "start" | ControlAction) {
    setOperating(true);
    setPendingAction(null);
    try {
      if (action === "start")   await agentClient.startProcess(name);
      if (action === "stop")    await agentClient.stopProcess(name);
      if (action === "restart") await agentClient.restartProcess(name);
      refresh();
    } catch (err) {
      const e = err as AppError;
      if (e?.type === "unauthorized") { logout(); return; }
      const label =
        action === "start" ? "启动" : action === "stop" ? "停止" : "重启";
      toast.error(`${name} ${label}失败`);
    } finally {
      setOperating(false);
    }
  }

  return {
    operating,
    pendingAction,
    onStart:   () => execute("start"),
    onStop:    () => setPendingAction("stop"),
    onRestart: () => setPendingAction("restart"),
    onConfirm: () => { if (pendingAction) execute(pendingAction); },
    onCancel:  () => setPendingAction(null),
  };
}

// ─── 辅助：当 data 为 null 时的 fallback 状态 ─────────────────────────────────

export function fallbackStatus(name: ModuleName): ModuleStatus {
  return {
    name,
    status:          "unknown",
    pid:             null,
    uptime_seconds:  0,
    memory_mb:       0,
    cpu_percent:     0,
    started_at:      null,
    restart_count:   0,
  };
}
