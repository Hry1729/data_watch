import { useState } from "react";
import type { ReactNode } from "react";
import { useNavigate } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { agentClient } from "@/lib/agent-client";
import type { BootstrapTarget } from "@/lib/agent-client";
import { setBackendBaseUrl } from "@/lib/backend-client";
import type { AppError } from "@/types";

// ─── 默认值 ───────────────────────────────────────────────────────────────────

const DEFAULTS = {
  backend:  { base_url: "http://127.0.0.1:8000" },
  mysql:    { host: "127.0.0.1", port: "3306", user: "root", password: "", database: "device_watch" },
  redis:    { host: "127.0.0.1", port: "6379", password: "" },
  tdengine: { host: "127.0.0.1", port: "6041", user: "root", password: "", database: "device_data" },
};

type TestStatus = "idle" | "testing" | "success" | "failed";

const STATUS_VARIANT: Record<
  TestStatus,
  "secondary" | "default" | "destructive" | "outline"
> = { idle: "secondary", testing: "outline", success: "default", failed: "destructive" };

// ─── 小组件 ───────────────────────────────────────────────────────────────────

function Field({
  label,
  className = "",
  ...props
}: { label: string } & React.ComponentProps<typeof Input>) {
  return (
    <div className={`space-y-1.5 ${className}`}>
      <Label className="text-xs text-muted-foreground">{label}</Label>
      <Input {...props} />
    </div>
  );
}

