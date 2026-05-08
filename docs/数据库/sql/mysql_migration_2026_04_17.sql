-- MySQL migration: align the first-batch live schema with the confirmed
-- device/device_variable relationship and constraint strategy.
-- Target database: device_watch
-- Date: 2026-04-17
--
-- This script is designed to be re-runnable:
-- 1. column MODIFY statements can be executed repeatedly
-- 2. indexes/constraints are added only when missing
-- 3. old unique indexes named `id` are dropped only when present

USE `device_watch`;

SET @schema_name = DATABASE();

-- ---------------------------------------------------------------------
-- 1) Drop legacy unique indexes named `id` if they still exist
-- ---------------------------------------------------------------------

SELECT IF(
    EXISTS (
        SELECT 1
        FROM information_schema.statistics
        WHERE table_schema = @schema_name
          AND table_name = 'device_type'
          AND index_name = 'id'
    ),
    'ALTER TABLE `device_type` DROP INDEX `id`',
    'SELECT ''skip drop device_type.id'' AS message'
) INTO @sql;
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT IF(
    EXISTS (
        SELECT 1
        FROM information_schema.statistics
        WHERE table_schema = @schema_name
          AND table_name = 'device_group'
          AND index_name = 'id'
    ),
    'ALTER TABLE `device_group` DROP INDEX `id`',
    'SELECT ''skip drop device_group.id'' AS message'
) INTO @sql;
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT IF(
    EXISTS (
        SELECT 1
        FROM information_schema.statistics
        WHERE table_schema = @schema_name
          AND table_name = 'comm_protocol'
          AND index_name = 'id'
    ),
    'ALTER TABLE `comm_protocol` DROP INDEX `id`',
    'SELECT ''skip drop comm_protocol.id'' AS message'
) INTO @sql;
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT IF(
    EXISTS (
        SELECT 1
        FROM information_schema.statistics
        WHERE table_schema = @schema_name
          AND table_name = 'device'
          AND index_name = 'id'
    ),
    'ALTER TABLE `device` DROP INDEX `id`',
    'SELECT ''skip drop device.id'' AS message'
) INTO @sql;
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT IF(
    EXISTS (
        SELECT 1
        FROM information_schema.statistics
        WHERE table_schema = @schema_name
          AND table_name = 'device_variable'
          AND index_name = 'id'
    ),
    'ALTER TABLE `device_variable` DROP INDEX `id`',
    'SELECT ''skip drop device_variable.id'' AS message'
) INTO @sql;
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ---------------------------------------------------------------------
-- 2) Align column definitions
-- ---------------------------------------------------------------------

ALTER TABLE `comm_protocol`
    MODIFY COLUMN `create_time` DATETIME NOT NULL COMMENT 'create time';

ALTER TABLE `device`
    MODIFY COLUMN `type_id` BIGINT NOT NULL COMMENT 'device type id references device_type.id',
    MODIFY COLUMN `group_id` BIGINT NOT NULL COMMENT 'device group id references device_group.id',
    MODIFY COLUMN `protocol_id` BIGINT NOT NULL COMMENT 'protocol id references comm_protocol.id';

ALTER TABLE `device_variable`
    MODIFY COLUMN `device_id` BIGINT NOT NULL COMMENT 'device id references device.id',
    MODIFY COLUMN `io_type` VARCHAR(10) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'memory area type such as I M Q DB R',
    MODIFY COLUMN `var_type` VARCHAR(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'data type such as BOOL INT DINT REAL DOUBLE';

-- ---------------------------------------------------------------------
-- 3) Add confirmed unique constraints
-- ---------------------------------------------------------------------

