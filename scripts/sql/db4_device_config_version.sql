-- DB-4: device 表新增 variable_config_version 字段
-- 对应任务清单 DB-4，供采集模块轮询感知配置变更
-- 注意：MySQL 8.0 不支持 ADD COLUMN IF NOT EXISTS，幂等执行请通过配套 Python 脚本

ALTER TABLE device
    ADD COLUMN variable_config_version INT UNSIGNED NOT NULL DEFAULT 1
    COMMENT '设备点位配置版本号，任意分组 config_version 变更时同步递增';
