import { useEffect, useRef, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { RotateCcw, Save } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Tabs, TabsContent } from "@/components/ui/tabs";
import { toast } from "sonner";
import { agentClient } from "@/lib/agent-client";
import { backendClient } from "@/lib/backend-client";
import { buildConfigPatch } from "@/lib/config-patch";
import { useAuth } from "@/contexts/AuthContext";
import { usePageRefresh } from "@/contexts/PageRefreshContext";
import { useTheme } from "@/contexts/ThemeContext";
import { normalizeSettingsTab } from "@/components/layout/settings-tabs";
import { BUILD_ROLE } from "@/config/build-role";
import type { AppError, ConnectionConfig, DbConfig, ModuleName } from "@/types";

const ZH_TO_THEME = { "浅色": "light", "深色": "dark", "跟随系统": "system" } as const;
const THEME_TO_ZH = { light: "浅色", dark: "深色", system: "跟随系统" } as const;

// ─── 内部表单状态类型 ─────────────────────────────────────────────────────────

interface DbForm {
  host: string;
  port: string;
  user: string;
  /** 仅用于表单状态；调试输出已脱敏；不在 console 中打印明文 */
  password: string;
  database: string;
}

interface ProcessForm {
  executable: string;
  working_dir: string;
}

interface FormState {
  redis:    DbForm;
  mysql:    DbForm;
  tdengine: DbForm;
  collector: ProcessForm;
  ingest:    ProcessForm;
  writer:    ProcessForm;
}

/**
 * 用户是否修改了对应数据库的密码。
 * 未修改时不发送 password 字段，由 Agent 保留原密码。
 */
interface PasswordDirty {
  redis:    boolean;
  mysql:    boolean;
  tdengine: boolean;
}

// ─── 默认表单值（仅作加载前占位；getConfig 失败时禁止保存，不会被提交） ──────────

const DEFAULT_FORM: FormState = {
  redis:    { host: "127.0.0.1", port: "6379", user: "",     password: "", database: "0"            },
  mysql:    { host: "127.0.0.1", port: "3306", user: "root", password: "", database: "device_watch"  },
  tdengine: { host: "127.0.0.1", port: "6041", user: "root", password: "", database: "device_data"   },
  collector: { executable: "collector/data-watch-collector.exe", working_dir: "D:/data_watch_deploy/data-watch-collector" },
  ingest:    { executable: "ingest/data-watch-ingest.exe",       working_dir: "D:/data_watch_deploy/data-watch-ingest"    },
  writer:    { executable: "writer/data-watch-writer.exe",       working_dir: "D:/data_watch_deploy/data-watch-writer"    },
};

function configToForm(cfg: ConnectionConfig): FormState {
  // password 字段在 GET /admin/config 中可能缺失（契约：不返回真实密码）；
  // 表单回退为空串，用户不输入则后续提交不带 password key（保留原密码）。
  const collector = cfg.collector ?? DEFAULT_FORM.collector;
  const ingest = cfg.ingest ?? DEFAULT_FORM.ingest;
  const writer = cfg.writer ?? DEFAULT_FORM.writer;
  return {
    redis: {
      host:     cfg.redis.host,
      port:     String(cfg.redis.port),
      user:     cfg.redis.user ?? "",
      password: cfg.redis.password ?? "",
      database: cfg.redis.database ?? "0",
    },
    mysql: {
      host:     cfg.mysql.host,
      port:     String(cfg.mysql.port),
      user:     cfg.mysql.user,
      password: cfg.mysql.password ?? "",
      database: cfg.mysql.database ?? "",
    },
    tdengine: {
      host:     cfg.tdengine.host,
      port:     String(cfg.tdengine.port),
      user:     cfg.tdengine.user,
      password: cfg.tdengine.password ?? "",
      database: cfg.tdengine.database ?? "",
    },
    collector: {
      executable:  collector.executable,
      working_dir: collector.working_dir,
    },
    ingest: {
      executable:  ingest.executable,
      working_dir: ingest.working_dir,
    },
    writer: {
      executable:  writer.executable,
      working_dir: writer.working_dir,
    },
  };
}

