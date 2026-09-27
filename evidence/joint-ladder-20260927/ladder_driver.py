"""联合链路阶梯加压驱动（编排 + 停止判据 + 逐档留证）。

每档：plan → prep → 起生产 Writer → 并发 TD 延迟监控 → 采集窗口 → 停监控/Writer → 对账
     → 读 Writer 真实 CPU（容器 cgroup 差值）→ 判据。

停止增加输入的条件（命中即停，不再上更高档）：
  * 输入未达速（input_rate_ratio < 0.95）——驱动限速，本档不作容量结论
  * 拒收帧 > 0
  * Redis 主线程忙碌比 >= 0.95
  * 明确持续积压：后半窗平均积压 > 前半窗 1.2 倍且 > 200 帧，或后半窗 PEL>0 占比 >= 50%
  * Redis 内存接近护栏（>= 3.5 GB / maxmemory 4 GB）

用法：python ladder_driver.py --tiers 20,25,30,40 --points 500 --period-ms 100 \
        --screen-seconds 300 --soak-seconds 1800 --evidence <dir> --repo <dir>
"""
from __future__ import annotations

import argparse
import json
import subprocess
import time
from pathlib import Path

REDIS_GUARD_BYTES = int(3.5 * 1024 ** 3)
IMAGE_COLLECTOR = "data-watch-collector-test:20260925"
IMAGE_RUNNER = "data-watch-pipeline-runner:20260925"
NET = "dwv-joint"
TMP = "/tmp"


def sh(args: list[str], *, check: bool = True, capture: bool = True) -> subprocess.CompletedProcess:
    return subprocess.run(args, check=check, capture_output=capture, text=True,
                          encoding="utf-8", errors="replace")


def docker(*args: str, check: bool = True) -> subprocess.CompletedProcess:
    return sh(["docker", *args], check=check)


def container_cpu_seconds(name: str) -> float | None:
    """读容器真实 CPU 时间（cgroup v2 usage_usec，回退 cgroup v1 cpuacct.usage）。"""
    for path, divisor in (("/sys/fs/cgroup/cpu.stat", 1e6),
                          ("/sys/fs/cgroup/cpuacct/cpuacct.usage", 1e9)):
        result = docker("exec", name, "cat", path, check=False)
        if result.returncode != 0:
            continue
        for line in result.stdout.splitlines():
            parts = line.split()
            if len(parts) == 2 and parts[0] in ("usage_usec", "usage_ns"):
                return float(parts[1]) / divisor
            if len(parts) == 1 and parts[0].isdigit():
                return float(parts[0]) / divisor
    return None


def new_env(repo: Path, evidence: Path) -> None:
    docker("network", "create", NET, check=False)
    for name, args in (
            ("dwv-joint-redis", ["redis:7.4", "redis-server", "--appendonly", "yes",
                                 "--appendfsync", "everysec", "--maxmemory", "4gb",
                                 "--maxmemory-policy", "noeviction", "--save", ""]),
            ("dwv-joint-mysql", ["mysql:8.4"]),
            ("dwv-joint-td", ["tdengine/tsdb-amd64:3.4.0.9"])):
        docker("rm", "-f", name, check=False)
        cmd = ["run", "-d", "--name", name, "--network", NET,
               "--network-alias", {"dwv-joint-redis": "perf-redis",
                                   "dwv-joint-mysql": "perf-mysql",
                                   "dwv-joint-td": "perf-td"}[name]]
        if name == "dwv-joint-redis":
            cmd += ["--cpus", "4", "--memory", "6g"]
        if name == "dwv-joint-mysql":
            cmd += ["-e", "MYSQL_ALLOW_EMPTY_PASSWORD=yes", "-e", "MYSQL_ROOT_HOST=%"]
        docker(*cmd, *args)
    # 等 MySQL 就绪
    for _ in range(30):
        time.sleep(5)
        if docker("exec", "dwv-joint-mysql", "mysql", "-uroot", "-N", "-e",
                  "select 1", check=False).returncode == 0:
            break


