from __future__ import annotations

import argparse
import base64
from dataclasses import dataclass
from datetime import datetime
import json
import os
from pathlib import Path
import random
import re
import time
import tomllib
from typing import Iterable
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen


WORKSPACE_ROOT = Path(__file__).resolve().parents[2]
IDENTIFIER_RE = re.compile(r"^[A-Za-z0-9_]+$")


def read_env_file(path: Path) -> dict[str, str]:
    values: dict[str, str] = {}
    if not path.exists():
        return values

    for raw_line in path.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, value = line.split("=", 1)
        values[key.strip()] = value.strip()
    return values


def load_default_tdengine_config() -> dict[str, str]:
    backend_env = read_env_file(WORKSPACE_ROOT / "backend" / ".env")
    backend_env_example = read_env_file(WORKSPACE_ROOT / "backend" / ".env.example")

    collector_settings: dict[str, str] = {}
    settings_path = WORKSPACE_ROOT / "collector" / "config" / "settings.toml"
    if settings_path.exists():
        data = tomllib.loads(settings_path.read_text(encoding="utf-8"))
        tdengine = data.get("tdengine", {})
        collector_settings = {
            "TDENGINE_HOST": str(tdengine.get("host", "")),
            "TDENGINE_PORT": str(tdengine.get("port", "")),
            "TDENGINE_USER": str(tdengine.get("user", "")),
            "TDENGINE_PASSWORD": str(tdengine.get("password", "")),
        }

    def pick(key: str, fallback: str) -> str:
        return (
            os.getenv(key)
            or backend_env.get(key)
            or backend_env_example.get(key)
            or collector_settings.get(key)
            or fallback
        )

    return {
        "host": pick("TDENGINE_HOST", "127.0.0.1"),
        "port": pick("TDENGINE_PORT", "6041"),
        "user": pick("TDENGINE_USER", "root"),
        "password": pick("TDENGINE_PASSWORD", ""),
    }


def quote_identifier(value: str) -> str:
    if not IDENTIFIER_RE.fullmatch(value):
        raise ValueError(f"Invalid TDengine identifier: {value}")
    return f"`{value}`"


def format_timestamp(value: datetime) -> str:
    return value.strftime("%Y-%m-%d %H:%M:%S.%f")[:-3]


@dataclass
class VariableTableProfile:
    database: str
    stable_name: str
    table_name: str
    device_code: str
    device_id: int
    variable_id: int
    kind: str
    current: float
    low: float
    high: float
    drift: float
    precision: int = 2
    int_values: tuple[int, ...] = (0, 1)
    switch_probability: float = 0.15
    spike_probability: float = 0.0
    spike_low: float | None = None
    spike_high: float | None = None

    def next_value(self) -> int | float:
        if self.kind == "int":
            current_int = int(round(self.current))
            if current_int not in self.int_values:
                current_int = self.int_values[0]
            if random.random() < self.switch_probability:
                current_int = random.choice(self.int_values)
            self.current = float(current_int)
            return current_int

        if self.spike_probability and random.random() < self.spike_probability:
            low = self.spike_low if self.spike_low is not None else self.low
            high = self.spike_high if self.spike_high is not None else self.high
            next_value = random.uniform(low, high)
        else:
            next_value = self.current + random.uniform(-self.drift, self.drift)
            next_value = max(self.low, min(self.high, next_value))

        next_value = round(next_value, self.precision)
        self.current = next_value
        return next_value

    def build_insert_sql(self, timestamp: datetime | None = None) -> str:
        event_time = timestamp or datetime.now()
        value = self.next_value()
        return (
            f"INSERT INTO {quote_identifier(self.database)}.{quote_identifier(self.table_name)} "
            f"(`ts`, `value`) VALUES ('{format_timestamp(event_time)}', {value})"
        )


class TDengineRestClient:
    def __init__(self, host: str, port: int, user: str, password: str, timeout: float):
        self.host = host
        self.port = port
        self.user = user
        self.password = password
        self.timeout = timeout

    def execute(self, sql: str, database: str | None = None) -> dict:
        path = "/rest/sql"
        if database:
            path = f"{path}/{database}"
        url = f"http://{self.host}:{self.port}{path}?row_with_meta=true"
        token = base64.b64encode(f"{self.user}:{self.password}".encode("utf-8")).decode("ascii")
        request = Request(
            url=url,
            data=sql.encode("utf-8"),
            method="POST",
            headers={
                "Authorization": f"Basic {token}",
                "Content-Type": "text/plain; charset=utf-8",
            },
        )

        try:
            with urlopen(request, timeout=self.timeout) as response:
                payload = json.loads(response.read().decode("utf-8"))
        except HTTPError as exc:
            detail = exc.read().decode("utf-8", errors="replace")
            raise RuntimeError(f"HTTP {exc.code}: {detail}") from exc
        except URLError as exc:
            raise RuntimeError(f"TDengine REST unavailable: {exc}") from exc

        if payload.get("code") != 0:
            raise RuntimeError(
                f"TDengine error {payload.get('code')}: {payload.get('desc', 'unknown error')}"
            )
        return payload