function SectionCard({
  title,
  subtitle,
  status,
  error,
  onTest,
  children,
}: {
  title: string;
  subtitle: string;
  status: TestStatus;
  error: string | null;
  onTest: () => void;
  children: ReactNode;
}) {
  const statusLabel =
    status === "idle"    ? "待测试" :
    status === "testing" ? "测试中…" :
    status === "success" ? "连接成功" :
    (error ?? "连接失败");

  return (
    <Card
      style={{ background: "var(--card)", border: "1px solid var(--border)", borderRadius: 12 }}
    >
      <CardHeader className="flex flex-row items-center justify-between gap-3">
        <div>
          <CardTitle>{title}</CardTitle>
          <p className="mt-0.5 text-xs text-muted-foreground">{subtitle}</p>
        </div>
        <Badge variant={STATUS_VARIANT[status]} className="shrink-0">{statusLabel}</Badge>
      </CardHeader>

      <CardContent className="space-y-4">
        {children}
        <div className="pt-1">
          <Button variant="outline" size="sm" disabled={status === "testing"} onClick={onTest}>
            {status === "testing" ? "测试中…" : "测试连接"}
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

// ─── 页面 ─────────────────────────────────────────────────────────────────────

export function BootstrapPage() {
  const navigate = useNavigate();

  const [backend, setBackend]   = useState(DEFAULTS.backend);
  const [mysql, setMysql]       = useState(DEFAULTS.mysql);
  const [redis, setRedis]       = useState(DEFAULTS.redis);
  const [tdengine, setTdengine] = useState(DEFAULTS.tdengine);

  const [status, setStatus] = useState<Record<BootstrapTarget, TestStatus>>({
    backend: "idle", mysql: "idle", redis: "idle", tdengine: "idle",
  });
  const [errors, setErrors] = useState<Record<BootstrapTarget, string | null>>({
    backend: null, mysql: null, redis: null, tdengine: null,
  });
  const [finishing, setFinishing] = useState(false);

  const allPassed =
    status.backend === "success" && status.mysql === "success" &&
    status.redis === "success" && status.tdengine === "success";

  // 改任一字段即把该块测试状态重置，要求重新测试
  function resetStatus(target: BootstrapTarget) {
    setStatus((s) => (s[target] === "idle" ? s : { ...s, [target]: "idle" }));
    setErrors((e) => (e[target] === null ? e : { ...e, [target]: null }));
  }

  function mysqlPort()    { return parseInt(mysql.port, 10) || 3306; }
  function redisPort()    { return parseInt(redis.port, 10) || 6379; }
  function tdenginePort() { return parseInt(tdengine.port, 10) || 6041; }

  function testPayload(target: BootstrapTarget) {
    switch (target) {
      case "backend":  return { target, base_url: backend.base_url };
      case "mysql":    return { target, host: mysql.host, port: mysqlPort(), user: mysql.user, password: mysql.password, database: mysql.database };
      case "redis":    return { target, host: redis.host, port: redisPort(), password: redis.password };
      case "tdengine": return { target, host: tdengine.host, port: tdenginePort(), user: tdengine.user, password: tdengine.password, database: tdengine.database };
    }
  }

  async function handleTest(target: BootstrapTarget) {
    setStatus((s) => ({ ...s, [target]: "testing" }));
    setErrors((e) => ({ ...e, [target]: null }));
    try {
      const result = await agentClient.postBootstrapTestConnection(testPayload(target));
      if (result.success) {
        setStatus((s) => ({ ...s, [target]: "success" }));
      } else {
        setStatus((s) => ({ ...s, [target]: "failed" }));
        setErrors((e) => ({ ...e, [target]: result.message ?? "连接失败，请检查配置" }));
      }
    } catch (err) {
      const e = err as AppError;
      if (e?.type === "agent_unreachable") { navigate("/error", { replace: true }); return; }
      setStatus((s) => ({ ...s, [target]: "failed" }));
      setErrors((er) => ({ ...er, [target]: "连接测试失败，请检查配置" }));
    }
  }

  async function handleFinish() {
    if (!allPassed || finishing) return;
    setFinishing(true);
    try {
      await agentClient.putBootstrapConfig({
        backend:  { base_url: backend.base_url },
        mysql:    { host: mysql.host, port: mysqlPort(), user: mysql.user, password: mysql.password, database: mysql.database },
        redis:    { host: redis.host, port: redisPort(), password: redis.password },
        tdengine: { host: tdengine.host, port: tdenginePort(), user: tdengine.user, password: tdengine.password, database: tdengine.database },
      });
      await agentClient.postBootstrapFinish();
      const { in_bootstrap } = await agentClient.getBootstrapStatus();
      if (!in_bootstrap) {
        setBackendBaseUrl(backend.base_url); // 登录前注入运行期后端地址
        navigate("/login", { replace: true });
      }
    } catch (err) {
      const e = err as AppError;
      if (e?.type === "agent_unreachable") navigate("/error", { replace: true });
      // 其他错误：停留页面，用户可重试
    } finally {
      setFinishing(false);
    }
  }

  return (
    <div className="h-full overflow-auto bg-background">
      <div className="mx-auto w-full max-w-3xl px-6 py-8">
        {/* 标题 */}
        <div className="mb-6">
          <h1 className="text-2xl font-bold text-foreground">初始化向导</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            首次使用前依次配置以下服务，四项全部测试通过后即可完成初始化。
          </p>
        </div>

        <div className="space-y-5">
          {/* 后端 */}
          <SectionCard
            title="后端服务 Backend"
            subtitle="Console 登录与数据查询所依赖的后端地址"
            status={status.backend}
            error={errors.backend}
            onTest={() => handleTest("backend")}
          >
            <Field
              label="后端地址"
              value={backend.base_url}
              onChange={(e) => { setBackend({ base_url: e.target.value }); resetStatus("backend"); }}
              placeholder="http://127.0.0.1:8000"
            />
          </SectionCard>

          {/* MySQL */}
          <SectionCard
            title="MySQL"
            subtitle="系统配置库（device_watch），登录账号来自其 sys_user 表"
            status={status.mysql}
            error={errors.mysql}
            onTest={() => handleTest("mysql")}
          >
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <Field label="主机" value={mysql.host} onChange={(e) => { setMysql({ ...mysql, host: e.target.value }); resetStatus("mysql"); }} placeholder="127.0.0.1" />
              <Field label="端口" type="number" min={1} max={65535} value={mysql.port} onChange={(e) => { setMysql({ ...mysql, port: e.target.value }); resetStatus("mysql"); }} placeholder="3306" />
              <Field label="用户" value={mysql.user} onChange={(e) => { setMysql({ ...mysql, user: e.target.value }); resetStatus("mysql"); }} placeholder="root" />
              <Field label="密码" type="password" autoComplete="new-password" value={mysql.password} onChange={(e) => { setMysql({ ...mysql, password: e.target.value }); resetStatus("mysql"); }} placeholder="••••••" />
            </div>
            <Field className="max-w-xs" label="数据库" value={mysql.database} onChange={(e) => { setMysql({ ...mysql, database: e.target.value }); resetStatus("mysql"); }} placeholder="device_watch" />
          </SectionCard>

          {/* Redis */}
          <SectionCard
            title="Redis"
            subtitle="实时缓冲层，collector / writer 运行所必需"
            status={status.redis}
            error={errors.redis}
            onTest={() => handleTest("redis")}
          >
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
              <Field label="主机" value={redis.host} onChange={(e) => { setRedis({ ...redis, host: e.target.value }); resetStatus("redis"); }} placeholder="127.0.0.1" />
              <Field label="端口" type="number" min={1} max={65535} value={redis.port} onChange={(e) => { setRedis({ ...redis, port: e.target.value }); resetStatus("redis"); }} placeholder="6379" />
              <Field label="密码（无则留空）" type="password" autoComplete="new-password" value={redis.password} onChange={(e) => { setRedis({ ...redis, password: e.target.value }); resetStatus("redis"); }} placeholder="••••••" />
            </div>
          </SectionCard>

          {/* TDengine */}
          <SectionCard
            title="TDengine"
            subtitle="时序宽表库（device_data），writer 落库与后端查询所依赖"
            status={status.tdengine}
            error={errors.tdengine}
            onTest={() => handleTest("tdengine")}
          >
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <Field label="主机" value={tdengine.host} onChange={(e) => { setTdengine({ ...tdengine, host: e.target.value }); resetStatus("tdengine"); }} placeholder="127.0.0.1" />
              <Field label="端口" type="number" min={1} max={65535} value={tdengine.port} onChange={(e) => { setTdengine({ ...tdengine, port: e.target.value }); resetStatus("tdengine"); }} placeholder="6041" />
              <Field label="用户" value={tdengine.user} onChange={(e) => { setTdengine({ ...tdengine, user: e.target.value }); resetStatus("tdengine"); }} placeholder="root" />
              <Field label="密码" type="password" autoComplete="new-password" value={tdengine.password} onChange={(e) => { setTdengine({ ...tdengine, password: e.target.value }); resetStatus("tdengine"); }} placeholder="••••••" />
            </div>
            <Field className="max-w-xs" label="数据库" value={tdengine.database} onChange={(e) => { setTdengine({ ...tdengine, database: e.target.value }); resetStatus("tdengine"); }} placeholder="device_data" />
          </SectionCard>
        </div>

        {/* 底部操作 */}
        <div className="mt-8 flex items-center justify-between gap-4 border-t border-border pt-5">
          <p className="text-sm text-muted-foreground">
            {allPassed ? "四项连接已全部通过，可完成初始化" : "需四项全部测试通过后才能完成初始化"}
          </p>
          <Button
            className="shrink-0"
            disabled={!allPassed || finishing}
            onClick={handleFinish}
            style={
              allPassed && !finishing
                ? { background: "var(--brand)", color: "var(--brand-foreground)" }
                : undefined
            }
          >
            {finishing ? "完成中…" : "完成初始化"}
          </Button>
        </div>
      </div>
    </div>
  );
}
