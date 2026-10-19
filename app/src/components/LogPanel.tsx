import { forwardRef, useEffect, useMemo, useRef, type ReactNode } from "react";
import { Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardAction, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Switch } from "@/components/ui/switch";
import { Label } from "@/components/ui/label";
import { cn } from "@/lib/utils";
import type { LogLine, LogLevel, WsStatus } from "@/types";

// ─── Level chip 颜色映射 ───────────────────────────────────────────────────────

export const LOG_LEVEL_CLASS: Record<string, string> = {
  DEBUG:    "bg-muted text-muted-foreground",
  INFO:     "bg-brand-soft text-brand",
  WARN:     "bg-warning-soft text-warning",
  ERROR:    "bg-destructive-soft text-destructive",
  CRITICAL: "bg-destructive-soft text-destructive",
};

/** 日志行公共样式（实时 / 历史两张卡共用） */
export const LOG_LINES_CLASS =
  "min-h-0 flex-1 overflow-y-auto px-3.5 pb-2.5 pt-2 font-mono text-[11.5px] group-data-[size=sm]/card:px-3.5";
export const LOG_LINE_CLASS = "flex items-start gap-2 py-[2.5px] leading-[1.5]";
export const LOG_LEVEL_CHIP_CLASS =
  "mt-px min-w-[42px] shrink-0 rounded-[4px] px-[5px] py-[3px] text-center text-[10px] font-semibold leading-none";

// ─── 属性 Props ────────────────────────────────────────────────────────────────

export interface LogPanelProps {
  lines: LogLine[];
  /** WS 连接状态（T016 接入后使用；静态阶段传 "connected"） */
  wsStatus: WsStatus;
  autoScroll: boolean;
  onAutoScrollChange: (v: boolean) => void;
  /** 清空回调（T016 接入后实现；静态阶段传 undefined） */
  onClear?: () => void;
  /** 标题右侧额外控件（如实时/历史切换） */
  headerExtra?: ReactNode;
}

const COUNT_LEVELS: LogLevel[] = ["INFO", "DEBUG", "WARN", "ERROR"];

// ─── 组件（forwardRef 供父组件 scrollIntoView）────────────────────────────────

export const LogPanel = forwardRef<HTMLDivElement, LogPanelProps>(
  function LogPanel(
    { lines, wsStatus, autoScroll, onAutoScrollChange, onClear, headerExtra },
    ref
  ) {
    const scrollRef = useRef<HTMLDivElement>(null);

    // 自动滚动到底部（T016 接入真实 lines 后生效）
    useEffect(() => {
      if (autoScroll && scrollRef.current) {
        scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
      }
    }, [lines, autoScroll]);

    // 级别计数：前端缓冲的日志行统计
    const counts = useMemo(() => {
      const result: Record<string, number> = { INFO: 0, DEBUG: 0, WARN: 0, ERROR: 0 };
      lines.forEach((line) => {
        if (line.level in result) result[line.level] += 1;
      });
      return result;
    }, [lines]);

    return (
      // ref 指向面板根节点，供父组件 scrollIntoView
      <Card
        ref={ref}
        className="h-full min-h-0 w-full gap-0 py-0"
      >
        {/* 面板头部 */}
        <CardHeader className="items-center px-4 pt-4">
          <CardTitle className="self-center text-sm font-semibold leading-[26px]">实时日志</CardTitle>
          <CardAction className="self-center">
            <div className="flex items-center gap-2.5 text-xs text-muted-foreground">
              {headerExtra}
              {/* 自动滚动 */}
              <div className="ml-0.5 flex items-center gap-1.5">
                <Switch
                  id="log-auto-scroll"
                  checked={autoScroll}
                  onCheckedChange={onAutoScrollChange}
                  className="data-checked:bg-brand"
                />
                <Label
                  htmlFor="log-auto-scroll"
                  className="cursor-pointer text-xs font-normal text-muted-foreground"
                >
                  自动滚动
                </Label>
              </div>

              {/* 清空按钮 */}
              <Button
                variant="ghost"
                size="icon"
                className="size-[26px] rounded-md text-muted-foreground hover:text-foreground"
                onClick={onClear}
                aria-label="清空日志"
              >
                <Trash2 className="size-3.5" />
              </Button>
            </div>
          </CardAction>
        </CardHeader>

        {/* 级别计数 */}
        <div className="flex shrink-0 gap-3 border-b border-border px-4 py-2.5 text-[11px] text-muted-foreground">
          {COUNT_LEVELS.map((level) => (
            <span key={level}>
              {level}{" "}
              <b
                className={cn(
                  "font-semibold tabular-nums text-foreground",
                  level === "WARN" && "text-warning",
                  level === "ERROR" && "text-destructive",
                )}
              >
                {counts[level]}
              </b>
            </span>
          ))}
        </div>

        {/* 断线 Banner（wsStatus !== "connected" 时显示） */}
        {wsStatus !== "connected" && (
          <div
            className="flex-shrink-0 border-b border-warning/40 bg-warning-soft px-4 py-[7px] text-xs font-medium text-warning"
          >
            {wsStatus === "reconnecting"
              ? "连接已断开，正在重连…"
              : wsStatus === "connecting"
              ? "日志连接中…"
              : "日志连接断开"}
          </div>
        )}

        {/* 日志滚动区域 */}
        <CardContent ref={scrollRef} className={LOG_LINES_CLASS}>
          {lines.length === 0 ? (
            <p className="py-5 text-center font-sans text-xs text-muted-foreground">暂无日志</p>
          ) : (
            lines.map((line, i) => (
              <div key={i} className={LOG_LINE_CLASS}>
                {/* 时间戳 */}
                <span className="w-[84px] shrink-0 tabular-nums text-muted-foreground opacity-80">
                  {line.timestamp.slice(11, 23)}
                </span>
                {/* 级别 chip（不渲染 source — FR-009 单模块页约定） */}
                <span className={cn(LOG_LEVEL_CHIP_CLASS, LOG_LEVEL_CLASS[line.level] ?? LOG_LEVEL_CLASS.INFO)}>
                  {line.level}
                </span>
                {/* 消息 */}
                <span className="min-w-0 break-words text-foreground">
                  {line.message}
                </span>
              </div>
            ))
          )}
        </CardContent>
      </Card>
    );
  }
);
