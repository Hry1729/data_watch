-- 首批联调测试数据验证 SQL（MySQL + TDengine）
-- 适用范围：STEP-006 / docs/前后端联调/08-首批测试假数据落库清单.md
-- 说明：本文件仅作为执行准备模板，不代表数据已实际落库
-- 当前基线：MySQL 使用 var_seq 作为点位业务键；TDengine 使用 var_seq TAG，子表名由 device_code + '_' + var_seq 派生

/* ---------------------------------------------------------------------------
 * 一、MySQL 主数据验证
 * ------------------------------------------------------------------------- */

USE device_watch;

SELECT id, protocol, port
FROM comm_protocol
WHERE id IN (9901, 9902, 9903)
ORDER BY id;

SELECT id, group_code, group_name
FROM device_group
WHERE id IN (9910, 9911, 9912)
ORDER BY id;

SELECT id, type_code, type_name
FROM device_type
WHERE id IN (9920, 9921, 9922)
ORDER BY id;

SELECT
    id,
    device_code,
    device_name,
    type_id,
    group_id,
    status,
    comm_type,
    protocol_id,
    endpoint,
    rack,
    slot,
    scan_cycle
FROM device
WHERE id IN (991001, 991002, 991003)
ORDER BY id;

SELECT
    id,
    device_id,
    var_seq,
    var_name,
    io_type,
    var_type,
    var_address,
    var_offset,
    var_bit,
    scan_rate,
    is_alarm,
    is_storage,
    CONCAT((SELECT device_code FROM device WHERE device.id = device_variable.device_id), '_', var_seq) AS td_subtable_name
FROM device_variable
WHERE id IN (992001, 992002, 992003, 992004, 992005, 992006, 992007)
ORDER BY id;

/* ---------------------------------------------------------------------------
 * 二、TDengine 时序数据验证
 * 当前命名约定：
 *   database = device_type lower（例：qc, asc, agv）
 *   stable   = device_code（例：qc100, asc7011, agv801）
 *   subtable = {device_code}_{var_seq}
 *   TAGS     = var_seq
 *   行结构：ts, value
 * ------------------------------------------------------------------------- */

SHOW DATABASES;

USE qc;
SHOW STABLES LIKE 'qc100';
SHOW TABLES LIKE 'qc100_30';
SHOW TABLES LIKE 'qc100_11894';
SHOW TABLES LIKE 'qc100_11900';

USE asc;
SHOW STABLES LIKE 'asc7011';
SHOW TABLES LIKE 'asc7011_85';
SHOW TABLES LIKE 'asc7011_4200';

USE agv;
SHOW STABLES LIKE 'agv801';
SHOW TABLES LIKE 'agv801_100';
SHOW TABLES LIKE 'agv801_200';

SELECT ts, value FROM `qc`.`qc100_30` ORDER BY ts DESC LIMIT 20;
SELECT ts, value FROM `qc`.`qc100_11894` ORDER BY ts DESC LIMIT 20;
SELECT ts, value FROM `qc`.`qc100_11900` ORDER BY ts DESC LIMIT 20;
SELECT ts, value FROM `asc`.`asc7011_85` ORDER BY ts DESC LIMIT 20;
SELECT ts, value FROM `asc`.`asc7011_4200` ORDER BY ts DESC LIMIT 20;
SELECT ts, value FROM `agv`.`agv801_100` ORDER BY ts DESC LIMIT 20;
SELECT ts, value FROM `agv`.`agv801_200` ORDER BY ts DESC LIMIT 20;
