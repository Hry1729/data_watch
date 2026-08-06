import { useEffect, useMemo, useState } from "react";
import { CheckCircle2, LoaderCircle, PlugZap, ServerCog } from "lucide-react";
import { useNavigate } from "react-router-dom";
import { toast } from "sonner";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { agentClient, type BootstrapTarget } from "@/lib/agent-client";
import { setBackendBaseUrl } from "@/lib/backend-client";
import type { AppError } from "@/types";

type DbTarget = "mysql" | "redis" | "tdengine";
type TestStatus = "idle" | "testing" | "success" | "failed";
interface FormState { centerUrl: string; nodeName: string; registrationCode: string; }
interface DbForm { host: string; port: string; user: string; password: string; database: string; }
interface TestState { status: TestStatus; error: string | null; }

const DB_TARGETS: DbTarget[] = ["mysql", "redis", "tdengine"];
const DB_META: Record<DbTarget, { title: string; description: string }> = {
  mysql: { title: "MySQL", description: "中心配置与统一用户认证" },
  redis: { title: "Redis", description: "中心实时数据与运行状态" },
  tdengine: { title: "TDengine", description: "中心历史时序数据" },
};
const DEFAULT_DATABASES: Record<DbTarget, DbForm> = {
  mysql: { host: "127.0.0.1", port: "3306", user: "root", password: "", database: "device_watch" },
  redis: { host: "127.0.0.1", port: "6379", user: "", password: "", database: "0" },
  tdengine: { host: "127.0.0.1", port: "6041", user: "root", password: "", database: "device_data" },
};
const EMPTY_TESTS: Record<DbTarget, TestState> = {
  mysql: { status: "idle", error: null },
  redis: { status: "idle", error: null },
  tdengine: { status: "idle", error: null },
};
const EMPTY_FORM: FormState = { centerUrl: "", nodeName: "", registrationCode: "" };

function centerHost(value: string): string | null {
  try { return new URL(value.trim()).hostname || null; } catch { return null; }
}
function errorMessage(error: unknown): string {
  const appError = error as AppError;
  if (appError?.type === "api_error" && appError.message) return appError.message;
  if (appError?.type === "agent_unreachable") return "无法连接本机 Agent";
  return "初始化失败，请检查中心地址、数据库配置和注册码";
}
function testBadge(test: TestState) {
  if (test.status === "testing") return <Badge variant="secondary"><LoaderCircle className="size-3 animate-spin" />检测中</Badge>;
  if (test.status === "success") return <Badge variant="outline"><CheckCircle2 className="size-3 text-success" />连接成功</Badge>;
  if (test.status === "failed") return <Badge variant="destructive">连接失败</Badge>;
  return <Badge variant="secondary">待测试</Badge>;
}
function Field({ label, className = "", ...props }: { label: string } & React.ComponentProps<typeof Input>) {
  return <div className={["space-y-1.5", className].join(" ")}><Label className="text-xs text-muted-foreground">{label}</Label><Input {...props} /></div>;
}
function DatabaseCard({
  target, form, test, disabled, onChange, onTest,
}: {
  target: DbTarget;
  form: DbForm;
  test: TestState;
  disabled: boolean;
  onChange: (field: keyof DbForm, value: string) => void;
  onTest: () => void;
}) {
  const meta = DB_META[target];
  return (
    <Card className="border border-border">
      <CardHeader>
        <div><CardTitle className="flex items-center gap-2">{meta.title}{testBadge(test)}</CardTitle><CardDescription>{meta.description}</CardDescription></div>
        <CardAction><Button type="button" variant="outline" size="sm" onClick={onTest} disabled={disabled || test.status === "testing"}><PlugZap className="size-4" />测试连接</Button></CardAction>
      </CardHeader>
      <CardContent className="space-y-3">
        <div className={target === "redis" ? "grid gap-3 sm:grid-cols-3" : "grid gap-3 sm:grid-cols-2 lg:grid-cols-4"}>
          <Field label="主机" value={form.host} onChange={(event) => onChange("host", event.target.value)} />
          <Field label="端口" type="number" min={1} max={65535} value={form.port} onChange={(event) => onChange("port", event.target.value)} />
          {target !== "redis" && <Field label="用户" value={form.user} onChange={(event) => onChange("user", event.target.value)} />}
          <Field label="密码" type="password" autoComplete="new-password" value={form.password} onChange={(event) => onChange("password", event.target.value)} placeholder="无密码可留空" />
        </div>
        {target !== "redis" && <Field className="max-w-xs" label="数据库" value={form.database} onChange={(event) => onChange("database", event.target.value)} />}
        {test.error && <p className="text-xs text-destructive">{test.error}</p>}
      </CardContent>
    </Card>
  );
}

