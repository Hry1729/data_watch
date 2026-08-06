import { useCallback, useEffect, useRef, useState } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { usePageRefresh } from "@/contexts/PageRefreshContext";
import { agentClient } from "@/lib/agent-client";
import type { AppError, ModuleRate } from "@/types";

const POLL_INTERVAL = 5_000;
const MAX_POINTS = 121;

export interface EdgeRates {
  collector: ModuleRate;
  uploader: ModuleRate;
}

interface CounterSnapshot {
  collected: number;
  uploaded: number;
  sampledAt: number;
}

let cachedRates: EdgeRates = {
  collector: { unit: "批次/min", current: null, series: [] },
  uploader: { unit: "批次/min", current: null, series: [] },
};
let previousCounters: CounterSnapshot | null = null;

function clockLabel(timestamp: number): string {
  return new Date(timestamp).toLocaleTimeString("zh-CN", {
    hour12: false,
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  });
}

function ratePerMinute(current: number, previous: number, elapsedMs: number): number {
  if (elapsedMs <= 0 || current < previous) return 0;
  return Math.round(((current - previous) * 60_000) / elapsedMs);
}

function append(rate: ModuleRate, value: number, timestamp: number): ModuleRate {
  return {
    ...rate,
    current: value,
    series: [...rate.series, { t: clockLabel(timestamp), v: value }].slice(-MAX_POINTS),
  };
}

export function useEdgeRates() {
  const { logout } = useAuth();
  const { refreshVersion } = usePageRefresh();
  const logoutRef = useRef(logout);
  const [data, setData] = useState<EdgeRates>(cachedRates);
  const [loading, setLoading] = useState(cachedRates.collector.series.length === 0);
  const [error, setError] = useState<string | null>(null);
  const [tick, setTick] = useState(0);

  useEffect(() => {
    logoutRef.current = logout;
  }, [logout]);

  const refresh = useCallback(() => setTick((value) => value + 1), []);

  useEffect(() => {
    let active = true;
    let timer: ReturnType<typeof setInterval> | null = null;

    async function sample() {
      try {
        const outbox = await agentClient.getLocalOutbox();
        if (!active) return;
        const now = Date.now();
        const counters: CounterSnapshot = {
          collected: outbox.last_seq,
          uploaded: outbox.sent_records_total,
          sampledAt: now,
        };
        const elapsed = previousCounters ? now - previousCounters.sampledAt : 0;
        const collectedRate = previousCounters
          ? ratePerMinute(counters.collected, previousCounters.collected, elapsed)
          : 0;
        const uploadedRate = previousCounters
          ? ratePerMinute(counters.uploaded, previousCounters.uploaded, elapsed)
          : 0;
        previousCounters = counters;
        cachedRates = {
          collector: append(cachedRates.collector, collectedRate, now),
          uploader: append(cachedRates.uploader, uploadedRate, now),
        };
        setData(cachedRates);
        setError(null);
      } catch (reason) {
        if (!active) return;
        const appError = reason as AppError;
        if (appError?.type === "unauthorized") {
          logoutRef.current();
          return;
        }
        setError("边缘速率读取失败");
      } finally {
        if (active) setLoading(false);
      }
    }

    function startTimer() {
      if (timer) return;
      timer = setInterval(sample, POLL_INTERVAL);
    }

    function stopTimer() {
      if (!timer) return;
      clearInterval(timer);
      timer = null;
    }

    function onVisibilityChange() {
      if (document.hidden) stopTimer();
      else {
        void sample();
        startTimer();
      }
    }

    if (!document.hidden) {
      void sample();
      startTimer();
    }
    document.addEventListener("visibilitychange", onVisibilityChange);
    return () => {
      active = false;
      stopTimer();
      document.removeEventListener("visibilitychange", onVisibilityChange);
    };
  }, [refreshVersion, tick]);

  return { data, loading, error, refresh };
}