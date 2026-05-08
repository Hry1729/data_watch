USE `device_watch`;

SET NAMES utf8mb4;

CREATE TABLE IF NOT EXISTS `log_retention_policy` (
  `id` BIGINT NOT NULL,
  `enabled` TINYINT(1) NOT NULL DEFAULT 1,
  `retention_days` INT NOT NULL DEFAULT 7,
  `max_total_mb` INT NOT NULL DEFAULT 1024,
  `cleanup_time` VARCHAR(8) NOT NULL DEFAULT '03:00',
  `capacity_check_interval_minutes` INT NOT NULL DEFAULT 30,
  `batch_size` INT NOT NULL DEFAULT 5000,
  `debug_db_enabled` TINYINT(1) NOT NULL DEFAULT 0,
  `debug_enabled_until` DATETIME NULL DEFAULT NULL,
  `debug_reason` VARCHAR(255) NOT NULL DEFAULT '',
  `debug_enabled_by_id` BIGINT NULL DEFAULT NULL,
  `debug_enabled_by_name` VARCHAR(64) NOT NULL DEFAULT '',
  `last_cleanup_time` DATETIME NULL DEFAULT NULL,
  `last_cleanup_result_json` JSON NULL,
  `update_by_id` BIGINT NULL DEFAULT NULL,
  `update_by_name` VARCHAR(64) NOT NULL DEFAULT 'system',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  CONSTRAINT `fk_log_policy_debug_by` FOREIGN KEY (`debug_enabled_by_id`) REFERENCES `sys_user` (`id`) ON DELETE SET NULL,
  CONSTRAINT `fk_log_policy_update_by` FOREIGN KEY (`update_by_id`) REFERENCES `sys_user` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='system log retention and debug policy';

INSERT INTO `log_retention_policy` (
  `id`, `enabled`, `retention_days`, `max_total_mb`, `cleanup_time`,
  `capacity_check_interval_minutes`, `batch_size`, `debug_db_enabled`, `update_by_name`
)
SELECT 1, 1, 7, 1024, '03:00', 30, 5000, 0, 'system'
WHERE NOT EXISTS (SELECT 1 FROM `log_retention_policy` WHERE `id` = 1);

DROP PROCEDURE IF EXISTS `sp_add_log_index_if_missing`;

DELIMITER $$
CREATE PROCEDURE `sp_add_log_index_if_missing`(
  IN p_table_name VARCHAR(64),
  IN p_index_name VARCHAR(64),
  IN p_index_columns TEXT
)
BEGIN
  IF NOT EXISTS (
    SELECT 1
    FROM `information_schema`.`STATISTICS`
    WHERE `TABLE_SCHEMA` = DATABASE()
      AND `TABLE_NAME` = p_table_name
      AND `INDEX_NAME` = p_index_name
  ) THEN
    SET @ddl = CONCAT(
      'ALTER TABLE `', p_table_name, '` ADD INDEX `', p_index_name, '` ', p_index_columns
    );
    PREPARE stmt FROM @ddl;
    EXECUTE stmt;
    DEALLOCATE PREPARE stmt;
  END IF;
END$$
DELIMITER ;

CALL `sp_add_log_index_if_missing`('operation_log', 'idx_operation_log_request_id', '(`request_id`)');
CALL `sp_add_log_index_if_missing`('operation_log', 'idx_operation_log_result_log_time', '(`result_status`, `log_time`)');
CALL `sp_add_log_index_if_missing`('system_log', 'idx_system_log_source_log_time', '(`source`, `log_time`)');
CALL `sp_add_log_index_if_missing`('system_log', 'idx_system_log_service_log_time', '(`service_name`, `log_time`)');
CALL `sp_add_log_index_if_missing`('system_log', 'idx_system_log_device_code_log_time', '(`device_code`, `log_time`)');

DROP PROCEDURE IF EXISTS `sp_add_log_index_if_missing`;

SET @menu_system_log = (SELECT `id` FROM `sys_menu` WHERE `name` = 'SystemLog' LIMIT 1);

INSERT INTO `sys_permission` (
  `permission_code`, `permission_name`, `permission_type`, `menu_id`,
  `auth_mark`, `description`, `sort`, `enabled`
)
VALUES
  ('device_monitor:log:export', '系统日志导出', 'action', @menu_system_log, 'device_monitor:log:export', '', 71, 1),
  ('device_monitor:log:cleanup', '系统日志清理', 'action', @menu_system_log, 'device_monitor:log:cleanup', '', 72, 1),
  ('device_monitor:log:policy', '系统日志策略配置', 'action', @menu_system_log, 'device_monitor:log:policy', '', 73, 1),
  ('device_monitor:log:debug', '系统日志DEBUG开关', 'action', @menu_system_log, 'device_monitor:log:debug', '', 74, 1)
ON DUPLICATE KEY UPDATE
  `permission_name` = VALUES(`permission_name`),
  `permission_type` = VALUES(`permission_type`),
  `menu_id` = VALUES(`menu_id`),
  `auth_mark` = VALUES(`auth_mark`),
  `description` = VALUES(`description`),
  `sort` = VALUES(`sort`),
  `enabled` = VALUES(`enabled`);

INSERT INTO `sys_role_permission` (`role_id`, `permission_id`)
SELECT `r`.`id`, `p`.`id`
FROM `sys_role` AS `r`
JOIN `sys_permission` AS `p`
WHERE (
    (
      `r`.`role_code` = 'R_SUPER'
      AND `p`.`permission_code` IN (
        'device_monitor:log:export',
        'device_monitor:log:cleanup',
        'device_monitor:log:policy',
        'device_monitor:log:debug'
      )
    )
    OR (
      `r`.`role_code` = 'R_ADMIN'
      AND `p`.`permission_code` = 'device_monitor:log:export'
    )
  )
  AND NOT EXISTS (
    SELECT 1
    FROM `sys_role_permission` AS `existing`
    WHERE `existing`.`role_id` = `r`.`id`
      AND `existing`.`permission_id` = `p`.`id`
  );
