-- Auth migration baseline for device_watch
-- Generated on 2026-04-19
-- This file is self-contained: schema + built-in seed

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

USE `device_watch`;

SET NAMES utf8mb4;

SET @seed_password_hash = 'pbkdf2_sha256$600000$seed_super_admin_user_20260419$9eaca4b33c222bd74e314eed5e1c18fa0c5b7beb9fe46d6a51fc1d21b6495161';

INSERT INTO `sys_role` (`role_code`, `role_name`, `description`, `enabled`, `sort`)
VALUES
  ('R_SUPER', '超级管理员', '系统内置超级管理员，拥有全部权限', 1, 1),
  ('R_ADMIN', '运维管理员', '设备监控业务管理员，拥有设备监控和用户管理权限', 1, 2),
  ('R_USER', '监控用户', '普通监控账号，仅保留只读查看能力', 1, 3)
ON DUPLICATE KEY UPDATE
  `role_name` = VALUES(`role_name`),
  `description` = VALUES(`description`),
  `enabled` = VALUES(`enabled`),
  `sort` = VALUES(`sort`);

INSERT INTO `sys_user`
(`user_name`, `password_hash`, `nick_name`, `email`, `phone`, `gender`, `avatar`, `status`, `last_login_ip`, `create_by`, `update_by`, `deleted`)
VALUES
  ('Super', @seed_password_hash, '超级管理员', 'super@device-monitor.local', '13800000001', 'unknown', '', '1', '', 'bootstrap', 'bootstrap', 0),
  ('Admin', @seed_password_hash, '运维管理员', 'admin@device-monitor.local', '13800000002', 'unknown', '', '1', '', 'bootstrap', 'bootstrap', 0),
  ('User',  @seed_password_hash, '监控用户',   'user@device-monitor.local',  '13800000003', 'unknown', '', '1', '', 'bootstrap', 'bootstrap', 0)
ON DUPLICATE KEY UPDATE
  `password_hash` = VALUES(`password_hash`),
  `nick_name` = VALUES(`nick_name`),
  `email` = VALUES(`email`),
  `phone` = VALUES(`phone`),
  `gender` = VALUES(`gender`),
  `avatar` = VALUES(`avatar`),
  `status` = VALUES(`status`),
  `update_by` = VALUES(`update_by`),
  `deleted` = VALUES(`deleted`);

INSERT INTO `sys_menu`
(`parent_id`, `name`, `path`, `component`, `title_key`, `icon`, `menu_type`, `link`, `is_iframe`, `keep_alive`, `is_hide`, `is_hide_tab`, `fixed_tab`, `active_path`, `is_full_page`, `sort`, `enabled`)
VALUES
  (NULL, 'DeviceMonitor', '/device-monitor', '/index/index', 'menus.deviceMonitor.title', 'ri:cpu-line', 'directory', '', 0, 0, 0, 0, 0, '', 0, 10, 1),
  (NULL, 'System', '/system', '/index/index', 'menus.system.title', 'ri:user-3-line', 'directory', '', 0, 0, 0, 0, 0, '', 0, 90, 1)
ON DUPLICATE KEY UPDATE
  `parent_id` = VALUES(`parent_id`),
  `path` = VALUES(`path`),
  `component` = VALUES(`component`),
  `title_key` = VALUES(`title_key`),
  `icon` = VALUES(`icon`),
  `menu_type` = VALUES(`menu_type`),
  `link` = VALUES(`link`),
  `is_iframe` = VALUES(`is_iframe`),
  `keep_alive` = VALUES(`keep_alive`),
  `is_hide` = VALUES(`is_hide`),
  `is_hide_tab` = VALUES(`is_hide_tab`),
  `fixed_tab` = VALUES(`fixed_tab`),
  `active_path` = VALUES(`active_path`),
  `is_full_page` = VALUES(`is_full_page`),
  `sort` = VALUES(`sort`),
  `enabled` = VALUES(`enabled`);

SET @menu_device_monitor = (SELECT `id` FROM `sys_menu` WHERE `name` = 'DeviceMonitor');
SET @menu_system = (SELECT `id` FROM `sys_menu` WHERE `name` = 'System');