SELECT IF(
    EXISTS (
        SELECT 1
        FROM information_schema.table_constraints
        WHERE table_schema = @schema_name
          AND table_name = 'device_type'
          AND constraint_name = 'uq_device_type_type_code'
          AND constraint_type = 'UNIQUE'
    ),
    'SELECT ''skip uq_device_type_type_code'' AS message',
    'ALTER TABLE `device_type` ADD CONSTRAINT `uq_device_type_type_code` UNIQUE (`type_code`)'
) INTO @sql;
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT IF(
    EXISTS (
        SELECT 1
        FROM information_schema.table_constraints
        WHERE table_schema = @schema_name
          AND table_name = 'device_group'
          AND constraint_name = 'uq_device_group_group_code'
          AND constraint_type = 'UNIQUE'
    ),
    'SELECT ''skip uq_device_group_group_code'' AS message',
    'ALTER TABLE `device_group` ADD CONSTRAINT `uq_device_group_group_code` UNIQUE (`group_code`)'
) INTO @sql;
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT IF(
    EXISTS (
        SELECT 1
        FROM information_schema.table_constraints
        WHERE table_schema = @schema_name
          AND table_name = 'comm_protocol'
          AND constraint_name = 'uq_comm_protocol_protocol'
          AND constraint_type = 'UNIQUE'
    ),
    'SELECT ''skip uq_comm_protocol_protocol'' AS message',
    'ALTER TABLE `comm_protocol` ADD CONSTRAINT `uq_comm_protocol_protocol` UNIQUE (`protocol`)'
) INTO @sql;
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT IF(
    EXISTS (
        SELECT 1
        FROM information_schema.table_constraints
        WHERE table_schema = @schema_name
          AND table_name = 'device'
          AND constraint_name = 'uq_device_device_code'
          AND constraint_type = 'UNIQUE'
    ),
    'SELECT ''skip uq_device_device_code'' AS message',
    'ALTER TABLE `device` ADD CONSTRAINT `uq_device_device_code` UNIQUE (`device_code`)'
) INTO @sql;
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT IF(
    EXISTS (
        SELECT 1
        FROM information_schema.table_constraints
        WHERE table_schema = @schema_name
          AND table_name = 'device_variable'
          AND constraint_name = 'uq_device_variable_device_var_seq'
          AND constraint_type = 'UNIQUE'
    ) OR NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = @schema_name
          AND table_name = 'device_variable'
          AND column_name = 'var_seq'
    ),
    'SELECT ''skip uq_device_variable_device_var_seq'' AS message',
    'ALTER TABLE `device_variable` ADD CONSTRAINT `uq_device_variable_device_var_seq` UNIQUE (`device_id`, `var_seq`)'
) INTO @sql;
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT IF(
    EXISTS (
        SELECT 1
        FROM information_schema.table_constraints
        WHERE table_schema = @schema_name
          AND table_name = 'device_variable'
          AND constraint_name = 'uq_device_variable_device_var_address'
          AND constraint_type = 'UNIQUE'
    ),
    'SELECT ''skip uq_device_variable_device_var_address'' AS message',
    'ALTER TABLE `device_variable` ADD CONSTRAINT `uq_device_variable_device_var_address` UNIQUE (`device_id`, `var_address`)'
) INTO @sql;
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ---------------------------------------------------------------------
-- 4) Add missing secondary indexes
-- ---------------------------------------------------------------------

SELECT IF(
    EXISTS (
        SELECT 1
        FROM information_schema.statistics
        WHERE table_schema = @schema_name
          AND table_name = 'device_variable'
          AND index_name = 'idx_device_variable_device_id'
    ),
    'SELECT ''skip idx_device_variable_device_id'' AS message',
    'ALTER TABLE `device_variable` ADD INDEX `idx_device_variable_device_id` (`device_id`)'
) INTO @sql;
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT IF(
    EXISTS (
        SELECT 1
        FROM information_schema.statistics
        WHERE table_schema = @schema_name
          AND table_name = 'device_variable'
          AND index_name = 'idx_device_variable_scan_rate'
    ),
    'SELECT ''skip idx_device_variable_scan_rate'' AS message',
    'ALTER TABLE `device_variable` ADD INDEX `idx_device_variable_scan_rate` (`scan_rate`)'
) INTO @sql;
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT IF(
    EXISTS (
        SELECT 1
        FROM information_schema.statistics
        WHERE table_schema = @schema_name
          AND table_name = 'device_variable'
          AND index_name = 'idx_device_variable_is_alarm'
    ),
    'SELECT ''skip idx_device_variable_is_alarm'' AS message',
    'ALTER TABLE `device_variable` ADD INDEX `idx_device_variable_is_alarm` (`is_alarm`)'
) INTO @sql;
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ---------------------------------------------------------------------
-- 5) Add foreign key for device_variable.device_id if missing
-- ---------------------------------------------------------------------

SELECT IF(
    EXISTS (
        SELECT 1
        FROM information_schema.table_constraints
        WHERE table_schema = @schema_name
          AND table_name = 'device_variable'
          AND constraint_name = 'fk_device_variable_device'
          AND constraint_type = 'FOREIGN KEY'
    ),
    'SELECT ''skip fk_device_variable_device'' AS message',
    'ALTER TABLE `device_variable` ADD CONSTRAINT `fk_device_variable_device` FOREIGN KEY (`device_id`) REFERENCES `device` (`id`)'
) INTO @sql;
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ---------------------------------------------------------------------
-- 6) Verification snapshot
-- ---------------------------------------------------------------------

SELECT TABLE_NAME, CONSTRAINT_NAME, CONSTRAINT_TYPE
FROM information_schema.table_constraints
WHERE table_schema = @schema_name
  AND table_name IN ('device_type', 'device_group', 'comm_protocol', 'device', 'device_variable')
ORDER BY TABLE_NAME, CONSTRAINT_TYPE, CONSTRAINT_NAME;
