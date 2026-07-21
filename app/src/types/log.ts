import type { PaginatedResult } from "@/types/device-monitor";

export type SystemLogLevel = "DEBUG" | "INFO" | "WARN" | "ERROR" | "CRITICAL";
export type SystemLogType = "system" | "auth" | "operation" | "collect" | "storage" | "frontend" | "security" | "alarm";
export type SystemLogKind = "system" | "operation" | "all";

export interface SystemLogItem {
  id: number;
  kind: SystemLogKind;
  time: string;
  level: SystemLogLevel;
  type: SystemLogType;
  source: string;
  serviceName: string;
  moduleName?: string;
  actionName?: string;
  operatorName?: string;
  deviceCode?: string;
  content: string;
  resultStatus?: string;
  durationMs?: number;
  requestId?: string;
  traceId?: string;
  detail?: Record<string, unknown>;
  exceptionStack?: string;
}

export interface SystemLogQuery {
  pageNo?: number;
  pageSize?: number;
  keyword?: string;
  startTime?: string;
  endTime?: string;
  level?: SystemLogLevel | "";
  type?: SystemLogType | "";
  kind?: SystemLogKind | "";
  source?: string;
  serviceName?: string;
  deviceCode?: string;
  operatorName?: string;
  requestId?: string;
  traceId?: string;
  resultStatus?: "success" | "failed" | "denied" | "";
}

export type SystemLogPageResult = PaginatedResult<SystemLogItem>;

export interface SystemLogStats {
  todayTotal: number;
  yesterdayTotal: number;
  todayWarnTotal: number;
  todayErrorTotal: number;
  todayCriticalTotal: number;
  debugEnabled: boolean;
  debugRemainingSeconds: number;
  debugEnabledUntil?: string;
  totalSizeMb: number;
  maxTotalMb: number;
  lastCleanupTime?: string;
  lastCleanupDeletedTotal: number;
  levelTrend: Array<{ time: string; info: number; warn: number; error: number; critical: number; debug: number }>;
  typeRatio: Array<{ type: string; count: number; ratio: number }>;
  sourceRatio: Array<{ source: string; count: number; ratio: number }>;
}

export interface LogCleanupPayload {
  mode: "policy" | "beforeTime" | "range" | "currentFilter";
  beforeTime?: string;
  startTime?: string;
  endTime?: string;
  force?: boolean;
}

export interface LogCleanupResult {
  deletedOperationLogs: number;
  deletedSystemLogs: number;
  sizeBeforeMb: number;
  sizeAfterMb: number;
  triggeredBy: string;
  triggerMode: string;
  force: boolean;
}

export interface TdengineLogFile {
  name: string;
  path?: string;
  size?: number;
  sizeMb?: number;
  modifiedAt?: string;
  updateTime?: string;
}

export interface TdengineLogTail {
  file: string;
  lines: string[] | string;
  totalLines?: number;
  truncated?: boolean;
}
