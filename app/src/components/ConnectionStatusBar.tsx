import { useConnectionHealth } from "@/hooks/useConnectionHealth";
import type { ConnectionHealth, ConnectionHealthStatus, ServiceHealth } from "@/types";

const SERVICES: {
  key: keyof ConnectionHealth;
  label: string;
}[] = [
  { key: "redis", label: "Redis" },
  { key: "mysql", label: "MySQL" },
  { key: "tdengine", label: "TDengine" },
];

const STATUS_STYLE: Record<ConnectionHealthStatus, { color: string; label: string }> = {
  connected:    { color: "var(--success)",          label: "已连接" },
  checking:     { color: "var(--brand)",             label: "检测中" },
  disconnected: { color: "var(--destructive)",       label: "断开" },
  unknown:      { color: "var(--muted-foreground)",  label: "未知" },
};

function displayHealth(
  item: ServiceHealth | undefined,
  loading: boolean
): ServiceHealth {
  if (item) return item;
  return { status: loading ? "checking" : "unknown" };
}

export function ConnectionStatusBar() {
  const { data, loading } = useConnectionHealth();

  return (
    <div className="flex items-center gap-3">
      {SERVICES.map((service) => {
        const health = displayHealth(data?.[service.key], loading);
        const status = STATUS_STYLE[health.status];
        const title = [
          `${service.label} ${status.label}`,
          health.latencyMs != null ? `${health.latencyMs}ms` : null,
          health.message ?? null,
        ].filter(Boolean).join(" · ");

        return (
          <div
            key={service.key}
            className="flex items-center gap-1.5 text-xs text-muted-foreground"
            title={title}
          >
            <span
              className="w-2 h-2 rounded-full"
              style={{ backgroundColor: status.color }}
            />
            {service.label}
        </div>
        );
      })}
    </div>
  );
}
