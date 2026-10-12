# AGENTS.md

Behavioral guidelines to reduce common LLM coding mistakes. Merge with project-specific instructions as needed.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

## 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

## 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

## 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

## 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:
```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

---

**These guidelines are working if:** fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes.

## Project-Specific Rules for This Repository

Before any implementation, do diagnosis only. Do not edit, create, delete, or refactor files until the intended changes have been explicitly described.

Follow these rules:

1. This project runs in a Windows environment. Do not use Bash.
2. All commands must be executed with PowerShell or CMD.
3. The project path is: `D:\python\device_alarm\data_watch`
4. Any path containing Chinese characters must be wrapped in quotes.
5. First list the root causes you believe are real. Do not modify any files yet.
6. Fix only one minimal issue at a time.
7. Before making changes, explain which files will be modified, what will be changed in each file, and why.
8. Do not refactor, do not casually optimize, and do not modify unrelated files.

## Cursor Cloud 开发环境

上面的 Windows / PowerShell 规则适用于本机开发。Cursor Cloud Agent 是 Linux，仓库在 `/agent/repos/data_watch`，这里使用 bash。

- 前端：`cd frontend && pnpm install --frozen-lockfile && pnpm test`。`pnpm exec vite --host 127.0.0.1 --port 5173` 能启动，但页面会因缺少 `src/views/monitor-light/screen.menu` 报 Vite 导入错误。`tests/monitor-light.test.ts` 也会因缺少 `src/views/monitor-light/data` 失败。这是仓库现状。
- CLI：`python3 -m venv .venv`，再安装 `cli/requirements.txt`、`pytest` 和 `click`（Typer 0.27 不再带上 click，而源码直接导入它），然后 `.venv/bin/python -m pytest cli/tests -q`。
- Backend 运行时需要 free-threaded Python 3.14t。现场采集、Agent 和 Tauri 桌面端依赖 Windows，不要在这个 Linux 环境里启动它们。
