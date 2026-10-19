import type { ReactNode } from "react";
import { RefreshCw } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardAction, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import type { WriterLogRow } from "@/types";

// ─── Level chip 颜色映射（CRITICAL 复用 ERROR 红）─────────────────────────────

const LEVEL_CLASS: Record<string, string> = {
  DEBUG:    "bg-muted text-muted-foreground",
  INFO:     "bg-brand-soft text-brand",
  WARN:     "bg-warning-soft text-warning",
  ERROR:    "bg-destructive-soft text-destructive",
  CRITICAL: "bg-destructive-soft text-destructive",
};

// ─── 属性 Props ────────────────────────────────────────────────────────────────

export interface ModuleDbLogPanelProps {
  rows: WriterLogRow[];
  loading: boolean;
  error: string | null;
  onRefresh: () => void;
  /** 标题右侧额外控件（如实时/历史切换） */
  headerExtra?: ReactNode;
}

// ─── 组件（DB system_log 历史视图，结构对齐 LogPanel 外壳；collector/writer 通用）──

export function ModuleDbLogPanel({
  rows,
  loading,
  error,
  onRefresh,
  headerExtra,
}: ModuleDbLogPanelProps) {
  return (
    <Card className="h-full min-h-0 w-full">
      {/* 面板头部 */}
      <CardHeader>
        <CardTitle>历史日志（system_log）</CardTitle>
        <CardAction>
        <div className="flex items-center gap-2">
        {headerExtra}
        <Button
          variant="ghost"
          size="icon"
          className="h-6 w-6"
          onClick={onRefresh}
          disabled={loading}
          aria-label="刷新历史日志"
        >
          <RefreshCw
            className={`h-3.5 w-3.5 text-muted-foreground ${loading ? "animate-spin" : ""}`}
          />
        </Button>
        </div>
        </CardAction>
      </CardHeader>

      {/* 错误 Banner */}
      {error && (
        <div
          className="flex-shrink-0 px-4 py-2 text-xs font-medium bg-destructive-soft text-destructive border-b border-destructive/40"
        >
          {error}
        </div>
      )}

      {/* 日志滚动区域（DB 返回按时间倒序，最新在上） */}
      <CardContent
        className="min-h-0 flex-1 space-y-0.5 overflow-y-auto px-3 py-2"
        style={{ fontFamily: "monospace", fontSize: 13 }}
      >
        {rows.length === 0 ? (
          <p className="text-xs text-muted-foreground/70 py-4 text-center">
            {loading ? "加载中…" : "暂无日志"}
          </p>
        ) : (
          rows.map((row) => (
            <div key={row.id} className="flex items-start gap-2 py-0.5">
              {/* 时间戳 */}
              <span className="flex-shrink-0 text-muted-foreground/70 text-xs w-32">{row.time}</span>
              {/* 级别 chip */}
              <span
                className={`flex-shrink-0 inline-block px-1.5 py-0.5 rounded text-[10px] font-semibold leading-none ${LEVEL_CLASS[row.level] ?? LEVEL_CLASS.INFO}`}
              >
                {row.level}
              </span>
              {/* source（DB 视图保留模块细分） */}
              <span className="flex-shrink-0 text-muted-foreground text-xs">{row.source}</span>
              {/* 内容 */}
              <span className="text-foreground text-xs break-all leading-relaxed">
                {row.content}
              </span>
            </div>
          ))
        )}
      </CardContent>
    </Card>
  );
}
