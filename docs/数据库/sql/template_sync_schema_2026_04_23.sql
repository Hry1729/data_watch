USE `device_watch`;

SET NAMES utf8mb4;

-- Prerequisite: core business tables (`device_type`, `device`, `device_variable`) already exist.

CREATE TABLE IF NOT EXISTS `unit_dict` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `unit_code` VARCHAR(32) NOT NULL,
  `unit_symbol` VARCHAR(20) NOT NULL DEFAULT '',
  `unit_name` VARCHAR(50) NOT NULL,
  `unit_category` VARCHAR(32) NOT NULL DEFAULT '',
  `description` VARCHAR(255) NOT NULL DEFAULT '',
  `create_user` VARCHAR(20) NULL DEFAULT NULL,
  `create_time` DATETIME NULL DEFAULT CURRENT_TIMESTAMP,
  `update_user` VARCHAR(20) NULL DEFAULT NULL,
  `update_time` DATETIME NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_unit_dict_unit_code` (`unit_code`),
  UNIQUE KEY `uq_unit_dict_unit_symbol` (`unit_symbol`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='计量单位字典表';

CREATE TABLE IF NOT EXISTS `variable_template` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `template_code` VARCHAR(50) NOT NULL,
  `template_name` VARCHAR(100) NOT NULL,
  `device_type_id` BIGINT NOT NULL,
  `version_no` INT NOT NULL DEFAULT 1,
  `enabled` TINYINT(1) NOT NULL DEFAULT 1,
  `description` VARCHAR(255) NOT NULL DEFAULT '',
  `create_user` VARCHAR(20) NULL DEFAULT NULL,
  `create_time` DATETIME NULL DEFAULT CURRENT_TIMESTAMP,
  `update_user` VARCHAR(20) NULL DEFAULT NULL,
  `update_time` DATETIME NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_variable_template_template_code` (`template_code`),
  KEY `idx_variable_template_device_type_id` (`device_type_id`),
  KEY `idx_variable_template_enabled` (`enabled`),
  CONSTRAINT `fk_variable_template_device_type` FOREIGN KEY (`device_type_id`) REFERENCES `device_type` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='点位模板主表';

CREATE TABLE IF NOT EXISTS `template_variable` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `template_id` BIGINT NOT NULL,
  `var_seq` INT NOT NULL COMMENT '点位序号，纯序号；TDengine 子表名由 device_code + _ + var_seq 派生',
  `var_name` VARCHAR(50) NOT NULL,
  `io_type` VARCHAR(10) NOT NULL,
  `var_type` VARCHAR(20) NOT NULL,
  `var_address` VARCHAR(20) NOT NULL,
  `var_offset` INT NULL DEFAULT NULL,
  `var_bit` INT NULL DEFAULT NULL,
  `unit_id` BIGINT NULL DEFAULT NULL,
  `description` VARCHAR(255) NULL DEFAULT NULL,
  `scan_rate` INT NULL DEFAULT NULL,
  `is_alarm` TINYINT NOT NULL,
  `is_storage` TINYINT NOT NULL,
  `create_user` VARCHAR(20) NULL DEFAULT NULL,
  `create_time` DATETIME NULL DEFAULT NULL,
  `update_user` VARCHAR(20) NULL DEFAULT NULL,
  `update_time` DATETIME NULL DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_template_variable_template_var_seq` (`template_id`, `var_seq`),
  UNIQUE KEY `uq_template_variable_template_var_address` (`template_id`, `var_address`),
  KEY `idx_template_variable_template_id` (`template_id`),
  KEY `idx_template_variable_scan_rate` (`scan_rate`),
  KEY `idx_template_variable_is_alarm` (`is_alarm`),
  KEY `idx_template_variable_unit_id` (`unit_id`),
  CONSTRAINT `fk_template_variable_template` FOREIGN KEY (`template_id`) REFERENCES `variable_template` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_template_variable_unit` FOREIGN KEY (`unit_id`) REFERENCES `unit_dict` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='模板点位表（与 device_variable 业务字段同构）';

CREATE TABLE IF NOT EXISTS `cross_db_task` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `task_no` VARCHAR(64) NOT NULL,
  `biz_type` VARCHAR(32) NOT NULL,
  `biz_id` BIGINT NOT NULL,
  `action_type` VARCHAR(64) NOT NULL,
  `payload_json` JSON NOT NULL,
  `status` VARCHAR(20) NOT NULL DEFAULT 'pending',
  `retry_count` INT NOT NULL DEFAULT 0,
  `next_retry_time` DATETIME NULL DEFAULT NULL,
  `last_error` VARCHAR(1000) NULL DEFAULT NULL,
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_cross_db_task_task_no` (`task_no`),
  KEY `idx_cross_db_task_biz` (`biz_type`, `biz_id`),
  KEY `idx_cross_db_task_status_retry` (`status`, `next_retry_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='跨库补偿任务表';

ALTER TABLE `device`
  ADD COLUMN `template_id` BIGINT NULL DEFAULT NULL COMMENT '绑定模板ID' AFTER `type_id`,
  ADD COLUMN `template_last_sync_version` INT NULL DEFAULT NULL COMMENT '最后处理过的模板版本号' AFTER `scan_cycle`,
  ADD COLUMN `template_sync_time` DATETIME NULL DEFAULT NULL COMMENT '最近一次模板同步时间' AFTER `template_last_sync_version`,
  ADD KEY `idx_device_template_id` (`template_id`),
  ADD CONSTRAINT `fk_device_template` FOREIGN KEY (`template_id`) REFERENCES `variable_template` (`id`) ON DELETE SET NULL;

ALTER TABLE `device_variable`
  ADD COLUMN `var_seq` INT NULL DEFAULT NULL COMMENT '点位序号，对应 TDengine var_seq' AFTER `device_id`,
  ADD COLUMN `unit_id` BIGINT NULL DEFAULT NULL COMMENT '关联单位ID，允许为空' AFTER `var_bit`,
  ADD KEY `idx_device_variable_var_seq` (`var_seq`),
  ADD KEY `idx_device_variable_unit_id` (`unit_id`),
  ADD CONSTRAINT `fk_device_variable_unit` FOREIGN KEY (`unit_id`) REFERENCES `unit_dict` (`id`) ON DELETE SET NULL;

DROP TRIGGER IF EXISTS `trg_device_code_no_update`;
DELIMITER //
CREATE TRIGGER `trg_device_code_no_update`
BEFORE UPDATE ON `device`
FOR EACH ROW
BEGIN
  IF NEW.`device_code` <> OLD.`device_code` THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'device_code 创建后不可修改';
  END IF;
END//
DELIMITER ;