// ─── 测试状态类型 ─────────────────────────────────────────────────────────────

type TestStatus = "unknown" | "testing" | "connected" | "disconnected";

// ─── 状态标签 ─────────────────────────────────────────────────────────────────

function StatusBadge({ status }: { status: TestStatus }) {
  if (status === "connected") {
    return (
      <span
        className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium"
        style={{ background: "var(--success-soft)", color: "var(--success)" }}
      >
        <span className="w-1.5 h-1.5 rounded-full bg-success inline-block" />
        已连接
      </span>
    );
  }
  if (status === "testing") {
    return (
      <span
        className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium"
        style={{ background: "var(--warning-soft)", color: "var(--warning)" }}
      >
        <span className="w-1.5 h-1.5 rounded-full bg-warning inline-block animate-pulse" />
        测试中…
      </span>
    );
  }
  if (status === "disconnected") {
    return (
      <span
        className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium"
        style={{ background: "var(--destructive-soft)", color: "var(--destructive)" }}
      >
        <span className="w-1.5 h-1.5 rounded-full bg-destructive inline-block" />
        连接失败
      </span>
    );
  }
  return (
    <span
      className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium"
      style={{ background: "var(--muted)", color: "var(--muted-foreground)" }}
    >
      <span className="w-1.5 h-1.5 rounded-full bg-muted-foreground/70 inline-block" />
      未知
    </span>
  );
}

// ─── 受控字段行 ───────────────────────────────────────────────────────────────

function FieldRow({
  label,
  value,
  onChange,
  type = "text",
  width = "w-36",
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  type?: string;
  width?: string;
}) {
  return (
    <div className="flex flex-col gap-1">
      <Label className="text-xs text-muted-foreground">{label}</Label>
      <Input
        type={type}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        autoComplete={type === "password" ? "off" : undefined}
        className={`${width} h-8 text-sm`}
        style={{ borderColor: "var(--border)" }}
      />
    </div>
  );
}

// ─── Segmented 按钮组（外观/密度静态切换）────────────────────────────────────

function SegmentedGroup({
  options,
  value,
  onChange,
}: {
  options: string[];
  value: string;
  onChange: (v: string) => void;
}) {
  return (
    <div
      className="inline-flex rounded-lg overflow-hidden"
      style={{ border: "1px solid var(--border)" }}
    >
      {options.map((opt, i) => (
        <button
          key={opt}
          type="button"
          onClick={() => onChange(opt)}
          className="px-4 py-1.5 text-sm transition-colors"
          style={{
            background: value === opt ? "var(--brand)" : "var(--card)",
            color: value === opt ? "var(--brand-foreground)" : "var(--muted-foreground)",
            borderRight: i < options.length - 1 ? "1px solid var(--border)" : "none",
          }}
        >
          {opt}
        </button>
      ))}
    </div>
  );
}

// ─── 数据库行卡片 ─────────────────────────────────────────────────────────────

function DbCard({
  title,
  status,
  error,
  onTest,
  children,
}: {
  title: string;
  status: TestStatus;
  error?: string | null;
  onTest: () => void;
  children: React.ReactNode;
}) {
  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-3">
          <CardTitle>{title}</CardTitle>
          <StatusBadge status={status} />
          <div className="ml-auto">
            <Button
              variant="outline"
              size="sm"
              className="h-7 text-xs"
              disabled={status === "testing"}
              onClick={onTest}
            >
              测试连接
            </Button>
          </div>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {error && (
          <p className="text-xs text-destructive" role="alert">
            {error}
          </p>
        )}
        <div className="flex flex-wrap gap-x-6 gap-y-4">{children}</div>
      </CardContent>
    </Card>
  );
}

// ─── 标签内容：数据库 ──────────────────────────────────────────────────────────

interface RowState {
  status: TestStatus;
  error: string | null;
}

const INITIAL_ROW: RowState = { status: "unknown", error: null };

