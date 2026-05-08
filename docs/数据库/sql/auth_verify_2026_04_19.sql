USE `device_watch`;

SET NAMES utf8mb4;

SELECT 'sys table count' AS `check_name`, COUNT(*) AS `actual_value`
FROM information_schema.tables
WHERE table_schema = DATABASE()
  AND table_name IN (
    'sys_user',
    'sys_role',
    'sys_user_role',
    'sys_menu',
    'sys_permission',
    'sys_role_menu',
    'sys_role_permission',
    'sys_data_permission'
  );

SELECT TABLE_NAME, CONSTRAINT_NAME, CONSTRAINT_TYPE
FROM information_schema.table_constraints
WHERE table_schema = DATABASE()
  AND table_name IN (
    'sys_user',
    'sys_role',
    'sys_user_role',
    'sys_menu',
    'sys_permission',
    'sys_role_menu',
    'sys_role_permission',
    'sys_data_permission'
  )
ORDER BY TABLE_NAME, CONSTRAINT_TYPE, CONSTRAINT_NAME;

SELECT `role_code`, `role_name`, `enabled`, `sort`
FROM `sys_role`
WHERE `role_code` IN ('R_SUPER', 'R_ADMIN', 'R_USER')
ORDER BY `sort`, `id`;

SELECT `user_name`, `status`, `deleted`
FROM `sys_user`
WHERE `user_name` IN ('Super', 'Admin', 'User')
ORDER BY `id`;

SELECT `u`.`user_name`, GROUP_CONCAT(`r`.`role_code` ORDER BY `r`.`sort`, `r`.`id` SEPARATOR ',') AS `role_codes`
FROM `sys_user` AS `u`
LEFT JOIN `sys_user_role` AS `sur` ON `sur`.`user_id` = `u`.`id`
LEFT JOIN `sys_role` AS `r` ON `r`.`id` = `sur`.`role_id`
WHERE `u`.`user_name` IN ('Super', 'Admin', 'User')
GROUP BY `u`.`id`, `u`.`user_name`
ORDER BY `u`.`id`;

SELECT `r`.`role_code`, COUNT(*) AS `menu_count`
FROM `sys_role_menu` AS `srm`
JOIN `sys_role` AS `r` ON `r`.`id` = `srm`.`role_id`
WHERE `r`.`role_code` IN ('R_SUPER', 'R_ADMIN', 'R_USER')
GROUP BY `r`.`role_code`
ORDER BY `r`.`role_code`;

SELECT `r`.`role_code`, COUNT(*) AS `permission_count`
FROM `sys_role_permission` AS `srp`
JOIN `sys_role` AS `r` ON `r`.`id` = `srp`.`role_id`
WHERE `r`.`role_code` IN ('R_SUPER', 'R_ADMIN', 'R_USER')
GROUP BY `r`.`role_code`
ORDER BY `r`.`role_code`;

SELECT
  'R_SUPER_has_role_menu' AS `check_name`,
  EXISTS (
    SELECT 1
    FROM `sys_role_menu` AS `srm`
    JOIN `sys_role` AS `r` ON `r`.`id` = `srm`.`role_id`
    JOIN `sys_menu` AS `m` ON `m`.`id` = `srm`.`menu_id`
    WHERE `r`.`role_code` = 'R_SUPER'
      AND `m`.`name` = 'Role'
  ) AS `pass_flag`
UNION ALL
SELECT
  'R_SUPER_has_menus_menu',
  EXISTS (
    SELECT 1
    FROM `sys_role_menu` AS `srm`
    JOIN `sys_role` AS `r` ON `r`.`id` = `srm`.`role_id`
    JOIN `sys_menu` AS `m` ON `m`.`id` = `srm`.`menu_id`
    WHERE `r`.`role_code` = 'R_SUPER'
      AND `m`.`name` = 'Menus'
  )
UNION ALL
SELECT
  'R_ADMIN_lacks_role_menu',
  NOT EXISTS (
    SELECT 1
    FROM `sys_role_menu` AS `srm`
    JOIN `sys_role` AS `r` ON `r`.`id` = `srm`.`role_id`
    JOIN `sys_menu` AS `m` ON `m`.`id` = `srm`.`menu_id`
    WHERE `r`.`role_code` = 'R_ADMIN'
      AND `m`.`name` = 'Role'
  )
UNION ALL
SELECT
  'R_ADMIN_lacks_menus_menu',
  NOT EXISTS (
    SELECT 1
    FROM `sys_role_menu` AS `srm`
    JOIN `sys_role` AS `r` ON `r`.`id` = `srm`.`role_id`
    JOIN `sys_menu` AS `m` ON `m`.`id` = `srm`.`menu_id`
    WHERE `r`.`role_code` = 'R_ADMIN'
      AND `m`.`name` = 'Menus'
  )
UNION ALL
SELECT
  'R_USER_lacks_driver_manage',
  NOT EXISTS (
    SELECT 1
    FROM `sys_role_menu` AS `srm`
    JOIN `sys_role` AS `r` ON `r`.`id` = `srm`.`role_id`
    JOIN `sys_menu` AS `m` ON `m`.`id` = `srm`.`menu_id`
    WHERE `r`.`role_code` = 'R_USER'
      AND `m`.`name` = 'DriverManage'
  )
UNION ALL
SELECT
  'R_USER_lacks_system_directory',
  NOT EXISTS (
    SELECT 1
    FROM `sys_role_menu` AS `srm`
    JOIN `sys_role` AS `r` ON `r`.`id` = `srm`.`role_id`
    JOIN `sys_menu` AS `m` ON `m`.`id` = `srm`.`menu_id`
    WHERE `r`.`role_code` = 'R_USER'
      AND `m`.`name` = 'System'
  );