def build_default_profiles() -> dict[str, VariableTableProfile]:
    return {
        "qc100_1": VariableTableProfile(
            database="qc",
            stable_name="qc100",
            table_name="qc100_1",
            device_code="qc100",
            device_id=991001,
            variable_id=992001,
            kind="float",
            current=12.60,
            low=10.00,
            high=15.50,
            drift=0.45,
        ),
        "qc100_11901": VariableTableProfile(
            database="qc",
            stable_name="qc100",
            table_name="qc100_11901",
            device_code="qc100",
            device_id=991001,
            variable_id=992002,
            kind="float",
            current=6.40,
            low=4.50,
            high=8.20,
            drift=0.35,
        ),
        "qc101_1": VariableTableProfile(
            database="qc",
            stable_name="qc101",
            table_name="qc101_1",
            device_code="qc101",
            device_id=991004,
            variable_id=992007,
            kind="float",
            current=11.20,
            low=9.20,
            high=14.80,
            drift=0.40,
        ),
        "qc101_11901": VariableTableProfile(
            database="qc",
            stable_name="qc101",
            table_name="qc101_11901",
            device_code="qc101",
            device_id=991004,
            variable_id=992008,
            kind="float",
            current=7.10,
            low=5.30,
            high=9.10,
            drift=0.30,
        ),
        "asc7011_85": VariableTableProfile(
            database="asc",
            stable_name="asc7011",
            table_name="asc7011_85",
            device_code="asc7011",
            device_id=991002,
            variable_id=992003,
            kind="int",
            current=0,
            low=0,
            high=1,
            drift=0,
            int_values=(0, 1),
            switch_probability=0.08,
        ),
        "asc7011_4200": VariableTableProfile(
            database="asc",
            stable_name="asc7011",
            table_name="asc7011_4200",
            device_code="asc7011",
            device_id=991002,
            variable_id=992004,
            kind="float",
            current=41.80,
            low=35.00,
            high=47.00,
            drift=0.60,
        ),
        "asc7021_85": VariableTableProfile(
            database="asc",
            stable_name="asc7021",
            table_name="asc7021_85",
            device_code="asc7021",
            device_id=991005,
            variable_id=992009,
            kind="int",
            current=1,
            low=0,
            high=1,
            drift=0,
            int_values=(0, 1),
            switch_probability=0.06,
        ),
        "asc7021_4200": VariableTableProfile(
            database="asc",
            stable_name="asc7021",
            table_name="asc7021_4200",
            device_code="asc7021",
            device_id=991005,
            variable_id=992010,
            kind="float",
            current=39.60,
            low=33.00,
            high=45.00,
            drift=0.50,
        ),
        "agv801_100": VariableTableProfile(
            database="agv",
            stable_name="agv801",
            table_name="agv801_100",
            device_code="agv801",
            device_id=991003,
            variable_id=992005,
            kind="int",
            current=1,
            low=0,
            high=1,
            drift=0,
            int_values=(0, 1),
            switch_probability=0.08,
        ),
        "agv801_200": VariableTableProfile(
            database="agv",
            stable_name="agv801",
            table_name="agv801_200",
            device_code="agv801",
            device_id=991003,
            variable_id=992006,
            kind="float",
            current=92.50,
            low=60.00,
            high=96.00,
            drift=1.80,
            spike_probability=0.22,
            spike_low=86.00,
            spike_high=98.00,
        ),
        "agv802_100": VariableTableProfile(
            database="agv",
            stable_name="agv802",
            table_name="agv802_100",
            device_code="agv802",
            device_id=991006,
            variable_id=992011,
            kind="int",
            current=1,
            low=0,
            high=1,
            drift=0,
            int_values=(0, 1),
            switch_probability=0.10,
        ),
        "agv802_200": VariableTableProfile(
            database="agv",
            stable_name="agv802",
            table_name="agv802_200",
            device_code="agv802",
            device_id=991006,
            variable_id=992012,
            kind="float",
            current=88.60,
            low=58.00,
            high=94.00,
            drift=1.60,
            spike_probability=0.18,
            spike_low=84.00,
            spike_high=97.00,
        ),
    }


def build_default_presets() -> dict[str, list[str]]:
    return {
        # Default keeps ASC untouched, so asc7011/asc7021 can continue to represent stale/offline.
        "default": [
            "qc100_1",
            "qc100_11901",
            "qc101_1",
            "qc101_11901",
            "agv801_100",
            "agv801_200",
            "agv802_100",
            "agv802_200",
        ],
        "qc": ["qc100_1", "qc100_11901", "qc101_1", "qc101_11901"],
        "asc": ["asc7011_85", "asc7011_4200", "asc7021_85", "asc7021_4200"],
        "agv": ["agv801_100", "agv801_200", "agv802_100", "agv802_200"],
        "all": [
            "qc100_1",
            "qc100_11901",
            "qc101_1",
            "qc101_11901",
            "asc7011_85",
            "asc7011_4200",
            "asc7021_85",
            "asc7021_4200",
            "agv801_100",
            "agv801_200",
            "agv802_100",
            "agv802_200",
        ],
    }


