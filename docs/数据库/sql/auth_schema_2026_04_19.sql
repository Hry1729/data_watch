USE `device_watch`;

SET NAMES utf8mb4;

CREATE TABLE IF NOT EXISTS `sys_user` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `user_name` VARCHAR(64) NOT NULL,
  `password_hash` VARCHAR(255) NOT NULL,
  `nick_name` VARCHAR(64) NOT NULL DEFAULT '',
  `email` VARCHAR(128) NOT NULL DEFAULT '',
  `phone` VARCHAR(32) NOT NULL DEFAULT '',
  `gender` VARCHAR(16) NOT NULL DEFAULT 'unknown',
  `avatar` VARCHAR(255) NOT NULL DEFAULT '',
  `status` VARCHAR(16) NOT NULL DEFAULT '1',
  `last_login_time` DATETIME NULL DEFAULT NULL,
  `last_login_ip` VARCHAR(64) NOT NULL DEFAULT '',
  `create_by` VARCHAR(64) NOT NULL DEFAULT 'system',
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `update_by` VARCHAR(64) NOT NULL DEFAULT 'system',
  `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `deleted` TINYINT(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_sys_user_user_name` (`user_name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='system users';

CREATE TABLE IF NOT EXISTS `sys_role` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `role_code` VARCHAR(64) NOT NULL,
  `role_name` VARCHAR(64) NOT NULL,
  `description` VARCHAR(255) NOT NULL DEFAULT '',
  `enabled` TINYINT(1) NOT NULL DEFAULT 1,
  `sort` INT NOT NULL DEFAULT 1,
  `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_sys_role_role_code` (`role_code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='system roles';

CREATE TABLE IF NOT EXISTS `sys_user_role` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `user_id` BIGINT NOT NULL,
  `role_id` BIGINT NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_sys_user_role_user_role` (`user_id`, `role_id`),
  KEY `idx_sys_user_role_user_id` (`user_id`),
  KEY `idx_sys_user_role_role_id` (`role_id`),
  CONSTRAINT `fk_sys_user_role_user` FOREIGN KEY (`user_id`) REFERENCES `sys_user` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_sys_user_role_role` FOREIGN KEY (`role_id`) REFERENCES `sys_role` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='user role mapping';

CREATE TABLE IF NOT EXISTS `sys_menu` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `parent_id` BIGINT NULL DEFAULT NULL,
  `name` VARCHAR(128) NOT NULL,
  `path` VARCHAR(255) NOT NULL DEFAULT '',
  `component` VARCHAR(255) NOT NULL DEFAULT '',
  `title_key` VARCHAR(255) NOT NULL,
  `icon` VARCHAR(128) NOT NULL DEFAULT '',
  `menu_type` VARCHAR(32) NOT NULL DEFAULT 'menu',
  `link` VARCHAR(255) NOT NULL DEFAULT '',
  `is_iframe` TINYINT(1) NOT NULL DEFAULT 0,
  `keep_alive` TINYINT(1) NOT NULL DEFAULT 0,
  `is_hide` TINYINT(1) NOT NULL DEFAULT 0,
  `is_hide_tab` TINYINT(1) NOT NULL DEFAULT 0,
  `fixed_tab` TINYINT(1) NOT NULL DEFAULT 0,
  `active_path` VARCHAR(255) NOT NULL DEFAULT '',
  `is_full_page` TINYINT(1) NOT NULL DEFAULT 0,
  `sort` INT NOT NULL DEFAULT 1,
  `enabled` TINYINT(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_sys_menu_name` (`name`),
  KEY `idx_sys_menu_parent_id` (`parent_id`),
  KEY `idx_sys_menu_sort` (`sort`),
  CONSTRAINT `fk_sys_menu_parent` FOREIGN KEY (`parent_id`) REFERENCES `sys_menu` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='system menus';

CREATE TABLE IF NOT EXISTS `sys_permission` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `permission_code` VARCHAR(128) NOT NULL,
  `permission_name` VARCHAR(128) NOT NULL,
  `permission_type` VARCHAR(32) NOT NULL DEFAULT 'action',
  `menu_id` BIGINT NULL DEFAULT NULL,
  `auth_mark` VARCHAR(128) NOT NULL DEFAULT '',
  `description` TEXT NOT NULL,
  `sort` INT NOT NULL DEFAULT 1,
  `enabled` TINYINT(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_sys_permission_permission_code` (`permission_code`),
  KEY `idx_sys_permission_menu_id` (`menu_id`),
  KEY `idx_sys_permission_type` (`permission_type`),
  CONSTRAINT `fk_sys_permission_menu` FOREIGN KEY (`menu_id`) REFERENCES `sys_menu` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='system permissions';

CREATE TABLE IF NOT EXISTS `sys_role_menu` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `role_id` BIGINT NOT NULL,
  `menu_id` BIGINT NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_sys_role_menu_role_menu` (`role_id`, `menu_id`),
  KEY `idx_sys_role_menu_role_id` (`role_id`),
  KEY `idx_sys_role_menu_menu_id` (`menu_id`),
  CONSTRAINT `fk_sys_role_menu_role` FOREIGN KEY (`role_id`) REFERENCES `sys_role` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_sys_role_menu_menu` FOREIGN KEY (`menu_id`) REFERENCES `sys_menu` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='role menu mapping';

CREATE TABLE IF NOT EXISTS `sys_role_permission` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `role_id` BIGINT NOT NULL,
  `permission_id` BIGINT NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_sys_role_permission_role_permission` (`role_id`, `permission_id`),
  KEY `idx_sys_role_permission_role_id` (`role_id`),
  KEY `idx_sys_role_permission_permission_id` (`permission_id`),
  CONSTRAINT `fk_sys_role_permission_role` FOREIGN KEY (`role_id`) REFERENCES `sys_role` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_sys_role_permission_permission` FOREIGN KEY (`permission_id`) REFERENCES `sys_permission` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='role permission mapping';

CREATE TABLE IF NOT EXISTS `sys_data_permission` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `role_id` BIGINT NOT NULL,
  `scope_type` VARCHAR(32) NOT NULL DEFAULT 'all',
  `scope_value` VARCHAR(128) NOT NULL DEFAULT '*',
  `enabled` TINYINT(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`),
  KEY `idx_sys_data_permission_role_id` (`role_id`),
  KEY `idx_sys_data_permission_scope` (`scope_type`, `scope_value`),
  CONSTRAINT `fk_sys_data_permission_role` FOREIGN KEY (`role_id`) REFERENCES `sys_role` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='role data scope skeleton';
