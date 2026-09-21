import { useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  CheckCircle2,
  CircleDashed,
  Database,
  LoaderCircle,
  RefreshCw,
  Server,
  TriangleAlert,
} from "lucide-react";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import {
  Field,
  FieldDescription,
  FieldError,
  FieldGroup,
  FieldLabel,
} from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Progress } from "@/components/ui/progress";
import { agentClient } from "@/lib/agent-client";
import type { BootstrapTarget } from "@/lib/agent-client";
import { setBackendBaseUrl } from "@/lib/backend-client";
import type { AppError } from "@/types";

const DEFAULTS = {
  backend: { base_url: "http://127.0.0.1:8000" },
  mysql: { host: "127.0.0.1", port: "3306", user: "root", password: "", database: "device_watch" },
  redis: { host: "127.0.0.1", port: "6379", password: "" },
  tdengine: { host: "127.0.0.1", port: "6041", user: "root", password: "", database: "device_data" },
};

type TestStatus = "idle" | "testing" | "success" | "failed";

const TARGETS: Array<{
  key: BootstrapTarget;
  title: string;
  description: string;
  icon: typeof Server;
}> = [
  { key: "backend", title: "后端服务", description: "登录与数据查询", icon: Server },
  { key: "mysql", title: "MySQL", description: "系统配置与账号", icon: Database },
  { key: "redis", title: "Redis", description: "实时缓存与队列", icon: Database },
  { key: "tdengine", title: "TDengine", description: "时序数据存储", icon: Database },
];

function StatusBadge({ status }: { status: TestStatus }) {
  if (status === "success") {
    return (
      <Badge variant="outline" className="border-success/30 bg-success-soft text-success">
        <CheckCircle2 className="size-3" />
        已通过
      </Badge>
    );
  }
  if (status === "failed") return <Badge variant="destructive">异常</Badge>;
  if (status === "testing") {
    return (
      <Badge variant="outline">
        <LoaderCircle className="size-3 animate-spin" />
        检测中
      </Badge>
    );
  }
  return <Badge variant="secondary">待检测</Badge>;
}

function ConfigField({
  id,
  label,
  description,
  error,
  ...props
}: {
  id: string;
  label: string;
  description?: string;
  error?: string | null;
} & React.ComponentProps<typeof Input>) {
  return (
    <Field data-invalid={Boolean(error)}>
      <FieldLabel htmlFor={id}>{label}</FieldLabel>
      <Input id={id} aria-invalid={Boolean(error)} {...props} />
      {description ? <FieldDescription>{description}</FieldDescription> : null}
      <FieldError>{error}</FieldError>
    </Field>
  );
}

