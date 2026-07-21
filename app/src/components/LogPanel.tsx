import { forwardRef, useEffect, useRef } from "react";
import { Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardAction, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Switch } from "@/components/ui/switch";
import { Label } from "@/components/ui/label";
import type { LogLine, LogLevel, WsStatus } from "@/types";

// ─── Level chip 颜色映射 ───────────────────────────────────────────────────────

const LEVEL_CLASS: Record<LogLevel, string> = {
  DEBUG: "bg-muted text-muted-foreground",
  INFO:  "bg-brand-soft text-brand",
  WARN:  "bg-warning-soft text-warning",
  ERROR: "bg-destructive-soft text-destructive",
};

// ─── 属性 Props ────────────────────────────────────────────────────────────────

export interface LogPanelProps {
  lines: LogLine[];
  /** WS 连接状态（T016 接入后使用；静态阶段传 "connected"） */
  wsStatus: WsStatus;
  autoScroll: boolean;
  onAutoScrollChange: (v: boolean) => void;
  /** 清空回调（T016 接入后实现；静态阶段传 undefined） */
  onClear?: () => void;
}

// ─── 组件（forwardRef 供父组件 scrollIntoView）────────────────────────────────

export const LogPanel = forwardRef<HTMLDivElement, LogPanelProps>(
  function LogPanel(
    { lines, wsStatus, autoScroll, onAutoScrollChange, onClear },
    ref
  ) {
    const scrollRef = useRef<HTMLDivElement>(null);

    // 自动滚动到底部（T016 接入真实 lines 后生效）
    useEffect(() => {
      if (autoScroll && scrollRef.current) {
        scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
      }
    }, [lines, autoScroll]);

    return (
      // ref 指向面板根节点，供父组件 scrollIntoView
      <Card
        ref={ref}
        className="h-full min-h-0 w-full"
      >
        {/* 面板头部 */}
        <CardHeader>
          <CardTitle>实时日志</CardTitle>
          <CardAction>
          <div className="flex items-center gap-3">
            {/* 自动滚动 */}
            <div className="flex items-center gap-1.5">
              <Switch
                id="log-auto-scroll"
                checked={autoScroll}
                onCheckedChange={onAutoScrollChange}
              />
              <Label
                htmlFor="log-auto-scroll"
                className="text-xs text-muted-foreground"
              >
                自动滚动
              </Label>
            </div>

            {/* 清空按钮 */}
            <Button
              variant="ghost"
              size="icon"
              className="h-6 w-6"
              onClick={onClear}
              aria-label="清空日志"
            >
              <Trash2 className="h-3.5 w-3.5 text-muted-foreground" />
            </Button>
          </div>
          </CardAction>
        </CardHeader>

        {/* 断线 Banner（wsStatus !== "connected" 时显示） */}
        {wsStatus !== "connected" && (
          <div
            className="flex-shrink-0 px-4 py-2 text-xs font-medium bg-warning-soft text-warning border-b border-warning/40"
          >
            {wsStatus === "reconnecting"
              ? "连接已断开，正在重连…"
              : wsStatus === "connecting"
              ? "日志连接中…"
              : "日志连接断开"}
          </div>
        )}

        {/* 日志滚动区域 */}
        <CardContent
          ref={scrollRef}
          className="min-h-0 flex-1 space-y-0.5 overflow-y-auto px-3 py-2"
          style={{ fontFamily: "monospace", fontSize: 13 }}
        >
          {lines.length === 0 ? (
            <p className="text-xs text-muted-foreground/70 py-4 text-center">暂无日志</p>
          ) : (
            lines.map((line, i) => (
              <div key={i} className="flex items-start gap-2 py-0.5">
                {/* 时间戳 */}
                <span className="flex-shrink-0 text-muted-foreground/70 text-xs w-24">
                  {line.timestamp.slice(11, 23)}
                </span>
                {/* 级别 chip（不渲染 source — FR-009 单模块页约定） */}
                <span
                  className={`flex-shrink-0 inline-block px-1.5 py-0.5 rounded text-[10px] font-semibold leading-none ${LEVEL_CLASS[line.level] ?? LEVEL_CLASS.INFO}`}
                >
                  {line.level}
                </span>
                {/* 消息 */}
                <span className="text-foreground text-xs break-all leading-relaxed">
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
