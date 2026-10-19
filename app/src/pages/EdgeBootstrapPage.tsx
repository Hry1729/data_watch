import { useEffect, useMemo, useRef, useState } from "react";
import {
  CheckCircle2,
  CircleDashed,
  Database,
  LoaderCircle,
  RefreshCw,
  ServerCog,
  TriangleAlert,
} from "lucide-react";
import { useNavigate } from "react-router-dom";
import { toast } from "sonner";
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
import { agentClient, type BootstrapTarget } from "@/lib/agent-client";
import { setBackendBaseUrl } from "@/lib/backend-client";
import { validateBackendOrigin } from "@/lib/backend-proxy-policy";
import type { AppError } from "@/types";

// B12：边缘角色不使用中心 Redis/TDengine（边缘本地 SQLite → Uploader → Ingest HTTP），
// 因此边缘引导只要求 MySQL（中心配置库）。中心式/分布式中心的引导页仍各自要求所需的库。
type DbTarget = "mysql";
type StepKey = "node" | DbTarget;
type TestStatus = "idle" | "testing" | "success" | "failed";

interface FormState {
  centerUrl: string;
  nodeName: string;
  registrationCode: string;
}

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

const DB_TARGETS: DbTarget[] = ["mysql"];
const STEPS: Array<{
  key: StepKey;
  title: string;
  description: string;
  icon: typeof Database;
}> = [
  { key: "node", title: "节点与中心", description: "中心地址与节点注册", icon: ServerCog },
  { key: "mysql", title: "MySQL", description: "中心配置与用户认证", icon: Database },
];

const DEFAULT_DATABASES: Record<DbTarget, DbForm> = {
  mysql: { host: "127.0.0.1", port: "3306", user: "root", password: "", database: "device_watch" },
};

const EMPTY_TESTS: Record<DbTarget, TestState> = {
  mysql: { status: "idle", error: null },
};

const EMPTY_FORM: FormState = {
  centerUrl: "",
  nodeName: "",
  registrationCode: "",
};

function centerHost(value: string): string | null {
  try {
    return new URL(value.trim()).hostname || null;
  } catch {
    return null;
  }
}

