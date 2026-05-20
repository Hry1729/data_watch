-- DB-1: 新增 device_point_group 表
-- 对应任务清单 DB-1，v0.6 点位分组设计
-- 可重复执行：CREATE TABLE IF NOT EXISTS

CREATE TABLE IF NOT EXISTS device_point_group (
    id                   BIGINT UNSIGNED     NOT NULL AUTO_INCREMENT,
    device_id            BIGINT UNSIGNED     NOT NULL             COMMENT '设备 ID，关联 device.id',
    group_no             INT UNSIGNED        NOT NULL             COMMENT '分组序号，从 1 开始',
    group_code           VARCHAR(16)         NOT NULL             COMMENT '分组编码，如 g001',
    group_name           VARCHAR(64)                              COMMENT '分组名称，可为空',
    max_point_count      INT UNSIGNED        NOT NULL DEFAULT 3072 COMMENT '该分组最大点位列数',
    active_point_count   INT UNSIGNED        NOT NULL DEFAULT 0   COMMENT '当前启用点位数',
    disabled_point_count INT UNSIGNED        NOT NULL DEFAULT 0   COMMENT '当前停用点位数',
    deleted_point_count  INT UNSIGNED        NOT NULL DEFAULT 0   COMMENT '软删除点位数',
    used_column_count    INT UNSIGNED        NOT NULL DEFAULT 0   COMMENT '已分配列位总数（含软删除，列位不回收）',
    next_column_index    INT UNSIGNED        NOT NULL DEFAULT 1   COMMENT '下一个可用列位序号',
    status               VARCHAR(16)         NOT NULL DEFAULT 'active' COMMENT 'active / full / disabled',
    config_version       INT UNSIGNED        NOT NULL DEFAULT 1   COMMENT '配置版本号，点位变更时递增',
    create_user          VARCHAR(64),
    create_time          DATETIME            DEFAULT CURRENT_TIMESTAMP,
    update_user          VARCHAR(64),
    update_time          DATETIME            DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    UNIQUE KEY uq_device_group (device_id, group_code),
    KEY idx_device_id (device_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='设备点位分组';