def tier(args, devices: int, seconds: float, label: str) -> dict:
    evidence: Path = args.evidence
    token_dir = evidence / label
    token_dir.mkdir(parents=True, exist_ok=True)
    plan_path = token_dir / "plan.json"
    collector_json = token_dir / "collector.json"
    reconcile_json = token_dir / "reconcile.json"
    tdlags_json = token_dir / "tdlag.json"
    stop_file = token_dir / "tdlag.stop"
    runtime = token_dir / "runtime"
    log_path = token_dir / "writer.log"
    if stop_file.exists():
        stop_file.unlink()

    host_py = [str(args.python)]
    mode = ["scripts/diagnose_joint_pipeline.py"]

    def host(*extra: str) -> subprocess.CompletedProcess:
        return sh(host_py + mode + list(extra))

    def in_runner(*extra: str) -> subprocess.CompletedProcess:
        return docker("run", "--rm", "--network", NET,
                      "-v", f"{args.repo}:/workspace", "-v", f"{evidence}:/evidence",
                      "-w", "/workspace",
                      "-e", "PYTHONPATH=/workspace:/workspace/collector:/workspace/writer",
                      IMAGE_RUNNER, "python", "/workspace/scripts/diagnose_joint_pipeline.py",
                      *extra)

    def in_runner_soft(*extra: str) -> subprocess.CompletedProcess:
        return docker("run", "--rm", "--network", NET,
                      "-v", f"{args.repo}:/workspace", "-v", f"{evidence}:/evidence",
                      "-w", "/workspace",
                      "-e", "PYTHONPATH=/workspace:/workspace/collector:/workspace/writer",
                      IMAGE_RUNNER, "python", "/workspace/scripts/diagnose_joint_pipeline.py",
                      *extra, check=False)

    # 每档用全新库名，避免跨档累积（Redis 也清库）
    host("--mode", "plan", "--plan", str(plan_path), "--devices", str(devices),
         "--points", str(args.points), "--seconds", str(seconds),
         "--period-ms", str(args.period_ms), "--shard-count", "4")
    plan = json.loads(plan_path.read_text(encoding="utf-8"))
    docker("exec", "dwv-joint-redis", "redis-cli", "-n", "0", "flushdb", check=False)
    in_runner("--mode", "prep", "--plan", f"/evidence/{label}/plan.json",
              "--runtime", f"/evidence/{label}/runtime", "--consumer-workers", "1")
    docker("rm", "-f", "dwv-joint-writer", check=False)
    docker("run", "-d", "--name", "dwv-joint-writer", "--network", NET,
           "-v", f"{args.repo}:/workspace", "-v", f"{evidence}:/evidence",
           "-w", f"/evidence/{label}/runtime",
           "-e", "PYTHONPATH=/workspace:/workspace/writer:/workspace/collector",
           "-e", "PYTHONUNBUFFERED=1", IMAGE_RUNNER,
           "python", "/workspace/writer/main.py", "--runtime-dir",
           f"/evidence/{label}/runtime")
    time.sleep(12)
    writer_started = docker("ps", "-q", "-f", "name=dwv-joint-writer").stdout.strip() != ""
    cpu_before = container_cpu_seconds("dwv-joint-writer")

    # 并发 TD 延迟监控（后台容器）
    docker("rm", "-f", "dwv-joint-tdlag", check=False)
    docker("run", "-d", "--name", "dwv-joint-tdlag", "--network", NET,
           "-v", f"{args.repo}:/workspace", "-v", f"{evidence}:/evidence",
           "-w", "/workspace",
           "-e", "PYTHONPATH=/workspace:/workspace/collector:/workspace/writer",
           IMAGE_RUNNER, "python", "/workspace/scripts/diagnose_joint_pipeline.py",
           "--mode", "tdlag", "--plan", f"/evidence/{label}/plan.json",
           "--output", f"/evidence/{label}/tdlag.json",
           "--stop-file", f"/evidence/{label}/tdlag.stop",
           "--seconds", str(seconds + 60), "--td-host", "perf-td")
    time.sleep(3)

    window_started = time.monotonic()
    collector_run = docker(
        "run", "--rm", "--name", "dwv-joint-collector", "--network", NET,
        "-v", f"{args.repo}:/workspace", "-v", f"{evidence}:/evidence",
        "-w", "/workspace", "-e", "PYTHONPATH=/workspace:/workspace/collector",
        IMAGE_COLLECTOR, "python3.14t",
        "/workspace/scripts/diagnose_joint_pipeline.py",
        "--mode", "collector", "--plan", f"/evidence/{label}/plan.json",
        "--output", f"/evidence/{label}/collector.json", "--outbox-dir", TMP,
        "--redis-host", "perf-redis", "--redis-db", "0", "--drain-seconds", "180",
        check=False)
    collector_exit = collector_run.returncode
    if collector_exit != 0:
        # 采集侧异常退出：这是**基础设施失败**，不是性能边界，必须区分开。
        (token_dir / "collector-failure.log").write_text(
            (collector_run.stdout or "") + "\n" + (collector_run.stderr or ""),
            encoding="utf-8")
    window_seconds = time.monotonic() - window_started
    cpu_after = container_cpu_seconds("dwv-joint-writer")
    stop_file.write_text("stop", encoding="utf-8")
    time.sleep(3)
    docker("rm", "-f", "dwv-joint-tdlag", check=False)
    if collector_exit != 0:
        # 采集侧异常退出：立刻收尾并记为**基础设施失败**（不是性能边界），
        # 且不再往下走 reconcile（此时没有 collector.json）。第一版就是在这里
        # 用 check=True 直接抛出，把整条阶梯打断、还丢了这一档的现场。
        docker("rm", "-f", "dwv-joint-writer", check=False)
        docker("run", "--rm", "--network", NET, IMAGE_RUNNER, "python", "-c",
               "import taosws; c=taosws.connect(host='perf-td',port=6041,user='root',"
               f"password='taosdata'); c.execute('DROP DATABASE IF EXISTS {plan['mysql_database']}'); c.close()",
               check=False)
        summary = {"label": label, "devices": devices, "seconds": seconds,
                   "collector_exit_code": collector_exit,
                   "infrastructure_failure": True, "sustainable": False,
                   "stop_reasons": [f"采集侧异常退出（exit {collector_exit}）：本档未测成，"
                                    "不计入容量结论，见 collector-failure.log"],
                   "window_wall_seconds": round(window_seconds, 3)}
        (token_dir / "summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2),
                                                encoding="utf-8")
        return summary

    # 等 Writer 排空后正常停机，取日志
    for _ in range(60):
        total = 0
        for shard in range(4):
            out = docker("exec", "dwv-joint-redis", "redis-cli", "-n", "0", "xlen",
                         f"rt:stream:{plan['device_type']}:{plan['group']}:{shard}",
                         check=False).stdout.strip()
            total += int(out or 0)
        if total == 0:
            break
        time.sleep(3)
    (runtime / "writer.shutdown_flag").write_text("stop\n", encoding="utf-8")
    for _ in range(20):
        if docker("ps", "-q", "-f", "name=dwv-joint-writer").stdout.strip() == "":
            break
        time.sleep(3)
    log = docker("logs", "dwv-joint-writer", check=False)
    log_path.write_text(log.stdout + log.stderr, encoding="utf-8")
    docker("rm", "-f", "dwv-joint-writer", check=False)

    cpu_seconds = (None if cpu_before is None or cpu_after is None
                   else round(cpu_after - cpu_before, 3))
    reconcile_args = ["--mode", "reconcile", "--plan", f"/evidence/{label}/plan.json",
                      "--collector-output", f"/evidence/{label}/collector.json",
                      "--output", f"/evidence/{label}/reconcile.json",
                      "--redis-host", "perf-redis", "--redis-db", "0",
                      "--sample-ticks", "2"]
    if cpu_seconds is not None:
        reconcile_args += ["--writer-cpu-seconds", str(cpu_seconds)]
    # 对账失败不能打断整条阶梯（饱和档常因缺口缺少子表）：记下来继续。
    reconcile_run = in_runner_soft(*reconcile_args)
    reconcile_error = (None if reconcile_run.returncode == 0 else
                       (reconcile_run.stdout or "")[-800:] + (reconcile_run.stderr or "")[-800:])

    collector = json.loads(collector_json.read_text(encoding="utf-8"))
    reconcile = json.loads(reconcile_json.read_text(encoding="utf-8"))
    # 每档测完就删库：单节点 TDengine 的 vnode 有限，留着会在阶梯中途耗尽
    # （数值已在 reconcile.json 里留证）。清理动作只针对本档自建库。
    docker("run", "--rm", "--network", NET, IMAGE_RUNNER, "python", "-c",
           "import taosws,sys; c=taosws.connect(host='perf-td',port=6041,user='root',"
           f"password='taosdata'); c.execute('DROP DATABASE IF EXISTS {plan['mysql_database']}'); c.close()",
           check=False)
    docker("exec", "dwv-joint-mysql", "mysql", "-uroot", "-e",
           f"DROP DATABASE IF EXISTS `{plan['mysql_database']}`", check=False)
    tdlags = (json.loads(tdlags_json.read_text(encoding="utf-8"))
              if tdlags_json.exists() else {})
    samples = collector.get("samples", [])
    half = len(samples) // 2
    first = samples[:half] or [{"backlog_frames": 0}]
    second = samples[half:] or [{"backlog_frames": 0}]
    mean = lambda rows, key: (sum(r.get(key) or 0 for r in rows) / len(rows)) if rows else 0
    backlog_first, backlog_second = mean(first, "backlog_frames"), mean(second, "backlog_frames")
    pel_share = (sum(1 for r in second if (r.get("pending_frames") or 0) > 0) / len(second)
                 if second else 0)
    busy = collector.get("redis_cpu_main_thread_busy")
    mem = docker("exec", "dwv-joint-redis", "redis-cli", "info", "memory", check=False).stdout
    used = 0
    for line in mem.splitlines():
        if line.startswith("used_memory:"):
            used = int(line.split(":")[1].strip())
    # PEL 只判"增长"不判"存在"：流式消费天然总有少量在途未确认，
    # 第一版用"后半窗 PEL>0 占比 >= 0.5"会把健康流水线误判成持续积压。
    quarter = max(1, len(samples) // 4)
    pel_first = mean(samples[:quarter], "pending_frames")
    pel_last = mean(samples[-quarter:], "pending_frames")
    pel_max = max((r.get("pending_frames") or 0) for r in samples) if samples else 0
    reasons = []
    if not collector.get("input_rate_reached"):
        reasons.append(f"输入未达速（ratio={collector.get('input_rate_ratio')}）")
    if collector.get("rejected_frames"):
        reasons.append(f"拒收帧={collector['rejected_frames']}")
    if busy is not None and busy >= 0.95:
        reasons.append(f"Redis 主线程忙碌比 {busy} >= 0.95")
    if backlog_second > backlog_first * 1.2 and backlog_second > 200:
        reasons.append(f"持续积压：后半窗均值 {backlog_second:.0f} > 前半窗 {backlog_first:.0f} × 1.2")
    if pel_last > pel_first * 1.5 and pel_max > 500:
        reasons.append(f"PEL 增长：末 1/4 均值 {pel_last:.0f} > 首 1/4 {pel_first:.0f} × 1.5（峰值 {pel_max}）")
    if used >= REDIS_GUARD_BYTES:
        reasons.append(f"Redis 内存 {used/1024**3:.2f} GB 接近护栏")
    summary = {
        "label": label, "devices": devices, "seconds": seconds,
        "tier": collector.get("tier"), "writer_started": writer_started,
        "planned_frames": collector.get("planned_frames"),
        "attempted_frames": collector.get("attempted_frames"),
        "accepted_at_feed_end": collector.get("accepted_at_feed_end"),
        "accepted_after_drain": collector.get("accepted_after_drain"),
        "redis_confirmed_at_feed_end": collector.get("redis_confirmed_at_feed_end"),
        "redis_confirmed_after_drain": collector.get("redis_confirmed_after_drain"),
        "rejected_frames": collector.get("rejected_frames"),
        "input_rate_ratio": collector.get("input_rate_ratio"),
        "submitted_fps": collector.get("submitted_fps"),
        "redis_cpu_main_thread_busy": busy,
        "redis_cpu_process_busy": collector.get("redis_cpu_process_busy"),
        "writer_process_cpu_seconds": cpu_seconds,
        "writer_cpu_busy_ratio": reconcile.get("writer_cpu_busy_ratio"),
        "window_seconds": collector.get("window_seconds"),
        "latency_in_window": collector.get("latency_in_window"),
        "latency_second_half": collector.get("latency_second_half"),
        "td_confirm_lag_ms_p50": tdlags.get("td_confirm_lag_ms_p50"),
        "td_confirm_lag_ms_max": tdlags.get("td_confirm_lag_ms_max"),
        "backlog_mean_first_half": round(backlog_first, 1),
        "backlog_mean_second_half": round(backlog_second, 1),
        "backlog_max": max((r.get("backlog_frames") or 0) for r in samples) if samples else None,
        "pel_share_second_half": round(pel_share, 3),
        "redis_used_memory_bytes": used,
        "rows": reconcile.get("rows"), "reconcile_status": reconcile.get("status"),
        "reconcile_error": reconcile_error,
        "latest_hash_ok_devices": reconcile.get("latest_hash_ok_devices"),
        "dlq_entries": reconcile.get("dlq_entries"),
        "collector_drain_seconds": collector.get("collector_drain_seconds"),
        "window_wall_seconds": round(window_seconds, 3),
        "stop_reasons": reasons,
        "sustainable": not reasons,
    }
    (token_dir / "summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2),
                                            encoding="utf-8")
    return summary


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", required=True)
    parser.add_argument("--evidence", required=True)
    parser.add_argument("--python", required=True)
    parser.add_argument("--tiers", default="20,25,30,40")
    parser.add_argument("--points", type=int, default=500)
    parser.add_argument("--period-ms", type=int, default=100)
    parser.add_argument("--screen-seconds", type=float, default=300)
    parser.add_argument("--soak-seconds", type=float, default=1800)
    parser.add_argument("--baseline-seconds", type=float, default=300)
    parser.add_argument("--skip-env", action="store_true")
    args = parser.parse_args()
    args.repo = Path(args.repo).resolve()
    args.evidence = Path(args.evidence).resolve()
    args.evidence.mkdir(parents=True, exist_ok=True)

    if not args.skip_env:
        new_env(args.repo, args.evidence)

    results, boundary = [], None
    # 续跑支持：继承本目录里已完成的档（例如先跑了 20/25/30，再单独补 40），
    # 否则"最高可持续档"会在续跑时被算错。
    planned_labels = {f"tier-{int(x)}dev" for x in args.tiers.split(",")}
    for path in sorted(args.evidence.glob("*/summary.json")):
        if path.parent.name in planned_labels or not path.parent.name.startswith("tier-"):
            continue
        results.append(json.loads(path.read_text(encoding="utf-8")))
    for devices in [int(x) for x in args.tiers.split(",")]:
        label = f"tier-{devices}dev"
        summary = tier(args, devices, args.screen_seconds, label)
        results = [r for r in results if r.get("label") != label] + [summary]
        print(json.dumps({k: v for k, v in summary.items()
                          if k not in ("rows", "latency_in_window")}, ensure_ascii=False),
              flush=True)
        (args.evidence / "ladder-summary.json").write_text(
            json.dumps({"tiers": results, "boundary": boundary},
                       ensure_ascii=False, indent=2), encoding="utf-8")
        if summary.get("infrastructure_failure"):
            boundary = {"devices": devices, "reasons": summary["stop_reasons"],
                        "infrastructure_failure": True,
                        "note": "采集侧异常退出，不是性能边界"}
            continue          # 基础设施失败不算容量边界，但也不再往上加压
        if summary["stop_reasons"]:
            boundary = {"devices": devices, "reasons": summary["stop_reasons"],
                        "input_rate_ratio": summary["input_rate_ratio"]}
            break

    sustainable = [r for r in results if r["sustainable"]]
    if sustainable:
        best = max(sustainable, key=lambda r: r["devices"])
        soak = tier(args, best["devices"], args.soak_seconds, f"soak-{best['devices']}dev")
        results.append(soak)
    # 环境漂移复跑：20 台基线（同一源码/资源，检查前后是否漂移）
    drift = tier(args, 20, args.baseline_seconds, "baseline-20dev-rerun")
    results.append(drift)
    (args.evidence / "ladder-summary.json").write_text(
        json.dumps({"tiers": results, "boundary": boundary},
                   ensure_ascii=False, indent=2), encoding="utf-8")
    print("ladder done", flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
