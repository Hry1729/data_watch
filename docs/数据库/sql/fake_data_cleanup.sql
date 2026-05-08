-- 首批联调测试数据清理 SQL（MySQL + TDengine）
-- 适用范围：STEP-006 / docs/前后端联调/08-首批测试假数据落库清单.md
-- 注意：仅清理约定测试 ID 段，请执行前再次确认本地库中这些 ID / 编码未被真实业务占用
-- 当前基线：TDengine 使用“设备类型数据库 -> 设备超级表 -> 变量子表”模型

/* ---------------------------------------------------------------------------
 * 一、TDengine 清理
 * 仅删除测试变量子表，不删除设备超级表，避免影响其他真实设备
 * ------------------------------------------------------------------------- */

DROP TABLE IF EXISTS `qc`.`qc100_1`;
DROP TABLE IF EXISTS `qc`.`qc100_11901`;
DROP TABLE IF EXISTS `asc`.`asc7011_85`;
DROP TABLE IF EXISTS `asc`.`asc7011_4200`;
DROP TABLE IF EXISTS `agv`.`agv801_100`;
DROP TABLE IF EXISTS `agv`.`agv801_200`;

/* ---------------------------------------------------------------------------
 * 二、MySQL 清理
 * 建议按照外键依赖倒序执行
 * ------------------------------------------------------------------------- */

USE device_watch;

DELETE FROM device_variable
WHERE id IN (992001, 992002, 992003, 992004, 992005, 992006);

DELETE FROM device
WHERE id IN (991001, 991002, 991003);

DELETE FROM variable_group
WHERE id IN (9930, 9931, 9932);

DELETE FROM device_group
WHERE id IN (9910, 9911, 9912);

DELETE FROM device_type
WHERE id IN (9920, 9921, 9922);

DELETE FROM comm_protocol
WHERE id IN (9901, 9902, 9903);