function errorMessage(error: unknown): string {
  const appError = error as AppError;
  const fallback = "初始化失败，请检查中心地址、数据库配置和注册码";
  // M6：展示完整失败原因（Agent detail / HTTP 状态 / 网络错误），不吞掉
  if (appError?.type === "api_error") {
    return appError.message ? appError.message : `${fallback}（HTTP ${appError.status}）`;
  }
  if (appError?.type === "network_error" && appError.message) return appError.message;
  if (appError?.type === "agent_unreachable") return "无法连接本机 Agent";
  if (error instanceof Error && error.message) return error.message;
  return fallback;
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

function StepStatusBadge({
  step,
  test,
  nodeReady,
  registered,
}: {
  step: StepKey;
  test?: TestState;
  nodeReady: boolean;
  registered: boolean;
}) {
  if (step === "node") {
    if (registered) {
      return <Badge variant="outline" className="border-success/30 bg-success-soft text-success">已注册</Badge>;
    }
    return nodeReady ? <Badge variant="outline">已填写</Badge> : <Badge variant="secondary">待填写</Badge>;
  }
  if (test?.status === "testing") {
    return (
      <Badge variant="outline">
        <LoaderCircle className="size-3 animate-spin" />
        检测中
      </Badge>
    );
  }
  if (test?.status === "success") {
    return <Badge variant="outline" className="border-success/30 bg-success-soft text-success">已通过</Badge>;
  }
  if (test?.status === "failed") return <Badge variant="destructive">异常</Badge>;
  return <Badge variant="secondary">待检测</Badge>;
}

export default function EdgeBootstrapPage() {
  const navigate = useNavigate();
  const [selectedStep, setSelectedStep] = useState<StepKey>("node");
  const [form, setForm] = useState(EMPTY_FORM);
  const [databases, setDatabases] = useState(DEFAULT_DATABASES);
  const [tests, setTests] = useState(EMPTY_TESTS);
  const [nodeUuid, setNodeUuid] = useState("");
  const [registrationStatus, setRegistrationStatus] = useState("not_registered");
  const [registered, setRegistered] = useState(false);
  // M1：已注册节点的中心地址（Uploader / 心跳使用的 edge.center_url），注册后锁定
  const [registeredCenterUrl, setRegisteredCenterUrl] = useState("");
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [retesting, setRetesting] = useState(false);
  // M6：检测结果与表单版本绑定，检测期间表单被修改则丢弃旧结果
  const formVersionRef = useRef<Record<DbTarget, number>>({ mysql: 0 });

  const allPassed = useMemo(
    () => DB_TARGETS.every((target) => tests[target].status === "success"),
    [tests],
  );
  const centerCheck = validateBackendOrigin(form.centerUrl);
  const nodeReady = Boolean(
    centerCheck.origin
      && form.nodeName.trim()
      && (registered || form.registrationCode.trim()),
  );
  const passedCount = DB_TARGETS.filter((target) => tests[target].status === "success").length
    + (nodeReady ? 1 : 0);
  const readyToFinish = nodeReady && allPassed;
  const activeStep = STEPS.find((step) => step.key === selectedStep) ?? STEPS[0];

  useEffect(() => {
    agentClient.getEdgeBootstrapStatus()
      .then((status) => {
        setNodeUuid(status.nodeUuid);
        setRegistrationStatus(status.registrationStatus);
        setRegistered(status.registered);
        setRegisteredCenterUrl(status.registered ? status.centerUrl : "");
        setForm((current) => ({
          ...current,
          centerUrl: status.centerUrl,
          nodeName: status.nodeName,
        }));
        const host = centerHost(status.centerUrl);
        if (host) {
          setDatabases((current) => Object.fromEntries(
            DB_TARGETS.map((target) => [target, { ...current[target], host }]),
          ) as Record<DbTarget, DbForm>);
        }
      })
      .catch((error) => toast.error(errorMessage(error)))
      .finally(() => setLoading(false));
  }, []);

  function resetTest(target: DbTarget) {
    formVersionRef.current[target] += 1;
    setTests((current) => ({
      ...current,
      [target]: { status: "idle", error: null },
    }));
  }

  function updateCenterUrl(value: string) {
    // 已注册：中心地址锁定，更换中心必须重新注册
    if (registered) return;
    setForm((current) => ({ ...current, centerUrl: value }));
    const host = centerHost(value);
    if (host) {
      setDatabases((current) => Object.fromEntries(
        DB_TARGETS.map((target) => [target, { ...current[target], host }]),
      ) as Record<DbTarget, DbForm>);
    }
    for (const target of DB_TARGETS) formVersionRef.current[target] += 1;
    setTests(EMPTY_TESTS);
  }

  function updateDatabase(target: DbTarget, field: keyof DbForm, value: string) {
    setDatabases((current) => ({
      ...current,
      [target]: { ...current[target], [field]: value },
    }));
    resetTest(target);
  }

  function databasePayload(target: DbTarget) {
    const value = databases[target];
    return {
      target: target as BootstrapTarget,
      host: value.host.trim(),
      port: Number(value.port),
      user: value.user.trim(),
      password: value.password,
      database: value.database.trim(),
    };
  }

  async function testDatabase(target: DbTarget, formVersion = formVersionRef.current[target]) {
    const isCurrent = () => formVersionRef.current[target] === formVersion;
    if (!isCurrent()) return;
    const value = databases[target];
    const port = Number(value.port);
    if (!value.host.trim() || !Number.isInteger(port) || port < 1 || port > 65535) {
      setTests((current) => ({
        ...current,
        [target]: { status: "failed", error: "主机或端口无效" },
      }));
      return;
    }
    if (!value.user.trim() || !value.database.trim()) {
      setTests((current) => ({
        ...current,
        [target]: { status: "failed", error: "用户和数据库不能为空" },
      }));
      return;
    }

    setTests((current) => ({
      ...current,
      [target]: { status: "testing", error: null },
    }));
    try {
      const result = await agentClient.postBootstrapTestConnection(databasePayload(target));
      // 检测期间表单已被修改：结果属于旧配置，丢弃
      if (!isCurrent()) return;
      setTests((current) => ({
        ...current,
        [target]: result.success
          ? { status: "success", error: null }
          : { status: "failed", error: result.message ?? "连接失败，请检查配置" },
      }));
    } catch (error) {
      if (!isCurrent()) return;
      setTests((current) => ({
        ...current,
        [target]: { status: "failed", error: errorMessage(error) },
      }));
    }
  }

  async function retestAll() {
    if (retesting || submitting) return;
    setRetesting(true);
    const versions = { ...formVersionRef.current };
    for (const target of DB_TARGETS) {
      setSelectedStep(target);
      await testDatabase(target, versions[target]);
    }
    setRetesting(false);
  }

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (submitting) return;
    if (!centerCheck.origin) return toast.error(`中心地址无效：${centerCheck.error}`);
    if (registered && centerCheck.origin !== validateBackendOrigin(registeredCenterUrl).origin) {
      return toast.error("节点已注册到原中心，不能直接修改中心地址；更换中心需要重新注册");
    }
    if (!form.nodeName.trim()) return toast.error("请输入节点名称");
    if (!registered && !form.registrationCode.trim()) return toast.error("请输入中心生成的一次性注册码");
    if (!allPassed) return toast.error("请先完成 MySQL 连接测试");

    setSubmitting(true);
    try {
      const centerUrl = centerCheck.origin;
      await agentClient.putBootstrapConfig({
        backend: { base_url: centerUrl },
        mysql: {
          host: databases.mysql.host.trim(),
          port: Number(databases.mysql.port),
          user: databases.mysql.user.trim(),
          password: databases.mysql.password,
          database: databases.mysql.database.trim(),
        },
      });
      if (!registered) {
        const result = await agentClient.registerEdge({
          center_url: centerUrl,
          node_name: form.nodeName.trim(),
          registration_code: form.registrationCode.trim(),
        });
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

  if (loading) {
    return (
      <div className="flex h-full items-center justify-center bg-background">
        <LoaderCircle className="size-5 animate-spin text-muted-foreground" />
      </div>
    );
  }

  const selectedTest = selectedStep === "node" ? null : tests[selectedStep];

  return (
    <form className="flex h-full min-h-0 flex-col bg-muted/20" onSubmit={submit}>
      <div className="min-h-0 flex-1 overflow-auto">
        <main className="mx-auto w-full max-w-6xl px-6 py-7">
          <div className="mb-6 flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
            <div>
              <Badge variant="outline" className="mb-3">边缘节点首次设置</Badge>
              <h1 className="text-2xl font-semibold tracking-tight">初始化 Data Watch Edge</h1>
              <p className="mt-1 text-sm text-muted-foreground">
                连接分布式中心、验证数据库并注册本机节点。
              </p>
            </div>
            <div className="w-full max-w-xs space-y-2">
              <div className="flex items-center justify-between text-xs">
                <span className="text-muted-foreground">配置进度</span>
                <span className="font-medium tabular-nums">{passedCount} / {STEPS.length}</span>
              </div>
              <Progress value={(passedCount / STEPS.length) * 100} />
            </div>
          </div>

          <div className="grid gap-5 lg:grid-cols-[280px_minmax(0,1fr)]">
            <Card className="h-fit">
              <CardHeader>
                <CardTitle>配置步骤</CardTitle>
                <CardDescription>选择步骤后在右侧完成设置</CardDescription>
              </CardHeader>
              <CardContent className="space-y-2">
                {STEPS.map((step, index) => {
                  const Icon = step.icon;
                  const active = selectedStep === step.key;
                  const test = step.key === "node" ? undefined : tests[step.key];
                  return (
                    <Button
                      key={step.key}
                      type="button"
                      variant="ghost"
                      className={"h-auto w-full justify-start gap-3 px-3 py-3 text-left " + (active ? "bg-accent" : "")}
                      onClick={() => setSelectedStep(step.key)}
                    >
                      <span className={"flex size-9 shrink-0 items-center justify-center rounded-md " + (active ? "bg-primary text-primary-foreground" : "bg-muted text-muted-foreground")}>
                        <Icon className="size-4" />
                      </span>
                      <span className="min-w-0 flex-1">
                        <span className="block truncate text-sm font-medium">{index + 1}. {step.title}</span>
                        <span className="block truncate text-xs font-normal text-muted-foreground">{step.description}</span>
                      </span>
                      <StepStatusBadge step={step.key} test={test} nodeReady={nodeReady} registered={registered} />
                    </Button>
                  );
                })}
              </CardContent>
            </Card>

            <Card className="min-h-[430px]">
              <CardHeader className="border-b">
                <div className="flex items-start justify-between gap-4">
                  <div>
                    <CardTitle>{activeStep.title}</CardTitle>
                    <CardDescription className="mt-1">{activeStep.description}</CardDescription>
                  </div>
                  <StepStatusBadge
                    step={selectedStep}
                    test={selectedTest ?? undefined}
                    nodeReady={nodeReady}
                    registered={registered}
                  />
                </div>
              </CardHeader>
              <CardContent className="pt-6">
                {selectedStep === "node" ? (
                  <FieldGroup>
                    {registered ? (
                      <Alert className="border-success/30 bg-success-soft text-success">
                        <CheckCircle2 className="size-4" />
                        <AlertTitle>节点已注册</AlertTitle>
                        <AlertDescription className="flex flex-wrap items-center gap-2 text-success/80">
                          <span className="break-all font-mono">{nodeUuid}</span>
                          <Badge variant="secondary">{registrationStatus}</Badge>
                        </AlertDescription>
                      </Alert>
                    ) : null}
                    <ConfigField
                      id="edge-center-url"
                      label="分布式中心地址"
                      description={registered
                        ? "节点已注册到该中心，地址已锁定（只读）。更换中心需要重新注册：当前版本需重置边缘节点后重新执行注册"
                        : "该地址会同步为三个数据库的默认主机地址"}
                      error={form.centerUrl.trim() && centerCheck.error ? centerCheck.error : null}
                      type="url"
                      readOnly={registered}
                      value={form.centerUrl}
                      onChange={(event) => updateCenterUrl(event.target.value)}
                      placeholder="http://192.168.1.10:8080"
                    />
                    <div className="grid gap-4 sm:grid-cols-2">
                      <ConfigField
                        id="edge-node-name"
                        label="节点名称"
                        maxLength={128}
                        value={form.nodeName}
                        onChange={(event) => setForm((current) => ({ ...current, nodeName: event.target.value }))}
                        placeholder="例如：一号产线边缘节点"
                      />
                      <ConfigField
                        id="edge-registration-code"
                        label="注册码"
                        description={registered ? "当前节点已完成注册" : "由分布式中心生成的一次性注册码"}
                        disabled={registered}
                        autoComplete="off"
                        value={form.registrationCode}
                        onChange={(event) => setForm((current) => ({ ...current, registrationCode: event.target.value }))}
                        placeholder={registered ? "已完成注册" : "请输入注册码"}
                      />
                    </div>
                  </FieldGroup>
                ) : null}

                {selectedStep !== "node" ? (
                  <FieldGroup>
                    {selectedTest?.status === "failed" ? (
                      <Alert variant="destructive">
                        <TriangleAlert className="size-4" />
                        <AlertTitle>连接检测未通过</AlertTitle>
                        <AlertDescription>{selectedTest.error}</AlertDescription>
                      </Alert>
                    ) : selectedTest?.status === "success" ? (
                      <Alert className="border-success/30 bg-success-soft text-success">
                        <CheckCircle2 className="size-4" />
                        <AlertTitle>连接检测已通过</AlertTitle>
                        <AlertDescription className="text-success/80">此数据库配置已就绪。</AlertDescription>
                      </Alert>
                    ) : null}
                    <div className="grid gap-4 sm:grid-cols-2">
                      <ConfigField
                        id={"edge-" + selectedStep + "-host"}
                        label="主机"
                        value={databases[selectedStep].host}
                        onChange={(event) => updateDatabase(selectedStep, "host", event.target.value)}
                      />
                      <ConfigField
                        id={"edge-" + selectedStep + "-port"}
                        label="端口"
                        type="number"
                        min={1}
                        max={65535}
                        value={databases[selectedStep].port}
                        onChange={(event) => updateDatabase(selectedStep, "port", event.target.value)}
                      />
                      <ConfigField
                        id={"edge-" + selectedStep + "-user"}
                        label="用户"
                        value={databases[selectedStep].user}
                        onChange={(event) => updateDatabase(selectedStep, "user", event.target.value)}
                      />
                      <ConfigField
                        id={"edge-" + selectedStep + "-password"}
                        label="密码"
                        description="未启用密码时可留空"
                        type="password"
                        autoComplete="new-password"
                        value={databases[selectedStep].password}
                        onChange={(event) => updateDatabase(selectedStep, "password", event.target.value)}
                        placeholder="••••••"
                      />
                    </div>
                    <ConfigField
                      id={"edge-" + selectedStep + "-database"}
                      label="数据库"
                      value={databases[selectedStep].database}
                      onChange={(event) => updateDatabase(selectedStep, "database", event.target.value)}
                    />
                  </FieldGroup>
                ) : null}
              </CardContent>
            </Card>
          </div>
        </main>
      </div>

      <div className="shrink-0 border-t bg-background/95 px-6 py-4 backdrop-blur">
        <div className="mx-auto flex w-full max-w-6xl flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            {readyToFinish ? <CheckCircle2 className="size-4 text-success" /> : <CircleDashed className="size-4" />}
            <span>
              {readyToFinish
                ? "节点信息与数据库连接均已就绪"
                : "已完成 " + passedCount + " 项，还需完成 " + (STEPS.length - passedCount) + " 项"}
            </span>
          </div>
          <div className="flex flex-wrap justify-end gap-2">
            <Button type="button" variant="outline" onClick={retestAll} disabled={retesting || submitting}>
              <RefreshCw className={"size-4 " + (retesting ? "animate-spin" : "")} />
              全部重新检测
            </Button>
            {selectedStep === "node" ? (
              <Button type="button" variant="outline" onClick={() => setSelectedStep("mysql")}>
                下一步
              </Button>
            ) : (
              <Button
                type="button"
                variant="outline"
                onClick={() => testDatabase(selectedStep)}
                disabled={selectedTest?.status === "testing" || retesting || submitting}
              >
                {selectedTest?.status === "testing" ? <LoaderCircle className="size-4 animate-spin" /> : null}
                测试当前服务
              </Button>
            )}
            <Button type="submit" disabled={!readyToFinish || submitting || retesting}>
              {submitting ? <LoaderCircle className="size-4 animate-spin" /> : null}
              {submitting ? "完成中…" : registered ? "保存并完成" : "注册并完成"}
            </Button>
          </div>
        </div>
      </div>
    </form>
  );
}
