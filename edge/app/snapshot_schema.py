from __future__ import annotations


EDGE_SCHEDULE_SCHEMA = """
CREATE TABLE IF NOT EXISTS edge_assignment_schedule (
    device_code TEXT PRIMARY KEY,
    assignment_epoch INTEGER NOT NULL,
    base_enabled INTEGER NOT NULL,
    collect_from_ms INTEGER,
    collect_until_ms INTEGER
);
"""

CONFIG_SNAPSHOT_SCHEMA = """
CREATE TABLE IF NOT EXISTS edge_config_meta (
    singleton INTEGER PRIMARY KEY CHECK (singleton = 1),
    config_revision INTEGER NOT NULL,
    checksum TEXT NOT NULL,
    read_timeout_ms INTEGER,
    applied_at INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS edge_device (
    device_code TEXT PRIMARY KEY,
    device_pk INTEGER,
    device_type TEXT NOT NULL,
    protocol TEXT NOT NULL,
    endpoint TEXT NOT NULL,
    ip TEXT NOT NULL,
    port INTEGER NOT NULL,
    rack INTEGER NOT NULL,
    slot INTEGER NOT NULL,
    scan_rate_ms INTEGER NOT NULL,
    opc_server TEXT,
    config_version INTEGER NOT NULL,
    assignment_epoch INTEGER NOT NULL,
    enabled INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS edge_variable (
    variable_id TEXT PRIMARY KEY,
    device_code TEXT NOT NULL,
    address TEXT NOT NULL,
    data_type TEXT NOT NULL,
    scan_rate_ms INTEGER NOT NULL,
    is_storage INTEGER NOT NULL,
    var_seq INTEGER NOT NULL,
    group_code TEXT NOT NULL,
    group_config_version INTEGER NOT NULL,
    FOREIGN KEY (device_code) REFERENCES edge_device(device_code) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_edge_variable_device
ON edge_variable(device_code);

CREATE TABLE IF NOT EXISTS edge_device_status (
    device_code TEXT PRIMARY KEY,
    status INTEGER NOT NULL,
    message TEXT NOT NULL,
    event_type TEXT NOT NULL,
    updated_at INTEGER NOT NULL
);
""" + EDGE_SCHEDULE_SCHEMA
