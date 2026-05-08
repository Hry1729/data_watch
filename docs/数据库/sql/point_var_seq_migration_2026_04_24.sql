USE `device_watch`;

SET NAMES utf8mb4;
SET @schema_name = DATABASE();

-- ---------------------------------------------------------------------
-- 1) device_variable: backfill canonical var_seq from legacy var_code
-- ---------------------------------------------------------------------

SELECT EXISTS (
    SELECT 1
    FROM information_schema.columns
    WHERE table_schema = @schema_name
      AND table_name = 'device_variable'
      AND column_name = 'var_seq'
) INTO @device_has_var_seq;

SELECT EXISTS (
    SELECT 1
    FROM information_schema.columns
    WHERE table_schema = @schema_name
      AND table_name = 'device_variable'
      AND column_name = 'var_code'
) INTO @device_has_var_code;

SELECT IF(
    @device_has_var_seq = 1 AND @device_has_var_code = 1,
    "UPDATE `device_variable` SET `var_seq` = CAST(REGEXP_SUBSTR(`var_code`, '[0-9]+$') AS UNSIGNED) WHERE (`var_seq` IS NULL OR `var_seq` = 0) AND `var_code` IS NOT NULL AND TRIM(`var_code`) <> '' AND REGEXP_SUBSTR(`var_code`, '[0-9]+$') IS NOT NULL",
    "SELECT 'skip device_variable var_seq backfill' AS message"
) INTO @sql;
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT IF(
    @device_has_var_code = 1,
    (
        SELECT COUNT(*)
        FROM `device_variable`
        WHERE `var_seq` IS NULL OR `var_seq` = 0
    ),
    0
) INTO @device_unresolved;

SELECT CONCAT('device_variable unresolved rows: ', @device_unresolved) AS message;

SELECT IF(
    @device_unresolved > 0,
    "SELECT `id`, `device_id`, `var_code` FROM `device_variable` WHERE `var_seq` IS NULL OR `var_seq` = 0 ORDER BY `id` LIMIT 50",
    "SELECT 'skip device_variable unresolved detail' AS message"
) INTO @sql;
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT COUNT(*) INTO @device_duplicate_count
FROM (
    SELECT `device_id`, `var_seq`
    FROM `device_variable`
    WHERE `var_seq` IS NOT NULL AND `var_seq` <> 0
    GROUP BY `device_id`, `var_seq`
    HAVING COUNT(*) > 1
) AS duplicate_rows;

SELECT CONCAT('device_variable duplicate (device_id, var_seq) groups: ', @device_duplicate_count) AS message;

SELECT IF(
    @device_duplicate_count > 0,
    "SELECT `device_id`, `var_seq`, COUNT(*) AS `duplicate_count` FROM `device_variable` WHERE `var_seq` IS NOT NULL AND `var_seq` <> 0 GROUP BY `device_id`, `var_seq` HAVING COUNT(*) > 1 ORDER BY `device_id`, `var_seq` LIMIT 50",
    "SELECT 'skip device_variable duplicate detail' AS message"
) INTO @sql;
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT IF(
    @device_has_var_seq = 1 AND @device_unresolved = 0 AND @device_duplicate_count = 0,
    "ALTER TABLE `device_variable` MODIFY COLUMN `var_seq` INT NOT NULL COMMENT '点位序号，纯序号；TDengine 子表名由 device_code + ''_'' + var_seq 派生'",
    "SELECT 'skip device_variable.var_seq modify not null' AS message"
) INTO @sql;
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT IF(
    EXISTS (
        SELECT 1
        FROM information_schema.statistics
        WHERE table_schema = @schema_name
          AND table_name = 'device_variable'
          AND index_name = 'uq_device_variable_device_var_code'
    ) AND @device_unresolved = 0 AND @device_duplicate_count = 0,
    "ALTER TABLE `device_variable` DROP INDEX `uq_device_variable_device_var_code`",
    "SELECT 'skip drop uq_device_variable_device_var_code' AS message"
) INTO @sql;
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT IF(
    EXISTS (
        SELECT 1
        FROM information_schema.statistics
        WHERE table_schema = @schema_name
          AND table_name = 'device_variable'
          AND index_name = 'idx_device_variable_var_seq'
    ) AND NOT EXISTS (
        SELECT 1
        FROM information_schema.statistics
        WHERE table_schema = @schema_name
          AND table_name = 'device_variable'
          AND index_name = 'uq_device_variable_device_var_seq'
    ) AND @device_unresolved = 0 AND @device_duplicate_count = 0,
    "ALTER TABLE `device_variable` DROP INDEX `idx_device_variable_var_seq`",
    "SELECT 'skip drop idx_device_variable_var_seq' AS message"
) INTO @sql;
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT IF(
    NOT EXISTS (
        SELECT 1
        FROM information_schema.statistics
        WHERE table_schema = @schema_name
          AND table_name = 'device_variable'
          AND index_name = 'uq_device_variable_device_var_seq'
    ) AND @device_has_var_seq = 1 AND @device_unresolved = 0 AND @device_duplicate_count = 0,
    "ALTER TABLE `device_variable` ADD CONSTRAINT `uq_device_variable_device_var_seq` UNIQUE (`device_id`, `var_seq`)",
    "SELECT 'skip add uq_device_variable_device_var_seq' AS message"
) INTO @sql;
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT IF(
    @device_has_var_code = 1 AND @device_unresolved = 0 AND @device_duplicate_count = 0,
    "ALTER TABLE `device_variable` DROP COLUMN `var_code`",
    "SELECT 'skip drop device_variable.var_code' AS message"
) INTO @sql;
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ---------------------------------------------------------------------
-- 2) template_variable: backfill canonical var_seq from legacy var_code
-- ---------------------------------------------------------------------

