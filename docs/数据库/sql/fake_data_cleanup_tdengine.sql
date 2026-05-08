-- TDengine 测试数据清理 SQL（新模型）
-- 仅删除测试变量子表，保留设备超级表，避免误伤同设备类型下其他对象

DROP TABLE IF EXISTS `qc`.`qc100_1`;
DROP TABLE IF EXISTS `qc`.`qc100_11901`;
DROP TABLE IF EXISTS `asc`.`asc7011_85`;
DROP TABLE IF EXISTS `asc`.`asc7011_4200`;
DROP TABLE IF EXISTS `agv`.`agv801_100`;
DROP TABLE IF EXISTS `agv`.`agv801_200`;