function DatabaseTab({
  redis,
  mysql,
  tdengine,
  onRedisChange,
  onMysqlChange,
  onTdengineChange,
  passwordDirty,
  onPasswordDirty,
}: {
  redis:    DbForm;
  mysql:    DbForm;
  tdengine: DbForm;
  onRedisChange:    (f: DbForm) => void;
  onMysqlChange:    (f: DbForm) => void;
  onTdengineChange: (f: DbForm) => void;
  passwordDirty: PasswordDirty;
  /** 通知父组件某库的密码字段已被用户修改 */
  onPasswordDirty: (db: keyof PasswordDirty) => void;
}) {
  const { logout } = useAuth();
  const navigate   = useNavigate();

  const [redisRow, setRedisRow] = useState<RowState>(INITIAL_ROW);
  const [mysqlRow, setMysqlRow] = useState<RowState>(INITIAL_ROW);
  const [tdRow,    setTdRow]    = useState<RowState>(INITIAL_ROW);

  function healthConfig(f: DbForm, dirty: boolean): Partial<DbConfig> {
    const cfg: Partial<DbConfig> = {
      host: f.host,
      port: parseInt(f.port) || 0,
      user: f.user,
      database: f.database || undefined,
    };
    return dirty ? { ...cfg, password: f.password } : cfg;
  }

  // 真实健康检查：传当前表单值；密码未修改时不传 password，Agent 使用已保存密码。
  async function handleTest(
    target: "redis" | "mysql" | "tdengine",
    setRow: (s: RowState) => void,
    config: Partial<DbConfig>
  ) {
    setRow({ status: "testing", error: null });
    try {
      const result = await agentClient.checkHealth(target, config);
      if (result.success) {
        setRow({ status: "connected", error: null });
      } else {
        setRow({ status: "disconnected", error: result.message ?? "连接失败" });
      }
    } catch (err) {
      const e = err as AppError;
      if (e?.type === "unauthorized")      { logout(); return; }
      if (e?.type === "agent_unreachable") { navigate("/error", { replace: true }); return; }
      setRow({ status: "disconnected", error: "测试失败，请重试" });
    }
  }

  return (
    <>
      {/* Redis */}
      <DbCard
        title="Redis 实时缓冲"
        status={redisRow.status}
        error={redisRow.error}
        onTest={() => handleTest("redis", setRedisRow, healthConfig(redis, passwordDirty.redis))}
      >
        <FieldRow label="主机"  value={redis.host}     onChange={(v) => onRedisChange({ ...redis, host: v })} />
        <FieldRow label="端口"  value={redis.port}     onChange={(v) => onRedisChange({ ...redis, port: v })} width="w-24" />
        <FieldRow label="DB"    value={redis.database} onChange={(v) => onRedisChange({ ...redis, database: v })} width="w-16" />
        <FieldRow
          label="密码" type="password" value={redis.password} width="w-36"
          onChange={(v) => { onRedisChange({ ...redis, password: v }); onPasswordDirty("redis"); }}
        />
        {/* Stream 分片数暂不在 ConnectionConfig 中；T017 保留静态默认 */}
        <div className="flex flex-col gap-1">
          <Label className="text-xs text-muted-foreground">Stream 分片数</Label>
          <Input defaultValue="4" className="w-20 h-8 text-sm" style={{ borderColor: "var(--border)" }} />
        </div>
      </DbCard>

      {/* MySQL */}
      <DbCard
        title="MySQL 配置库"
        status={mysqlRow.status}
        error={mysqlRow.error}
        onTest={() => handleTest("mysql", setMysqlRow, healthConfig(mysql, passwordDirty.mysql))}
      >
        <FieldRow label="主机"   value={mysql.host}     onChange={(v) => onMysqlChange({ ...mysql, host: v })} />
        <FieldRow label="端口"   value={mysql.port}     onChange={(v) => onMysqlChange({ ...mysql, port: v })} width="w-24" />
        <FieldRow label="用户"   value={mysql.user}     onChange={(v) => onMysqlChange({ ...mysql, user: v })} />
        <FieldRow
          label="密码" type="password" value={mysql.password} width="w-36"
          onChange={(v) => { onMysqlChange({ ...mysql, password: v }); onPasswordDirty("mysql"); }}
        />
        <FieldRow label="数据库" value={mysql.database} onChange={(v) => onMysqlChange({ ...mysql, database: v })} width="w-40" />
      </DbCard>

      {/* TDengine */}
      <DbCard
        title="TDengine 时序库"
        status={tdRow.status}
        error={tdRow.error}
        onTest={() => handleTest("tdengine", setTdRow, healthConfig(tdengine, passwordDirty.tdengine))}
      >
        <FieldRow label="主机"   value={tdengine.host}     onChange={(v) => onTdengineChange({ ...tdengine, host: v })} />
        <FieldRow label="端口"   value={tdengine.port}     onChange={(v) => onTdengineChange({ ...tdengine, port: v })} width="w-24" />
        <FieldRow label="用户"   value={tdengine.user}     onChange={(v) => onTdengineChange({ ...tdengine, user: v })} />
        <FieldRow
          label="密码" type="password" value={tdengine.password} width="w-36"
          onChange={(v) => { onTdengineChange({ ...tdengine, password: v }); onPasswordDirty("tdengine"); }}
        />
        <FieldRow label="数据库" value={tdengine.database} onChange={(v) => onTdengineChange({ ...tdengine, database: v })} width="w-40" />
        {/* Stable 模式暂不在 ConnectionConfig 中；T017 保留静态默认 */}
        <div className="flex flex-col gap-1">
          <Label className="text-xs text-muted-foreground">Stable 模式</Label>
          <Input defaultValue="sd{type}" className="w-32 h-8 text-sm font-mono" style={{ borderColor: "var(--border)" }} />
        </div>
      </DbCard>
    </>
  );
}