export default function EdgeBootstrapPage() {
  const navigate = useNavigate();
  const [form, setForm] = useState(EMPTY_FORM);
  const [databases, setDatabases] = useState(DEFAULT_DATABASES);
  const [tests, setTests] = useState(EMPTY_TESTS);
  const [nodeUuid, setNodeUuid] = useState("");
  const [registrationStatus, setRegistrationStatus] = useState("not_registered");
  const [registered, setRegistered] = useState(false);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const allPassed = useMemo(() => DB_TARGETS.every((target) => tests[target].status === "success"), [tests]);

  useEffect(() => {
    agentClient.getEdgeBootstrapStatus()
      .then((status) => {
        setNodeUuid(status.nodeUuid);
        setRegistrationStatus(status.registrationStatus);
        setRegistered(status.registered);
        setForm((current) => ({ ...current, centerUrl: status.centerUrl, nodeName: status.nodeName }));
        const host = centerHost(status.centerUrl);
        if (host) setDatabases((current) => Object.fromEntries(DB_TARGETS.map((target) => [target, { ...current[target], host }])) as Record<DbTarget, DbForm>);
      })
      .catch((error) => toast.error(errorMessage(error)))
      .finally(() => setLoading(false));
  }, []);

  function resetTest(target: DbTarget) {
    setTests((current) => ({ ...current, [target]: { status: "idle", error: null } }));
  }
  function updateCenterUrl(value: string) {
    setForm((current) => ({ ...current, centerUrl: value }));
    const host = centerHost(value);
    if (host) setDatabases((current) => Object.fromEntries(DB_TARGETS.map((target) => [target, { ...current[target], host }])) as Record<DbTarget, DbForm>);
    setTests(EMPTY_TESTS);
  }
  function updateDatabase(target: DbTarget, field: keyof DbForm, value: string) {
    setDatabases((current) => ({ ...current, [target]: { ...current[target], [field]: value } }));
    resetTest(target);
  }
  function databasePayload(target: DbTarget) {
    const value = databases[target];
    return {
      target: target as BootstrapTarget,
      host: value.host.trim(),
      port: Number(value.port),
      user: target === "redis" ? undefined : value.user.trim(),
      password: value.password,
      database: target === "redis" ? undefined : value.database.trim(),
    };
  }
  async function testDatabase(target: DbTarget) {
    const value = databases[target];
    const port = Number(value.port);
    if (!value.host.trim() || !Number.isInteger(port) || port < 1 || port > 65535) {
      setTests((current) => ({ ...current, [target]: { status: "failed", error: "主机或端口无效" } }));
      return;
    }
    if (target !== "redis" && (!value.user.trim() || !value.database.trim())) {
      setTests((current) => ({ ...current, [target]: { status: "failed", error: "用户和数据库不能为空" } }));
      return;
    }
    setTests((current) => ({ ...current, [target]: { status: "testing", error: null } }));
    try {
      const result = await agentClient.postBootstrapTestConnection(databasePayload(target));
      setTests((current) => ({
        ...current,
        [target]: result.success ? { status: "success", error: null } : { status: "failed", error: result.message ?? "连接失败，请检查配置" },
      }));
    } catch (error) {
      setTests((current) => ({ ...current, [target]: { status: "failed", error: errorMessage(error) } }));
    }
  }
  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (submitting) return;
    if (!centerHost(form.centerUrl)) return toast.error("请输入有效的中心 HTTP(S) 地址");
    if (!allPassed) return toast.error("请先完成 MySQL、Redis 和 TDengine 三项连接测试");
    setSubmitting(true);
    try {
      const centerUrl = form.centerUrl.trim().replace(/\/$/, "");
      await agentClient.putBootstrapConfig({
        backend: { base_url: centerUrl },
        mysql: { host: databases.mysql.host.trim(), port: Number(databases.mysql.port), user: databases.mysql.user.trim(), password: databases.mysql.password, database: databases.mysql.database.trim() },
        redis: { host: databases.redis.host.trim(), port: Number(databases.redis.port), password: databases.redis.password },
        tdengine: { host: databases.tdengine.host.trim(), port: Number(databases.tdengine.port), user: databases.tdengine.user.trim(), password: databases.tdengine.password, database: databases.tdengine.database.trim() },
      });
      if (!registered) {
        const result = await agentClient.registerEdge({ center_url: centerUrl, node_name: form.nodeName.trim(), registration_code: form.registrationCode.trim() });
        setNodeUuid(result.nodeUuid);
        setRegistrationStatus(result.status);
        setRegistered(true);
      }
      await agentClient.postBootstrapFinish();
      setBackendBaseUrl(centerUrl);
      navigate("/login", { replace: true });
    } catch (error) {
      toast.error(errorMessage(error));
    } finally {
      setSubmitting(false);
    }
  }

  if (loading) return <div className="flex h-full items-center justify-center bg-background"><LoaderCircle className="size-5 animate-spin text-muted-foreground" /></div>;

  return (
    <div className="h-full overflow-auto bg-background">
      <main className="mx-auto w-full max-w-4xl px-5 py-8 sm:px-8">
        <header className="mb-6 flex items-center gap-3">
          <div className="flex size-10 items-center justify-center rounded-md border bg-card"><ServerCog className="size-5 text-brand" /></div>
          <div className="min-w-0"><h1 className="text-xl font-semibold text-foreground">边缘节点初始化</h1><p className="text-sm text-muted-foreground">连接中心、验证数据库并注册本机节点</p></div>
        </header>
        <form className="space-y-5" onSubmit={submit}>
          <Card className="border border-border">
            <CardHeader><CardTitle>节点与中心</CardTitle><CardDescription>中心地址会自动同步为三个数据库的主机地址，端口和凭据仍可单独调整。</CardDescription></CardHeader>
            <CardContent className="space-y-4">
              {registered && <Alert><CheckCircle2 /><AlertTitle>节点已注册</AlertTitle><AlertDescription className="flex flex-wrap items-center gap-2"><span className="break-all font-mono">{nodeUuid}</span><Badge variant="secondary">{registrationStatus}</Badge></AlertDescription></Alert>}
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-1.5 sm:col-span-2"><Label htmlFor="center-url">分布式中心地址</Label><Input id="center-url" type="url" required value={form.centerUrl} onChange={(event) => updateCenterUrl(event.target.value)} placeholder="http://192.168.1.10:8080" /></div>
                <div className="space-y-1.5"><Label htmlFor="node-name">节点名称</Label><Input id="node-name" required maxLength={128} value={form.nodeName} onChange={(event) => setForm((current) => ({ ...current, nodeName: event.target.value }))} /></div>
                <div className="space-y-1.5"><Label htmlFor="registration-code">注册码</Label><Input id="registration-code" required={!registered} disabled={registered} autoComplete="off" value={form.registrationCode} onChange={(event) => setForm((current) => ({ ...current, registrationCode: event.target.value }))} placeholder={registered ? "已完成注册" : "输入中心生成的一次性注册码"} /></div>
              </div>
            </CardContent>
          </Card>
          {DB_TARGETS.map((target) => <DatabaseCard key={target} target={target} form={databases[target]} test={tests[target]} disabled={submitting} onChange={(field, value) => updateDatabase(target, field, value)} onTest={() => void testDatabase(target)} />)}
          <div className="flex items-center justify-between gap-4 border-t border-border pt-5">
            <p className="text-sm text-muted-foreground">{allPassed ? "三项数据库连接已通过，可完成初始化" : "完成前需通过三项数据库连接测试"}</p>
            <Button type="submit" disabled={!allPassed || submitting}>{submitting && <LoaderCircle className="animate-spin" />}{registered ? "保存并完成" : "注册并完成"}</Button>
          </div>
        </form>
      </main>
    </div>
  );
}