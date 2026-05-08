-- Alarm and log migration baseline for device_watch
-- Generated on 2026-04-20
-- This file is self-contained: schema + seed

USE `device_watch`;

SET NAMES utf8mb4;

CREATE TABLE IF NOT EXISTS `alarm_level` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `level_code` VARCHAR(32) NOT NULL,
  `level_name` VARCHAR(32) NOT NULL,
  `severity` TINYINT NOT NULL,
  `color` VARCHAR(16) NOT NULL DEFAULT '',
  `sort` INT NOT NULL DEFAULT 1,
  `enabled` TINYINT(1) NOT NULL DEFAULT 1,
  `icon` VARCHAR(64) NOT NULL DEFAULT '',
  `description` VARCHAR(255) NOT NULL DEFAULT '',
  `create_by` VARCHAR(64) NOT NULL DEFAULT 'system',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `update_by` VARCHAR(64) NOT NULL DEFAULT 'system',
  `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_alarm_level_level_code` (`level_code`),
  KEY `idx_alarm_level_severity` (`severity`),
  KEY `idx_alarm_level_enabled_sort` (`enabled`, `sort`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='alarm level dictionary';

CREATE TABLE IF NOT EXISTS `alarm_rule` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `rule_code` VARCHAR(64) NOT NULL,
  `rule_name` VARCHAR(128) NOT NULL,
  `device_id` BIGINT NULL DEFAULT NULL,
  `point_id` BIGINT NULL DEFAULT NULL,
  `alarm_type` VARCHAR(32) NOT NULL,
  `level_id` BIGINT NOT NULL,
  `compare_op` VARCHAR(16) NULL DEFAULT NULL,
  `threshold_value` VARCHAR(128) NULL DEFAULT NULL,
  `recover_value` VARCHAR(128) NULL DEFAULT NULL,
  `enabled` TINYINT(1) NOT NULL DEFAULT 1,
  `description` VARCHAR(255) NOT NULL DEFAULT '',
  `deadband_value` DECIMAL(20,6) NULL DEFAULT NULL,
  `hold_seconds` INT NOT NULL DEFAULT 0,
  `message_template` VARCHAR(255) NULL DEFAULT NULL,
  `threshold_value_num` DECIMAL(20,6) NULL DEFAULT NULL,
  `recover_value_num` DECIMAL(20,6) NULL DEFAULT NULL,
  `extra_json` JSON NULL,
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_alarm_rule_rule_code` (`rule_code`),
  KEY `idx_alarm_rule_device_point_enabled` (`device_id`, `point_id`, `enabled`),
  KEY `idx_alarm_rule_alarm_type_enabled` (`alarm_type`, `enabled`),
  KEY `idx_alarm_rule_level_enabled` (`level_id`, `enabled`),
  CONSTRAINT `fk_alarm_rule_device` FOREIGN KEY (`device_id`) REFERENCES `device` (`id`) ON DELETE SET NULL,
  CONSTRAINT `fk_alarm_rule_point` FOREIGN KEY (`point_id`) REFERENCES `device_variable` (`id`) ON DELETE SET NULL,
  CONSTRAINT `fk_alarm_rule_level` FOREIGN KEY (`level_id`) REFERENCES `alarm_level` (`id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='alarm rule definition';

CREATE TABLE IF NOT EXISTS `alarm_record` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `alarm_no` VARCHAR(64) NOT NULL,
  `rule_id` BIGINT NULL DEFAULT NULL,
  `device_id` BIGINT NULL DEFAULT NULL,
  `device_code` VARCHAR(64) NOT NULL DEFAULT '',
  `device_name` VARCHAR(128) NOT NULL DEFAULT '',
  `device_type_code` VARCHAR(32) NOT NULL DEFAULT '',
  `point_id` BIGINT NULL DEFAULT NULL,
  `point_code` VARCHAR(64) NOT NULL DEFAULT '',
  `point_name` VARCHAR(128) NOT NULL DEFAULT '',
  `alarm_type` VARCHAR(32) NOT NULL,
  `level_code` VARCHAR(32) NOT NULL,
  `severity` TINYINT NOT NULL,
  `status` VARCHAR(32) NOT NULL,
  `alarm_value` VARCHAR(128) NOT NULL DEFAULT '',
  `threshold_value` VARCHAR(128) NOT NULL DEFAULT '',
  `content` VARCHAR(500) NOT NULL,
  `rule_name_snapshot` VARCHAR(128) NOT NULL DEFAULT '',
  `level_name_snapshot` VARCHAR(32) NOT NULL DEFAULT '',
  `alarm_value_num` DECIMAL(20,6) NULL DEFAULT NULL,
  `threshold_value_num` DECIMAL(20,6) NULL DEFAULT NULL,
  `occur_count` INT NOT NULL DEFAULT 1,
  `start_time` DATETIME NOT NULL,
  `end_time` DATETIME NULL DEFAULT NULL,
  `ack_time` DATETIME NULL DEFAULT NULL,
  `ack_by` VARCHAR(64) NOT NULL DEFAULT '',
  `ack_user_id` BIGINT NULL DEFAULT NULL,
  `recover_time` DATETIME NULL DEFAULT NULL,
  `trace_id` VARCHAR(64) NOT NULL DEFAULT '',
  `extra_json` JSON NULL,
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_alarm_record_alarm_no` (`alarm_no`),
  KEY `idx_alarm_record_start_time` (`start_time`),
  KEY `idx_alarm_record_device_start_time` (`device_id`, `start_time`),
  KEY `idx_alarm_record_level_start_time` (`level_code`, `start_time`),
  KEY `idx_alarm_record_alarm_type_start_time` (`alarm_type`, `start_time`),
  KEY `idx_alarm_record_status_start_time` (`status`, `start_time`),
  KEY `idx_alarm_record_device_type_start_time` (`device_type_code`, `start_time`),
  KEY `idx_alarm_record_rule_start_time` (`rule_id`, `start_time`),
  CONSTRAINT `fk_alarm_record_rule` FOREIGN KEY (`rule_id`) REFERENCES `alarm_rule` (`id`) ON DELETE SET NULL,
  CONSTRAINT `fk_alarm_record_device` FOREIGN KEY (`device_id`) REFERENCES `device` (`id`) ON DELETE SET NULL,
  CONSTRAINT `fk_alarm_record_point` FOREIGN KEY (`point_id`) REFERENCES `device_variable` (`id`) ON DELETE SET NULL,
  CONSTRAINT `fk_alarm_record_ack_user` FOREIGN KEY (`ack_user_id`) REFERENCES `sys_user` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='alarm history event fact';

CREATE TABLE IF NOT EXISTS `alarm_active` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `record_id` BIGINT NOT NULL,
  `rule_id` BIGINT NULL DEFAULT NULL,
  `device_id` BIGINT NULL DEFAULT NULL,
  `device_code` VARCHAR(64) NOT NULL DEFAULT '',
  `device_name` VARCHAR(128) NOT NULL DEFAULT '',
  `device_type_code` VARCHAR(32) NOT NULL DEFAULT '',
  `point_id` BIGINT NULL DEFAULT NULL,
  `point_name` VARCHAR(128) NOT NULL DEFAULT '',
  `alarm_type` VARCHAR(32) NOT NULL,
  `level_code` VARCHAR(32) NOT NULL,
  `severity` TINYINT NOT NULL,
  `status` VARCHAR(32) NOT NULL,
  `content` VARCHAR(500) NOT NULL DEFAULT '',
  `alarm_value` VARCHAR(128) NOT NULL DEFAULT '',
  `start_time` DATETIME NOT NULL,
  `latest_time` DATETIME NOT NULL,
  `ack_time` DATETIME NULL DEFAULT NULL,
  `ack_by` VARCHAR(64) NOT NULL DEFAULT '',
  `extra_json` JSON NULL,
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_alarm_active_record_id` (`record_id`),
  KEY `idx_alarm_active_device_id` (`device_id`),
  KEY `idx_alarm_active_level_code` (`level_code`),
  KEY `idx_alarm_active_status` (`status`),
  KEY `idx_alarm_active_latest_time` (`latest_time`),
  CONSTRAINT `fk_alarm_active_record` FOREIGN KEY (`record_id`) REFERENCES `alarm_record` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_alarm_active_rule` FOREIGN KEY (`rule_id`) REFERENCES `alarm_rule` (`id`) ON DELETE SET NULL,
  CONSTRAINT `fk_alarm_active_device` FOREIGN KEY (`device_id`) REFERENCES `device` (`id`) ON DELETE SET NULL,
  CONSTRAINT `fk_alarm_active_point` FOREIGN KEY (`point_id`) REFERENCES `device_variable` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='active alarm snapshot';

CREATE TABLE IF NOT EXISTS `operation_log` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `log_time` DATETIME NOT NULL,
  `operator_id` BIGINT NULL DEFAULT NULL,
  `operator_name` VARCHAR(64) NOT NULL DEFAULT '',
  `module_name` VARCHAR(64) NOT NULL,
  `action_name` VARCHAR(64) NOT NULL,
  `level` VARCHAR(16) NOT NULL,
  `content` VARCHAR(500) NOT NULL,
  `detail_json` JSON NULL,
  `request_id` VARCHAR(64) NOT NULL DEFAULT '',
  `ip` VARCHAR(64) NOT NULL DEFAULT '',
  `user_agent` VARCHAR(255) NOT NULL DEFAULT '',
  `biz_type` VARCHAR(64) NOT NULL DEFAULT '',
  `biz_id` VARCHAR(64) NOT NULL DEFAULT '',
  `duration_ms` INT NULL DEFAULT NULL,
  `result_status` VARCHAR(32) NOT NULL DEFAULT '',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `idx_operation_log_log_time` (`log_time`),
  KEY `idx_operation_log_level_log_time` (`level`, `log_time`),
  KEY `idx_operation_log_operator_log_time` (`operator_id`, `log_time`),
  KEY `idx_operation_log_module_log_time` (`module_name`, `log_time`),
  CONSTRAINT `fk_operation_log_operator` FOREIGN KEY (`operator_id`) REFERENCES `sys_user` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='operation audit log';

CREATE TABLE IF NOT EXISTS `system_log` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `log_time` DATETIME NOT NULL,
  `level` VARCHAR(16) NOT NULL,
  `log_type` VARCHAR(16) NOT NULL,
  `source` VARCHAR(64) NOT NULL,
  `device_id` BIGINT NULL DEFAULT NULL,
  `device_code` VARCHAR(64) NOT NULL DEFAULT '',
  `point_id` BIGINT NULL DEFAULT NULL,
  `host_name` VARCHAR(64) NOT NULL DEFAULT '',
  `service_name` VARCHAR(64) NOT NULL DEFAULT '',
  `content` VARCHAR(500) NOT NULL,
  `detail_json` JSON NULL,
  `trace_id` VARCHAR(64) NOT NULL DEFAULT '',
  `exception_stack` TEXT NULL,
  `elapsed_ms` INT NULL DEFAULT NULL,
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `idx_system_log_log_time` (`log_time`),
  KEY `idx_system_log_level_log_time` (`level`, `log_time`),
  KEY `idx_system_log_log_type_log_time` (`log_type`, `log_time`),
  KEY `idx_system_log_device_log_time` (`device_id`, `log_time`),
  KEY `idx_system_log_trace_id` (`trace_id`),
  CONSTRAINT `fk_system_log_device` FOREIGN KEY (`device_id`) REFERENCES `device` (`id`) ON DELETE SET NULL,
  CONSTRAINT `fk_system_log_point` FOREIGN KEY (`point_id`) REFERENCES `device_variable` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='system runtime log';

USE `device_watch`;

SET NAMES utf8mb4;

INSERT INTO `alarm_level`
(`level_code`, `level_name`, `severity`, `color`, `sort`, `enabled`, `icon`, `description`, `create_by`, `update_by`)
VALUES
  ('INFO', '提示', 1, '#4A6CF7', 1, 1, 'ri:information-line', '联调默认信息级别', 'seed', 'seed'),
  ('WARN', '预警', 2, '#F4A542', 2, 1, 'ri:error-warning-line', '联调默认预警级别', 'seed', 'seed'),
  ('ERROR', '告警', 3, '#D66B88', 3, 1, 'ri:alarm-warning-line', '联调默认错误级别', 'seed', 'seed')
ON DUPLICATE KEY UPDATE
  `level_name` = VALUES(`level_name`),
  `severity` = VALUES(`severity`),
  `color` = VALUES(`color`),
  `sort` = VALUES(`sort`),
  `enabled` = VALUES(`enabled`),
  `icon` = VALUES(`icon`),
  `description` = VALUES(`description`),
  `update_by` = VALUES(`update_by`),
  `update_time` = CURRENT_TIMESTAMP;

SET @seed_level_info_id = (SELECT `id` FROM `alarm_level` WHERE `level_code` = 'INFO');
SET @seed_level_warn_id = (SELECT `id` FROM `alarm_level` WHERE `level_code` = 'WARN');
SET @seed_level_error_id = (SELECT `id` FROM `alarm_level` WHERE `level_code` = 'ERROR');
SET @seed_super_user_id = (SELECT `id` FROM `sys_user` WHERE `user_name` = 'Super' LIMIT 1);
SET @seed_admin_user_id = (SELECT `id` FROM `sys_user` WHERE `user_name` = 'Admin' LIMIT 1);
SET @seed_user_user_id = (SELECT `id` FROM `sys_user` WHERE `user_name` = 'User' LIMIT 1);
SET @seed_ack_user_id = COALESCE(@seed_admin_user_id, @seed_super_user_id, @seed_user_user_id);
SET @seed_ack_by = IF(@seed_admin_user_id IS NOT NULL, 'Admin', IF(@seed_super_user_id IS NOT NULL, 'Super', 'system'));

SET
  @seed_point_1_id = NULL,
  @seed_point_1_code = NULL,
  @seed_point_1_name = NULL,
  @seed_device_1_id = NULL,
  @seed_device_1_code = NULL,
  @seed_device_1_name = NULL,
  @seed_device_1_type_code = NULL;

SET
  @seed_point_2_id = NULL,
  @seed_point_2_code = NULL,
  @seed_point_2_name = NULL,
  @seed_device_2_id = NULL,
  @seed_device_2_code = NULL,
  @seed_device_2_name = NULL,
  @seed_device_2_type_code = NULL;

SET
  @seed_point_3_id = NULL,
  @seed_point_3_code = NULL,
  @seed_point_3_name = NULL,
  @seed_device_3_id = NULL,
  @seed_device_3_code = NULL,
  @seed_device_3_name = NULL,
  @seed_device_3_type_code = NULL;

SELECT
  dv.`id`,
  CASE
    WHEN COALESCE(d.`device_code`, '') <> '' AND dv.`var_seq` IS NOT NULL THEN CONCAT(d.`device_code`, '_', CAST(dv.`var_seq` AS CHAR))
    ELSE COALESCE(CAST(dv.`var_seq` AS CHAR), '')
  END,
  COALESCE(dv.`var_name`, CAST(dv.`var_seq` AS CHAR), ''),
  d.`id`,
  COALESCE(d.`device_code`, ''),
  COALESCE(d.`device_name`, d.`device_code`, ''),
  COALESCE(dt.`type_code`, '')
INTO
  @seed_point_1_id,
  @seed_point_1_code,
  @seed_point_1_name,
  @seed_device_1_id,
  @seed_device_1_code,
  @seed_device_1_name,
  @seed_device_1_type_code
FROM `device_variable` AS `dv`
JOIN `device` AS `d` ON `d`.`id` = `dv`.`device_id`
LEFT JOIN `device_type` AS `dt` ON `dt`.`id` = `d`.`type_id`
ORDER BY `dv`.`id`
LIMIT 1 OFFSET 0;

SELECT
  dv.`id`,
  CASE
    WHEN COALESCE(d.`device_code`, '') <> '' AND dv.`var_seq` IS NOT NULL THEN CONCAT(d.`device_code`, '_', CAST(dv.`var_seq` AS CHAR))
    ELSE COALESCE(CAST(dv.`var_seq` AS CHAR), '')
  END,
  COALESCE(dv.`var_name`, CAST(dv.`var_seq` AS CHAR), ''),
  d.`id`,
  COALESCE(d.`device_code`, ''),
  COALESCE(d.`device_name`, d.`device_code`, ''),
  COALESCE(dt.`type_code`, '')
INTO
  @seed_point_2_id,
  @seed_point_2_code,
  @seed_point_2_name,
  @seed_device_2_id,
  @seed_device_2_code,
  @seed_device_2_name,
  @seed_device_2_type_code
FROM `device_variable` AS `dv`
JOIN `device` AS `d` ON `d`.`id` = `dv`.`device_id`
LEFT JOIN `device_type` AS `dt` ON `dt`.`id` = `d`.`type_id`
ORDER BY `dv`.`id`
LIMIT 1 OFFSET 1;

SELECT
  dv.`id`,
  CASE
    WHEN COALESCE(d.`device_code`, '') <> '' AND dv.`var_seq` IS NOT NULL THEN CONCAT(d.`device_code`, '_', CAST(dv.`var_seq` AS CHAR))
    ELSE COALESCE(CAST(dv.`var_seq` AS CHAR), '')
  END,
  COALESCE(dv.`var_name`, CAST(dv.`var_seq` AS CHAR), ''),
  d.`id`,
  COALESCE(d.`device_code`, ''),
  COALESCE(d.`device_name`, d.`device_code`, ''),
  COALESCE(dt.`type_code`, '')
INTO
  @seed_point_3_id,
  @seed_point_3_code,
  @seed_point_3_name,
  @seed_device_3_id,
  @seed_device_3_code,
  @seed_device_3_name,
  @seed_device_3_type_code
FROM `device_variable` AS `dv`
JOIN `device` AS `d` ON `d`.`id` = `dv`.`device_id`
LEFT JOIN `device_type` AS `dt` ON `dt`.`id` = `d`.`type_id`
ORDER BY `dv`.`id`
LIMIT 1 OFFSET 2;

SET @seed_base_device_id = COALESCE(@seed_device_1_id, @seed_device_2_id, @seed_device_3_id);
SET @seed_base_device_code = COALESCE(@seed_device_1_code, @seed_device_2_code, @seed_device_3_code, 'seed-device');
SET @seed_base_device_name = COALESCE(@seed_device_1_name, @seed_device_2_name, @seed_device_3_name, 'Seed Device');
SET @seed_base_device_type_code = COALESCE(@seed_device_1_type_code, @seed_device_2_type_code, @seed_device_3_type_code, 'SEED');
SET @seed_base_point_id = COALESCE(@seed_point_1_id, @seed_point_2_id, @seed_point_3_id);
SET @seed_base_point_code = COALESCE(@seed_point_1_code, @seed_point_2_code, @seed_point_3_code, 'seed_point');
SET @seed_base_point_name = COALESCE(@seed_point_1_name, @seed_point_2_name, @seed_point_3_name, 'Seed Point');

INSERT INTO `alarm_rule`
(`rule_code`, `rule_name`, `device_id`, `point_id`, `alarm_type`, `level_id`, `compare_op`, `threshold_value`, `recover_value`, `enabled`, `description`, `deadband_value`, `hold_seconds`, `message_template`, `threshold_value_num`, `recover_value_num`, `extra_json`, `create_time`, `update_time`)
SELECT
  'SEED_ALARM_RULE_001',
  '联调样本-高值预警',
  @seed_device_1_id,
  @seed_point_1_id,
  'threshold',
  COALESCE(@seed_level_warn_id, @seed_level_info_id),
  '>',
  '80',
  '60',
  1,
  '基于首个点位生成的高值预警规则样本',
  5.000000,
  30,
  '设备{{deviceName}}点位{{pointName}}数值高于阈值',
  80.000000,
  60.000000,
  JSON_OBJECT('seed', TRUE, 'sampleNo', 1),
  NOW(),
  NOW()
 FROM DUAL
ON DUPLICATE KEY UPDATE
  `rule_name` = VALUES(`rule_name`),
  `device_id` = VALUES(`device_id`),
  `point_id` = VALUES(`point_id`),
  `alarm_type` = VALUES(`alarm_type`),
  `level_id` = VALUES(`level_id`),
  `compare_op` = VALUES(`compare_op`),
  `threshold_value` = VALUES(`threshold_value`),
  `recover_value` = VALUES(`recover_value`),
  `enabled` = VALUES(`enabled`),
  `description` = VALUES(`description`),
  `deadband_value` = VALUES(`deadband_value`),
  `hold_seconds` = VALUES(`hold_seconds`),
  `message_template` = VALUES(`message_template`),
  `threshold_value_num` = VALUES(`threshold_value_num`),
  `recover_value_num` = VALUES(`recover_value_num`),
  `extra_json` = VALUES(`extra_json`),
  `update_time` = CURRENT_TIMESTAMP;

INSERT INTO `alarm_rule`
(`rule_code`, `rule_name`, `device_id`, `point_id`, `alarm_type`, `level_id`, `compare_op`, `threshold_value`, `recover_value`, `enabled`, `description`, `deadband_value`, `hold_seconds`, `message_template`, `threshold_value_num`, `recover_value_num`, `extra_json`, `create_time`, `update_time`)
SELECT
  'SEED_ALARM_RULE_002',
  '联调样本-通讯中断',
  COALESCE(@seed_device_2_id, @seed_base_device_id),
  COALESCE(@seed_point_2_id, @seed_base_point_id),
  'communication',
  COALESCE(@seed_level_error_id, @seed_level_warn_id, @seed_level_info_id),
  '=',
  'offline',
  'online',
  1,
  '用于 Dashboard 最近事件和告警分布的通讯告警样本',
  NULL,
  0,
  '设备{{deviceName}}通讯中断',
  NULL,
  NULL,
  JSON_OBJECT('seed', TRUE, 'sampleNo', 2),
  NOW(),
  NOW()
 FROM DUAL
ON DUPLICATE KEY UPDATE
  `rule_name` = VALUES(`rule_name`),
  `device_id` = VALUES(`device_id`),
  `point_id` = VALUES(`point_id`),
  `alarm_type` = VALUES(`alarm_type`),
  `level_id` = VALUES(`level_id`),
  `compare_op` = VALUES(`compare_op`),
  `threshold_value` = VALUES(`threshold_value`),
  `recover_value` = VALUES(`recover_value`),
  `enabled` = VALUES(`enabled`),
  `description` = VALUES(`description`),
  `deadband_value` = VALUES(`deadband_value`),
  `hold_seconds` = VALUES(`hold_seconds`),
  `message_template` = VALUES(`message_template`),
  `threshold_value_num` = VALUES(`threshold_value_num`),
  `recover_value_num` = VALUES(`recover_value_num`),
  `extra_json` = VALUES(`extra_json`),
  `update_time` = CURRENT_TIMESTAMP;

INSERT INTO `alarm_rule`
(`rule_code`, `rule_name`, `device_id`, `point_id`, `alarm_type`, `level_id`, `compare_op`, `threshold_value`, `recover_value`, `enabled`, `description`, `deadband_value`, `hold_seconds`, `message_template`, `threshold_value_num`, `recover_value_num`, `extra_json`, `create_time`, `update_time`)
SELECT
  'SEED_ALARM_RULE_003',
  '联调样本-状态异常',
  COALESCE(@seed_device_3_id, @seed_base_device_id),
  COALESCE(@seed_point_3_id, @seed_base_point_id),
  'bool_state',
  COALESCE(@seed_level_warn_id, @seed_level_info_id),
  '=',
  '1',
  '0',
  1,
  '用于状态类点位的 BOOL 异常规则样本',
  NULL,
  10,
  '设备{{deviceName}}状态异常',
  1.000000,
  0.000000,
  JSON_OBJECT('seed', TRUE, 'sampleNo', 3),
  NOW(),
  NOW()
 FROM DUAL
ON DUPLICATE KEY UPDATE
  `rule_name` = VALUES(`rule_name`),
  `device_id` = VALUES(`device_id`),
  `point_id` = VALUES(`point_id`),
  `alarm_type` = VALUES(`alarm_type`),
  `level_id` = VALUES(`level_id`),
  `compare_op` = VALUES(`compare_op`),
  `threshold_value` = VALUES(`threshold_value`),
  `recover_value` = VALUES(`recover_value`),
  `enabled` = VALUES(`enabled`),
  `description` = VALUES(`description`),
  `deadband_value` = VALUES(`deadband_value`),
  `hold_seconds` = VALUES(`hold_seconds`),
  `message_template` = VALUES(`message_template`),
  `threshold_value_num` = VALUES(`threshold_value_num`),
  `recover_value_num` = VALUES(`recover_value_num`),
  `extra_json` = VALUES(`extra_json`),
  `update_time` = CURRENT_TIMESTAMP;

SET @seed_rule_1_id = (SELECT `id` FROM `alarm_rule` WHERE `rule_code` = 'SEED_ALARM_RULE_001' LIMIT 1);
SET @seed_rule_2_id = (SELECT `id` FROM `alarm_rule` WHERE `rule_code` = 'SEED_ALARM_RULE_002' LIMIT 1);
SET @seed_rule_3_id = (SELECT `id` FROM `alarm_rule` WHERE `rule_code` = 'SEED_ALARM_RULE_003' LIMIT 1);
SET @seed_base_rule_id = COALESCE(@seed_rule_1_id, @seed_rule_2_id, @seed_rule_3_id);

INSERT INTO `alarm_record`
(`alarm_no`, `rule_id`, `device_id`, `device_code`, `device_name`, `device_type_code`, `point_id`, `point_code`, `point_name`, `alarm_type`, `level_code`, `severity`, `status`, `alarm_value`, `threshold_value`, `content`, `rule_name_snapshot`, `level_name_snapshot`, `alarm_value_num`, `threshold_value_num`, `occur_count`, `start_time`, `end_time`, `ack_time`, `ack_by`, `ack_user_id`, `recover_time`, `trace_id`, `extra_json`, `create_time`)
VALUES
  ('ALM_SEED_20260420_001', COALESCE(@seed_rule_1_id, @seed_base_rule_id), COALESCE(@seed_device_1_id, @seed_base_device_id), COALESCE(@seed_device_1_code, @seed_base_device_code), COALESCE(@seed_device_1_name, @seed_base_device_name), COALESCE(@seed_device_1_type_code, @seed_base_device_type_code), COALESCE(@seed_point_1_id, @seed_base_point_id), COALESCE(@seed_point_1_code, @seed_base_point_code), COALESCE(@seed_point_1_name, @seed_base_point_name), 'threshold', 'WARN', 2, 'recorded', '92.5', '80', '点位值持续高于预警阈值，等待确认。', '联调样本-高值预警', '预警', 92.500000, 80.000000, 1, NOW() - INTERVAL 10 MINUTE, NULL, NULL, '', NULL, NULL, 'trace-alarm-seed-001', JSON_OBJECT('seed', TRUE, 'batch', 'alarm_record'), NOW()),
  ('ALM_SEED_20260420_002', COALESCE(@seed_rule_2_id, @seed_base_rule_id), COALESCE(@seed_device_2_id, @seed_base_device_id), COALESCE(@seed_device_2_code, @seed_base_device_code), COALESCE(@seed_device_2_name, @seed_base_device_name), COALESCE(@seed_device_2_type_code, @seed_base_device_type_code), COALESCE(@seed_point_2_id, @seed_base_point_id), COALESCE(@seed_point_2_code, @seed_base_point_code), COALESCE(@seed_point_2_name, @seed_base_point_name), 'communication', 'ERROR', 3, 'confirmed', 'offline', 'online', '设备最近心跳丢失，已进入通讯中断确认态。', '联调样本-通讯中断', '告警', NULL, NULL, 1, NOW() - INTERVAL 35 MINUTE, NULL, NOW() - INTERVAL 25 MINUTE, @seed_ack_by, @seed_ack_user_id, NULL, 'trace-alarm-seed-002', JSON_OBJECT('seed', TRUE, 'batch', 'alarm_record'), NOW()),
  ('ALM_SEED_20260420_003', COALESCE(@seed_rule_3_id, @seed_base_rule_id), COALESCE(@seed_device_3_id, @seed_base_device_id), COALESCE(@seed_device_3_code, @seed_base_device_code), COALESCE(@seed_device_3_name, @seed_base_device_name), COALESCE(@seed_device_3_type_code, @seed_base_device_type_code), COALESCE(@seed_point_3_id, @seed_base_point_id), COALESCE(@seed_point_3_code, @seed_base_point_code), COALESCE(@seed_point_3_name, @seed_base_point_name), 'bool_state', 'WARN', 2, 'processing', '1', '0', '设备状态位异常，正在处理中。', '联调样本-状态异常', '预警', 1.000000, 0.000000, 2, NOW() - INTERVAL 75 MINUTE, NULL, NOW() - INTERVAL 60 MINUTE, @seed_ack_by, @seed_ack_user_id, NULL, 'trace-alarm-seed-003', JSON_OBJECT('seed', TRUE, 'batch', 'alarm_record'), NOW()),
  ('ALM_SEED_20260420_004', COALESCE(@seed_rule_1_id, @seed_base_rule_id), COALESCE(@seed_device_1_id, @seed_base_device_id), COALESCE(@seed_device_1_code, @seed_base_device_code), COALESCE(@seed_device_1_name, @seed_base_device_name), COALESCE(@seed_device_1_type_code, @seed_base_device_type_code), COALESCE(@seed_point_1_id, @seed_base_point_id), COALESCE(@seed_point_1_code, @seed_base_point_code), COALESCE(@seed_point_1_name, @seed_base_point_name), 'threshold', 'WARN', 2, 'handled', '88.2', '80', '点位值越限后已处置完成。', '联调样本-高值预警', '预警', 88.200000, 80.000000, 1, NOW() - INTERVAL 3 HOUR, NOW() - INTERVAL 2 HOUR, NOW() - INTERVAL 2 HOUR, @seed_ack_by, @seed_ack_user_id, NOW() - INTERVAL 110 MINUTE, 'trace-alarm-seed-004', JSON_OBJECT('seed', TRUE, 'batch', 'alarm_record'), NOW()),
  ('ALM_SEED_20260420_005', COALESCE(@seed_rule_2_id, @seed_base_rule_id), COALESCE(@seed_device_2_id, @seed_base_device_id), COALESCE(@seed_device_2_code, @seed_base_device_code), COALESCE(@seed_device_2_name, @seed_base_device_name), COALESCE(@seed_device_2_type_code, @seed_base_device_type_code), COALESCE(@seed_point_2_id, @seed_base_point_id), COALESCE(@seed_point_2_code, @seed_base_point_code), COALESCE(@seed_point_2_name, @seed_base_point_name), 'communication', 'ERROR', 3, 'recovered', 'offline', 'online', '通讯中断告警已恢复。', '联调样本-通讯中断', '告警', NULL, NULL, 1, NOW() - INTERVAL 5 HOUR, NOW() - INTERVAL 4 HOUR, NOW() - INTERVAL 250 MINUTE, @seed_ack_by, @seed_ack_user_id, NOW() - INTERVAL 4 HOUR, 'trace-alarm-seed-005', JSON_OBJECT('seed', TRUE, 'batch', 'alarm_record'), NOW()),
  ('ALM_SEED_20260420_006', COALESCE(@seed_rule_1_id, @seed_base_rule_id), COALESCE(@seed_device_1_id, @seed_base_device_id), COALESCE(@seed_device_1_code, @seed_base_device_code), COALESCE(@seed_device_1_name, @seed_base_device_name), COALESCE(@seed_device_1_type_code, @seed_base_device_type_code), COALESCE(@seed_point_1_id, @seed_base_point_id), COALESCE(@seed_point_1_code, @seed_base_point_code), COALESCE(@seed_point_1_name, @seed_base_point_name), 'threshold', 'INFO', 1, 'handled', '76.0', '70', '点位值短时越线后已确认。', '联调样本-高值预警', '提示', 76.000000, 70.000000, 1, NOW() - INTERVAL 18 HOUR, NOW() - INTERVAL 17 HOUR, NOW() - INTERVAL 17 HOUR, @seed_ack_by, @seed_ack_user_id, NOW() - INTERVAL 16 HOUR, 'trace-alarm-seed-006', JSON_OBJECT('seed', TRUE, 'batch', 'alarm_record'), NOW()),
  ('ALM_SEED_20260420_007', COALESCE(@seed_rule_3_id, @seed_base_rule_id), COALESCE(@seed_device_3_id, @seed_base_device_id), COALESCE(@seed_device_3_code, @seed_base_device_code), COALESCE(@seed_device_3_name, @seed_base_device_name), COALESCE(@seed_device_3_type_code, @seed_base_device_type_code), COALESCE(@seed_point_3_id, @seed_base_point_id), COALESCE(@seed_point_3_code, @seed_base_point_code), COALESCE(@seed_point_3_name, @seed_base_point_name), 'bool_state', 'WARN', 2, 'recovered', '1', '0', '状态位恢复正常，告警关闭。', '联调样本-状态异常', '预警', 1.000000, 0.000000, 1, NOW() - INTERVAL 1 DAY, NOW() - INTERVAL 1 DAY + INTERVAL 20 MINUTE, NOW() - INTERVAL 1 DAY + INTERVAL 5 MINUTE, @seed_ack_by, @seed_ack_user_id, NOW() - INTERVAL 1 DAY + INTERVAL 20 MINUTE, 'trace-alarm-seed-007', JSON_OBJECT('seed', TRUE, 'batch', 'alarm_record'), NOW()),
  ('ALM_SEED_20260420_008', COALESCE(@seed_rule_2_id, @seed_base_rule_id), COALESCE(@seed_device_2_id, @seed_base_device_id), COALESCE(@seed_device_2_code, @seed_base_device_code), COALESCE(@seed_device_2_name, @seed_base_device_name), COALESCE(@seed_device_2_type_code, @seed_base_device_type_code), COALESCE(@seed_point_2_id, @seed_base_point_id), COALESCE(@seed_point_2_code, @seed_base_point_code), COALESCE(@seed_point_2_name, @seed_base_point_name), 'communication', 'ERROR', 3, 'handled', 'offline', 'online', '通讯抖动导致告警，已完成记录。', '联调样本-通讯中断', '告警', NULL, NULL, 1, NOW() - INTERVAL 2 DAY, NOW() - INTERVAL 2 DAY + INTERVAL 40 MINUTE, NOW() - INTERVAL 2 DAY + INTERVAL 10 MINUTE, @seed_ack_by, @seed_ack_user_id, NOW() - INTERVAL 2 DAY + INTERVAL 40 MINUTE, 'trace-alarm-seed-008', JSON_OBJECT('seed', TRUE, 'batch', 'alarm_record'), NOW()),
  ('ALM_SEED_20260420_009', COALESCE(@seed_rule_1_id, @seed_base_rule_id), COALESCE(@seed_device_1_id, @seed_base_device_id), COALESCE(@seed_device_1_code, @seed_base_device_code), COALESCE(@seed_device_1_name, @seed_base_device_name), COALESCE(@seed_device_1_type_code, @seed_base_device_type_code), COALESCE(@seed_point_1_id, @seed_base_point_id), COALESCE(@seed_point_1_code, @seed_base_point_code), COALESCE(@seed_point_1_name, @seed_base_point_name), 'threshold', 'WARN', 2, 'handled', '84.0', '80', '高值预警历史样本，用于近 7 天统计。', '联调样本-高值预警', '预警', 84.000000, 80.000000, 1, NOW() - INTERVAL 4 DAY, NOW() - INTERVAL 4 DAY + INTERVAL 30 MINUTE, NOW() - INTERVAL 4 DAY + INTERVAL 15 MINUTE, @seed_ack_by, @seed_ack_user_id, NOW() - INTERVAL 4 DAY + INTERVAL 30 MINUTE, 'trace-alarm-seed-009', JSON_OBJECT('seed', TRUE, 'batch', 'alarm_record'), NOW()),
  ('ALM_SEED_20260420_010', COALESCE(@seed_rule_3_id, @seed_base_rule_id), COALESCE(@seed_device_3_id, @seed_base_device_id), COALESCE(@seed_device_3_code, @seed_base_device_code), COALESCE(@seed_device_3_name, @seed_base_device_name), COALESCE(@seed_device_3_type_code, @seed_base_device_type_code), COALESCE(@seed_point_3_id, @seed_base_point_id), COALESCE(@seed_point_3_code, @seed_base_point_code), COALESCE(@seed_point_3_name, @seed_base_point_name), 'bool_state', 'INFO', 1, 'handled', '1', '0', '状态异常历史样本，用于等级分布和最近 7 天筛选。', '联调样本-状态异常', '提示', 1.000000, 0.000000, 1, NOW() - INTERVAL 6 DAY, NOW() - INTERVAL 6 DAY + INTERVAL 15 MINUTE, NOW() - INTERVAL 6 DAY + INTERVAL 3 MINUTE, @seed_ack_by, @seed_ack_user_id, NOW() - INTERVAL 6 DAY + INTERVAL 15 MINUTE, 'trace-alarm-seed-010', JSON_OBJECT('seed', TRUE, 'batch', 'alarm_record'), NOW())
ON DUPLICATE KEY UPDATE
  `rule_id` = VALUES(`rule_id`),
  `device_id` = VALUES(`device_id`),
  `device_code` = VALUES(`device_code`),
  `device_name` = VALUES(`device_name`),
  `device_type_code` = VALUES(`device_type_code`),
  `point_id` = VALUES(`point_id`),
  `point_code` = VALUES(`point_code`),
  `point_name` = VALUES(`point_name`),
  `alarm_type` = VALUES(`alarm_type`),
  `level_code` = VALUES(`level_code`),
  `severity` = VALUES(`severity`),
  `status` = VALUES(`status`),
  `alarm_value` = VALUES(`alarm_value`),
  `threshold_value` = VALUES(`threshold_value`),
  `content` = VALUES(`content`),
  `rule_name_snapshot` = VALUES(`rule_name_snapshot`),
  `level_name_snapshot` = VALUES(`level_name_snapshot`),
  `alarm_value_num` = VALUES(`alarm_value_num`),
  `threshold_value_num` = VALUES(`threshold_value_num`),
  `occur_count` = VALUES(`occur_count`),
  `start_time` = VALUES(`start_time`),
  `end_time` = VALUES(`end_time`),
  `ack_time` = VALUES(`ack_time`),
  `ack_by` = VALUES(`ack_by`),
  `ack_user_id` = VALUES(`ack_user_id`),
  `recover_time` = VALUES(`recover_time`),
  `trace_id` = VALUES(`trace_id`),
  `extra_json` = VALUES(`extra_json`),
  `create_time` = VALUES(`create_time`);

SET @seed_record_1_id = (SELECT `id` FROM `alarm_record` WHERE `alarm_no` = 'ALM_SEED_20260420_001' LIMIT 1);
SET @seed_record_2_id = (SELECT `id` FROM `alarm_record` WHERE `alarm_no` = 'ALM_SEED_20260420_002' LIMIT 1);
SET @seed_record_3_id = (SELECT `id` FROM `alarm_record` WHERE `alarm_no` = 'ALM_SEED_20260420_003' LIMIT 1);

INSERT INTO `alarm_active`
(`record_id`, `rule_id`, `device_id`, `device_code`, `device_name`, `device_type_code`, `point_id`, `point_name`, `alarm_type`, `level_code`, `severity`, `status`, `content`, `alarm_value`, `start_time`, `latest_time`, `ack_time`, `ack_by`, `extra_json`, `create_time`, `update_time`)
SELECT @seed_record_1_id, COALESCE(@seed_rule_1_id, @seed_base_rule_id), COALESCE(@seed_device_1_id, @seed_base_device_id), COALESCE(@seed_device_1_code, @seed_base_device_code), COALESCE(@seed_device_1_name, @seed_base_device_name), COALESCE(@seed_device_1_type_code, @seed_base_device_type_code), COALESCE(@seed_point_1_id, @seed_base_point_id), COALESCE(@seed_point_1_name, @seed_base_point_name), 'threshold', 'WARN', 2, 'recorded', '点位值持续高于预警阈值，等待确认。', '92.5', NOW() - INTERVAL 10 MINUTE, NOW() - INTERVAL 5 MINUTE, NULL, '', JSON_OBJECT('seed', TRUE, 'active', TRUE), NOW(), NOW()
FROM DUAL
WHERE @seed_record_1_id IS NOT NULL
UNION ALL
SELECT @seed_record_2_id, COALESCE(@seed_rule_2_id, @seed_base_rule_id), COALESCE(@seed_device_2_id, @seed_base_device_id), COALESCE(@seed_device_2_code, @seed_base_device_code), COALESCE(@seed_device_2_name, @seed_base_device_name), COALESCE(@seed_device_2_type_code, @seed_base_device_type_code), COALESCE(@seed_point_2_id, @seed_base_point_id), COALESCE(@seed_point_2_name, @seed_base_point_name), 'communication', 'ERROR', 3, 'confirmed', '设备最近心跳丢失，已进入通讯中断确认态。', 'offline', NOW() - INTERVAL 35 MINUTE, NOW() - INTERVAL 20 MINUTE, NOW() - INTERVAL 25 MINUTE, @seed_ack_by, JSON_OBJECT('seed', TRUE, 'active', TRUE), NOW(), NOW()
FROM DUAL
WHERE @seed_record_2_id IS NOT NULL
UNION ALL
SELECT @seed_record_3_id, COALESCE(@seed_rule_3_id, @seed_base_rule_id), COALESCE(@seed_device_3_id, @seed_base_device_id), COALESCE(@seed_device_3_code, @seed_base_device_code), COALESCE(@seed_device_3_name, @seed_base_device_name), COALESCE(@seed_device_3_type_code, @seed_base_device_type_code), COALESCE(@seed_point_3_id, @seed_base_point_id), COALESCE(@seed_point_3_name, @seed_base_point_name), 'bool_state', 'WARN', 2, 'processing', '设备状态位异常，正在处理中。', '1', NOW() - INTERVAL 75 MINUTE, NOW() - INTERVAL 15 MINUTE, NOW() - INTERVAL 60 MINUTE, @seed_ack_by, JSON_OBJECT('seed', TRUE, 'active', TRUE), NOW(), NOW()
FROM DUAL
WHERE @seed_record_3_id IS NOT NULL
ON DUPLICATE KEY UPDATE
  `rule_id` = VALUES(`rule_id`),
  `device_id` = VALUES(`device_id`),
  `device_code` = VALUES(`device_code`),
  `device_name` = VALUES(`device_name`),
  `device_type_code` = VALUES(`device_type_code`),
  `point_id` = VALUES(`point_id`),
  `point_name` = VALUES(`point_name`),
  `alarm_type` = VALUES(`alarm_type`),
  `level_code` = VALUES(`level_code`),
  `severity` = VALUES(`severity`),
  `status` = VALUES(`status`),
  `content` = VALUES(`content`),
  `alarm_value` = VALUES(`alarm_value`),
  `start_time` = VALUES(`start_time`),
  `latest_time` = VALUES(`latest_time`),
  `ack_time` = VALUES(`ack_time`),
  `ack_by` = VALUES(`ack_by`),
  `extra_json` = VALUES(`extra_json`),
  `update_time` = CURRENT_TIMESTAMP;

INSERT INTO `operation_log`
(`log_time`, `operator_id`, `operator_name`, `module_name`, `action_name`, `level`, `content`, `detail_json`, `request_id`, `ip`, `user_agent`, `biz_type`, `biz_id`, `duration_ms`, `result_status`, `create_time`)
SELECT
  `seed`.`log_time`,
  `seed`.`operator_id`,
  `seed`.`operator_name`,
  `seed`.`module_name`,
  `seed`.`action_name`,
  `seed`.`level`,
  `seed`.`content`,
  `seed`.`detail_json`,
  `seed`.`request_id`,
  `seed`.`ip`,
  `seed`.`user_agent`,
  `seed`.`biz_type`,
  `seed`.`biz_id`,
  `seed`.`duration_ms`,
  `seed`.`result_status`,
  `seed`.`create_time`
FROM (
  SELECT NOW() - INTERVAL 5 MINUTE AS `log_time`, @seed_admin_user_id AS `operator_id`, 'Admin' AS `operator_name`, 'auth' AS `module_name`, 'login' AS `action_name`, 'INFO' AS `level`, '管理员登录成功。' AS `content`, JSON_OBJECT('page', '/auth/login') AS `detail_json`, 'req-op-seed-001' AS `request_id`, '127.0.0.1' AS `ip`, 'browser' AS `user_agent`, 'login' AS `biz_type`, 'auth-login' AS `biz_id`, 45 AS `duration_ms`, 'success' AS `result_status`, NOW() AS `create_time`
  UNION ALL
  SELECT NOW() - INTERVAL 12 MINUTE, @seed_admin_user_id, 'Admin', 'dashboard', 'view', 'INFO', '查看工作台概览。', JSON_OBJECT('page', '/device-monitor/dashboard'), 'req-op-seed-002', '127.0.0.1', 'browser', 'page', 'dashboard', 32, 'success', NOW()
  UNION ALL
  SELECT NOW() - INTERVAL 20 MINUTE, @seed_super_user_id, 'Super', 'alarm_rule', 'create', 'INFO', '新增联调样本告警规则。', JSON_OBJECT('ruleCode', 'SEED_ALARM_RULE_001'), 'req-op-seed-003', '127.0.0.1', 'browser', 'alarm_rule', 'SEED_ALARM_RULE_001', 68, 'success', NOW()
  UNION ALL
  SELECT NOW() - INTERVAL 30 MINUTE, @seed_admin_user_id, 'Admin', 'device', 'update', 'WARN', '修改设备通讯参数并触发联调验证。', JSON_OBJECT('deviceCode', @seed_base_device_code), 'req-op-seed-004', '127.0.0.1', 'browser', 'device', COALESCE(@seed_base_device_code, 'seed-device'), 93, 'success', NOW()
  UNION ALL
  SELECT NOW() - INTERVAL 45 MINUTE, @seed_user_user_id, 'User', 'log', 'query', 'INFO', '按时间范围查询系统日志。', JSON_OBJECT('range', 'last24h'), 'req-op-seed-005', '127.0.0.1', 'browser', 'log', 'query-last24h', 40, 'success', NOW()
  UNION ALL
  SELECT NOW() - INTERVAL 90 MINUTE, @seed_super_user_id, 'Super', 'alarm_record', 'ack', 'INFO', '确认通讯中断告警。', JSON_OBJECT('alarmNo', 'ALM_SEED_20260420_002'), 'req-op-seed-006', '127.0.0.1', 'browser', 'alarm', 'ALM_SEED_20260420_002', 55, 'success', NOW()
  UNION ALL
  SELECT NOW() - INTERVAL 4 HOUR, @seed_admin_user_id, 'Admin', 'alarm_record', 'handle', 'WARN', '处置高值预警并记录处理意见。', JSON_OBJECT('alarmNo', 'ALM_SEED_20260420_004'), 'req-op-seed-007', '127.0.0.1', 'browser', 'alarm', 'ALM_SEED_20260420_004', 88, 'success', NOW()
  UNION ALL
  SELECT NOW() - INTERVAL 1 DAY, @seed_super_user_id, 'Super', 'user', 'password_reset', 'INFO', '重置联调账号密码。', JSON_OBJECT('targetUser', 'User'), 'req-op-seed-008', '127.0.0.1', 'browser', 'user', 'User', 70, 'success', NOW()
  UNION ALL
  SELECT NOW() - INTERVAL 2 DAY, @seed_admin_user_id, 'Admin', 'alarm_rule', 'disable', 'WARN', '临时停用一条告警规则进行验证。', JSON_OBJECT('ruleCode', 'SEED_ALARM_RULE_003'), 'req-op-seed-009', '127.0.0.1', 'browser', 'alarm_rule', 'SEED_ALARM_RULE_003', 64, 'success', NOW()
  UNION ALL
  SELECT NOW() - INTERVAL 6 DAY, @seed_super_user_id, 'Super', 'auth', 'logout', 'INFO', '联调结束后退出系统。', JSON_OBJECT('page', '/device-monitor/log'), 'req-op-seed-010', '127.0.0.1', 'browser', 'logout', 'auth-logout', 18, 'success', NOW()
) AS `seed`
LEFT JOIN `operation_log` AS `existing`
  ON `existing`.`request_id` = `seed`.`request_id`
WHERE `existing`.`id` IS NULL;

INSERT INTO `system_log`
(`log_time`, `level`, `log_type`, `source`, `device_id`, `device_code`, `point_id`, `host_name`, `service_name`, `content`, `detail_json`, `trace_id`, `exception_stack`, `elapsed_ms`, `create_time`)
SELECT
  `seed`.`log_time`,
  `seed`.`level`,
  `seed`.`log_type`,
  `seed`.`source`,
  `seed`.`device_id`,
  `seed`.`device_code`,
  `seed`.`point_id`,
  `seed`.`host_name`,
  `seed`.`service_name`,
  `seed`.`content`,
  `seed`.`detail_json`,
  `seed`.`trace_id`,
  `seed`.`exception_stack`,
  `seed`.`elapsed_ms`,
  `seed`.`create_time`
FROM (
  SELECT NOW() - INTERVAL 3 MINUTE AS `log_time`, 'INFO' AS `level`, 'system' AS `log_type`, 'backend-api' AS `source`, NULL AS `device_id`, '' AS `device_code`, NULL AS `point_id`, 'localhost' AS `host_name`, 'backend' AS `service_name`, 'Dashboard 概览接口调用成功。' AS `content`, JSON_OBJECT('endpoint', '/api/device-monitor/dashboard/overview') AS `detail_json`, 'trace-sys-seed-001' AS `trace_id`, NULL AS `exception_stack`, 28 AS `elapsed_ms`, NOW() AS `create_time`
  UNION ALL
  SELECT NOW() - INTERVAL 6 MINUTE, 'WARN', 'collect', 'collector-scheduler', @seed_base_device_id, @seed_base_device_code, @seed_base_point_id, 'localhost', 'collector', '设备采集周期发生轻微延迟。', JSON_OBJECT('deviceCode', @seed_base_device_code, 'delayMs', 1350), 'trace-sys-seed-002', NULL, 1350, NOW()
  UNION ALL
  SELECT NOW() - INTERVAL 9 MINUTE, 'ERROR', 'alarm', 'alarm-engine', @seed_base_device_id, @seed_base_device_code, @seed_base_point_id, 'localhost', 'backend', '通讯中断告警触发。', JSON_OBJECT('alarmNo', 'ALM_SEED_20260420_002'), 'trace-sys-seed-003', 'connect timeout', 120, NOW()
  UNION ALL
  SELECT NOW() - INTERVAL 12 MINUTE, 'INFO', 'storage', 'tdengine-writer', @seed_base_device_id, @seed_base_device_code, @seed_base_point_id, 'localhost', 'collector', '时序数据写入成功。', JSON_OBJECT('batchSize', 64), 'trace-sys-seed-004', NULL, 36, NOW()
  UNION ALL
  SELECT NOW() - INTERVAL 18 MINUTE, 'INFO', 'collect', 'collector-scheduler', @seed_base_device_id, @seed_base_device_code, @seed_base_point_id, 'localhost', 'collector', '设备心跳恢复正常。', JSON_OBJECT('status', 'online'), 'trace-sys-seed-005', NULL, 42, NOW()
  UNION ALL
  SELECT NOW() - INTERVAL 26 MINUTE, 'WARN', 'alarm', 'alarm-engine', @seed_base_device_id, @seed_base_device_code, @seed_base_point_id, 'localhost', 'backend', '高值预警仍未恢复。', JSON_OBJECT('alarmNo', 'ALM_SEED_20260420_001'), 'trace-sys-seed-006', NULL, 52, NOW()
  UNION ALL
  SELECT NOW() - INTERVAL 40 MINUTE, 'INFO', 'system', 'backend-api', NULL, '', NULL, 'localhost', 'backend', '日志列表查询完成。', JSON_OBJECT('endpoint', '/api/device-monitor/log/list'), 'trace-sys-seed-007', NULL, 24, NOW()
  UNION ALL
  SELECT NOW() - INTERVAL 55 MINUTE, 'ERROR', 'storage', 'tdengine-writer', @seed_base_device_id, @seed_base_device_code, @seed_base_point_id, 'localhost', 'collector', '写入时序库失败，已进入重试。', JSON_OBJECT('retry', 1), 'trace-sys-seed-008', 'write batch failed', 215, NOW()
  UNION ALL
  SELECT NOW() - INTERVAL 70 MINUTE, 'WARN', 'collect', 'collector-scheduler', @seed_base_device_id, @seed_base_device_code, @seed_base_point_id, 'localhost', 'collector', '设备采集值超过预警阈值。', JSON_OBJECT('value', '92.5', 'threshold', '80'), 'trace-sys-seed-009', NULL, 48, NOW()
  UNION ALL
  SELECT NOW() - INTERVAL 95 MINUTE, 'INFO', 'alarm', 'alarm-engine', @seed_base_device_id, @seed_base_device_code, @seed_base_point_id, 'localhost', 'backend', '活动告警快照刷新成功。', JSON_OBJECT('activeTotal', 3), 'trace-sys-seed-010', NULL, 21, NOW()
  UNION ALL
  SELECT NOW() - INTERVAL 3 HOUR, 'INFO', 'system', 'backend-api', NULL, '', NULL, 'localhost', 'backend', '工作台趋势接口调用成功。', JSON_OBJECT('endpoint', '/api/device-monitor/dashboard/trend'), 'trace-sys-seed-011', NULL, 27, NOW()
  UNION ALL
  SELECT NOW() - INTERVAL 4 HOUR, 'WARN', 'storage', 'tdengine-writer', @seed_base_device_id, @seed_base_device_code, @seed_base_point_id, 'localhost', 'collector', '历史数据批量写入耗时偏高。', JSON_OBJECT('elapsedMs', 1800), 'trace-sys-seed-012', NULL, 1800, NOW()
  UNION ALL
  SELECT NOW() - INTERVAL 8 HOUR, 'INFO', 'collect', 'collector-scheduler', @seed_base_device_id, @seed_base_device_code, @seed_base_point_id, 'localhost', 'collector', '采集任务轮询完成。', JSON_OBJECT('deviceCode', @seed_base_device_code), 'trace-sys-seed-013', NULL, 49, NOW()
  UNION ALL
  SELECT NOW() - INTERVAL 12 HOUR, 'ERROR', 'alarm', 'alarm-engine', @seed_base_device_id, @seed_base_device_code, @seed_base_point_id, 'localhost', 'backend', '状态异常告警进入处理中。', JSON_OBJECT('alarmNo', 'ALM_SEED_20260420_003'), 'trace-sys-seed-014', 'state abnormal', 77, NOW()
  UNION ALL
  SELECT NOW() - INTERVAL 18 HOUR, 'INFO', 'storage', 'tdengine-writer', @seed_base_device_id, @seed_base_device_code, @seed_base_point_id, 'localhost', 'collector', '离线设备历史数据归档完成。', JSON_OBJECT('archivedRows', 128), 'trace-sys-seed-015', NULL, 66, NOW()
  UNION ALL
  SELECT NOW() - INTERVAL 1 DAY, 'INFO', 'system', 'backend-api', NULL, '', NULL, 'localhost', 'backend', '前后端联调 smoke 验证通过。', JSON_OBJECT('stage', 'dashboard-log'), 'trace-sys-seed-016', NULL, 30, NOW()
  UNION ALL
  SELECT NOW() - INTERVAL 2 DAY, 'WARN', 'collect', 'collector-scheduler', @seed_base_device_id, @seed_base_device_code, @seed_base_point_id, 'localhost', 'collector', '设备心跳出现抖动，已自动重连。', JSON_OBJECT('reconnect', 2), 'trace-sys-seed-017', NULL, 95, NOW()
  UNION ALL
  SELECT NOW() - INTERVAL 3 DAY, 'INFO', 'alarm', 'alarm-engine', @seed_base_device_id, @seed_base_device_code, @seed_base_point_id, 'localhost', 'backend', '通讯中断告警已恢复。', JSON_OBJECT('alarmNo', 'ALM_SEED_20260420_005'), 'trace-sys-seed-018', NULL, 35, NOW()
  UNION ALL
  SELECT NOW() - INTERVAL 4 DAY, 'ERROR', 'storage', 'tdengine-writer', @seed_base_device_id, @seed_base_device_code, @seed_base_point_id, 'localhost', 'collector', '批量写入出现异常后恢复。', JSON_OBJECT('retry', 3), 'trace-sys-seed-019', 'temporary storage pressure', 260, NOW()
  UNION ALL
  SELECT NOW() - INTERVAL 6 DAY, 'INFO', 'system', 'backend-api', NULL, '', NULL, 'localhost', 'backend', '系统运行日志种子数据已准备完成。', JSON_OBJECT('seed', TRUE), 'trace-sys-seed-020', NULL, 19, NOW()
) AS `seed`
LEFT JOIN `system_log` AS `existing`
  ON `existing`.`trace_id` = `seed`.`trace_id`
WHERE `existing`.`id` IS NULL;