// ─── 标签内容：基础 ───────────────────────────────────────────────────────────

function BasicTab() {
  const { theme, setTheme } = useTheme();
  const [density, setDensity] = useState("标准");

  return (
    <Card>
      <CardHeader>
        <CardTitle>基础设置</CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <Label className="text-sm font-medium">外观主题</Label>
          <SegmentedGroup
            options={["浅色", "深色", "跟随系统"]}
            value={THEME_TO_ZH[theme]}
            onChange={(zh) => setTheme(ZH_TO_THEME[zh as keyof typeof ZH_TO_THEME])}
          />
        </div>
        <div className="flex flex-wrap items-center justify-between gap-3">
          <Label className="text-sm font-medium">页面密度</Label>
          <SegmentedGroup options={["紧凑", "标准", "宽松"]} value={density} onChange={setDensity} />
        </div>
      </CardContent>
    </Card>
  );
}

// ─── 标签内容：Collector ──────────────────────────────────────────────────────

function CollectorTab({
  form,
  onChange,
}: {
  form: ProcessForm;
  onChange: (f: ProcessForm) => void;
}) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Collector 配置</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="grid grid-cols-2 gap-x-8 gap-y-4">
          <div className="flex flex-col gap-1">
            <Label className="text-xs text-muted-foreground">可执行文件路径</Label>
            <Input
              value={form.executable}
              onChange={(e) => onChange({ ...form, executable: e.target.value })}
              className="h-8 text-sm font-mono"
              style={{ borderColor: "var(--border)" }}
            />
          </div>
          <div className="flex flex-col gap-1">
            <Label className="text-xs text-muted-foreground">工作目录</Label>
            <Input
              value={form.working_dir}
              onChange={(e) => onChange({ ...form, working_dir: e.target.value })}
              className="h-8 text-sm font-mono"
              style={{ borderColor: "var(--border)" }}
            />
          </div>
          {/* 以下字段暂不在 ConnectionConfig 中；T017 保留静态默认 */}
          <div className="flex flex-col gap-1">
            <Label className="text-xs text-muted-foreground">默认采集周期</Label>
            <Input defaultValue="100ms" className="h-8 text-sm w-28" style={{ borderColor: "var(--border)" }} />
          </div>
          <div className="flex flex-col gap-1">
            <Label className="text-xs text-muted-foreground">日志级别</Label>
            <Input defaultValue="INFO" className="h-8 text-sm w-28" style={{ borderColor: "var(--border)" }} />
          </div>
        </div>

        <div className="flex flex-col gap-1">
          <Label className="text-xs text-muted-foreground">Redis Key 预览</Label>
          <div
            className="rounded-lg px-3 py-2 space-y-0.5"
            style={{ background: "var(--background)", border: "1px solid var(--border)", fontFamily: "monospace", fontSize: 12 }}
          >
            <div className="text-muted-foreground">rt:stream:agv:main:0</div>
            <div className="text-muted-foreground">rt:stream:plc:line1:0</div>
            <div className="text-muted-foreground">rt:stream:qc:main:0</div>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