INSERT INTO `sys_menu`
(`parent_id`, `name`, `path`, `component`, `title_key`, `icon`, `menu_type`, `link`, `is_iframe`, `keep_alive`, `is_hide`, `is_hide_tab`, `fixed_tab`, `active_path`, `is_full_page`, `sort`, `enabled`)
VALUES
  (@menu_device_monitor, 'MonitorDashboard', 'dashboard', '/device_monitor/dashboard/index', 'menus.deviceMonitor.dashboard', 'ri:dashboard-line', 'menu', '', 0, 1, 0, 0, 1, '', 0, 1, 1),
  (@menu_device_monitor, 'DriverManage', 'driver', '/device_monitor/driver/index', 'menus.deviceMonitor.driver', 'ri:plug-line', 'menu', '', 0, 1, 0, 0, 0, '', 0, 2, 1),
  (@menu_device_monitor, 'DeviceManage', 'device', '/device_monitor/device/index', 'menus.deviceMonitor.device', 'ri:server-line', 'menu', '', 0, 1, 0, 0, 0, '', 0, 3, 1),
  (@menu_device_monitor, 'PointManage', 'point', '/device_monitor/point/index', 'menus.deviceMonitor.point', 'ri:flashlight-line', 'menu', '', 0, 1, 0, 0, 0, '', 0, 4, 1),
  (@menu_device_monitor, 'RealtimeMonitor', 'realtime', '/device_monitor/realtime/index', 'menus.deviceMonitor.realtime', 'ri:pulse-line', 'menu', '', 0, 0, 0, 0, 0, '', 0, 5, 1),
  (@menu_device_monitor, 'HistoryData', 'history', '/device_monitor/history/index', 'menus.deviceMonitor.history', 'ri:line-chart-line', 'menu', '', 0, 0, 0, 0, 0, '', 0, 6, 1),
  (@menu_device_monitor, 'SystemLog', 'log', '/device_monitor/log/index', 'menus.deviceMonitor.log', 'ri:file-list-3-line', 'menu', '', 0, 1, 0, 0, 0, '', 0, 7, 1),
  (@menu_system, 'User', 'user', '/system/user', 'menus.system.user', 'ri:user-line', 'menu', '', 0, 1, 0, 0, 0, '', 0, 1, 1),
  (@menu_system, 'Role', 'role', '/system/role', 'menus.system.role', 'ri:user-settings-line', 'menu', '', 0, 1, 0, 0, 0, '', 0, 2, 1),
  (@menu_system, 'Menus', 'menu', '/system/menu', 'menus.system.menu', 'ri:menu-line', 'menu', '', 0, 1, 0, 0, 0, '', 0, 3, 1)
ON DUPLICATE KEY UPDATE
  `parent_id` = VALUES(`parent_id`),
  `path` = VALUES(`path`),
  `component` = VALUES(`component`),
  `title_key` = VALUES(`title_key`),
  `icon` = VALUES(`icon`),
  `menu_type` = VALUES(`menu_type`),
  `link` = VALUES(`link`),
  `is_iframe` = VALUES(`is_iframe`),
  `keep_alive` = VALUES(`keep_alive`),
  `is_hide` = VALUES(`is_hide`),
  `is_hide_tab` = VALUES(`is_hide_tab`),
  `fixed_tab` = VALUES(`fixed_tab`),
  `active_path` = VALUES(`active_path`),
  `is_full_page` = VALUES(`is_full_page`),
  `sort` = VALUES(`sort`),
  `enabled` = VALUES(`enabled`);

SET @menu_monitor_dashboard = (SELECT `id` FROM `sys_menu` WHERE `name` = 'MonitorDashboard');
SET @menu_driver_manage = (SELECT `id` FROM `sys_menu` WHERE `name` = 'DriverManage');
SET @menu_device_manage = (SELECT `id` FROM `sys_menu` WHERE `name` = 'DeviceManage');
SET @menu_point_manage = (SELECT `id` FROM `sys_menu` WHERE `name` = 'PointManage');
SET @menu_realtime_monitor = (SELECT `id` FROM `sys_menu` WHERE `name` = 'RealtimeMonitor');
SET @menu_history_data = (SELECT `id` FROM `sys_menu` WHERE `name` = 'HistoryData');
SET @menu_system_log = (SELECT `id` FROM `sys_menu` WHERE `name` = 'SystemLog');
SET @menu_user = (SELECT `id` FROM `sys_menu` WHERE `name` = 'User');
SET @menu_role = (SELECT `id` FROM `sys_menu` WHERE `name` = 'Role');
SET @menu_menus = (SELECT `id` FROM `sys_menu` WHERE `name` = 'Menus');