def ensure_schema(client: TDengineRestClient, profiles: Iterable[VariableTableProfile]) -> None:
    created_databases: set[str] = set()
    created_stables: set[tuple[str, str]] = set()

    for profile in profiles:
        if profile.database not in created_databases:
            client.execute(
                f"CREATE DATABASE IF NOT EXISTS {quote_identifier(profile.database)} "
                f"KEEP 365 DURATION 30 BUFFER 16 PAGES 128"
            )
            created_databases.add(profile.database)

        stable_key = (profile.database, profile.stable_name)
        if stable_key not in created_stables:
            client.execute(
                f"CREATE STABLE IF NOT EXISTS {quote_identifier(profile.database)}.{quote_identifier(profile.stable_name)} "
                f"(`ts` TIMESTAMP, `value` DOUBLE) TAGS (`device_id` BIGINT, `variable_id` BIGINT)",
                database=profile.database,
            )
            created_stables.add(stable_key)

        client.execute(
            f"CREATE TABLE IF NOT EXISTS {quote_identifier(profile.database)}.{quote_identifier(profile.table_name)} "
            f"USING {quote_identifier(profile.database)}.{quote_identifier(profile.stable_name)} "
            f"TAGS ({profile.device_id}, {profile.variable_id})",
            database=profile.database,
        )


def resolve_selected_names(
    raw: str,
    available_profiles: dict[str, VariableTableProfile],
    presets: dict[str, list[str]],
) -> list[str]:
    preset_name = raw.strip().lower()
    if preset_name in presets:
        return presets[preset_name]

    values = [item.strip() for item in raw.split(",") if item.strip()]
    if not values:
        raise ValueError("At least one table name must be provided.")
    return values


def build_parser() -> argparse.ArgumentParser:
    defaults = load_default_tdengine_config()
    parser = argparse.ArgumentParser(
        description="Continuously write fake TDengine samples using the new model: device-type DB -> device stable -> variable subtable."
    )
    parser.add_argument("--host", default=defaults["host"], help="TDengine REST host")
    parser.add_argument("--port", type=int, default=int(defaults["port"]), help="TDengine REST port")
    parser.add_argument("--user", default=defaults["user"], help="TDengine user")
    parser.add_argument("--password", default=defaults["password"], help="TDengine password")
    parser.add_argument(
        "--tables",
        default="default",
        help=(
            "Comma-separated variable subtables to write, or preset: "
            "default/qc/asc/agv/all. Default keeps ASC stale/offline."
        ),
    )
    parser.add_argument("--min-interval", type=float, default=0.1, help="Minimum sleep seconds between batches")
    parser.add_argument("--max-interval", type=float, default=1.0, help="Maximum sleep seconds between batches")
    parser.add_argument("--iterations", type=int, default=0, help="Number of write batches. 0 means run forever")
    parser.add_argument("--timeout", type=float, default=5.0, help="HTTP timeout in seconds")
    parser.add_argument("--skip-ensure-schema", action="store_true", help="Skip CREATE DATABASE/STABLE/TABLE checks")
    return parser


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()

    if args.min_interval <= 0 or args.max_interval <= 0:
        parser.error("min-interval and max-interval must be greater than 0.")
    if args.min_interval > args.max_interval:
        parser.error("min-interval cannot be greater than max-interval.")

    available_profiles = build_default_profiles()
    presets = build_default_presets()
    selected_names = resolve_selected_names(args.tables, available_profiles, presets)
    missing = [name for name in selected_names if name not in available_profiles]
    if missing:
        parser.error(
            "Unknown subtable profile(s): "
            + ", ".join(missing)
            + ". Extend build_default_profiles() if you add new TDengine test subtables."
        )

    profiles = [available_profiles[name] for name in selected_names]
    client = TDengineRestClient(
        host=args.host,
        port=args.port,
        user=args.user,
        password=args.password,
        timeout=args.timeout,
    )

    if not args.skip_ensure_schema:
        ensure_schema(client, profiles)

    print(
        f"[writer] start tables={','.join(selected_names)} "
        f"interval={args.min_interval:.3f}s-{args.max_interval:.3f}s"
    )

    batch_count = 0
    while True:
        batch_count += 1
        for profile in profiles:
            event_time = datetime.now()
            sql = profile.build_insert_sql(timestamp=event_time)
            payload = client.execute(sql, database=profile.database)
            affected_rows = payload.get("data", [{}])[0]
            rows = affected_rows.get("affected_rows", 1) if isinstance(affected_rows, dict) else 1
            print(
                f"[writer] batch={batch_count} db={profile.database} table={profile.table_name} "
                f"rows={rows} ts={format_timestamp(event_time)}"
            )

        if args.iterations and batch_count >= args.iterations:
            break

        time.sleep(random.uniform(args.min_interval, args.max_interval))

    print(f"[writer] done batches={batch_count}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