// ─── 标签内容：Ingest ─────────────────────────────────────────────────────────

function IngestTab({ form }: { form: ProcessForm }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Ingest 配置</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <p className="text-xs text-muted-foreground">进程路径由分布式中心安装目录自动派生。</p>
        <div className="grid grid-cols-2 gap-x-8 gap-y-4">
          <div className="flex flex-col gap-1">
            <Label className="text-xs text-muted-foreground">可执行文件路径</Label>
            <Input value={form.executable} readOnly className="h-8 text-sm font-mono" />
          </div>
          <div className="flex flex-col gap-1">
            <Label className="text-xs text-muted-foreground">工作目录</Label>
            <Input value={form.working_dir} readOnly className="h-8 text-sm font-mono" />
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
// ─── 标签内容：Writer ─────────────────────────────────────────────────────────

function WriterTab({
  form,
  onChange,
}: {
  form: ProcessForm;
  onChange: (f: ProcessForm) => void;
}) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Writer 配置</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="grid grid-cols-2 gap-x-8 gap-y-4">
          <div className="flex flex-col gap-1">
            <Label className="text-xs text-muted-foreground">可执行文件路径</Label>
            <Input
              value={form.executable}
              onChange={(e) => onChange({ ...form, executable: e.target.value })}
              className="h-8 text-sm font-mono"
              style={{ borderColor: "var(--border)" }}
            />
          </div>
          <div className="flex flex-col gap-1">
            <Label className="text-xs text-muted-foreground">工作目录</Label>
            <Input
              value={form.working_dir}
              onChange={(e) => onChange({ ...form, working_dir: e.target.value })}
              className="h-8 text-sm font-mono"
              style={{ borderColor: "var(--border)" }}
            />
          </div>
          {/* 以下字段暂不在 ConnectionConfig 中；T017 保留静态默认 */}
          <div className="flex flex-col gap-1">
            <Label className="text-xs text-muted-foreground">flush_interval_ms</Label>
            <Input defaultValue="500" className="h-8 text-sm w-28" style={{ borderColor: "var(--border)" }} />
          </div>
          <div className="flex flex-col gap-1">
            <Label className="text-xs text-muted-foreground">batch_count</Label>
            <Input defaultValue="200" className="h-8 text-sm w-28" style={{ borderColor: "var(--border)" }} />
          </div>
          <div className="flex flex-col gap-1">
            <Label className="text-xs text-muted-foreground">max_buffer_rows</Label>
            <Input defaultValue="2000" className="h-8 text-sm w-28" style={{ borderColor: "var(--border)" }} />
          </div>
          <div className="flex flex-col gap-1">
            <Label className="text-xs text-muted-foreground">pending_min_idle_ms</Label>
            <Input defaultValue="30000" className="h-8 text-sm w-28" style={{ borderColor: "var(--border)" }} />
          </div>
          <div className="flex flex-col gap-1">
            <Label className="text-xs text-muted-foreground">pending_max_retry</Label>
            <Input defaultValue="5" className="h-8 text-sm w-24" style={{ borderColor: "var(--border)" }} />
          </div>
          <div className="flex flex-col gap-1">
            <Label className="text-xs text-muted-foreground">stable pattern</Label>
            <Input defaultValue="sd{type}" className="h-8 text-sm w-32 font-mono" style={{ borderColor: "var(--border)" }} />
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

// ─── 保存策略卡片 ─────────────────────────────────────────────────────────────

function moduleLabel(name: ModuleName): string {
  if (name === "collector") return "Collector";
  if (name === "ingest") return "Ingest";
  return "Writer";
}

function SaveCard({
  saving,
  restartModules,
  disabled,
  onSave,
  onSaveAndRestart,
}: {
  saving: boolean;
  restartModules: ModuleName[];
  disabled: boolean;
  onSave: () => void;
  onSaveAndRestart: (name: ModuleName) => void;
}) {
  return (
    <Card className="py-0">
      <CardContent className="flex flex-wrap items-center gap-3 px-4 py-3">
        <span className="text-sm font-medium">保存策略</span>
        <Button variant="default" size="sm" className="gap-1.5" disabled={saving || disabled} onClick={onSave}>
          <Save className="h-3.5 w-3.5" />
          保存
        </Button>
        {restartModules.map((name) => (
          <Button
            key={name}
            variant="outline"
            size="sm"
            className="gap-1.5"
            disabled={saving || disabled}
            onClick={() => onSaveAndRestart(name)}
          >
            <RotateCcw className="h-3.5 w-3.5" />
            保存并重启 {moduleLabel(name)}
          </Button>
        ))}
        {restartModules.length > 0 && (
          <span className="ml-auto text-xs text-muted-foreground">重启后生效</span>
        )}
      </CardContent>
    </Card>
  );
}

// ─── 页面 ─────────────────────────────────────────────────────────────────────

export function SettingsPage() {
  const navigate = useNavigate();
  const { auth, logout } = useAuth();
  const { refreshVersion } = usePageRefresh();
  const backendToken = auth?.backendToken ?? null;
  const [searchParams] = useSearchParams();
  const activeTab = normalizeSettingsTab(searchParams.get("tab"));

  const [configLoading, setConfigLoading] = useState(true);
  const [configError,   setConfigError]   = useState<string | null>(null);
  const [form,          setForm]          = useState<FormState>(DEFAULT_FORM);
  // 最近一次从 Agent 成功加载（或成功保存）的配置；为 null 表示从未加载成功，禁止保存
  const [loadedForm,    setLoadedForm]    = useState<FormState | null>(null);
  const [passwordDirty, setPasswordDirty] = useState<PasswordDirty>({ redis: false, mysql: false, tdengine: false });
  const [saving,        setSaving]        = useState(false);
  // collector 运行参数（stream_maxlen，存 MySQL，走 backend API，独立于 agentClient 连接配置）
  const [streamMaxlen,       setStreamMaxlen]      = useState("50000");
  const [streamMaxlenSaving, setStreamMaxlenSaving] = useState(false);
  const formDirtyRef = useRef(false);
  const streamMaxlenDirtyRef = useRef(false);

  // 页面进入时加载配置
  useEffect(() => {
    agentClient
      .getConfig()
      .then((cfg) => {
        if (!formDirtyRef.current) {
          const loaded = configToForm(cfg);
          setForm(loaded);
          setLoadedForm(loaded);
          setPasswordDirty({ redis: false, mysql: false, tdengine: false });
        }
        setConfigError(null);
      })
      .catch((err: unknown) => {
        const e = err as AppError;
        if (e.type === "unauthorized") { logout(); return; }
        // H6：加载失败时禁止保存，避免把默认值（127.0.0.1/root）覆盖到真实配置
        const reason = e && "message" in e && e.message ? `：${e.message}` : "";
        setConfigError(`加载配置失败${reason}。为避免覆盖现有配置，已禁用保存，请刷新页面重新加载`);
      })
      .finally(() => setConfigLoading(false));
  }, [logout, refreshVersion]);

  // 加载 collector 运行参数（stream_maxlen）
  useEffect(() => {
    if (BUILD_ROLE !== "centralized" || !backendToken) return;
    let active = true;
    backendClient
      .getCollectorConfig(backendToken)
      .then((cfg) => { if (active && !streamMaxlenDirtyRef.current) setStreamMaxlen(String(cfg.streamMaxlen)); })
      .catch((err) => {
        if (!active) return;
        const e = err as AppError;
        if (e?.type === "unauthorized") { logout(); return; }
        // 其他错误保留默认 50000
      });
    return () => { active = false; };
  }, [backendToken, logout, refreshVersion]);

  // Esc 键返回
  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      if (e.key === "Escape") navigate(-1);
    }
    document.addEventListener("keydown", onKeyDown);
    return () => document.removeEventListener("keydown", onKeyDown);
  }, [navigate]);

  const canSave = loadedForm !== null && configError === null;

  /** 构建 PUT /admin/config 的请求体：只包含相对已加载配置改动过的字段 */
  function buildPayload(baseline: FormState): Partial<ConnectionConfig> {
    // 密码 partial update 契约（spec §Key Entities / plan §4.x）：
    //   - dirty=false：请求体不包含 password key → Agent 保留原密码
    //   - dirty=true ：请求体包含 password: f.password → Agent 覆盖为新值
    //   - 本阶段不支持清空密码；清空必须走独立接口。
    //   - 禁止发送 password: "" 表达"保留"。
    //   - password 明文不出现在 console（agentFetch sanitize 脱敏）。
    function dbSection(f: DbForm, dirty: boolean): DbConfig {
      const base: DbConfig = {
        host:     f.host,
        port:     parseInt(f.port) || 0,
        user:     f.user,
        database: f.database || undefined,
      };
      return dirty ? { ...base, password: f.password } : base;
    }
    const db = (name: keyof PasswordDirty) => ({
      baseline: dbSection(baseline[name], false),
      current: dbSection(form[name], passwordDirty[name]),
      includePassword: passwordDirty[name],
    });
    const processModule = BUILD_ROLE === "distributed-center" ? "ingest" : "collector";
    return buildConfigPatch({
      redis:    db("redis"),
      mysql:    db("mysql"),
      tdengine: db("tdengine"),
      [processModule]: { baseline: baseline[processModule], current: form[processModule] },
      writer: { baseline: baseline.writer, current: form.writer },
    }) as Partial<ConnectionConfig>;
  }

  async function handleSave() {
    if (!canSave || !loadedForm) { toast.error("配置未成功加载，不能保存"); return; }
    setSaving(true);
    try {
      await agentClient.putConfig(buildPayload(loadedForm));
      setLoadedForm(form);
      formDirtyRef.current = false;
      setPasswordDirty({ redis: false, mysql: false, tdengine: false });
      toast.success("配置已保存");
    } catch (err) {
      const e = err as AppError;
      if (e.type === "unauthorized") { logout(); return; }
      toast.error("保存失败，请重试");
    } finally {
      setSaving(false);
    }
  }

  async function handleSaveStreamMaxlen() {
    if (!backendToken) return;
    const trimmed = streamMaxlen.trim();
    if (!/^[1-9]\d*$/.test(trimmed)) {
      toast.error("Stream 上限必须为正整数");
      return;
    }
    const value = Number(trimmed);
    setStreamMaxlenSaving(true);
    try {
      const cfg = await backendClient.updateCollectorConfig(backendToken, value);
      setStreamMaxlen(String(cfg.streamMaxlen));
      streamMaxlenDirtyRef.current = false;
      toast.success("Stream 上限已保存，重启 Collector 后生效");
    } catch (err) {
      const e = err as AppError;
      if (e?.type === "unauthorized") { logout(); return; }
      toast.error("保存失败，请重试");
    } finally {
      setStreamMaxlenSaving(false);
    }
  }

  async function handleSaveAndRestart(name: ModuleName) {
    const label = name === "collector" ? "Collector" : name === "ingest" ? "Ingest" : "Writer";
    if (!canSave || !loadedForm) { toast.error("配置未成功加载，不能保存"); return; }
    setSaving(true);

    // 第一步：保存配置
    try {
      await agentClient.putConfig(buildPayload(loadedForm));
      setLoadedForm(form);
      formDirtyRef.current = false;
      setPasswordDirty({ redis: false, mysql: false, tdengine: false });
    } catch (err) {
      const e = err as AppError;
      if (e.type === "unauthorized") { logout(); return; }
      toast.error("保存失败，请重试");
      setSaving(false);
      return;
    }

    // 第二步：按模块当前状态决定是否重启
    try {
      const metrics = await agentClient.getProcessMetrics(name);
      if (metrics.status === "running") {
        await agentClient.restartProcess(name);
        toast.success(`配置已保存，${label} 已重启`);
      } else if (metrics.status === "stopped") {
        toast(`配置已保存，${label} 当前已停止，配置将在下次启动时生效`);
      } else {
        // 崩溃 | 未知
        toast.warning(`配置已保存，但无法确认 ${label} 状态，请到模块页手动重启`);
      }
    } catch (err) {
      const e = err as AppError;
      if (e.type === "unauthorized") { logout(); return; }
      toast.error(`配置已保存，但重启 ${label} 失败，请到模块页手动重启`);
    } finally {
      setSaving(false);
    }
  }

  function markPasswordDirty(db: keyof PasswordDirty) {
    formDirtyRef.current = true;
    setPasswordDirty((d) => ({ ...d, [db]: true }));
  }

  const restartModules: ModuleName[] = activeTab === "database"
    ? (BUILD_ROLE === "distributed-center" ? ["ingest", "writer"] : ["collector", "writer"])
    : activeTab === "collector"
      ? ["collector"]
      : activeTab === "writer"
        ? ["writer"]
        : [];
  const showSaveCard = activeTab === "database"
    || activeTab === "collector"
    || activeTab === "writer";

  // ─── 加载中 ────────────────────────────────────────────────────────────────
  if (configLoading) {
    return (
      <div className="flex min-h-[300px] items-center justify-center">
        <p className="text-sm text-muted-foreground/70">正在加载配置…</p>
      </div>
    );
  }

  // ─── 主视图 ────────────────────────────────────────────────────────────────
  return (
    <div className="mx-auto w-full max-w-[1200px] space-y-4">
      {/* 加载失败 inline 提示（保存已禁用，需刷新重新加载） */}
      {configError && (
        <p className="text-xs bg-warning-soft border border-warning/40 text-warning rounded-lg px-3 py-2">
          {configError}
        </p>
      )}

      <Tabs value={activeTab}>

        <TabsContent value="basic" className="space-y-4">
          <BasicTab />
        </TabsContent>

        <TabsContent value="database" className="space-y-4">
          <DatabaseTab
            redis={form.redis}
            mysql={form.mysql}
            tdengine={form.tdengine}
            onRedisChange={(f) => { formDirtyRef.current = true; setForm((s) => ({ ...s, redis: f })); }}
            onMysqlChange={(f) => { formDirtyRef.current = true; setForm((s) => ({ ...s, mysql: f })); }}
            onTdengineChange={(f) => { formDirtyRef.current = true; setForm((s) => ({ ...s, tdengine: f })); }}
            passwordDirty={passwordDirty}
            onPasswordDirty={markPasswordDirty}
          />
        </TabsContent>

        <TabsContent value="collector" className="space-y-4">
          <CollectorTab
            form={form.collector}
            onChange={(f) => { formDirtyRef.current = true; setForm((s) => ({ ...s, collector: f })); }}
          />
          <Card>
            <CardHeader>
              <CardTitle>Collector 运行参数</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <p className="text-xs text-muted-foreground">修改后需重启 Collector 后生效。</p>
              <div className="flex items-end gap-3">
                <FieldRow
                  label="Stream 上限（每 shard 条数）"
                  value={streamMaxlen}
                  onChange={(value) => { streamMaxlenDirtyRef.current = true; setStreamMaxlen(value); }}
                  width="w-32"
                />
                <Button
                  variant="default"
                  size="sm"
                  className="gap-1.5"
                  disabled={streamMaxlenSaving}
                  onClick={handleSaveStreamMaxlen}
                >
                  <Save className="w-4 h-4" />
                  {streamMaxlenSaving ? "保存中…" : "保存"}
                </Button>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="ingest" className="space-y-4">
          <IngestTab form={form.ingest} />
        </TabsContent>

        <TabsContent value="writer" className="space-y-4">
          <WriterTab
            form={form.writer}
            onChange={(f) => { formDirtyRef.current = true; setForm((s) => ({ ...s, writer: f })); }}
          />
        </TabsContent>
      </Tabs>

      {showSaveCard && (
        <SaveCard
          saving={saving}
          disabled={!canSave}
          restartModules={restartModules}
          onSave={handleSave}
          onSaveAndRestart={handleSaveAndRestart}
        />
      )}
    </div>
  );
}
