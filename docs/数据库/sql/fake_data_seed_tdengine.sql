-- 首批联调测试数据 seed SQL（TDengine）
-- 新模型：设备类型数据库 -> 设备超级表 -> 变量子表
-- 约定：
--   qc  数据库：桥吊类设备
--   asc 数据库：轨道吊类设备
--   agv 数据库：自动导引车设备
--   超级表名：device.device_code
--   子表名：{device_code}_{variable_suffix}
--   行结构：ts, value
--   TAGS：device_id, variable_id

CREATE DATABASE IF NOT EXISTS `qc` KEEP 365 DURATION 30 BUFFER 16 PAGES 128;
CREATE DATABASE IF NOT EXISTS `asc` KEEP 365 DURATION 30 BUFFER 16 PAGES 128;
CREATE DATABASE IF NOT EXISTS `agv` KEEP 365 DURATION 30 BUFFER 16 PAGES 128;

CREATE STABLE IF NOT EXISTS `qc`.`qc100` (
    `ts` TIMESTAMP,
    `value` DOUBLE
) TAGS (
    `device_id` BIGINT,
    `variable_id` BIGINT
);

CREATE STABLE IF NOT EXISTS `asc`.`asc7011` (
    `ts` TIMESTAMP,
    `value` DOUBLE
) TAGS (
    `device_id` BIGINT,
    `variable_id` BIGINT
);

CREATE STABLE IF NOT EXISTS `agv`.`agv801` (
    `ts` TIMESTAMP,
    `value` DOUBLE
) TAGS (
    `device_id` BIGINT,
    `variable_id` BIGINT
);

DROP TABLE IF EXISTS `qc`.`qc100_1`;
DROP TABLE IF EXISTS `qc`.`qc100_11901`;
DROP TABLE IF EXISTS `asc`.`asc7011_85`;
DROP TABLE IF EXISTS `asc`.`asc7011_4200`;
DROP TABLE IF EXISTS `agv`.`agv801_100`;
DROP TABLE IF EXISTS `agv`.`agv801_200`;

CREATE TABLE IF NOT EXISTS `qc`.`qc100_1`
USING `qc`.`qc100` TAGS (991001, 992001);

CREATE TABLE IF NOT EXISTS `qc`.`qc100_11901`
USING `qc`.`qc100` TAGS (991001, 992002);

CREATE TABLE IF NOT EXISTS `asc`.`asc7011_85`
USING `asc`.`asc7011` TAGS (991002, 992003);

CREATE TABLE IF NOT EXISTS `asc`.`asc7011_4200`
USING `asc`.`asc7011` TAGS (991002, 992004);

CREATE TABLE IF NOT EXISTS `agv`.`agv801_100`
USING `agv`.`agv801` TAGS (991003, 992005);

CREATE TABLE IF NOT EXISTS `agv`.`agv801_200`
USING `agv`.`agv801` TAGS (991003, 992006);

INSERT INTO `qc`.`qc100_1` (`ts`, `value`) VALUES
    (now - 5m, 12.30)
    (now - 30s, 12.45)
    (now - 10s, 12.60);

INSERT INTO `qc`.`qc100_11901` (`ts`, `value`) VALUES
    (now - 5m, 5.10)
    (now - 30s, 5.85)
    (now - 10s, 6.40);

INSERT INTO `asc`.`asc7011_85` (`ts`, `value`) VALUES
    (now - 20m, 1)
    (now - 12m, 1)
    (now - 8m, 0);

INSERT INTO `asc`.`asc7011_4200` (`ts`, `value`) VALUES
    (now - 20m, 42.0)
    (now - 12m, 43.5)
    (now - 8m, 41.8);

INSERT INTO `agv`.`agv801_100` (`ts`, `value`) VALUES
    (now - 5m, 1)
    (now - 30s, 1)
    (now - 10s, 1);

INSERT INTO `agv`.`agv801_200` (`ts`, `value`) VALUES
    (now - 5m, 48.0)
    (now - 30s, 72.0)
    (now - 10s, 92.5);