export function BootstrapPage() {
  const navigate = useNavigate();
  const [selectedTarget, setSelectedTarget] = useState<BootstrapTarget>("backend");
  const [backend, setBackend] = useState(DEFAULTS.backend);
  const [mysql, setMysql] = useState(DEFAULTS.mysql);
  const [redis, setRedis] = useState(DEFAULTS.redis);
  const [tdengine, setTdengine] = useState(DEFAULTS.tdengine);
  const [status, setStatus] = useState<Record<BootstrapTarget, TestStatus>>({
    backend: "idle",
    mysql: "idle",
    redis: "idle",
    tdengine: "idle",
  });
  const [errors, setErrors] = useState<Record<BootstrapTarget, string | null>>({
    backend: null,
    mysql: null,
    redis: null,
    tdengine: null,
  });
  const [finishing, setFinishing] = useState(false);
  const [retesting, setRetesting] = useState(false);

  const passedCount = TARGETS.filter(({ key }) => status[key] === "success").length;
  const allPassed = passedCount === TARGETS.length;
  const activeTarget = TARGETS.find(({ key }) => key === selectedTarget) ?? TARGETS[0];

  function resetStatus(target: BootstrapTarget) {
    setStatus((current) => current[target] === "idle" ? current : { ...current, [target]: "idle" });
    setErrors((current) => current[target] === null ? current : { ...current, [target]: null });
  }

  function mysqlPort() { return parseInt(mysql.port, 10) || 3306; }
  function redisPort() { return parseInt(redis.port, 10) || 6379; }
  function tdenginePort() { return parseInt(tdengine.port, 10) || 6041; }

  function testPayload(target: BootstrapTarget) {
    switch (target) {
      case "backend": return { target, base_url: backend.base_url };
      case "mysql": return { target, host: mysql.host, port: mysqlPort(), user: mysql.user, password: mysql.password, database: mysql.database };
      case "redis": return { target, host: redis.host, port: redisPort(), password: redis.password };
      case "tdengine": return { target, host: tdengine.host, port: tdenginePort(), user: tdengine.user, password: tdengine.password, database: tdengine.database };
    }
  }

  async function handleTest(target: BootstrapTarget) {
    setStatus((current) => ({ ...current, [target]: "testing" }));
    setErrors((current) => ({ ...current, [target]: null }));
    try {
      const result = await agentClient.postBootstrapTestConnection(testPayload(target));
      if (result.success) {
        setStatus((current) => ({ ...current, [target]: "success" }));
        return true;
      }
      setStatus((current) => ({ ...current, [target]: "failed" }));
      setErrors((current) => ({ ...current, [target]: result.message ?? "连接失败，请检查配置" }));
    } catch (err) {
      const appError = err as AppError;
      if (appError?.type === "agent_unreachable") {
        navigate("/error", { replace: true });
        return false;
      }
      setStatus((current) => ({ ...current, [target]: "failed" }));
      setErrors((current) => ({ ...current, [target]: "连接测试失败，请检查配置" }));
    }
    return false;
  }

  async function handleRetestAll() {
    if (retesting) return;
    setRetesting(true);
    for (const target of TARGETS) {
      setSelectedTarget(target.key);
      await handleTest(target.key);
    }
    setRetesting(false);
  }

  async function handleFinish() {
    if (!allPassed || finishing) return;
    setFinishing(true);
    try {
      await agentClient.putBootstrapConfig({
        backend: { base_url: backend.base_url },
        mysql: { host: mysql.host, port: mysqlPort(), user: mysql.user, password: mysql.password, database: mysql.database },
        redis: { host: redis.host, port: redisPort(), password: redis.password },
        tdengine: { host: tdengine.host, port: tdenginePort(), user: tdengine.user, password: tdengine.password, database: tdengine.database },
      });
      await agentClient.postBootstrapFinish();
      const { in_bootstrap } = await agentClient.getBootstrapStatus();
      if (!in_bootstrap) {
        setBackendBaseUrl(backend.base_url);
        navigate("/login", { replace: true });
      }
    } catch (err) {
      const appError = err as AppError;
      if (appError?.type === "agent_unreachable") navigate("/error", { replace: true });
    } finally {
      setFinishing(false);
    }
  }

  const selectedError = errors[selectedTarget];
  const selectedStatus = status[selectedTarget];

  return (
    <div className="flex h-full min-h-0 flex-col bg-muted/20">
      <div className="min-h-0 flex-1 overflow-auto">
        <div className="mx-auto w-full max-w-6xl px-6 py-7">
          <div className="mb-6 flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
            <div>
              <Badge variant="outline" className="mb-3">首次运行设置</Badge>
              <h1 className="text-2xl font-semibold tracking-tight">初始化 Data Watch</h1>
              <p className="mt-1 text-sm text-muted-foreground">
                完成四项服务配置并通过连接检测，随后即可进入登录页。
              </p>
            </div>
            <div className="w-full max-w-xs space-y-2">
              <div className="flex items-center justify-between text-xs">
                <span className="text-muted-foreground">配置进度</span>
                <span className="font-medium tabular-nums">{passedCount} / {TARGETS.length}</span>
              </div>
              <Progress value={(passedCount / TARGETS.length) * 100} />
            </div>
          </div>

          <div className="grid gap-5 lg:grid-cols-[280px_minmax(0,1fr)]">
            <Card className="h-fit border-border shadow-xs">
              <CardHeader>
                <CardTitle>服务清单</CardTitle>
                <CardDescription>选择服务后在右侧完成配置</CardDescription>
              </CardHeader>
              <CardContent className="space-y-2">
                {TARGETS.map((target, index) => {
                  const Icon = target.icon;
                  const active = selectedTarget === target.key;
                  return (
                    <Button
                      key={target.key}
                      type="button"
                      variant="ghost"
                      className={"h-auto w-full justify-start gap-3 px-3 py-3 text-left " + (active ? "bg-accent" : "")}
                      onClick={() => setSelectedTarget(target.key)}
                    >
                      <span className={"flex size-9 shrink-0 items-center justify-center rounded-md " + (active ? "bg-primary text-primary-foreground" : "bg-muted text-muted-foreground")}>
                        <Icon className="size-4" />
                      </span>
                      <span className="min-w-0 flex-1">
                        <span className="block truncate text-sm font-medium">{index + 1}. {target.title}</span>
                        <span className="block truncate text-xs font-normal text-muted-foreground">{target.description}</span>
                      </span>
                      <StatusBadge status={status[target.key]} />
                    </Button>
                  );
                })}
              </CardContent>
            </Card>

            <Card className="min-h-[430px] border-border shadow-xs">
              <CardHeader className="border-b">
                <div className="flex items-start justify-between gap-4">
                  <div>
                    <CardTitle>{activeTarget.title}</CardTitle>
                    <CardDescription className="mt-1">{activeTarget.description}</CardDescription>
                  </div>
                  <StatusBadge status={selectedStatus} />
                </div>
              </CardHeader>
              <CardContent className="pt-6">
                {selectedStatus === "failed" ? (
                  <Alert variant="destructive" className="mb-5">
                    <TriangleAlert className="size-4" />
                    <AlertTitle>连接检测未通过</AlertTitle>
                    <AlertDescription>{selectedError}</AlertDescription>
                  </Alert>
                ) : selectedStatus === "success" ? (
                  <Alert className="mb-5 border-success/30 bg-success-soft text-success">
                    <CheckCircle2 className="size-4" />
                    <AlertTitle>连接检测已通过</AlertTitle>
                    <AlertDescription className="text-success/80">此服务配置已就绪。</AlertDescription>
                  </Alert>
                ) : null}

                {selectedTarget === "backend" ? (
                  <FieldGroup>
                    <ConfigField
                      id="bootstrap-backend-url"
                      label="后端地址"
                      description="Console 登录与数据查询使用的 HTTP 地址"
                      value={backend.base_url}
                      onChange={(event) => { setBackend({ base_url: event.target.value }); resetStatus("backend"); }}
                      placeholder="http://127.0.0.1:8000"
                    />
                  </FieldGroup>
                ) : null}

                {selectedTarget === "mysql" ? (
                  <FieldGroup>
                    <div className="grid gap-4 sm:grid-cols-2">
                      <ConfigField id="bootstrap-mysql-host" label="主机" value={mysql.host} onChange={(event) => { setMysql({ ...mysql, host: event.target.value }); resetStatus("mysql"); }} placeholder="127.0.0.1" />
                      <ConfigField id="bootstrap-mysql-port" label="端口" type="number" min={1} max={65535} value={mysql.port} onChange={(event) => { setMysql({ ...mysql, port: event.target.value }); resetStatus("mysql"); }} placeholder="3306" />
                      <ConfigField id="bootstrap-mysql-user" label="用户" value={mysql.user} onChange={(event) => { setMysql({ ...mysql, user: event.target.value }); resetStatus("mysql"); }} placeholder="root" />
                      <ConfigField id="bootstrap-mysql-password" label="密码" type="password" autoComplete="new-password" value={mysql.password} onChange={(event) => { setMysql({ ...mysql, password: event.target.value }); resetStatus("mysql"); }} placeholder="••••••" />
                    </div>
                    <ConfigField id="bootstrap-mysql-database" label="数据库" description="系统配置库，默认 device_watch" value={mysql.database} onChange={(event) => { setMysql({ ...mysql, database: event.target.value }); resetStatus("mysql"); }} placeholder="device_watch" />
                  </FieldGroup>
                ) : null}

                {selectedTarget === "redis" ? (
                  <FieldGroup>
                    <div className="grid gap-4 sm:grid-cols-2">
                      <ConfigField id="bootstrap-redis-host" label="主机" value={redis.host} onChange={(event) => { setRedis({ ...redis, host: event.target.value }); resetStatus("redis"); }} placeholder="127.0.0.1" />
                      <ConfigField id="bootstrap-redis-port" label="端口" type="number" min={1} max={65535} value={redis.port} onChange={(event) => { setRedis({ ...redis, port: event.target.value }); resetStatus("redis"); }} placeholder="6379" />
                    </div>
                    <ConfigField id="bootstrap-redis-password" label="密码" description="未启用密码时可留空" type="password" autoComplete="new-password" value={redis.password} onChange={(event) => { setRedis({ ...redis, password: event.target.value }); resetStatus("redis"); }} placeholder="••••••" />
                  </FieldGroup>
                ) : null}

                {selectedTarget === "tdengine" ? (
                  <FieldGroup>
                    <div className="grid gap-4 sm:grid-cols-2">
                      <ConfigField id="bootstrap-tdengine-host" label="主机" value={tdengine.host} onChange={(event) => { setTdengine({ ...tdengine, host: event.target.value }); resetStatus("tdengine"); }} placeholder="127.0.0.1" />
                      <ConfigField id="bootstrap-tdengine-port" label="端口" type="number" min={1} max={65535} value={tdengine.port} onChange={(event) => { setTdengine({ ...tdengine, port: event.target.value }); resetStatus("tdengine"); }} placeholder="6041" />
                      <ConfigField id="bootstrap-tdengine-user" label="用户" value={tdengine.user} onChange={(event) => { setTdengine({ ...tdengine, user: event.target.value }); resetStatus("tdengine"); }} placeholder="root" />
                      <ConfigField id="bootstrap-tdengine-password" label="密码" type="password" autoComplete="new-password" value={tdengine.password} onChange={(event) => { setTdengine({ ...tdengine, password: event.target.value }); resetStatus("tdengine"); }} placeholder="••••••" />
                    </div>
                    <ConfigField id="bootstrap-tdengine-database" label="数据库" description="时序宽表库，默认 device_data" value={tdengine.database} onChange={(event) => { setTdengine({ ...tdengine, database: event.target.value }); resetStatus("tdengine"); }} placeholder="device_data" />
                  </FieldGroup>
                ) : null}
              </CardContent>
            </Card>
          </div>
        </div>
      </div>

      <div className="shrink-0 border-t bg-background/95 px-6 py-4 backdrop-blur">
        <div className="mx-auto flex w-full max-w-6xl flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            {allPassed ? <CheckCircle2 className="size-4 text-success" /> : <CircleDashed className="size-4" />}
            <span>{allPassed ? "四项连接均已通过，可以完成初始化" : "已通过 " + passedCount + " 项，还需检测 " + (TARGETS.length - passedCount) + " 项"}</span>
          </div>
          <div className="flex flex-wrap justify-end gap-2">
            <Button type="button" variant="outline" onClick={handleRetestAll} disabled={retesting || finishing}>
              <RefreshCw className={"size-4 " + (retesting ? "animate-spin" : "")} />
              全部重新检测
            </Button>
            <Button type="button" variant="outline" onClick={() => handleTest(selectedTarget)} disabled={selectedStatus === "testing" || retesting || finishing}>
              {selectedStatus === "testing" ? <LoaderCircle className="size-4 animate-spin" /> : null}
              测试当前服务
            </Button>
            <Button type="button" onClick={handleFinish} disabled={!allPassed || finishing || retesting}>
              {finishing ? "完成中…" : "完成初始化"}
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
}
