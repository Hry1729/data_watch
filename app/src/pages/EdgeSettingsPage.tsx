import { useCallback, useEffect, useState } from "react";
import { CircleCheck, CircleX, LoaderCircle, PlugZap, Save } from "lucide-react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { CONNECTION_HEALTH_REFRESH_EVENT } from "@/hooks/useConnectionHealth";
import { agentClient, type EdgeLocalSettings } from "@/lib/agent-client";
import { buildConfigPatch } from "@/lib/config-patch";
import type { ConnectionConfig, DbConfig } from "@/types";

type DbTarget = "redis" | "mysql" | "tdengine";
type TestStatus = "unknown" | "testing" | "connected" | "disconnected";

interface DbForm {
  host: string;
  port: string;
  user: string;
  password: string;
  database: string;
}

interface TestState {
  status: TestStatus;
  error: string | null;
}

const DB_META: Record<DbTarget, { title: string; description: string }> = {
  redis: { title: "Redis", description: "中心实时缓冲" },
  mysql: { title: "MySQL", description: "中心配置库" },
  tdengine: { title: "TDengine", description: "中心时序库" },
};

const DEFAULT_DATABASES: Record<DbTarget, DbForm> = {
  redis: { host: "127.0.0.1", port: "6379", user: "", password: "", database: "0" },
  mysql: { host: "127.0.0.1", port: "3306", user: "root", password: "", database: "device_watch" },
  tdengine: { host: "127.0.0.1", port: "6041", user: "root", password: "", database: "device_data" },
};

const INITIAL_TESTS: Record<DbTarget, TestState> = {
  redis: { status: "unknown", error: null },
  mysql: { status: "unknown", error: null },
  tdengine: { status: "unknown", error: null },
};

function registrationLabel(value: string): string {
  if (value === "approved") return "已批准";
  if (value === "pending") return "待审批";
  if (value === "revoked") return "已吊销";
  if (value === "register_error") return "注册失败";
  return value || "未注册";
}

function databaseForms(config: ConnectionConfig): Record<DbTarget, DbForm> {
  const convert = (value: DbConfig, fallback: DbForm): DbForm => ({
    host: value.host,
    port: String(value.port),
    user: value.user ?? fallback.user,
    password: "",
    database: value.database ?? fallback.database,
  });
  return {
    redis: convert(config.redis, DEFAULT_DATABASES.redis),
    mysql: convert(config.mysql, DEFAULT_DATABASES.mysql),
    tdengine: convert(config.tdengine, DEFAULT_DATABASES.tdengine),
  };
}

function validateDatabase(target: DbTarget, form: DbForm): string | null {
  const port = Number(form.port);
  if (!form.host.trim()) return `${DB_META[target].title} 主机不能为空`;
  if (!Number.isInteger(port) || port < 1 || port > 65535) {
    return `${DB_META[target].title} 端口必须是 1-65535 的整数`;
  }
  if (target !== "redis" && !form.user.trim()) {
    return `${DB_META[target].title} 用户不能为空`;
  }
  return null;
}

function databasePayload(form: DbForm, includePassword: boolean): Partial<DbConfig> {
  const payload: Partial<DbConfig> = {
    host: form.host.trim(),
    port: Number(form.port),
    user: form.user.trim(),
    database: form.database.trim() || undefined,
  };
  if (includePassword) payload.password = form.password;
  return payload;
}

function testBadge(status: TestStatus) {
  if (status === "testing") {
    return <Badge variant="secondary"><LoaderCircle className="size-3 animate-spin" />检测中</Badge>;
  }
  if (status === "connected") {
    return <Badge variant="outline"><CircleCheck className="size-3 text-success" />已连接</Badge>;
  }
  if (status === "disconnected") {
    return <Badge variant="destructive"><CircleX className="size-3" />连接失败</Badge>;
  }
  return <Badge variant="secondary">未检测</Badge>;
}

