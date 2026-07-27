-- DB-5: 外置驱动生命周期表（installed_driver / driver_operation）
-- 生产环境由运维执行；运行时 Backend 仅做 DML，不再 CREATE/ALTER。
--
-- 安全原则：
--   1) 旧表补 driver_id 时先加可空列，禁止 DEFAULT '' 批量灌空串。
--   2) 不自动猜测、不静默删除重复；空值/重复时由临时存储过程原生 SIGNAL 中止
--      （禁止 PREPARE SIGNAL；需 mysql 客户端执行含 DELIMITER 的段落）。
--   3) 确认无 NULL/空串/重复后，再改为 NOT NULL 并加 UNIQUE。
--   4) 可重复执行（information_schema 条件分支）。
--
-- 人工回填示例（中止后按需执行，再重跑本脚本）：
--   UPDATE installed_driver SET driver_id = '<Manifest.id>' WHERE protocol = '<protocol>';

SET @db := DATABASE();

-- ---------------------------------------------------------------------------
-- 1) 建表（仅新库；已有表不会被 CREATE IF NOT EXISTS 改结构）
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS installed_driver (
    protocol       VARCHAR(100) NOT NULL COMMENT 'comm_protocol.protocol',
    driver_id      VARCHAR(100) NOT NULL COMMENT 'Manifest.id，Agent 安装目录名',
    driver_version VARCHAR(50)  NULL,
    driver_sha256  CHAR(64)     NULL,
    install_source VARCHAR(20)  NOT NULL DEFAULT 'zip',
    lifecycle_status ENUM('active', 'uninstalling') NOT NULL DEFAULT 'active',
    lifecycle_operation_id CHAR(36) NULL,
    updated_at     DATETIME     NOT NULL,
    PRIMARY KEY (protocol),
    UNIQUE KEY uk_installed_driver_id (driver_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='外置驱动 protocol↔driver_id 映射';

CREATE TABLE IF NOT EXISTS driver_operation (
    operation_id   CHAR(36)     NOT NULL,
    driver_id      VARCHAR(100) NOT NULL,
    operation_type ENUM('install', 'upgrade', 'uninstall') NOT NULL,
    target_version VARCHAR(50)  NULL,
    agent_phase    ENUM('pending', 'success', 'failed', 'unknown') NOT NULL,
    db_phase       ENUM('pending', 'committed', 'failed') NOT NULL,
    created_at     DATETIME     NOT NULL,
    updated_at     DATETIME     NOT NULL,
    PRIMARY KEY (operation_id),
    KEY idx_pending (agent_phase, db_phase)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='驱动安装/卸载跨进程补偿状态机';

-- ---------------------------------------------------------------------------
-- 2) 旧表补齐列：driver_id 必须先可空（禁止 DEFAULT ''）
-- ---------------------------------------------------------------------------
SET @exists := (
    SELECT COUNT(1) FROM information_schema.columns
    WHERE table_schema = @db AND table_name = 'installed_driver' AND column_name = 'driver_id'
);
SET @sql := IF(@exists = 0,
    'ALTER TABLE installed_driver ADD COLUMN driver_id VARCHAR(100) NULL COMMENT ''Manifest.id，待人工/可靠来源回填'' AFTER protocol',
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @exists := (
    SELECT COUNT(1) FROM information_schema.columns
    WHERE table_schema = @db AND table_name = 'installed_driver' AND column_name = 'driver_version'
);
SET @sql := IF(@exists = 0,
    'ALTER TABLE installed_driver ADD COLUMN driver_version VARCHAR(50) NULL AFTER driver_id',
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @exists := (
    SELECT COUNT(1) FROM information_schema.columns
    WHERE table_schema = @db AND table_name = 'installed_driver' AND column_name = 'driver_sha256'
);
SET @sql := IF(@exists = 0,
    'ALTER TABLE installed_driver ADD COLUMN driver_sha256 CHAR(64) NULL AFTER driver_version',
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @exists := (
    SELECT COUNT(1) FROM information_schema.columns
    WHERE table_schema = @db AND table_name = 'installed_driver' AND column_name = 'install_source'
);
SET @sql := IF(@exists = 0,
    'ALTER TABLE installed_driver ADD COLUMN install_source VARCHAR(20) NOT NULL DEFAULT ''zip'' AFTER driver_sha256',
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @exists := (
    SELECT COUNT(1) FROM information_schema.columns
    WHERE table_schema = @db AND table_name = 'installed_driver' AND column_name = 'lifecycle_status'
);
SET @sql := IF(@exists = 0,
    'ALTER TABLE installed_driver ADD COLUMN lifecycle_status ENUM(''active'', ''uninstalling'') NOT NULL DEFAULT ''active'' AFTER install_source',
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @exists := (
    SELECT COUNT(1) FROM information_schema.columns
    WHERE table_schema = @db AND table_name = 'installed_driver' AND column_name = 'lifecycle_operation_id'
);
SET @sql := IF(@exists = 0,
    'ALTER TABLE installed_driver ADD COLUMN lifecycle_operation_id CHAR(36) NULL AFTER lifecycle_status',
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @exists := (
    SELECT COUNT(1) FROM information_schema.columns
    WHERE table_schema = @db AND table_name = 'installed_driver' AND column_name = 'updated_at'
);
SET @sql := IF(@exists = 0,
    'ALTER TABLE installed_driver ADD COLUMN updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP AFTER install_source',
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ---------------------------------------------------------------------------
-- 3) 待回填清单（供运维查看；不自动猜测）
--    含 NULL / 空串 / 首尾空白（与 Backend .strip() 语义对齐）
-- ---------------------------------------------------------------------------
SELECT protocol, driver_id, driver_version, install_source, updated_at
FROM installed_driver
WHERE driver_id IS NULL
   OR TRIM(driver_id) = ''
   OR driver_id <> TRIM(driver_id)
ORDER BY protocol;

-- ---------------------------------------------------------------------------
-- 4) 门禁：空值、空白、按 strip 后重复则中止（不静默删除）
-- ---------------------------------------------------------------------------
SET @null_or_empty := (
    SELECT COUNT(1) FROM installed_driver
    WHERE driver_id IS NULL
       OR TRIM(driver_id) = ''
       OR driver_id <> TRIM(driver_id)
);
-- 重复按应用侧 strip 规则：TRIM(driver_id)
SET @dup_driver_id := (
    SELECT COUNT(1) FROM (
        SELECT TRIM(driver_id) AS did
        FROM installed_driver
        WHERE driver_id IS NOT NULL AND TRIM(driver_id) <> ''
        GROUP BY TRIM(driver_id)
        HAVING COUNT(1) > 1
    ) d
);

-- SIGNAL 不能作为 prepared statement（MySQL/MariaDB 均不支持）。
-- 用临时存储过程执行原生 SIGNAL；需 mysql 客户端支持 DELIMITER。
DROP PROCEDURE IF EXISTS assert_db5_driver_data;
DELIMITER //
CREATE PROCEDURE assert_db5_driver_data()
BEGIN
    IF IFNULL(@null_or_empty, 0) > 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'db5 abort: installed_driver.driver_id has NULL/empty/untrimmed rows; backfill manually then re-run';
    END IF;
    IF IFNULL(@dup_driver_id, 0) > 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'db5 abort: duplicate driver_id after TRIM; resolve manually then re-run';
    END IF;
END//
DELIMITER ;
CALL assert_db5_driver_data();
DROP PROCEDURE IF EXISTS assert_db5_driver_data;

-- ---------------------------------------------------------------------------
-- 5) 确认干净后：NOT NULL + 正确 UNIQUE(driver_id)
-- ---------------------------------------------------------------------------
SET @nullable := (
    SELECT COUNT(1) FROM information_schema.columns
    WHERE table_schema = @db
      AND table_name = 'installed_driver'
      AND column_name = 'driver_id'
      AND IS_NULLABLE = 'YES'
);
SET @sql := IF(@nullable > 0,
    'ALTER TABLE installed_driver MODIFY COLUMN driver_id VARCHAR(100) NOT NULL COMMENT ''Manifest.id，Agent 安装目录名''',
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 有效唯一索引：同名 + NON_UNIQUE=0 + 仅一列 driver_id 且 SEQ=1
SET @uk_ok := (
    SELECT COUNT(1) FROM information_schema.statistics
    WHERE table_schema = @db
      AND table_name = 'installed_driver'
      AND index_name = 'uk_installed_driver_id'
      AND NON_UNIQUE = 0
      AND SEQ_IN_INDEX = 1
      AND COLUMN_NAME = 'driver_id'
      AND NOT EXISTS (
          SELECT 1 FROM information_schema.statistics s2
          WHERE s2.table_schema = @db
            AND s2.table_name = 'installed_driver'
            AND s2.index_name = 'uk_installed_driver_id'
            AND s2.SEQ_IN_INDEX > 1
      )
);
-- 同名但定义错误：先 DROP 再重建
SET @uk_name_exists := (
    SELECT COUNT(1) FROM information_schema.statistics
    WHERE table_schema = @db
      AND table_name = 'installed_driver'
      AND index_name = 'uk_installed_driver_id'
);
SET @sql := IF(@uk_name_exists > 0 AND @uk_ok = 0,
    'ALTER TABLE installed_driver DROP INDEX uk_installed_driver_id',
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @uk_ok := (
    SELECT COUNT(1) FROM information_schema.statistics
    WHERE table_schema = @db
      AND table_name = 'installed_driver'
      AND index_name = 'uk_installed_driver_id'
      AND NON_UNIQUE = 0
      AND SEQ_IN_INDEX = 1
      AND COLUMN_NAME = 'driver_id'
);
SET @sql := IF(@uk_ok = 0,
    'ALTER TABLE installed_driver ADD UNIQUE KEY uk_installed_driver_id (driver_id)',
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ---------------------------------------------------------------------------
-- 6) driver_operation：补 unknown 枚举与正确 idx_pending(agent_phase, db_phase)
-- ---------------------------------------------------------------------------
SET @has_unknown := (
    SELECT COUNT(1) FROM information_schema.columns
    WHERE table_schema = @db
      AND table_name = 'driver_operation'
      AND column_name = 'agent_phase'
      AND COLUMN_TYPE LIKE '%unknown%'
);
SET @sql := IF(@has_unknown = 0,
    'ALTER TABLE driver_operation MODIFY COLUMN agent_phase ENUM(''pending'', ''success'', ''failed'', ''unknown'') NOT NULL',
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- idx_pending 必须：普通非唯一索引 + 列序 agent_phase(seq1), db_phase(seq2)
-- （若误建成 UNIQUE，会阻止同阶段组合的第二条 operation）
SET @idx_pending_ok := (
    SELECT CASE
        WHEN EXISTS (
            SELECT 1 FROM information_schema.statistics
            WHERE table_schema = @db AND table_name = 'driver_operation'
              AND index_name = 'idx_pending' AND NON_UNIQUE = 1
              AND SEQ_IN_INDEX = 1 AND COLUMN_NAME = 'agent_phase'
        ) AND EXISTS (
            SELECT 1 FROM information_schema.statistics
            WHERE table_schema = @db AND table_name = 'driver_operation'
              AND index_name = 'idx_pending' AND NON_UNIQUE = 1
              AND SEQ_IN_INDEX = 2 AND COLUMN_NAME = 'db_phase'
        ) AND NOT EXISTS (
            SELECT 1 FROM information_schema.statistics
            WHERE table_schema = @db AND table_name = 'driver_operation'
              AND index_name = 'idx_pending' AND SEQ_IN_INDEX > 2
        )
        THEN 1 ELSE 0 END
);
SET @idx_pending_name := (
    SELECT COUNT(1) FROM information_schema.statistics
    WHERE table_schema = @db
      AND table_name = 'driver_operation'
      AND index_name = 'idx_pending'
);
SET @sql := IF(@idx_pending_name > 0 AND @idx_pending_ok = 0,
    'ALTER TABLE driver_operation DROP INDEX idx_pending',
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @idx_pending_ok := (
    SELECT CASE
        WHEN EXISTS (
            SELECT 1 FROM information_schema.statistics
            WHERE table_schema = @db AND table_name = 'driver_operation'
              AND index_name = 'idx_pending' AND NON_UNIQUE = 1
              AND SEQ_IN_INDEX = 1 AND COLUMN_NAME = 'agent_phase'
        ) AND EXISTS (
            SELECT 1 FROM information_schema.statistics
            WHERE table_schema = @db AND table_name = 'driver_operation'
              AND index_name = 'idx_pending' AND NON_UNIQUE = 1
              AND SEQ_IN_INDEX = 2 AND COLUMN_NAME = 'db_phase'
        ) AND NOT EXISTS (
            SELECT 1 FROM information_schema.statistics
            WHERE table_schema = @db AND table_name = 'driver_operation'
              AND index_name = 'idx_pending' AND SEQ_IN_INDEX > 2
        )
        THEN 1 ELSE 0 END
);
SET @sql := IF(@idx_pending_ok = 0,
    'ALTER TABLE driver_operation ADD KEY idx_pending (agent_phase, db_phase)',
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ---------------------------------------------------------------------------
-- 7) 迁移后断言（期望前八列均为 1；pending_* 均为 0）
-- ---------------------------------------------------------------------------
SELECT
    (SELECT COUNT(1) FROM information_schema.tables
     WHERE table_schema = @db AND table_name = 'installed_driver') AS has_installed_driver,
    (SELECT COUNT(1) FROM information_schema.tables
     WHERE table_schema = @db AND table_name = 'driver_operation') AS has_driver_operation,
    (SELECT COUNT(1) FROM information_schema.columns
     WHERE table_schema = @db AND table_name = 'installed_driver'
       AND column_name = 'driver_id' AND IS_NULLABLE = 'NO') AS has_driver_id_not_null,
    (SELECT COUNT(1) FROM information_schema.columns
     WHERE table_schema = @db AND table_name = 'installed_driver'
       AND column_name = 'lifecycle_status'
       AND COLUMN_TYPE LIKE '%uninstalling%') AS has_lifecycle_status,
    (SELECT COUNT(1) FROM information_schema.columns
     WHERE table_schema = @db AND table_name = 'installed_driver'
       AND column_name = 'lifecycle_operation_id') AS has_lifecycle_operation_id,
    (SELECT CASE
        WHEN EXISTS (
            SELECT 1 FROM information_schema.statistics
            WHERE table_schema = @db AND table_name = 'installed_driver'
              AND index_name = 'uk_installed_driver_id'
              AND NON_UNIQUE = 0 AND SEQ_IN_INDEX = 1 AND COLUMN_NAME = 'driver_id'
        ) THEN 1 ELSE 0 END
    ) AS has_uk_driver_id,
    (SELECT COUNT(1) FROM information_schema.columns
     WHERE table_schema = @db AND table_name = 'driver_operation'
       AND column_name = 'agent_phase' AND COLUMN_TYPE LIKE '%unknown%') AS has_unknown_enum,
    (SELECT CASE
        WHEN EXISTS (
            SELECT 1 FROM information_schema.statistics
            WHERE table_schema = @db AND table_name = 'driver_operation'
              AND index_name = 'idx_pending' AND NON_UNIQUE = 1
              AND SEQ_IN_INDEX = 1 AND COLUMN_NAME = 'agent_phase'
        ) AND EXISTS (
            SELECT 1 FROM information_schema.statistics
            WHERE table_schema = @db AND table_name = 'driver_operation'
              AND index_name = 'idx_pending' AND NON_UNIQUE = 1
              AND SEQ_IN_INDEX = 2 AND COLUMN_NAME = 'db_phase'
        ) AND NOT EXISTS (
            SELECT 1 FROM information_schema.statistics
            WHERE table_schema = @db AND table_name = 'driver_operation'
              AND index_name = 'idx_pending' AND SEQ_IN_INDEX > 2
        ) THEN 1 ELSE 0 END
    ) AS has_idx_pending,
    (SELECT COUNT(1) FROM installed_driver
     WHERE driver_id IS NULL
        OR TRIM(driver_id) = ''
        OR driver_id <> TRIM(driver_id)) AS pending_empty_driver_id,
    (SELECT COUNT(1) FROM (
         SELECT TRIM(driver_id) AS did FROM installed_driver
         WHERE driver_id IS NOT NULL AND TRIM(driver_id) <> ''
         GROUP BY TRIM(driver_id) HAVING COUNT(1) > 1
     ) d) AS pending_dup_driver_id;
