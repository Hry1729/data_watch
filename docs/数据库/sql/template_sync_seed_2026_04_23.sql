USE `device_watch`;

SET NAMES utf8mb4;

INSERT INTO `unit_dict` (
  `unit_code`,
  `unit_symbol`,
  `unit_name`,
  `unit_category`,
  `description`,
  `create_user`,
  `create_time`,
  `update_user`,
  `update_time`
) VALUES
  ('TEMP_C', '℃', '摄氏度', '温度', '标准温度单位', 'system', NOW(), 'system', NOW()),
  ('TEMP_K', 'K', '开尔文', '温度', '标准温度单位', 'system', NOW(), 'system', NOW()),
  ('SPEED_M_S', 'm/s', '米每秒', '速度', '线速度单位', 'system', NOW(), 'system', NOW()),
  ('SPEED_RPM', 'rpm', '转每分钟', '速度', '旋转速度单位', 'system', NOW(), 'system', NOW()),
  ('PRESSURE_MPA', 'MPa', '兆帕', '压力', '压力单位', 'system', NOW(), 'system', NOW()),
  ('PRESSURE_KPA', 'kPa', '千帕', '压力', '压力单位', 'system', NOW(), 'system', NOW()),
  ('LENGTH_M', 'm', '米', '长度', '长度单位', 'system', NOW(), 'system', NOW()),
  ('LENGTH_MM', 'mm', '毫米', '长度', '长度单位', 'system', NOW(), 'system', NOW()),
  ('WEIGHT_T', 't', '吨', '重量', '重量单位', 'system', NOW(), 'system', NOW()),
  ('WEIGHT_KG', 'kg', '千克', '重量', '重量单位', 'system', NOW(), 'system', NOW()),
  ('PERCENT', '%', '百分比', '无量纲', '比例单位', 'system', NOW(), 'system', NOW()),
  ('BOOL', '', '布尔值', '开关量', '开关量可不显示单位', 'system', NOW(), 'system', NOW())
ON DUPLICATE KEY UPDATE
  `unit_symbol` = VALUES(`unit_symbol`),
  `unit_name` = VALUES(`unit_name`),
  `unit_category` = VALUES(`unit_category`),
  `description` = VALUES(`description`),
  `update_user` = VALUES(`update_user`),
  `update_time` = VALUES(`update_time`);

-- 模板头和模板点位不在 seed 中预置固定业务数据。
-- 推荐先从标准设备的 `device_variable` 抽取模板，再按设备显式应用或重同步。