INSERT INTO `sys_permission`
(`permission_code`, `permission_name`, `permission_type`, `menu_id`, `auth_mark`, `description`, `sort`, `enabled`)
VALUES
  ('device_monitor:dashboard:view', '工作台查看', 'page', @menu_monitor_dashboard, 'device_monitor:dashboard:view', '', 10, 1),
  ('device_monitor:driver:view', '驱动管理查看', 'page', @menu_driver_manage, 'device_monitor:driver:view', '', 20, 1),
  ('device_monitor:device:view', '设备管理查看', 'page', @menu_device_manage, 'device_monitor:device:view', '', 30, 1),
  ('device_monitor:device:create', '设备新增', 'action', @menu_device_manage, 'device_monitor:device:create', '', 31, 1),
  ('device_monitor:device:update', '设备编辑', 'action', @menu_device_manage, 'device_monitor:device:update', '', 32, 1),
  ('device_monitor:device:delete', '设备删除', 'action', @menu_device_manage, 'device_monitor:device:delete', '', 33, 1),
  ('device_monitor:device:import', '设备导入', 'action', @menu_device_manage, 'device_monitor:device:import', '', 34, 1),
  ('device_monitor:device:export', '设备导出', 'action', @menu_device_manage, 'device_monitor:device:export', '', 35, 1),
  ('device_monitor:point:view', '点位管理查看', 'page', @menu_point_manage, 'device_monitor:point:view', '', 40, 1),
  ('device_monitor:point:create', '点位新增', 'action', @menu_point_manage, 'device_monitor:point:create', '', 41, 1),
  ('device_monitor:point:update', '点位编辑', 'action', @menu_point_manage, 'device_monitor:point:update', '', 42, 1),
  ('device_monitor:point:delete', '点位删除', 'action', @menu_point_manage, 'device_monitor:point:delete', '', 43, 1),
  ('device_monitor:point:import', '点位导入', 'action', @menu_point_manage, 'device_monitor:point:import', '', 44, 1),
  ('device_monitor:point:export', '点位导出', 'action', @menu_point_manage, 'device_monitor:point:export', '', 45, 1),
  ('device_monitor:realtime:view', '实时监控查看', 'page', @menu_realtime_monitor, 'device_monitor:realtime:view', '', 50, 1),
  ('device_monitor:realtime:subscribe', '实时监控订阅', 'action', @menu_realtime_monitor, 'device_monitor:realtime:subscribe', '', 51, 1),
  ('device_monitor:realtime:refresh', '实时监控刷新', 'action', @menu_realtime_monitor, 'device_monitor:realtime:refresh', '', 52, 1),
  ('device_monitor:history:view', '历史数据查看', 'page', @menu_history_data, 'device_monitor:history:view', '', 60, 1),
  ('device_monitor:history:export', '历史数据导出', 'action', @menu_history_data, 'device_monitor:history:export', '', 61, 1),
  ('device_monitor:log:view', '系统日志查看', 'page', @menu_system_log, 'device_monitor:log:view', '', 70, 1),
  ('system:user:view', '用户管理查看', 'page', @menu_user, 'system:user:view', '', 110, 1),
  ('system:user:create', '用户新增', 'action', @menu_user, 'system:user:create', '', 111, 1),
  ('system:user:update', '用户编辑', 'action', @menu_user, 'system:user:update', '', 112, 1),
  ('system:user:delete', '用户删除', 'action', @menu_user, 'system:user:delete', '', 113, 1),
  ('system:user:password_reset', '用户密码重置', 'action', @menu_user, 'system:user:password_reset', '', 114, 1),
  ('system:role:view', '角色管理查看', 'page', @menu_role, 'system:role:view', '', 120, 1),
  ('system:role:create', '角色新增', 'action', @menu_role, 'system:role:create', '', 121, 1),
  ('system:role:update', '角色编辑', 'action', @menu_role, 'system:role:update', '', 122, 1),
  ('system:role:delete', '角色删除', 'action', @menu_role, 'system:role:delete', '', 123, 1),
  ('system:role:grant', '角色授权', 'action', @menu_role, 'system:role:grant', '', 124, 1),
  ('system:menu:view', '菜单管理查看', 'page', @menu_menus, 'system:menu:view', '', 130, 1),
  ('system:menu:create', '菜单新增', 'action', @menu_menus, 'add', '', 131, 1),
  ('system:menu:update', '菜单编辑', 'action', @menu_menus, 'edit', '', 132, 1),
  ('system:menu:delete', '菜单删除', 'action', @menu_menus, 'delete', '', 133, 1)