function DatabaseCard({
  target,
  form,
  test,
  disabled,
  onChange,
  onPasswordChange,
  onTest,
}: {
  target: DbTarget;
  form: DbForm;
  test: TestState;
  disabled: boolean;
  onChange: (field: keyof DbForm, value: string) => void;
  onPasswordChange: (value: string) => void;
  onTest: () => void;
}) {
  const meta = DB_META[target];
  return (
    <Card>
      <CardHeader>
        <div className="min-w-0">
          <CardTitle className="flex items-center gap-2">{meta.title}{testBadge(test.status)}</CardTitle>
          <CardDescription>{meta.description}</CardDescription>
        </div>
        <CardAction>
          <Button variant="outline" size="sm" onClick={onTest} disabled={disabled || test.status === "testing"}>
            <PlugZap className="size-4" />
            测试连接
          </Button>
        </CardAction>
      </CardHeader>
      <CardContent className="space-y-4">
        {test.error && <p className="text-xs text-destructive" role="alert">{test.error}</p>}
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-5">
          <div className="space-y-1.5 lg:col-span-2">
            <Label htmlFor={`${target}-host`}>主机</Label>
            <Input id={`${target}-host`} value={form.host} onChange={(event) => onChange("host", event.target.value)} disabled={disabled} />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor={`${target}-port`}>端口</Label>
            <Input id={`${target}-port`} type="number" min={1} max={65535} value={form.port} onChange={(event) => onChange("port", event.target.value)} disabled={disabled} />
          </div>
          {target === "redis" ? (
            <div className="space-y-1.5">
              <Label htmlFor={`${target}-database`}>DB</Label>
              <Input id={`${target}-database`} value={form.database} onChange={(event) => onChange("database", event.target.value)} disabled={disabled} />
            </div>
          ) : (
            <>
              <div className="space-y-1.5">
                <Label htmlFor={`${target}-user`}>用户</Label>
                <Input id={`${target}-user`} value={form.user} onChange={(event) => onChange("user", event.target.value)} disabled={disabled} />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor={`${target}-database`}>数据库</Label>
                <Input id={`${target}-database`} value={form.database} onChange={(event) => onChange("database", event.target.value)} disabled={disabled} />
              </div>
            </>
          )}
          <div className="space-y-1.5 lg:col-span-2">
            <Label htmlFor={`${target}-password`}>密码</Label>
            <Input
              id={`${target}-password`}
              type="password"
              value={form.password}
              placeholder="留空则保留原密码"
              autoComplete="off"
              onChange={(event) => onPasswordChange(event.target.value)}
              disabled={disabled}
            />
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

export default function EdgeSettingsPage() {
  const [settings, setSettings] = useState<EdgeLocalSettings | null>(null);
  const [databases, setDatabases] = useState(DEFAULT_DATABASES);
  // 最近一次从 Agent 成功加载（或成功应用）的配置；为 null 表示从未加载成功，禁止测试应用与保存
  const [loadedDatabases, setLoadedDatabases] = useState<Record<DbTarget, DbForm> | null>(null);
  const [passwordDirty, setPasswordDirty] = useState<Record<DbTarget, boolean>>({ redis: false, mysql: false, tdengine: false });
  const [tests, setTests] = useState(INITIAL_TESTS);
  const [retentionDays, setRetentionDays] = useState("7");
  const [maintenanceSeconds, setMaintenanceSeconds] = useState("60");
  const [loading, setLoading] = useState(true);
  const [savingLocal, setSavingLocal] = useState(false);
  const [savingDatabases, setSavingDatabases] = useState(false);
  const [localError, setLocalError] = useState<string | null>(null);
  const [databaseError, setDatabaseError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    const [localResult, databaseResult] = await Promise.allSettled([
      agentClient.getLocalSettings(),
      agentClient.getConfig(),
    ]);
    if (localResult.status === "fulfilled") {
      const current = localResult.value;
      setSettings(current);
      setRetentionDays(String(current.max_offline_retention_days));
      setMaintenanceSeconds(String(current.outbox_maintenance_seconds));
      setLocalError(null);
    } else {
      setLocalError("边缘本机配置加载失败");
    }
    if (databaseResult.status === "fulfilled") {
      const loaded = databaseForms(databaseResult.value);
      setDatabases(loaded);
      setLoadedDatabases(loaded);
      setPasswordDirty({ redis: false, mysql: false, tdengine: false });
      setDatabaseError(null);
    } else {
      // H6：加载失败时禁止保存，避免把默认值（127.0.0.1/root）覆盖到真实配置
      const reason = databaseResult.reason;
      const detail = reason && typeof reason === "object" && "message" in reason && reason.message
        ? `：${String(reason.message)}`
        : "";
      setDatabaseError(`中心数据库配置加载失败${detail}。为避免覆盖现有配置，已禁用测试与保存，请刷新页面重新加载`);
    }
    setLoading(false);
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const canSaveDatabases = loadedDatabases !== null && databaseError === null;

  /** 只提交相对已加载配置改动过的字段（Agent PUT /admin/config 为逐字段 partial update） */
  function databasePatch(baseline: Record<DbTarget, DbForm>, targets: DbTarget[]): Partial<ConnectionConfig> {
    return buildConfigPatch(Object.fromEntries(targets.map((target) => [target, {
      baseline: databasePayload(baseline[target], false),
      current: databasePayload(databases[target], passwordDirty[target]),
      includePassword: passwordDirty[target],
    }]))) as Partial<ConnectionConfig>;
  }

  function updateDatabase(target: DbTarget, field: keyof DbForm, value: string) {
    setDatabases((current) => ({
      ...current,
      [target]: { ...current[target], [field]: value },
    }));
    setTests((current) => ({ ...current, [target]: INITIAL_TESTS[target] }));
  }

  function updatePassword(target: DbTarget, value: string) {
    updateDatabase(target, "password", value);
    setPasswordDirty((current) => ({ ...current, [target]: value.length > 0 }));
  }

  async function testDatabase(target: DbTarget) {
    if (!canSaveDatabases || !loadedDatabases) {
      toast.error("中心数据库配置未成功加载，不能测试并应用");
      return;
    }
    const validation = validateDatabase(target, databases[target]);
    if (validation) {
      setTests((current) => ({ ...current, [target]: { status: "disconnected", error: validation } }));
      return;
    }
    setTests((current) => ({ ...current, [target]: { status: "testing", error: null } }));
    try {
      const payload = databasePayload(databases[target], passwordDirty[target]);
      const result = await agentClient.checkHealth(target, payload);
      if (!result.success) {
        setTests((current) => ({
          ...current,
          [target]: { status: "disconnected", error: result.message ?? "连接失败" },
        }));
        return;
      }

      await agentClient.putConfig(databasePatch(loadedDatabases, [target]));
      const applied = { ...databases[target], password: "" };
      setLoadedDatabases((current) => current && { ...current, [target]: applied });
      setDatabases((current) => ({
        ...current,
        [target]: { ...current[target], password: "" },
      }));
      setPasswordDirty((current) => ({ ...current, [target]: false }));
      setTests((current) => ({ ...current, [target]: { status: "connected", error: null } }));
      window.dispatchEvent(new Event(CONNECTION_HEALTH_REFRESH_EVENT));
      toast.success(`${DB_META[target].title} 连接成功，配置已应用`);
    } catch (reason) {
      const message = reason && typeof reason === "object" && "message" in reason
        ? String(reason.message)
        : "测试或应用失败，请重试";
      setTests((current) => ({ ...current, [target]: { status: "disconnected", error: message } }));
    }
  }

  async function saveDatabases() {
    if (!canSaveDatabases || !loadedDatabases) {
      toast.error("中心数据库配置未成功加载，不能保存");
      return;
    }
    for (const target of Object.keys(databases) as DbTarget[]) {
      const validation = validateDatabase(target, databases[target]);
      if (validation) {
        toast.error(validation);
        return;
      }
    }
    setSavingDatabases(true);
    try {
      await agentClient.putConfig(databasePatch(loadedDatabases, ["redis", "mysql", "tdengine"]));
      setLoadedDatabases({
        redis: { ...databases.redis, password: "" },
        mysql: { ...databases.mysql, password: "" },
        tdengine: { ...databases.tdengine, password: "" },
      });
      setDatabases((current) => ({
        redis: { ...current.redis, password: "" },
        mysql: { ...current.mysql, password: "" },
        tdengine: { ...current.tdengine, password: "" },
      }));
      setPasswordDirty({ redis: false, mysql: false, tdengine: false });
      window.dispatchEvent(new Event(CONNECTION_HEALTH_REFRESH_EVENT));
      toast.success("中心数据库连接已保存");
    } catch (reason) {
      const message = reason && typeof reason === "object" && "message" in reason
        ? String(reason.message)
        : "中心数据库连接保存失败";
      toast.error(message);
    } finally {
      setSavingDatabases(false);
    }
  }

  async function saveLocal() {
    const retention = Number(retentionDays);
    const maintenance = Number(maintenanceSeconds);
    if (!Number.isInteger(retention) || retention < 1 || retention > 365) {
      toast.error("离线保留天数必须是 1-365 的整数");
      return;
    }
    if (!Number.isInteger(maintenance) || maintenance < 10 || maintenance > 3600) {
      toast.error("维护周期必须是 10-3600 秒的整数");
      return;
    }

    setSavingLocal(true);
    try {
      const process = await agentClient.getProcessStatus("uploader");
      const updated = await agentClient.updateLocalSettings({
        max_offline_retention_days: retention,
        outbox_maintenance_seconds: maintenance,
      });
      setSettings(updated);
      if (process.status === "running") {
        try {
          await agentClient.restartProcess("uploader");
          toast.success("设置已保存，Uploader 已重启生效");
        } catch {
          toast.warning("设置已保存，但 Uploader 自动重启失败");
        }
      } else {
        toast.success("设置已保存");
      }
    } catch {
      toast.error("边缘本机设置保存失败");
    } finally {
      setSavingLocal(false);
    }
  }

  return (
    <div className="mx-auto flex w-full max-w-6xl flex-col gap-4">
      {localError && <p className="text-sm text-destructive" role="alert">{localError}</p>}

      <Card>
        <CardHeader>
          <div className="min-w-0">
            <CardTitle>节点与中心</CardTitle>
            <CardDescription>当前注册身份和中心服务目标</CardDescription>
          </div>
          {settings && <CardAction><Badge variant={settings.registered ? "outline" : "secondary"}>{registrationLabel(settings.registration_status)}</Badge></CardAction>}
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-2">
          {loading ? (
            <><Skeleton className="h-16 w-full" /><Skeleton className="h-16 w-full" /></>
          ) : (
            <>
              <div className="space-y-1.5">
                <Label htmlFor="edge-node-name">节点名称</Label>
                <Input id="edge-node-name" value={settings?.node_name ?? ""} readOnly />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="edge-node-uuid">节点 UUID</Label>
                <Input id="edge-node-uuid" className="font-mono text-xs" value={settings?.node_uuid ?? ""} readOnly />
              </div>
              <div className="space-y-1.5 md:col-span-2">
                <Label htmlFor="edge-center-url">中心服务地址</Label>
                <Input id="edge-center-url" className="font-mono" value={settings?.center_url ?? ""} readOnly />
              </div>
              <div className="text-xs text-muted-foreground">已应用配置修订 {settings?.applied_revision ?? 0}</div>
              {settings?.last_sync_error && <div className="text-xs text-destructive md:text-right">{settings.last_sync_error}</div>}
            </>
          )}
        </CardContent>
      </Card>

      <div className="flex items-center justify-between gap-3 px-1">
        <div>
          <h2 className="text-sm font-semibold text-foreground">中心数据库连接</h2>
          {databaseError && <p className="mt-1 text-xs text-destructive" role="alert">{databaseError}</p>}
        </div>
        <Button onClick={() => void saveDatabases()} disabled={loading || savingDatabases || !canSaveDatabases}>
          <Save className="size-4" />
          {savingDatabases ? "保存中" : "保存连接"}
        </Button>
      </div>

      {(Object.keys(DB_META) as DbTarget[]).map((target) => (
        <DatabaseCard
          key={target}
          target={target}
          form={databases[target]}
          test={tests[target]}
          disabled={loading || savingDatabases || !canSaveDatabases}
          onChange={(field, value) => updateDatabase(target, field, value)}
          onPasswordChange={(value) => updatePassword(target, value)}
          onTest={() => void testDatabase(target)}
        />
      ))}

      <Card>
        <CardHeader>
          <CardTitle>本地离线队列</CardTitle>
          <CardDescription>Outbox 数据保留和维护节拍</CardDescription>
        </CardHeader>
        <CardContent className="space-y-5">
          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-1.5">
              <Label htmlFor="retention-days">最大离线保留天数</Label>
              <Input id="retention-days" type="number" min={1} max={365} value={retentionDays} onChange={(event) => setRetentionDays(event.target.value)} disabled={loading || savingLocal} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="maintenance-seconds">Outbox 维护周期（秒）</Label>
              <Input id="maintenance-seconds" type="number" min={10} max={3600} value={maintenanceSeconds} onChange={(event) => setMaintenanceSeconds(event.target.value)} disabled={loading || savingLocal} />
            </div>
          </div>
          <div className="flex justify-end border-t border-border pt-4">
            <Button onClick={() => void saveLocal()} disabled={loading || savingLocal || !settings}>
              <Save className="size-4" />
              {savingLocal ? "保存中" : "保存设置"}
            </Button>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
