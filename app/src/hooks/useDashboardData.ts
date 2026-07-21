import { useEffect, useRef, useState } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { usePageRefresh } from "@/contexts/PageRefreshContext";
import { backendClient } from "@/lib/backend-client";
import type {
  AppError,
  DashboardConnectionHealth,
  DashboardDeviceTypeDistribution,
  DashboardOverview,
  DashboardRecentEventList,
  DashboardTrend,
} from "@/types";

export type DashboardTrendRange = "today" | "last24h" | "last7d";

interface DashboardErrors {
  overview: string | null;
  trend: string | null;
  distribution: string | null;
  health: string | null;
  events: string | null;
}

const EMPTY_ERRORS: DashboardErrors = {
  overview: null,
  trend: null,
  distribution: null,
  health: null,
  events: null,
};

function getErrorMessage(error: unknown) {
  const appError = error as AppError;
  return appError?.type === "network_error" ? "无法连接到后端服务" : "数据加载失败";
}

export function useDashboardData() {
  const { auth, logout } = useAuth();
  const { refreshVersion, refreshCurrentPage } = usePageRefresh();
  const backendToken = auth?.backendToken ?? null;
  const logoutRef = useRef(logout);

  const [overview, setOverview] = useState<DashboardOverview | null>(null);
  const [trend, setTrend] = useState<DashboardTrend | null>(null);
  const [distribution, setDistribution] = useState<DashboardDeviceTypeDistribution | null>(null);
  const [health, setHealth] = useState<DashboardConnectionHealth | null>(null);
  const [events, setEvents] = useState<DashboardRecentEventList | null>(null);
  const [errors, setErrors] = useState<DashboardErrors>(EMPTY_ERRORS);
  const [loading, setLoading] = useState(true);
  const [trendRange, setTrendRange] = useState<DashboardTrendRange>("last24h");


  useEffect(() => {
    logoutRef.current = logout;
  }, [logout]);





  useEffect(() => {
    if (!backendToken) return;
    const token = backendToken;
    let active = true;

    async function fetchDashboard() {
      const results = await Promise.allSettled([
        backendClient.dashboardOverview(token),
        backendClient.dashboardTrend(token, trendRange),
        backendClient.dashboardDeviceTypeDistribution(token),
        backendClient.dashboardConnectionHealth(token),
        backendClient.dashboardRecentEvents(token),
      ]);
      if (!active) return;

      const unauthorized = results.some(
        (result) => result.status === "rejected" && (result.reason as AppError)?.type === "unauthorized",
      );
      if (unauthorized) {
        logoutRef.current();
        return;
      }

      const [overviewResult, trendResult, distributionResult, healthResult, eventsResult] = results;
      if (overviewResult.status === "fulfilled") setOverview(overviewResult.value);
      if (trendResult.status === "fulfilled") setTrend(trendResult.value);
      if (distributionResult.status === "fulfilled") setDistribution(distributionResult.value);
      if (healthResult.status === "fulfilled") setHealth(healthResult.value);
      if (eventsResult.status === "fulfilled") setEvents(eventsResult.value);

      setErrors({
        overview: overviewResult.status === "rejected" ? getErrorMessage(overviewResult.reason) : null,
        trend: trendResult.status === "rejected" ? getErrorMessage(trendResult.reason) : null,
        distribution:
          distributionResult.status === "rejected" ? getErrorMessage(distributionResult.reason) : null,
        health: healthResult.status === "rejected" ? getErrorMessage(healthResult.reason) : null,
        events: eventsResult.status === "rejected" ? getErrorMessage(eventsResult.reason) : null,
      });
      setLoading(false);
    }

    void fetchDashboard();
    const timer = setInterval(() => void fetchDashboard(), 30_000);

    return () => {
      active = false;
      clearInterval(timer);
    };
  }, [backendToken, refreshVersion, trendRange]);

  return {
    overview,
    trend,
    distribution,
    health,
    events,
    errors,
    loading,
    trendRange,
    setTrendRange,
    refresh: refreshCurrentPage,
  };
}