ON DUPLICATE KEY UPDATE
  `permission_name` = VALUES(`permission_name`),
  `permission_type` = VALUES(`permission_type`),
  `menu_id` = VALUES(`menu_id`),
  `auth_mark` = VALUES(`auth_mark`),
  `description` = VALUES(`description`),
  `sort` = VALUES(`sort`),
  `enabled` = VALUES(`enabled`);

DELETE `sur`
FROM `sys_user_role` AS `sur`
JOIN `sys_user` AS `u` ON `u`.`id` = `sur`.`user_id`
WHERE `u`.`user_name` IN ('Super', 'Admin', 'User');

INSERT INTO `sys_user_role` (`user_id`, `role_id`)
SELECT `u`.`id`, `r`.`id`
FROM `sys_user` AS `u`
JOIN `sys_role` AS `r`
WHERE (`u`.`user_name` = 'Super' AND `r`.`role_code` = 'R_SUPER')
   OR (`u`.`user_name` = 'Admin' AND `r`.`role_code` = 'R_ADMIN')
   OR (`u`.`user_name` = 'User'  AND `r`.`role_code` = 'R_USER');

DELETE `srm`
FROM `sys_role_menu` AS `srm`
JOIN `sys_role` AS `r` ON `r`.`id` = `srm`.`role_id`
WHERE `r`.`role_code` IN ('R_SUPER', 'R_ADMIN', 'R_USER');

INSERT INTO `sys_role_menu` (`role_id`, `menu_id`)
SELECT `r`.`id`, `m`.`id`
FROM `sys_role` AS `r`
JOIN `sys_menu` AS `m`
WHERE (`r`.`role_code` = 'R_SUPER' AND `m`.`name` IN ('DeviceMonitor', 'MonitorDashboard', 'DriverManage', 'DeviceManage', 'PointManage', 'RealtimeMonitor', 'HistoryData', 'SystemLog', 'System', 'User', 'Role', 'Menus'))
   OR (`r`.`role_code` = 'R_ADMIN' AND `m`.`name` IN ('DeviceMonitor', 'MonitorDashboard', 'DriverManage', 'DeviceManage', 'PointManage', 'RealtimeMonitor', 'HistoryData', 'SystemLog', 'System', 'User'))
   OR (`r`.`role_code` = 'R_USER'  AND `m`.`name` IN ('DeviceMonitor', 'MonitorDashboard', 'RealtimeMonitor', 'HistoryData', 'SystemLog'));

DELETE `srp`
FROM `sys_role_permission` AS `srp`
JOIN `sys_role` AS `r` ON `r`.`id` = `srp`.`role_id`
WHERE `r`.`role_code` IN ('R_SUPER', 'R_ADMIN', 'R_USER');

INSERT INTO `sys_role_permission` (`role_id`, `permission_id`)
SELECT `r`.`id`, `p`.`id`
FROM `sys_role` AS `r`
JOIN `sys_permission` AS `p`
WHERE (`r`.`role_code` = 'R_SUPER')
   OR (`r`.`role_code` = 'R_ADMIN' AND `p`.`permission_code` LIKE 'device_monitor:%')
   OR (`r`.`role_code` = 'R_ADMIN' AND `p`.`permission_code` LIKE 'system:user:%')
   OR (`r`.`role_code` = 'R_USER'  AND `p`.`permission_code` IN (
      'device_monitor:dashboard:view',
      'device_monitor:realtime:view',
      'device_monitor:realtime:subscribe',
      'device_monitor:realtime:refresh',
      'device_monitor:history:view',
      'device_monitor:log:view'
   ));

DELETE `sdp`
FROM `sys_data_permission` AS `sdp`
JOIN `sys_role` AS `r` ON `r`.`id` = `sdp`.`role_id`
WHERE `r`.`role_code` IN ('R_SUPER', 'R_ADMIN', 'R_USER');

INSERT INTO `sys_data_permission` (`role_id`, `scope_type`, `scope_value`, `enabled`)
SELECT `r`.`id`, 'all', '*', 1
FROM `sys_role` AS `r`
WHERE `r`.`role_code` IN ('R_SUPER', 'R_ADMIN', 'R_USER');
