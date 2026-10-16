import { useCallback, useState } from "react";
import { usePageRefresh } from "@/contexts/PageRefreshContext";
import { backendClient } from "@/lib/backend-client";
import { useBackendQuery } from "@/hooks/useBackendQuery";

export type DashboardTrendRange = "today" | "last24h" | "last7d";

export function useDashboardData() {
  const { refreshCurrentPage } = usePageRefresh();
  const [trendRange, setTrendRange] = useState<DashboardTrendRange>("last24h");
  const loadOverview = useCallback((token: string, signal: AbortSignal) => backendClient.dashboardOverview(token, signal), []);
  const loadTrend = useCallback((token: string, signal: AbortSignal) => backendClient.dashboardTrend(token, trendRange, signal), [trendRange]);
  const loadDistribution = useCallback((token: string, signal: AbortSignal) => backendClient.dashboardDeviceTypeDistribution(token, signal), []);
  const loadHealth = useCallback((token: string, signal: AbortSignal) => backendClient.dashboardConnectionHealth(token, signal), []);
  const overview = useBackendQuery(loadOverview, { intervalMs: 30_000 });
  const trend = useBackendQuery(loadTrend, { intervalMs: 30_000 });
  const distribution = useBackendQuery(loadDistribution, { intervalMs: 30_000 });
  const health = useBackendQuery(loadHealth, { intervalMs: 30_000 });

  return {
    overview: overview.data,
    trend: trend.data,
    distribution: distribution.data,
    health: health.data,
    errors: {
      overview: overview.error,
      trend: trend.error,
      distribution: distribution.error,
      health: health.error,
    },
    loading: overview.loading,
    panelLoading: {
      trend: trend.loading,
      distribution: distribution.loading,
      health: health.loading,
    },
    trendRange,
    setTrendRange,
    refresh: refreshCurrentPage,
  };
}