SELECT EXISTS (
    SELECT 1
    FROM information_schema.tables
    WHERE table_schema = @schema_name
      AND table_name = 'template_variable'
) INTO @template_exists;

SELECT EXISTS (
    SELECT 1
    FROM information_schema.columns
    WHERE table_schema = @schema_name
      AND table_name = 'template_variable'
      AND column_name = 'var_seq'
) INTO @template_has_var_seq;

SELECT EXISTS (
    SELECT 1
    FROM information_schema.columns
    WHERE table_schema = @schema_name
      AND table_name = 'template_variable'
      AND column_name = 'var_code'
) INTO @template_has_var_code;

SELECT IF(
    @template_exists = 1 AND @template_has_var_seq = 1 AND @template_has_var_code = 1,
    "UPDATE `template_variable` SET `var_seq` = CAST(REGEXP_SUBSTR(`var_code`, '[0-9]+$') AS UNSIGNED) WHERE (`var_seq` IS NULL OR `var_seq` = 0) AND `var_code` IS NOT NULL AND TRIM(`var_code`) <> '' AND REGEXP_SUBSTR(`var_code`, '[0-9]+$') IS NOT NULL",
    "SELECT 'skip template_variable var_seq backfill' AS message"
) INTO @sql;
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT IF(
    @template_exists = 1 AND @template_has_var_code = 1,
    (
        SELECT COUNT(*)
        FROM `template_variable`
        WHERE `var_seq` IS NULL OR `var_seq` = 0
    ),
    0
) INTO @template_unresolved;

SELECT CONCAT('template_variable unresolved rows: ', @template_unresolved) AS message;

SELECT IF(
    @template_unresolved > 0,
    "SELECT `id`, `template_id`, `var_code` FROM `template_variable` WHERE `var_seq` IS NULL OR `var_seq` = 0 ORDER BY `id` LIMIT 50",
    "SELECT 'skip template_variable unresolved detail' AS message"
) INTO @sql;
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT IF(
    @template_exists = 1,
    (
        SELECT COUNT(*)
        FROM (
            SELECT `template_id`, `var_seq`
            FROM `template_variable`
            WHERE `var_seq` IS NOT NULL AND `var_seq` <> 0
            GROUP BY `template_id`, `var_seq`
            HAVING COUNT(*) > 1
        ) AS duplicate_rows
    ),
    0
) INTO @template_duplicate_count;

SELECT CONCAT('template_variable duplicate (template_id, var_seq) groups: ', @template_duplicate_count) AS message;

SELECT IF(
    @template_duplicate_count > 0,
    "SELECT `template_id`, `var_seq`, COUNT(*) AS `duplicate_count` FROM `template_variable` WHERE `var_seq` IS NOT NULL AND `var_seq` <> 0 GROUP BY `template_id`, `var_seq` HAVING COUNT(*) > 1 ORDER BY `template_id`, `var_seq` LIMIT 50",
    "SELECT 'skip template_variable duplicate detail' AS message"
) INTO @sql;
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT IF(
    @template_exists = 1 AND @template_has_var_seq = 1 AND @template_unresolved = 0 AND @template_duplicate_count = 0,
    "ALTER TABLE `template_variable` MODIFY COLUMN `var_seq` INT NOT NULL COMMENT '点位序号，纯序号；模板复制到设备后保持同一业务序号'",
    "SELECT 'skip template_variable.var_seq modify not null' AS message"
) INTO @sql;
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT IF(
    EXISTS (
        SELECT 1
        FROM information_schema.statistics
        WHERE table_schema = @schema_name
          AND table_name = 'template_variable'
          AND index_name = 'uq_template_variable_template_var_code'
    ) AND @template_unresolved = 0 AND @template_duplicate_count = 0,
    "ALTER TABLE `template_variable` DROP INDEX `uq_template_variable_template_var_code`",
    "SELECT 'skip drop uq_template_variable_template_var_code' AS message"
) INTO @sql;
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT IF(
    EXISTS (
        SELECT 1
        FROM information_schema.statistics
        WHERE table_schema = @schema_name
          AND table_name = 'template_variable'
          AND index_name = 'idx_template_variable_var_seq'
    ) AND NOT EXISTS (
        SELECT 1
        FROM information_schema.statistics
        WHERE table_schema = @schema_name
          AND table_name = 'template_variable'
          AND index_name = 'uq_template_variable_template_var_seq'
    ) AND @template_unresolved = 0 AND @template_duplicate_count = 0,
    "ALTER TABLE `template_variable` DROP INDEX `idx_template_variable_var_seq`",
    "SELECT 'skip drop idx_template_variable_var_seq' AS message"
) INTO @sql;
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT IF(
    @template_exists = 1 AND @template_has_var_seq = 1 AND @template_unresolved = 0 AND @template_duplicate_count = 0 AND NOT EXISTS (
        SELECT 1
        FROM information_schema.statistics
        WHERE table_schema = @schema_name
          AND table_name = 'template_variable'
          AND index_name = 'uq_template_variable_template_var_seq'
    ),
    "ALTER TABLE `template_variable` ADD CONSTRAINT `uq_template_variable_template_var_seq` UNIQUE (`template_id`, `var_seq`)",
    "SELECT 'skip add uq_template_variable_template_var_seq' AS message"
) INTO @sql;
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT IF(
    @template_exists = 1 AND @template_has_var_code = 1 AND @template_unresolved = 0 AND @template_duplicate_count = 0,
    "ALTER TABLE `template_variable` DROP COLUMN `var_code`",
    "SELECT 'skip drop template_variable.var_code' AS message"
) INTO @sql;
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
