from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
from pathlib import Path

SERVICE_NAME = "DataWatchAgent"


def _agent_dir() -> Path:
    return Path(__file__).resolve().parents[1]


def _find_nssm(explicit: str | None) -> str:
    if explicit:
        return explicit
    found = shutil.which("nssm")
    if found:
        return found
    raise SystemExit("nssm.exe not found. Pass --nssm or add it to PATH.")


def _run(cmd: list[str], dry_run: bool) -> None:
    print(" ".join(f'"{part}"' if " " in part else part for part in cmd))
    if not dry_run:
        subprocess.run(cmd, check=True)


def install(args: argparse.Namespace) -> None:
    nssm = _find_nssm(args.nssm)
    agent_dir = Path(args.agent_dir).resolve()
    python_exe = Path(args.python).resolve()
    main_py = agent_dir / "main.py"
    logs_dir = agent_dir / "logs"

    if not python_exe.exists():
        raise SystemExit(f"Python executable not found: {python_exe}")
    if not main_py.exists():
        raise SystemExit(f"Agent main.py not found: {main_py}")

    logs_dir.mkdir(parents=True, exist_ok=True)

    service = args.name
    _run([nssm, "install", service, str(python_exe)], args.dry_run)
    _run([nssm, "set", service, "AppParameters", str(main_py)], args.dry_run)
    _run([nssm, "set", service, "AppDirectory", str(agent_dir)], args.dry_run)
    _run([nssm, "set", service, "Start", "SERVICE_AUTO_START"], args.dry_run)
    _run([nssm, "set", service, "AppStdout", str(logs_dir / "agent-service.out.log")], args.dry_run)
    _run([nssm, "set", service, "AppStderr", str(logs_dir / "agent-service.err.log")], args.dry_run)


def service_action(args: argparse.Namespace) -> None:
    nssm = _find_nssm(args.nssm)
    cmd = [nssm, args.action, args.name]
    if args.action == "remove":
        cmd.append("confirm")
    _run(cmd, args.dry_run)


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Install DataWatch Agent as a Windows service via NSSM.")
    parser.add_argument("--nssm", help="Path to nssm.exe. Defaults to PATH lookup.")
    parser.add_argument("--name", default=SERVICE_NAME, help="Windows service name.")
    parser.add_argument("--dry-run", action="store_true", help="Print commands without executing them.")

    subparsers = parser.add_subparsers(dest="action", required=True)

    install_parser = subparsers.add_parser("install")
    install_parser.add_argument("--python", default=sys.executable, help="Python executable used to run Agent.")
    install_parser.add_argument("--agent-dir", default=str(_agent_dir()), help="Agent directory containing main.py.")
    install_parser.set_defaults(func=install)

    for action in ("start", "stop", "restart", "remove"):
        action_parser = subparsers.add_parser(action)
        action_parser.set_defaults(func=service_action)

    return parser


def main() -> None:
    parser = build_parser()
    args = parser.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
