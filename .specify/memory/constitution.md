# DataWatch Console Constitution

## Core Principles

### I. Windows Environment (NON-NEGOTIABLE)
All shell commands must use PowerShell. Bash is forbidden.
No Unix-only commands (`grep`, `sed`, `find`, `ls`); use PowerShell equivalents or dedicated tool calls.
Paths with Chinese characters must be quoted.

### II. Diagnose Before Implement (NON-NEGOTIABLE)
Every task follows this sequence:
1. Diagnose — read files, identify root cause, state assumptions explicitly.
2. Describe — list which files will change, what changes, and why.
3. Implement — make only the described changes; no unrelated edits.

Ambiguity stops work; ask before guessing.

### III. Technology Stack
Desktop application: `Tauri + React + TypeScript + shadcn/ui + Tailwind CSS v4`.
Agent backend: `Python + FastAPI`, registered as a Windows service via NSSM.
Data pipeline: `collector` writes to Redis Stream; `writer` consumes Redis Stream and writes to TDengine.
Config encryption: Windows DPAPI (Machine scope) for all passwords at rest.

### IV. Tauri Rust Layer Boundary (NON-NEGOTIABLE)
The Tauri Rust layer (`src-tauri/src/`) implements exactly two capabilities:

```
http_proxy.rs   agent_request(method, path, body?, token)
                  → HTTP proxy from WebView to Agent (resolves CORS)
                  → attaches Agent JWT when available
                  → must NOT log Authorization headers or password fields

tray.rs         show_window()
                hide_to_tray()
```

**Forbidden in Tauri Rust:**
- Spawning or terminating collector / writer processes.
- Reading or writing any configuration file directly.
- Capturing collector / writer stdout or stderr.
- Implementing ProcessManager, ConfigManager, HealthManager, or MetricsManager.

### V. DataWatch Agent Responsibilities
The Agent (Windows service, `C:\ProgramData\DataWatch\`, port 7070) owns:

| Responsibility | Detail |
|---|---|
| Process lifecycle | Start, stop, restart collector / writer; set workingDirectory per process |
| Configuration | Read/write `config.toml`; passwords stored as DPAPI-encrypted `password_enc` |
| Health checks | Verify Redis, MySQL, TDengine connectivity on demand |
| Metrics | PID, uptime, memory_mb, cpu_percent, restart_count (psutil) |
| Logging | Redirect collector/writer stdout/stderr to rolling files; tail via `WS /viewer/logs/{name}/tail` |
| Auth | Query `sys_user`/`sys_role` from MySQL; issue Agent JWTs (Admin or Viewer level) |
| Bootstrap | When MySQL unreachable: expose only `/bootstrap/*` on 127.0.0.1; block all other routes |

### VI. Data Flow Constraints
- `collector` reads from devices → writes **only** to Redis Stream. No direct TDengine writes.
- `writer` consumes Redis Stream → writes **only** to TDengine. No direct device access.
- Both modules must point to the **same** Redis instance.
- TDengine stable pattern: `sd{type}`.
- DLQ pattern: `rt:dlq:{device_type}:{group_code}`.

### VII. Acceptance Criteria Required
Every feature or task must define verifiable acceptance criteria before implementation begins.
Criteria must be concrete and testable: API returns expected shape, UI shows expected state, process starts/stops within N seconds, log line appears, etc.
"Works correctly" is not an acceptance criterion.

### VIII. Simplicity First
Minimum code that solves the problem. No speculative abstractions.
No features beyond what was asked.
No refactoring of unrelated code.
If a change touches more than what was described in step II, stop and re-describe.

## Architecture Reference

| Layer | Component | Key files |
|---|---|---|
| Desktop UI | React + shadcn/ui | `app/src/pages/`, `app/src/components/` |
| Tauri shell | Rust | `app/src-tauri/src/http_proxy.rs`, `tray.rs` |
| Agent | Python FastAPI | `agent/` (separate project, Windows service) |
| Collector | Python | `collector/` (PyInstaller → .exe) |
| Writer | Python | `writer/` (PyInstaller → .exe) |
| Config | TOML + DPAPI | `C:\ProgramData\DataWatch\config.toml` |

Reference documents:
- Architecture baseline: `docs/桌面应用/DataWatch-Console-设计方案.md` (v1.3)
- UI specification: `docs/设计/Data Watch Console 页面设计说明.md`

## Development Workflow

1. Start each feature with `/speckit.specify` → review → `/speckit.plan` → review → `/speckit.tasks` → `/speckit.implement`.
2. Git commits via `/speckit.git.commit` after each completed task.
3. No commit without passing acceptance criteria.
4. Password fields must never appear in logs, events, or git diffs.

## Governance

This constitution supersedes all other development conventions for this project.
Violations of Sections IV and VI require an explicit written override with justification.
All spec/plan/task documents must reference the relevant constitution principle when a constraint applies.

**Version**: 1.0.0 | **Ratified**: 2026-05-25 | **Last Amended**: 2026-05-25
