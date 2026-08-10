-- DB-6: distributed deployment core tables
-- Fresh/reference DDL. Ingest applies idempotent upgrades and gated mapping backfill before listening.

CREATE TABLE IF NOT EXISTS sys_node (
    node_uuid              CHAR(36)       NOT NULL,
    node_name              VARCHAR(128)   NOT NULL,
    status                 VARCHAR(16)    NOT NULL DEFAULT 'pending',
    public_key_pem         TEXT,
    previous_public_key_pem TEXT,
    previous_key_expires_at DATETIME(3),
    key_version            BIGINT UNSIGNED NOT NULL DEFAULT 1,
    config_version         BIGINT UNSIGNED NOT NULL DEFAULT 0,
    read_timeout_ms        INT UNSIGNED,
    applied_revision      BIGINT UNSIGNED NOT NULL DEFAULT 0,
    outbox_backlog        BIGINT UNSIGNED NOT NULL DEFAULT 0,
    outbox_database_bytes BIGINT UNSIGNED NOT NULL DEFAULT 0,
    outbox_disk_used_percent DECIMAL(5,2) NOT NULL DEFAULT 0,
    outbox_watermark      VARCHAR(16) NOT NULL DEFAULT 'normal',
    outbox_dropped_records BIGINT UNSIGNED NOT NULL DEFAULT 0,
    collector_status      VARCHAR(16) NOT NULL DEFAULT 'unknown',
    storage_status        VARCHAR(16) NOT NULL DEFAULT 'unknown',
    uploader_status       VARCHAR(16) NOT NULL DEFAULT 'unknown',
    storage_queue_depth   BIGINT UNSIGNED NOT NULL DEFAULT 0,
    storage_commit_p99_ms DECIMAL(10,3) NOT NULL DEFAULT 0,
    storage_errors_total  BIGINT UNSIGNED NOT NULL DEFAULT 0,
    edge_time_ms          BIGINT,
    clock_skew_ms         BIGINT,
    clock_anomaly         TINYINT(1) NOT NULL DEFAULT 0,
    last_error            VARCHAR(1000),
    last_seen_at           DATETIME(3),
    created_at             DATETIME(3)    NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at             DATETIME(3)    NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                           ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (node_uuid),
    KEY idx_sys_node_status (status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Distributed edge node identity';

CREATE TABLE IF NOT EXISTS node_registration_code (
    code_hash          CHAR(64)      NOT NULL,
    node_name          VARCHAR(128)  NOT NULL,
    expires_at         DATETIME(3)   NOT NULL,
    used_at            DATETIME(3),
    used_by_node_uuid  CHAR(36),
    created_by         VARCHAR(64)   NOT NULL,
    created_at         DATETIME(3)   NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    PRIMARY KEY (code_hash),
    KEY idx_node_registration_expiry (expires_at, used_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='One-time edge registration codes';
CREATE TABLE IF NOT EXISTS device_assignment (
    device_id          BIGINT          NOT NULL,
    node_uuid          CHAR(36)        NOT NULL,
    assignment_epoch   BIGINT UNSIGNED NOT NULL,
    effective_at       DATETIME(3)     NOT NULL,
    cutover_at         DATETIME(3),
    grace_until        DATETIME(3),
    status             VARCHAR(16)     NOT NULL DEFAULT 'pending',
    created_at         DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at         DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                       ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (device_id, assignment_epoch),
    KEY idx_assignment_node_status (node_uuid, status),
    KEY idx_assignment_device_cutover (device_id, cutover_at),
    CONSTRAINT fk_assignment_device FOREIGN KEY (device_id) REFERENCES device(id),
    CONSTRAINT fk_assignment_node FOREIGN KEY (node_uuid) REFERENCES sys_node(node_uuid)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Device ownership history and migration epoch';

CREATE TABLE IF NOT EXISTS point_storage_mapping (
    point_id          BIGINT          NOT NULL,
    device_code       VARCHAR(64)     NOT NULL,
    device_type_code  VARCHAR(32)     NOT NULL,
    stable_name       VARCHAR(64)     NOT NULL,
    child_table       VARCHAR(64)     NOT NULL,
    group_code        VARCHAR(64)     NOT NULL,
    td_column_name    VARCHAR(32)     NOT NULL,
    var_type          VARCHAR(32)     NOT NULL,
    created_at        DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    PRIMARY KEY (point_id),
    UNIQUE KEY uq_mapping_device_col (device_code, td_column_name),
    KEY idx_mapping_decode (device_code, group_code, point_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Immutable point to TDengine physical mapping';

CREATE TABLE IF NOT EXISTS point_storage_mapping_staging (
    point_id          BIGINT          NOT NULL,
    device_code       VARCHAR(64)     NOT NULL,
    device_type_code  VARCHAR(32)     NOT NULL,
    stable_name       VARCHAR(64)     NOT NULL,
    child_table       VARCHAR(64)     NOT NULL,
    group_code        VARCHAR(64)     NOT NULL,
    td_column_name    VARCHAR(32)     NOT NULL,
    var_type          VARCHAR(32)     NOT NULL,
    created_at        DATETIME(3)     NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Mapping backfill validation staging';
