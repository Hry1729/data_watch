USE `device_watch`;

SET NAMES utf8mb4;

SET @seed_actor = 'db-seed';
SET @qc_device_id = (
    SELECT `id`
    FROM `device`
    WHERE `device_code` = 'qc100'
    LIMIT 1
);

INSERT INTO `device_variable` (
    `device_id`,
    `var_seq`,
    `var_name`,
    `io_type`,
    `var_type`,
    `var_address`,
    `var_offset`,
    `var_bit`,
    `description`,
    `scan_rate`,
    `is_alarm`,
    `is_storage`,
    `create_user`,
    `create_time`,
    `update_user`,
    `update_time`
)
SELECT
    @qc_device_id,
    seed.`var_seq`,
    seed.`var_name`,
    seed.`io_type`,
    seed.`var_type`,
    seed.`var_address`,
    seed.`var_offset`,
    seed.`var_bit`,
    seed.`description`,
    seed.`scan_rate`,
    seed.`is_alarm`,
    seed.`is_storage`,
    @seed_actor,
    NOW(),
    @seed_actor,
    NOW()
FROM (
    SELECT 30 AS `var_seq`, 'Input' AS `var_name`, 'I' AS `io_type`, 'BOOL' AS `var_type`, '%I101.0' AS `var_address`, 101 AS `var_offset`, 0 AS `var_bit`, '控制合//+01F15/15CA10E' AS `description`, 1000 AS `scan_rate`, 1 AS `is_alarm`, 1 AS `is_storage`
    UNION ALL
    SELECT 11894, 'Main Hoist General', 'M', 'DINT', '%MD1304', 1304, NULL, '主起升 位置值 （1=1mm)//', 100, 0, 1
    UNION ALL
    SELECT 11900, 'Main Trolley General', 'M', 'DINT', '%MD1328', 1328, NULL, '主小车 位置 （1=1mm)//', 100, 0, 1
) AS seed
WHERE @qc_device_id IS NOT NULL
ON DUPLICATE KEY UPDATE
    `var_name` = VALUES(`var_name`),
    `io_type` = VALUES(`io_type`),
    `var_type` = VALUES(`var_type`),
    `var_address` = VALUES(`var_address`),
    `var_offset` = VALUES(`var_offset`),
    `var_bit` = VALUES(`var_bit`),
    `description` = VALUES(`description`),
    `scan_rate` = VALUES(`scan_rate`),
    `is_alarm` = VALUES(`is_alarm`),
    `is_storage` = VALUES(`is_storage`),
    `update_user` = VALUES(`update_user`),
    `update_time` = VALUES(`update_time`);

SELECT
    `dv`.`id`,
    `d`.`device_code`,
    `dv`.`var_seq`,
    CONCAT(`d`.`device_code`, '_', `dv`.`var_seq`) AS `td_subtable_name`,
    `dv`.`var_name`,
    `dv`.`io_type`,
    `dv`.`var_type`,
    `dv`.`var_address`,
    `dv`.`scan_rate`,
    `dv`.`is_alarm`,
    `dv`.`is_storage`
FROM `device_variable` AS `dv`
JOIN `device` AS `d`
  ON `d`.`id` = `dv`.`device_id`
WHERE `d`.`device_code` = 'qc100'
  AND `dv`.`var_seq` IN (30, 11894, 11900)
ORDER BY `dv`.`var_seq`;
