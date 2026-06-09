import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { agentClient } from "@/lib/agent-client";
import type { AppError } from "@/types";

// ─── 表单默认值 ───────────────────────────────────────────────────────────────

const DEFAULT_FORM = {
  host:     "127.0.0.1",
  port:     "3306",
  user:     "root",
  password: "",
  database: "device_watch",
};

// ─── 测试连接状态 ─────────────────────────────────────────────────────────────

type TestStatus = "idle" | "testing" | "success" | "failed";

const STATUS_VARIANT: Record<
  TestStatus,
  "secondary" | "default" | "destructive" | "outline"
> = {
  idle:    "secondary",
  testing: "outline",
  success: "default",
  failed:  "destructive",
};

// ─── 页面 ─────────────────────────────────────────────────────────────────────

export function BootstrapPage() {
  const navigate   = useNavigate();
  const [form, setForm] = useState(DEFAULT_FORM);

  const [testStatus, setTestStatus] = useState<TestStatus>("idle");
  const [testError,  setTestError]  = useState<string | null>(null);
  const [finishing,  setFinishing]  = useState(false);

  const testPassed = testStatus === "success";

  // 状态文案（失败时优先显示 API 返回的错误信息）
  const statusLabel =
    testStatus === "idle"    ? "等待测试连接" :
    testStatus === "testing" ? "测试中…" :
    testStatus === "success" ? "MySQL 连接成功" :
    (testError ?? "连接失败，请检查配置");

  function handleChange(field: keyof typeof DEFAULT_FORM) {
    return (e: React.ChangeEvent<HTMLInputElement>) => {
      setForm((prev) => ({ ...prev, [field]: e.target.value }));
      // 修改任意字段后重置测试状态，要求重新测试
      if (testStatus === "success") {
        setTestStatus("idle");
        setTestError(null);
      }
    };
  }

  // ── 测试连接：PUT config → POST test-connection ─────────────────────────────
  async function handleTestConnection() {
    setTestStatus("testing");
    setTestError(null);
    try {
      // Step 1：提交配置到 Agent（密码不经 console，agent-client 内部已脱敏）
      await agentClient.putBootstrapConfig({
        host:     form.host,
        port:     parseInt(form.port, 10) || 3306,
        user:     form.user,
        password: form.password,
        database: form.database,
      });
      // Step 2：发起连接测试
      const result = await agentClient.postBootstrapTestConnection("mysql");
      if (result.success) {
        setTestStatus("success");
      } else {
        setTestStatus("failed");
        setTestError(result.message ?? "连接失败，请检查配置");
      }
    } catch (err) {
      const e = err as AppError;
      if (e?.type === "agent_unreachable") {
        navigate("/error", { replace: true });
        return;
      }
      setTestStatus("failed");
      setTestError("连接测试失败，请检查配置");
    }
  }

  // ── 完成初始化：POST finish → GET status 确认 → 跳 /login ──────────────────
  async function handleFinish() {
    if (!testPassed || finishing) return;
    setFinishing(true);
    try {
      await agentClient.postBootstrapFinish();
      const { in_bootstrap } = await agentClient.getBootstrapStatus();
      if (!in_bootstrap) {
        navigate("/login", { replace: true });
      }
    } catch (err) {
      const e = err as AppError;
      if (e?.type === "agent_unreachable") {
        navigate("/error", { replace: true });
      }
      // 其他错误：停留页面，用户可重试
    } finally {
      setFinishing(false);
    }
  }

  return (
    // 全屏居中，无任何主导航（FR-015）
    <div
      className="min-h-full flex flex-col items-center justify-center px-6 py-12 bg-background"
    >
      {/* 页面标题 */}
      <div className="w-full max-w-2xl mb-6">
        <h1 className="text-2xl font-bold text-foreground">初始化向导</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          首次使用前需要配置 MySQL，Agent 将使用该连接读取系统配置。
        </p>
      </div>

      {/* 配置卡片 */}
      <Card
        className="w-full max-w-2xl"
        style={{
          background: "var(--card)",
          border: "1px solid var(--border)",
          borderRadius: 12,
        }}
      >
        <CardHeader className="pb-4">
          <CardTitle className="text-base font-semibold text-foreground">
            MySQL 配置
          </CardTitle>
        </CardHeader>

        <CardContent className="space-y-5">
          {/* 第一行：主机 / 端口 / 用户 / 密码 */}
          <div className="grid grid-cols-4 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="bs-host" className="text-xs text-muted-foreground">主机</Label>
              <Input
                id="bs-host"
                value={form.host}
                onChange={handleChange("host")}
                placeholder="127.0.0.1"
              />
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="bs-port" className="text-xs text-muted-foreground">端口</Label>
              <Input
                id="bs-port"
                type="number"
                min={1}
                max={65535}
                value={form.port}
                onChange={handleChange("port")}
                placeholder="3306"
              />
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="bs-user" className="text-xs text-muted-foreground">用户</Label>
              <Input
                id="bs-user"
                value={form.user}
                onChange={handleChange("user")}
                placeholder="root"
              />
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="bs-password" className="text-xs text-muted-foreground">密码</Label>
              {/* type="password" 始终掩码，不提供明文切换 */}
              <Input
                id="bs-password"
                type="password"
                value={form.password}
                onChange={handleChange("password")}
                placeholder="••••••"
                autoComplete="new-password"
              />
            </div>
          </div>

          {/* 第二行：数据库 */}
          <div className="max-w-xs space-y-1.5">
            <Label htmlFor="bs-database" className="text-xs text-muted-foreground">数据库</Label>
            <Input
              id="bs-database"
              value={form.database}
              onChange={handleChange("database")}
              placeholder="device_watch"
            />
          </div>

          {/* 操作按钮行 */}
          <div className="flex items-center justify-between pt-2">
            <Button
              variant="outline"
              disabled={testStatus === "testing"}
              onClick={handleTestConnection}
            >
              {testStatus === "testing" ? "测试中…" : "测试连接"}
            </Button>

            {/* 完成初始化：仅测试通过后可点击（FR-002） */}
            <Button
              disabled={!testPassed || finishing}
              onClick={handleFinish}
              style={
                testPassed && !finishing
                  ? { background: "var(--brand)", color: "var(--brand-foreground)" }
                  : undefined
              }
            >
              {finishing ? "初始化中…" : "完成初始化"}
            </Button>
          </div>
        </CardContent>
      </Card>

      {/* 状态区域 */}
      <div className="w-full max-w-2xl mt-4 flex items-center gap-2">
        <span className="text-sm text-muted-foreground">状态：</span>
        <Badge variant={STATUS_VARIANT[testStatus]}>{statusLabel}</Badge>
      </div>
    </div>
  );
}
