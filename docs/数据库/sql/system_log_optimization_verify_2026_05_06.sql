USE `device_watch`;

SET NAMES utf8mb4;

SELECT
  `TABLE_NAME`,
  `TABLE_ROWS`,
  ROUND((`DATA_LENGTH` + `INDEX_LENGTH`) / 1024 / 1024, 3) AS `size_mb`
FROM `information_schema`.`TABLES`
WHERE `TABLE_SCHEMA` = DATABASE()
  AND `TABLE_NAME` IN ('operation_log', 'system_log', 'plc_connection_log', 'log_retention_policy')
ORDER BY `TABLE_NAME`;

SELECT
  `INDEX_NAME`,
  GROUP_CONCAT(`COLUMN_NAME` ORDER BY `SEQ_IN_INDEX`) AS `columns`
FROM `information_schema`.`STATISTICS`
WHERE `TABLE_SCHEMA` = DATABASE()
  AND (
    (`TABLE_NAME` = 'operation_log' AND `INDEX_NAME` IN ('idx_operation_log_request_id', 'idx_operation_log_result_log_time'))
    OR (`TABLE_NAME` = 'system_log' AND `INDEX_NAME` IN ('idx_system_log_source_log_time', 'idx_system_log_service_log_time', 'idx_system_log_device_code_log_time'))
  )
GROUP BY `INDEX_NAME`
ORDER BY `INDEX_NAME`;

SELECT
  `id`,
  `enabled`,
  `retention_days`,
  `max_total_mb`,
  `cleanup_time`,
  `capacity_check_interval_minutes`,
  `batch_size`,
  `debug_db_enabled`,
  `debug_enabled_until`,
  `update_by_name`
FROM `log_retention_policy`
WHERE `id` = 1;

SELECT
  `permission_code`,
  `permission_name`,
  `permission_type`,
  `sort`,
  `enabled`
FROM `sys_permission`
WHERE `permission_code` IN (
  'device_monitor:log:view',
  'device_monitor:log:export',
  'device_monitor:log:cleanup',
  'device_monitor:log:policy',
  'device_monitor:log:debug'
)
ORDER BY `sort`;

SELECT
  `r`.`role_code`,
  GROUP_CONCAT(`p`.`permission_code` ORDER BY `p`.`permission_code`) AS `log_permissions`
FROM `sys_role` AS `r`
JOIN `sys_role_permission` AS `rp` ON `rp`.`role_id` = `r`.`id`
JOIN `sys_permission` AS `p` ON `p`.`id` = `rp`.`permission_id`
WHERE `r`.`role_code` IN ('R_SUPER', 'R_ADMIN', 'R_USER')
  AND `p`.`permission_code` LIKE 'device_monitor:log:%'
GROUP BY `r`.`role_code`
ORDER BY `r`.`role_code`;

SELECT COUNT(*) AS `operation_log_rows` FROM `operation_log`;
SELECT COUNT(*) AS `system_log_rows` FROM `system_log`;
SELECT COUNT(*) AS `plc_connection_log_rows` FROM `plc_connection_log`;
