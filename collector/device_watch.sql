-- Collector minimal MySQL initialization schema
-- Baseline date: 2026-04-24
-- Scope: collector runtime required tables only
-- Canonical point key: device_variable.var_seq
-- TDengine subtable name: device_code + '_' + var_seq

USE `device_watch`;
SET NAMES utf8mb4;

CREATE TABLE IF NOT EXISTS `device_type` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '设备类型ID',
    `type_code` VARCHAR(50) NOT NULL COMMENT '设备类型编码，如 QC / ASC / AGV',
    `type_name` VARCHAR(100) NOT NULL COMMENT '设备类型名称',
    `description` VARCHAR(255) NULL COMMENT '描述',
    `create_user` VARCHAR(20) NULL COMMENT '创建者',
    `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uq_device_type_type_code` (`type_code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='设备类型表';


CREATE TABLE IF NOT EXISTS `device_group` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '设备分组ID',
    `group_code` VARCHAR(50) NOT NULL COMMENT '分组编码',
    `group_name` VARCHAR(100) NOT NULL COMMENT '分组名称',
    `description` VARCHAR(255) NULL COMMENT '描述',
    `create_user` VARCHAR(20) NULL COMMENT '创建者',
    `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uq_device_group_group_code` (`group_code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='设备分组表';


CREATE TABLE IF NOT EXISTS `comm_protocol` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '通讯协议ID',
    `protocol` VARCHAR(50) NOT NULL COMMENT '通讯协议名称，如 SNAP7 / GE SRTP / OPC-DA-CENTER',
    `port` INT NULL COMMENT '默认端口',
    `server` VARCHAR(50) NULL COMMENT '服务标识，如 OPC DA ProgID: ZPMC.OPCServer',
    `description` VARCHAR(255) NULL COMMENT '描述',
    `create_user` VARCHAR(20) NULL COMMENT '创建者',
    `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uq_comm_protocol_protocol` (`protocol`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='通讯协议表';


CREATE TABLE IF NOT EXISTS `device` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '设备ID',
    `device_code` VARCHAR(50) NOT NULL COMMENT '设备编号，创建后不可修改',
    `device_name` VARCHAR(100) NULL COMMENT '设备名称',
    `type_id` BIGINT NOT NULL COMMENT '设备类型ID，关联 device_type.id',
    `group_id` BIGINT NOT NULL COMMENT '设备分组ID，关联 device_group.id',
    `enable` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '是否启用（1=启用，0=停用）',
    `status` TINYINT NOT NULL DEFAULT 0 COMMENT '在线状态（0=离线，1=在线，2=不稳定）',
    `comm_type` VARCHAR(20) NOT NULL DEFAULT 'PLC' COMMENT '通信类型，兼容旧字段',
    `protocol_id` BIGINT NOT NULL COMMENT '通讯协议ID，关联 comm_protocol.id',
    `endpoint` VARCHAR(100) NOT NULL COMMENT '端点，IP/URL 等',
    `endpoint_bkp` VARCHAR(100) NULL COMMENT '备用端点',
    `rack` INT NOT NULL DEFAULT 0 COMMENT 'PLC 机架',
    `slot` INT NOT NULL DEFAULT 1 COMMENT 'PLC 插槽',
    `scan_cycle` INT NOT NULL DEFAULT 1000 COMMENT '默认扫描周期（ms）',
    `create_user` VARCHAR(20) NULL COMMENT '创建者',
    `create_time` DATETIME NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_user` VARCHAR(20) NULL COMMENT '更新者',
    `update_time` DATETIME NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uq_device_device_code` (`device_code`),
    KEY `idx_device_type_id` (`type_id`),
    KEY `idx_device_group_id` (`group_id`),
    KEY `idx_device_protocol_id` (`protocol_id`),
    CONSTRAINT `fk_device_type` FOREIGN KEY (`type_id`) REFERENCES `device_type` (`id`),
    CONSTRAINT `fk_device_group` FOREIGN KEY (`group_id`) REFERENCES `device_group` (`id`),
    CONSTRAINT `fk_device_protocol` FOREIGN KEY (`protocol_id`) REFERENCES `comm_protocol` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='设备表';


CREATE TABLE IF NOT EXISTS `plc_connection_log` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT 'PLC连接日志ID',
    `device_id` BIGINT NULL COMMENT '设备ID',
    `event_type` VARCHAR(20) NULL COMMENT '事件类型：connect / disconnect / timeout / status_update',
    `status` TINYINT NULL COMMENT 'PLC连接状态（0=离线，1=在线，2=不稳定）',
    `messsage` VARCHAR(255) NULL COMMENT '详细信息，沿用历史字段名以兼容当前 ORM',
    `create_time` DATETIME NULL DEFAULT CURRENT_TIMESTAMP COMMENT '事件发生时间',
    PRIMARY KEY (`id`),
    KEY `idx_plc_connection_log_device_id` (`device_id`),
    KEY `idx_plc_connection_log_create_time` (`create_time`),
    CONSTRAINT `fk_plc_connection_log_device` FOREIGN KEY (`device_id`) REFERENCES `device` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='PLC连接状态变化日志';


CREATE TABLE IF NOT EXISTS `device_variable` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '点位ID',
    `device_id` BIGINT NOT NULL COMMENT '设备ID，关联 device.id',
    `var_seq` INT NOT NULL COMMENT '点位序号，纯序号；TDengine 子表名由 device_code + _ + var_seq 派生',
    `var_name` VARCHAR(100) NOT NULL COMMENT '点位名称',
    `io_type` VARCHAR(10) NOT NULL COMMENT '内存区域类型，如 I / M / Q / DB / R',
    `var_type` VARCHAR(20) NOT NULL COMMENT '数据类型，如 BOOL / INT / DINT / REAL',
    `var_address` VARCHAR(100) NOT NULL COMMENT '变量原始地址，如 %I101.0 / %MD1304',
    `var_offset` INT NULL COMMENT '地址偏移',
    `var_bit` INT NULL COMMENT '位偏移',
    `description` VARCHAR(255) NULL COMMENT '描述',
    `scan_rate` INT NULL COMMENT '采集周期（ms）',
    `is_alarm` TINYINT NOT NULL DEFAULT 0 COMMENT '是否报警',
    `is_storage` TINYINT NOT NULL DEFAULT 1 COMMENT '是否存储到时序库',
    `create_user` VARCHAR(20) NULL COMMENT '创建者',
    `create_time` DATETIME NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_user` VARCHAR(20) NULL COMMENT '更新者',
    `update_time` DATETIME NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uq_device_variable_device_var_seq` (`device_id`, `var_seq`),
    UNIQUE KEY `uq_device_variable_device_var_address` (`device_id`, `var_address`),
    KEY `idx_device_variable_device_id` (`device_id`),
    KEY `idx_device_variable_scan_rate` (`scan_rate`),
    KEY `idx_device_variable_is_alarm` (`is_alarm`),
    CONSTRAINT `fk_device_variable_device` FOREIGN KEY (`device_id`) REFERENCES `device` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='设备点位表';


-- Note:
-- 1. `unit_dict`、`variable_template`、`template_variable`、`cross_db_task` 等扩展表
--    由 docs/数据库/sql 下的正式 migration / schema 脚本负责初始化。
-- 2. 本文件只保留 collector 启动与基础联调所需的最小表结构。
