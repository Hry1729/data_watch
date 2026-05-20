-- DB-2: device_variable 新增分组映射字段
-- 对应任务清单 DB-2，v0.6 点位分组设计
-- 注意：MySQL 8.0 不支持 ADD COLUMN IF NOT EXISTS
-- 幂等执行请使用配套的 migrate_point_groups.py，它会先检查列是否存在

ALTER TABLE device_variable
    ADD COLUMN point_group_id  BIGINT UNSIGNED COMMENT '所属分组 ID，关联 device_point_group.id' AFTER device_id,
    ADD COLUMN group_index     INT UNSIGNED    COMMENT '组内列位序号，1~3072' AFTER point_group_id,
    ADD COLUMN td_column_name  VARCHAR(8)      COMMENT 'TDengine 列名，如 v0001' AFTER group_index,
    ADD COLUMN enable          TINYINT(1)      NOT NULL DEFAULT 1 COMMENT '点位是否启用' AFTER td_column_name,
    ADD COLUMN deleted         TINYINT(1)      NOT NULL DEFAULT 0 COMMENT '是否软删除' AFTER enable;

ALTER TABLE device_variable
    ADD KEY idx_point_group_id (point_group_id),
    ADD KEY idx_group_deleted  (point_group_id, deleted, enable);
