/*
 Navicat Premium Dump SQL

 Source Server         : mysql80
 Source Server Type    : MySQL
 Source Server Version : 80038 (8.0.38)
 Source Host           : localhost:3306
 Source Schema         : device_watch

 Target Server Type    : MySQL
 Target Server Version : 80038 (8.0.38)
 File Encoding         : 65001

 Date: 21/05/2026 05:32:24
*/

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- ----------------------------
-- Table structure for alarm_active
-- ----------------------------
DROP TABLE IF EXISTS `alarm_active`;
CREATE TABLE `alarm_active`  (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `record_id` bigint NOT NULL,
  `rule_id` bigint NULL DEFAULT NULL,
  `device_id` bigint NULL DEFAULT NULL,
  `device_code` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `device_name` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `device_type_code` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `point_id` bigint NULL DEFAULT NULL,
  `point_name` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `alarm_type` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `level_code` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `severity` tinyint NOT NULL,
  `status` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `content` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `alarm_value` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `start_time` datetime NOT NULL,
  `latest_time` datetime NOT NULL,
  `ack_time` datetime NULL DEFAULT NULL,
  `ack_by` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `extra_json` json NULL,
  `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `update_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uq_alarm_active_record_id`(`record_id` ASC) USING BTREE,
  INDEX `idx_alarm_active_device_id`(`device_id` ASC) USING BTREE,
  INDEX `idx_alarm_active_level_code`(`level_code` ASC) USING BTREE,
  INDEX `idx_alarm_active_status`(`status` ASC) USING BTREE,
  INDEX `idx_alarm_active_latest_time`(`latest_time` ASC) USING BTREE,
  INDEX `fk_alarm_active_rule`(`rule_id` ASC) USING BTREE,
  INDEX `fk_alarm_active_point`(`point_id` ASC) USING BTREE,
  CONSTRAINT `fk_alarm_active_device` FOREIGN KEY (`device_id`) REFERENCES `device` (`id`) ON DELETE SET NULL ON UPDATE RESTRICT,
  CONSTRAINT `fk_alarm_active_point` FOREIGN KEY (`point_id`) REFERENCES `device_variable` (`id`) ON DELETE SET NULL ON UPDATE RESTRICT,
  CONSTRAINT `fk_alarm_active_record` FOREIGN KEY (`record_id`) REFERENCES `alarm_record` (`id`) ON DELETE CASCADE ON UPDATE RESTRICT,
  CONSTRAINT `fk_alarm_active_rule` FOREIGN KEY (`rule_id`) REFERENCES `alarm_rule` (`id`) ON DELETE SET NULL ON UPDATE RESTRICT
) ENGINE = InnoDB AUTO_INCREMENT = 5 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = 'active alarm snapshot' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for alarm_level
-- ----------------------------
DROP TABLE IF EXISTS `alarm_level`;
CREATE TABLE `alarm_level`  (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `level_code` varchar(10) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `level_name` varchar(10) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `severity` tinyint NOT NULL,
  `color` varchar(16) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `sort` int NOT NULL DEFAULT 1,
  `enabled` tinyint(1) NOT NULL DEFAULT 1,
  `icon` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `description` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `create_by` varchar(10) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'system',
  `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `update_by` varchar(10) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'system',
  `update_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uq_alarm_level_level_code`(`level_code` ASC) USING BTREE,
  INDEX `idx_alarm_level_severity`(`severity` ASC) USING BTREE,
  INDEX `idx_alarm_level_enabled_sort`(`enabled` ASC, `sort` ASC) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 7 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = 'alarm level dictionary' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for alarm_record
-- ----------------------------
DROP TABLE IF EXISTS `alarm_record`;
CREATE TABLE `alarm_record`  (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `alarm_no` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `rule_id` bigint NULL DEFAULT NULL,
  `device_id` bigint NULL DEFAULT NULL,
  `device_code` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `device_name` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `device_type_code` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `point_id` bigint NULL DEFAULT NULL,
  `point_code` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `point_name` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `alarm_type` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `level_code` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `severity` tinyint NOT NULL,
  `status` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `alarm_value` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `threshold_value` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `content` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `rule_name_snapshot` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `level_name_snapshot` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `alarm_value_num` decimal(20, 6) NULL DEFAULT NULL,
  `threshold_value_num` decimal(20, 6) NULL DEFAULT NULL,
  `occur_count` int NOT NULL DEFAULT 1,
  `start_time` datetime NOT NULL,
  `end_time` datetime NULL DEFAULT NULL,
  `ack_time` datetime NULL DEFAULT NULL,
  `ack_by` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `ack_user_id` bigint NULL DEFAULT NULL,
  `recover_time` datetime NULL DEFAULT NULL,
  `trace_id` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `extra_json` json NULL,
  `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uq_alarm_record_alarm_no`(`alarm_no` ASC) USING BTREE,
  INDEX `idx_alarm_record_start_time`(`start_time` ASC) USING BTREE,
  INDEX `idx_alarm_record_device_start_time`(`device_id` ASC, `start_time` ASC) USING BTREE,
  INDEX `idx_alarm_record_level_start_time`(`level_code` ASC, `start_time` ASC) USING BTREE,
  INDEX `idx_alarm_record_alarm_type_start_time`(`alarm_type` ASC, `start_time` ASC) USING BTREE,
  INDEX `idx_alarm_record_status_start_time`(`status` ASC, `start_time` ASC) USING BTREE,
  INDEX `idx_alarm_record_device_type_start_time`(`device_type_code` ASC, `start_time` ASC) USING BTREE,
  INDEX `idx_alarm_record_rule_start_time`(`rule_id` ASC, `start_time` ASC) USING BTREE,
  INDEX `fk_alarm_record_point`(`point_id` ASC) USING BTREE,
  INDEX `fk_alarm_record_ack_user`(`ack_user_id` ASC) USING BTREE,
  CONSTRAINT `fk_alarm_record_ack_user` FOREIGN KEY (`ack_user_id`) REFERENCES `sys_user` (`id`) ON DELETE SET NULL ON UPDATE RESTRICT,
  CONSTRAINT `fk_alarm_record_device` FOREIGN KEY (`device_id`) REFERENCES `device` (`id`) ON DELETE SET NULL ON UPDATE RESTRICT,
  CONSTRAINT `fk_alarm_record_point` FOREIGN KEY (`point_id`) REFERENCES `device_variable` (`id`) ON DELETE SET NULL ON UPDATE RESTRICT,
  CONSTRAINT `fk_alarm_record_rule` FOREIGN KEY (`rule_id`) REFERENCES `alarm_rule` (`id`) ON DELETE SET NULL ON UPDATE RESTRICT
) ENGINE = InnoDB AUTO_INCREMENT = 12 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = 'alarm history event fact' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for alarm_rule
-- ----------------------------
DROP TABLE IF EXISTS `alarm_rule`;
CREATE TABLE `alarm_rule`  (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `rule_code` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `rule_name` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `device_id` bigint NULL DEFAULT NULL,
  `point_id` bigint NULL DEFAULT NULL,
  `alarm_type` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `level_id` bigint NOT NULL,
  `compare_op` varchar(16) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL,
  `threshold_value` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL,
  `recover_value` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL,
  `enabled` tinyint(1) NOT NULL DEFAULT 1,
  `description` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `deadband_value` decimal(20, 6) NULL DEFAULT NULL,
  `hold_seconds` int NOT NULL DEFAULT 0,
  `message_template` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL,
  `threshold_value_num` decimal(20, 6) NULL DEFAULT NULL,
  `recover_value_num` decimal(20, 6) NULL DEFAULT NULL,
  `extra_json` json NULL,
  `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `update_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uq_alarm_rule_rule_code`(`rule_code` ASC) USING BTREE,
  INDEX `idx_alarm_rule_device_point_enabled`(`device_id` ASC, `point_id` ASC, `enabled` ASC) USING BTREE,
  INDEX `idx_alarm_rule_alarm_type_enabled`(`alarm_type` ASC, `enabled` ASC) USING BTREE,
  INDEX `idx_alarm_rule_level_enabled`(`level_id` ASC, `enabled` ASC) USING BTREE,
  INDEX `fk_alarm_rule_point`(`point_id` ASC) USING BTREE,
  CONSTRAINT `fk_alarm_rule_device` FOREIGN KEY (`device_id`) REFERENCES `device` (`id`) ON DELETE SET NULL ON UPDATE RESTRICT,
  CONSTRAINT `fk_alarm_rule_level` FOREIGN KEY (`level_id`) REFERENCES `alarm_level` (`id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `fk_alarm_rule_point` FOREIGN KEY (`point_id`) REFERENCES `device_variable` (`id`) ON DELETE SET NULL ON UPDATE RESTRICT
) ENGINE = InnoDB AUTO_INCREMENT = 7 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = 'alarm rule definition' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for comm_protocol
-- ----------------------------
DROP TABLE IF EXISTS `comm_protocol`;
CREATE TABLE `comm_protocol`  (
  `id` bigint NOT NULL AUTO_INCREMENT COMMENT '通讯协议ID',
  `protocol` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '通讯协议名称，如S7、GE SRTP',
  `server` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '通讯协议公司',
  `port` int NULL DEFAULT NULL COMMENT '默认端口',
  `description` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '描述',
  `create_user` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '创建者',
  `create_time` datetime NOT NULL COMMENT '创建时间',
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uq_comm_protocol_protocol`(`protocol` ASC) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 9903 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '通讯协议表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for cross_db_task
-- ----------------------------
DROP TABLE IF EXISTS `cross_db_task`;
CREATE TABLE `cross_db_task`  (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `task_no` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `biz_type` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `biz_id` bigint NOT NULL,
  `action_type` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `payload_json` json NOT NULL,
  `status` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'pending',
  `retry_count` int NOT NULL DEFAULT 0,
  `next_retry_time` datetime NULL DEFAULT NULL,
  `last_error` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL,
  `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `update_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uq_cross_db_task_task_no`(`task_no` ASC) USING BTREE,
  INDEX `idx_cross_db_task_biz`(`biz_type` ASC, `biz_id` ASC) USING BTREE,
  INDEX `idx_cross_db_task_status_retry`(`status` ASC, `next_retry_time` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '???????' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for device
-- ----------------------------
DROP TABLE IF EXISTS `device`;
CREATE TABLE `device`  (
  `id` bigint NOT NULL AUTO_INCREMENT COMMENT '设备ID',
  `device_code` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '设备编号',
  `device_name` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '设备名称',
  `type_id` bigint NOT NULL COMMENT 'device type id references device_type.id',
  `template_id` bigint NULL DEFAULT NULL COMMENT '????ID',
  `group_id` bigint NOT NULL COMMENT 'device group id references device_group.id',
  `enable` tinyint NOT NULL COMMENT '是否启用（0=未启用，1=启用）',
  `status` tinyint NOT NULL COMMENT '在线状态（0=离线，1=在线）',
  `protocol_id` bigint NOT NULL COMMENT 'protocol id references comm_protocol.id',
  `endpoint` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '端点，IP/URL等',
  `endpoint_bkp` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '备用端点',
  `rack` int NOT NULL COMMENT 'PLC机架',
  `slot` int NOT NULL COMMENT 'PLC插槽',
  `scan_cycle` int NOT NULL DEFAULT 1000 COMMENT '默认扫描周期，点位未配置时使用',
  `template_last_sync_version` int NULL DEFAULT NULL COMMENT '???????????',
  `template_sync_time` datetime NULL DEFAULT NULL COMMENT '??????????',
  `create_user` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '创建者',
  `create_time` datetime NULL DEFAULT NULL COMMENT '创建时间',
  `update_user` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '更新者',
  `update_time` datetime NULL DEFAULT NULL COMMENT '更新时间',
  `variable_config_version` int UNSIGNED NOT NULL DEFAULT 1 COMMENT '设备点位配置版本号',
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uq_device_device_code`(`device_code` ASC) USING BTREE,
  INDEX `type_id`(`type_id` ASC) USING BTREE,
  INDEX `group_id`(`group_id` ASC) USING BTREE,
  INDEX `protocol_id`(`protocol_id` ASC) USING BTREE,
  INDEX `idx_device_template_id`(`template_id` ASC) USING BTREE,
  CONSTRAINT `device_ibfk_1` FOREIGN KEY (`type_id`) REFERENCES `device_type` (`id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `device_ibfk_2` FOREIGN KEY (`group_id`) REFERENCES `device_group` (`id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `device_ibfk_3` FOREIGN KEY (`protocol_id`) REFERENCES `comm_protocol` (`id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `fk_device_template` FOREIGN KEY (`template_id`) REFERENCES `variable_template` (`id`) ON DELETE SET NULL ON UPDATE RESTRICT
) ENGINE = InnoDB AUTO_INCREMENT = 991173 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '设备' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for device_group
-- ----------------------------
DROP TABLE IF EXISTS `device_group`;
CREATE TABLE `device_group`  (
  `id` bigint NOT NULL AUTO_INCREMENT COMMENT '设备分组ID',
  `group_code` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '分组编号',
  `group_name` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '分组名称',
  `description` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '描述',
  `create_user` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '创建者',
  `create_time` datetime NOT NULL COMMENT '创建时间',
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uq_device_group_group_code`(`group_code` ASC) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 9912 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '设备分组' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for device_point_group
-- ----------------------------
DROP TABLE IF EXISTS `device_point_group`;
CREATE TABLE `device_point_group`  (
  `id` bigint UNSIGNED NOT NULL AUTO_INCREMENT,
  `device_id` bigint UNSIGNED NOT NULL COMMENT '设备 ID，关联 device.id',
  `group_no` int UNSIGNED NOT NULL COMMENT '分组序号，从 1 开始',
  `group_code` varchar(16) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '分组编码，如 g001',
  `group_name` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL COMMENT '分组名称，可为空',
  `max_point_count` int UNSIGNED NOT NULL DEFAULT 3072 COMMENT '该分组最大点位列数',
  `active_point_count` int UNSIGNED NOT NULL DEFAULT 0 COMMENT '当前启用点位数',
  `disabled_point_count` int UNSIGNED NOT NULL DEFAULT 0 COMMENT '当前停用点位数',
  `deleted_point_count` int UNSIGNED NOT NULL DEFAULT 0 COMMENT '软删除点位数',
  `used_column_count` int UNSIGNED NOT NULL DEFAULT 0 COMMENT '已分配列位总数（含软删除，列位不回收）',
  `next_column_index` int UNSIGNED NOT NULL DEFAULT 1 COMMENT '下一个可用列位序号',
  `status` varchar(16) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL DEFAULT 'active' COMMENT 'active / full / disabled',
  `config_version` int UNSIGNED NOT NULL DEFAULT 1 COMMENT '配置版本号，点位变更时递增',
  `create_user` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL,
  `create_time` datetime NULL DEFAULT CURRENT_TIMESTAMP,
  `update_user` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL,
  `update_time` datetime NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uq_device_group`(`device_id` ASC, `group_code` ASC) USING BTREE,
  INDEX `idx_device_id`(`device_id` ASC) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 306 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci COMMENT = '设备点位分组' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for device_type
-- ----------------------------
DROP TABLE IF EXISTS `device_type`;
CREATE TABLE `device_type`  (
  `id` bigint NOT NULL AUTO_INCREMENT COMMENT '设备类型ID',
  `type_code` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '设备编码',
  `type_name` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '设备名称',
  `description` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '描述',
  `create_user` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '创建者',
  `create_time` datetime NOT NULL COMMENT '创建时间',
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uq_device_type_type_code`(`type_code` ASC) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 9922 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '设备类型表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for device_variable
-- ----------------------------
DROP TABLE IF EXISTS `device_variable`;
CREATE TABLE `device_variable`  (
  `id` bigint NOT NULL AUTO_INCREMENT COMMENT '变量ID',
  `device_id` bigint NOT NULL COMMENT 'device id references device.id',
  `point_group_id` bigint UNSIGNED NULL DEFAULT NULL COMMENT '所属分组 ID',
  `group_index` int UNSIGNED NULL DEFAULT NULL COMMENT '组内列位序号',
  `td_column_name` varchar(8) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT 'TDengine 列名',
  `enable` tinyint(1) NOT NULL DEFAULT 1 COMMENT '点位是否启用',
  `deleted` tinyint(1) NOT NULL DEFAULT 0 COMMENT '是否软删除',
  `var_seq` int NOT NULL COMMENT '?????????TDengine ???? device_code + \'_\' + var_seq ??',
  `var_name` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '变量名称',
  `io_type` varchar(10) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT 'memory area type such as I M Q DB R',
  `var_type` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'data type such as BOOL INT DINT REAL DOUBLE',
  `var_address` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '变量原始地址（type+offset+bit）',
  `var_offset` int NULL DEFAULT NULL COMMENT '地址偏移',
  `var_bit` int NULL DEFAULT NULL COMMENT '位偏移',
  `unit_id` bigint NULL DEFAULT NULL COMMENT '????ID?????',
  `description` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '描述',
  `scan_rate` int NULL DEFAULT NULL COMMENT '采集周期',
  `is_alarm` tinyint NOT NULL COMMENT '是否报警',
  `is_storage` tinyint NOT NULL COMMENT '是否存储',
  `create_user` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '创建者',
  `create_time` datetime NULL DEFAULT NULL COMMENT '创建时间',
  `update_user` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '更新者',
  `update_time` datetime NULL DEFAULT NULL COMMENT '更新时间',
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uq_device_variable_device_var_address`(`device_id` ASC, `var_address` ASC) USING BTREE,
  UNIQUE INDEX `uq_device_variable_device_var_seq`(`device_id` ASC, `var_seq` ASC) USING BTREE,
  INDEX `idx_device_variable_device_id`(`device_id` ASC) USING BTREE,
  INDEX `idx_device_variable_scan_rate`(`scan_rate` ASC) USING BTREE,
  INDEX `idx_device_variable_is_alarm`(`is_alarm` ASC) USING BTREE,
  INDEX `idx_device_variable_unit_id`(`unit_id` ASC) USING BTREE,
  INDEX `idx_point_group_id`(`point_group_id` ASC) USING BTREE,
  INDEX `idx_group_deleted`(`point_group_id` ASC, `deleted` ASC, `enable` ASC) USING BTREE,
  CONSTRAINT `fk_device_variable_device` FOREIGN KEY (`device_id`) REFERENCES `device` (`id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `fk_device_variable_unit` FOREIGN KEY (`unit_id`) REFERENCES `unit_dict` (`id`) ON DELETE SET NULL ON UPDATE RESTRICT
) ENGINE = InnoDB AUTO_INCREMENT = 992025 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '设备变量表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for log_retention_policy
-- ----------------------------
DROP TABLE IF EXISTS `log_retention_policy`;
CREATE TABLE `log_retention_policy`  (
  `id` bigint NOT NULL,
  `enabled` tinyint(1) NOT NULL DEFAULT 1,
  `retention_days` int NOT NULL DEFAULT 7,
  `max_total_mb` int NOT NULL DEFAULT 1024,
  `cleanup_time` varchar(8) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '03:00',
  `capacity_check_interval_minutes` int NOT NULL DEFAULT 30,
  `batch_size` int NOT NULL DEFAULT 5000,
  `debug_db_enabled` tinyint(1) NOT NULL DEFAULT 0,
  `debug_enabled_until` datetime NULL DEFAULT NULL,
  `debug_reason` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `debug_enabled_by_id` bigint NULL DEFAULT NULL,
  `debug_enabled_by_name` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `last_cleanup_time` datetime NULL DEFAULT NULL,
  `last_cleanup_result_json` json NULL,
  `update_by_id` bigint NULL DEFAULT NULL,
  `update_by_name` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'system',
  `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `update_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `fk_log_policy_debug_by`(`debug_enabled_by_id` ASC) USING BTREE,
  INDEX `fk_log_policy_update_by`(`update_by_id` ASC) USING BTREE,
  CONSTRAINT `fk_log_policy_debug_by` FOREIGN KEY (`debug_enabled_by_id`) REFERENCES `sys_user` (`id`) ON DELETE SET NULL ON UPDATE RESTRICT,
  CONSTRAINT `fk_log_policy_update_by` FOREIGN KEY (`update_by_id`) REFERENCES `sys_user` (`id`) ON DELETE SET NULL ON UPDATE RESTRICT
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = 'system log retention and debug policy' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for operation_log
-- ----------------------------
DROP TABLE IF EXISTS `operation_log`;
CREATE TABLE `operation_log`  (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `log_time` datetime NOT NULL,
  `operator_id` bigint NULL DEFAULT NULL,
  `operator_name` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `module_name` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `action_name` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `level` varchar(16) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `content` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `detail_json` json NULL,
  `request_id` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `ip` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `user_agent` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `biz_type` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `biz_id` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `duration_ms` int NULL DEFAULT NULL,
  `result_status` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `idx_operation_log_log_time`(`log_time` ASC) USING BTREE,
  INDEX `idx_operation_log_level_log_time`(`level` ASC, `log_time` ASC) USING BTREE,
  INDEX `idx_operation_log_operator_log_time`(`operator_id` ASC, `log_time` ASC) USING BTREE,
  INDEX `idx_operation_log_module_log_time`(`module_name` ASC, `log_time` ASC) USING BTREE,
  INDEX `idx_operation_log_request_id`(`request_id` ASC) USING BTREE,
  INDEX `idx_operation_log_result_log_time`(`result_status` ASC, `log_time` ASC) USING BTREE,
  CONSTRAINT `fk_operation_log_operator` FOREIGN KEY (`operator_id`) REFERENCES `sys_user` (`id`) ON DELETE SET NULL ON UPDATE RESTRICT
) ENGINE = InnoDB AUTO_INCREMENT = 12 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = 'operation audit log' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for plc_connection_log
-- ----------------------------
DROP TABLE IF EXISTS `plc_connection_log`;
CREATE TABLE `plc_connection_log`  (
  `id` bigint NOT NULL AUTO_INCREMENT COMMENT 'PLC????ID',
  `device_id` bigint NULL DEFAULT NULL COMMENT '??ID',
  `event_type` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '?????connect / disconnect / timeout / status_update',
  `status` tinyint NULL DEFAULT NULL COMMENT 'PLC?????0=???1=???2=????',
  `messsage` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL COMMENT '????????????????? ORM',
  `create_time` datetime NULL DEFAULT CURRENT_TIMESTAMP COMMENT '??????',
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `idx_plc_connection_log_device_id`(`device_id` ASC) USING BTREE,
  INDEX `idx_plc_connection_log_create_time`(`create_time` ASC) USING BTREE,
  CONSTRAINT `fk_plc_connection_log_device` FOREIGN KEY (`device_id`) REFERENCES `device` (`id`) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE = InnoDB AUTO_INCREMENT = 224 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = 'PLC????????' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for sys_data_permission
-- ----------------------------
DROP TABLE IF EXISTS `sys_data_permission`;
CREATE TABLE `sys_data_permission`  (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `role_id` bigint NOT NULL,
  `scope_type` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'all',
  `scope_value` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '*',
  `enabled` tinyint(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `idx_sys_data_permission_role_id`(`role_id` ASC) USING BTREE,
  INDEX `idx_sys_data_permission_scope`(`scope_type` ASC, `scope_value` ASC) USING BTREE,
  CONSTRAINT `fk_sys_data_permission_role` FOREIGN KEY (`role_id`) REFERENCES `sys_role` (`id`) ON DELETE CASCADE ON UPDATE RESTRICT
) ENGINE = InnoDB AUTO_INCREMENT = 373 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = 'role data scope skeleton' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for sys_menu
-- ----------------------------
DROP TABLE IF EXISTS `sys_menu`;
CREATE TABLE `sys_menu`  (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `parent_id` bigint NULL DEFAULT NULL,
  `name` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `path` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `component` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `title_key` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `icon` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `menu_type` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'menu',
  `link` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `is_iframe` tinyint(1) NOT NULL DEFAULT 0,
  `keep_alive` tinyint(1) NOT NULL DEFAULT 0,
  `is_hide` tinyint(1) NOT NULL DEFAULT 0,
  `is_hide_tab` tinyint(1) NOT NULL DEFAULT 0,
  `fixed_tab` tinyint(1) NOT NULL DEFAULT 0,
  `active_path` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `is_full_page` tinyint(1) NOT NULL DEFAULT 0,
  `sort` int NOT NULL DEFAULT 1,
  `enabled` tinyint(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uq_sys_menu_name`(`name` ASC) USING BTREE,
  INDEX `idx_sys_menu_parent_id`(`parent_id` ASC) USING BTREE,
  INDEX `idx_sys_menu_sort`(`sort` ASC) USING BTREE,
  CONSTRAINT `fk_sys_menu_parent` FOREIGN KEY (`parent_id`) REFERENCES `sys_menu` (`id`) ON DELETE CASCADE ON UPDATE RESTRICT
) ENGINE = InnoDB AUTO_INCREMENT = 73 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = 'system menus' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for sys_permission
-- ----------------------------
DROP TABLE IF EXISTS `sys_permission`;
CREATE TABLE `sys_permission`  (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `permission_code` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `permission_name` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `permission_type` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'action',
  `menu_id` bigint NULL DEFAULT NULL,
  `auth_mark` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `description` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `sort` int NOT NULL DEFAULT 1,
  `enabled` tinyint(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uq_sys_permission_permission_code`(`permission_code` ASC) USING BTREE,
  INDEX `idx_sys_permission_menu_id`(`menu_id` ASC) USING BTREE,
  INDEX `idx_sys_permission_type`(`permission_type` ASC) USING BTREE,
  CONSTRAINT `fk_sys_permission_menu` FOREIGN KEY (`menu_id`) REFERENCES `sys_menu` (`id`) ON DELETE SET NULL ON UPDATE RESTRICT
) ENGINE = InnoDB AUTO_INCREMENT = 42 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = 'system permissions' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for sys_role
-- ----------------------------
DROP TABLE IF EXISTS `sys_role`;
CREATE TABLE `sys_role`  (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `role_code` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `role_name` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `description` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `enabled` tinyint(1) NOT NULL DEFAULT 1,
  `sort` int NOT NULL DEFAULT 1,
  `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `update_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uq_sys_role_role_code`(`role_code` ASC) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 10 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = 'system roles' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for sys_role_menu
-- ----------------------------
DROP TABLE IF EXISTS `sys_role_menu`;
CREATE TABLE `sys_role_menu`  (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `role_id` bigint NOT NULL,
  `menu_id` bigint NOT NULL,
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uq_sys_role_menu_role_menu`(`role_id` ASC, `menu_id` ASC) USING BTREE,
  INDEX `idx_sys_role_menu_role_id`(`role_id` ASC) USING BTREE,
  INDEX `idx_sys_role_menu_menu_id`(`menu_id` ASC) USING BTREE,
  CONSTRAINT `fk_sys_role_menu_menu` FOREIGN KEY (`menu_id`) REFERENCES `sys_menu` (`id`) ON DELETE CASCADE ON UPDATE RESTRICT,
  CONSTRAINT `fk_sys_role_menu_role` FOREIGN KEY (`role_id`) REFERENCES `sys_role` (`id`) ON DELETE CASCADE ON UPDATE RESTRICT
) ENGINE = InnoDB AUTO_INCREMENT = 3365 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = 'role menu mapping' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for sys_role_permission
-- ----------------------------
DROP TABLE IF EXISTS `sys_role_permission`;
CREATE TABLE `sys_role_permission`  (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `role_id` bigint NOT NULL,
  `permission_id` bigint NOT NULL,
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uq_sys_role_permission_role_permission`(`role_id` ASC, `permission_id` ASC) USING BTREE,
  INDEX `idx_sys_role_permission_role_id`(`role_id` ASC) USING BTREE,
  INDEX `idx_sys_role_permission_permission_id`(`permission_id` ASC) USING BTREE,
  CONSTRAINT `fk_sys_role_permission_permission` FOREIGN KEY (`permission_id`) REFERENCES `sys_permission` (`id`) ON DELETE CASCADE ON UPDATE RESTRICT,
  CONSTRAINT `fk_sys_role_permission_role` FOREIGN KEY (`role_id`) REFERENCES `sys_role` (`id`) ON DELETE CASCADE ON UPDATE RESTRICT
) ENGINE = InnoDB AUTO_INCREMENT = 8732 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = 'role permission mapping' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for sys_user
-- ----------------------------
DROP TABLE IF EXISTS `sys_user`;
CREATE TABLE `sys_user`  (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `user_name` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `password_hash` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `nick_name` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `email` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `phone` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `gender` varchar(16) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'unknown',
  `avatar` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `status` varchar(16) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '1',
  `last_login_time` datetime NULL DEFAULT NULL,
  `last_login_ip` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `create_by` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'system',
  `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `update_by` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'system',
  `update_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `deleted` tinyint(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uq_sys_user_user_name`(`user_name` ASC) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 13 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = 'system users' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for sys_user_role
-- ----------------------------
DROP TABLE IF EXISTS `sys_user_role`;
CREATE TABLE `sys_user_role`  (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `user_id` bigint NOT NULL,
  `role_id` bigint NOT NULL,
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uq_sys_user_role_user_role`(`user_id` ASC, `role_id` ASC) USING BTREE,
  INDEX `idx_sys_user_role_user_id`(`user_id` ASC) USING BTREE,
  INDEX `idx_sys_user_role_role_id`(`role_id` ASC) USING BTREE,
  CONSTRAINT `fk_sys_user_role_role` FOREIGN KEY (`role_id`) REFERENCES `sys_role` (`id`) ON DELETE CASCADE ON UPDATE RESTRICT,
  CONSTRAINT `fk_sys_user_role_user` FOREIGN KEY (`user_id`) REFERENCES `sys_user` (`id`) ON DELETE CASCADE ON UPDATE RESTRICT
) ENGINE = InnoDB AUTO_INCREMENT = 373 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = 'user role mapping' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for system_log
-- ----------------------------
DROP TABLE IF EXISTS `system_log`;
CREATE TABLE `system_log`  (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `log_time` datetime NOT NULL,
  `level` varchar(16) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `log_type` varchar(16) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `source` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `device_id` bigint NULL DEFAULT NULL,
  `device_code` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `point_id` bigint NULL DEFAULT NULL,
  `host_name` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `service_name` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `content` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `detail_json` json NULL,
  `trace_id` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `exception_stack` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
  `elapsed_ms` int NULL DEFAULT NULL,
  `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `idx_system_log_log_time`(`log_time` ASC) USING BTREE,
  INDEX `idx_system_log_level_log_time`(`level` ASC, `log_time` ASC) USING BTREE,
  INDEX `idx_system_log_log_type_log_time`(`log_type` ASC, `log_time` ASC) USING BTREE,
  INDEX `idx_system_log_device_log_time`(`device_id` ASC, `log_time` ASC) USING BTREE,
  INDEX `idx_system_log_trace_id`(`trace_id` ASC) USING BTREE,
  INDEX `fk_system_log_point`(`point_id` ASC) USING BTREE,
  INDEX `idx_system_log_source_log_time`(`source` ASC, `log_time` ASC) USING BTREE,
  INDEX `idx_system_log_service_log_time`(`service_name` ASC, `log_time` ASC) USING BTREE,
  INDEX `idx_system_log_device_code_log_time`(`device_code` ASC, `log_time` ASC) USING BTREE,
  CONSTRAINT `fk_system_log_device` FOREIGN KEY (`device_id`) REFERENCES `device` (`id`) ON DELETE SET NULL ON UPDATE RESTRICT,
  CONSTRAINT `fk_system_log_point` FOREIGN KEY (`point_id`) REFERENCES `device_variable` (`id`) ON DELETE SET NULL ON UPDATE RESTRICT
) ENGINE = InnoDB AUTO_INCREMENT = 1032 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = 'system runtime log' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for template_variable
-- ----------------------------
DROP TABLE IF EXISTS `template_variable`;
CREATE TABLE `template_variable`  (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `template_id` bigint NOT NULL,
  `var_seq` int NOT NULL COMMENT '?????????????????????????',
  `var_name` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `io_type` varchar(10) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `var_type` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `var_address` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `var_offset` int NULL DEFAULT NULL,
  `var_bit` int NULL DEFAULT NULL,
  `unit_id` bigint NULL DEFAULT NULL,
  `description` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL,
  `scan_rate` int NULL DEFAULT NULL,
  `is_alarm` tinyint NOT NULL,
  `is_storage` tinyint NOT NULL,
  `create_user` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL,
  `create_time` datetime NULL DEFAULT NULL,
  `update_user` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL,
  `update_time` datetime NULL DEFAULT NULL,
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uq_template_variable_template_var_address`(`template_id` ASC, `var_address` ASC) USING BTREE,
  UNIQUE INDEX `uq_template_variable_template_var_seq`(`template_id` ASC, `var_seq` ASC) USING BTREE,
  INDEX `idx_template_variable_template_id`(`template_id` ASC) USING BTREE,
  INDEX `idx_template_variable_scan_rate`(`scan_rate` ASC) USING BTREE,
  INDEX `idx_template_variable_is_alarm`(`is_alarm` ASC) USING BTREE,
  INDEX `idx_template_variable_unit_id`(`unit_id` ASC) USING BTREE,
  CONSTRAINT `fk_template_variable_template` FOREIGN KEY (`template_id`) REFERENCES `variable_template` (`id`) ON DELETE CASCADE ON UPDATE RESTRICT,
  CONSTRAINT `fk_template_variable_unit` FOREIGN KEY (`unit_id`) REFERENCES `unit_dict` (`id`) ON DELETE SET NULL ON UPDATE RESTRICT
) ENGINE = InnoDB AUTO_INCREMENT = 4 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '??????? device_variable ???????' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for unit_dict
-- ----------------------------
DROP TABLE IF EXISTS `unit_dict`;
CREATE TABLE `unit_dict`  (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `unit_code` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `unit_symbol` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `unit_name` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `unit_category` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `description` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `create_user` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL,
  `create_time` datetime NULL DEFAULT CURRENT_TIMESTAMP,
  `update_user` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL,
  `update_time` datetime NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uq_unit_dict_unit_code`(`unit_code` ASC) USING BTREE,
  UNIQUE INDEX `uq_unit_dict_unit_symbol`(`unit_symbol` ASC) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 13 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '???????' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for variable_template
-- ----------------------------
DROP TABLE IF EXISTS `variable_template`;
CREATE TABLE `variable_template`  (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `template_code` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `template_name` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `device_type_id` bigint NOT NULL,
  `version_no` int NOT NULL DEFAULT 1,
  `enabled` tinyint(1) NOT NULL DEFAULT 1,
  `description` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `create_user` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL,
  `create_time` datetime NULL DEFAULT CURRENT_TIMESTAMP,
  `update_user` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL,
  `update_time` datetime NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uq_variable_template_template_code`(`template_code` ASC) USING BTREE,
  INDEX `idx_variable_template_device_type_id`(`device_type_id` ASC) USING BTREE,
  INDEX `idx_variable_template_enabled`(`enabled` ASC) USING BTREE,
  CONSTRAINT `fk_variable_template_device_type` FOREIGN KEY (`device_type_id`) REFERENCES `device_type` (`id`) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE = InnoDB AUTO_INCREMENT = 4 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '??????' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Triggers structure for table device
-- ----------------------------
DROP TRIGGER IF EXISTS `trg_device_code_no_update`;
delimiter ;;
CREATE TRIGGER `trg_device_code_no_update` BEFORE UPDATE ON `device` FOR EACH ROW BEGIN
  IF NEW.`device_code` <> OLD.`device_code` THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'device_code ???????';
  END IF;
END
;;
delimiter ;

SET FOREIGN_KEY_CHECKS = 1;
