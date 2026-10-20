import type { ComponentType, SVGProps } from "react";
import { useConnectionHealth } from "@/hooks/useConnectionHealth";
import {
  MysqlDbIcon,
  RedisDbIcon,
  TdengineDbIcon,
} from "@/components/icons/db-icons";
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import type { ConnectionHealth, ConnectionHealthStatus, ServiceHealth } from "@/types";

const SERVICES: {
  key: keyof ConnectionHealth;
  label: string;
  Icon: ComponentType<SVGProps<SVGSVGElement>>;
}[] = [
  { key: "redis", label: "Redis", Icon: RedisDbIcon },
  { key: "mysql", label: "MySQL", Icon: MysqlDbIcon },
  { key: "tdengine", label: "TDengine", Icon: TdengineDbIcon },
];

/* 颜色即状态:绿=正常,红=异常,灰=未配置(检测中同样按未就绪的灰处理) */
const STATUS_STYLE: Record<ConnectionHealthStatus, { color: string; label: string }> = {
  connected:    { color: "var(--success)",          label: "正常" },
  disconnected: { color: "var(--destructive)",      label: "异常" },
  checking:     { color: "var(--muted-foreground)", label: "检测中" },
  unknown:      { color: "var(--muted-foreground)", label: "未配置" },
};

/* 健康检查失败时，上次成功的结果只是旧快照，不能当作当前状态展示 */
const STALE_STYLE = { color: "var(--muted-foreground)", label: "状态未知" };

function formatLastSuccess(timestamp: number | null): string {
  if (timestamp == null) return "";
  return `，最后成功检测 ${new Date(timestamp).toLocaleTimeString("zh-CN", { hour12: false })}`;
}

function displayHealth(
  item: ServiceHealth | undefined,
  loading: boolean
): ServiceHealth {
  if (item) return item;
  return { status: loading ? "checking" : "unknown" };
}

/* 三个库各一个黑色图标,右下角一个状态点表示连接情况,悬停（或键盘聚焦）显示库名与状态 */
export function ConnectionStatusBar() {
  const { data, loading, error, lastSuccessAt } = useConnectionHealth();
  const stale = error !== null;
  const healths: ServiceHealth[] = stale
    ? SERVICES.map(() => ({ status: "unknown", message: `${error}${formatLastSuccess(lastSuccessAt)}` }))
    : SERVICES.map((service) => displayHealth(data?.[service.key], loading));
  const styles = healths.map((health) => (stale ? STALE_STYLE : STATUS_STYLE[health.status]));
  const summary = SERVICES.map(
    (service, index) => `${service.label}${styles[index].label}`
  ).join("，");

  return (
    <TooltipProvider delayDuration={200}>
      <span className="inline-flex shrink-0 items-center gap-1.5" aria-label={`数据库状态：${summary}`}>
        {SERVICES.map((service, index) => {
          const health = healths[index];
          const style = styles[index];
          const detail = [
            health.latencyMs != null ? `${health.latencyMs}ms` : null,
            health.message ?? null,
          ]
            .filter(Boolean)
            .join(" · ");

          return (
            <Tooltip key={service.key}>
              <TooltipTrigger asChild>
                <span
                  tabIndex={0}
                  aria-label={`${service.label}：${style.label}${detail ? `，${detail}` : ""}`}
                  className="relative flex h-7 w-7 shrink-0 cursor-pointer items-center justify-center rounded-md text-muted-foreground outline-none transition-colors hover:bg-sidebar-accent hover:text-foreground focus-visible:bg-sidebar-accent"
                >
                  <service.Icon className="h-4 w-4 shrink-0" />
                  <span
                    className="absolute bottom-1 right-1 h-1.5 w-1.5 rounded-full ring-2 ring-sidebar"
                    style={{ backgroundColor: style.color }}
                  />
                </span>
              </TooltipTrigger>
              <TooltipContent side="bottom" className="max-w-56">
                <p className="font-medium">
                  {service.label} · {style.label}
                </p>
                {detail && <p className="mt-1 text-xs opacity-80">{detail}</p>}
              </TooltipContent>
            </Tooltip>
          );
        })}
      </span>
    </TooltipProvider>
  );
}
