-- 首批联调测试数据落库 SQL（MySQL）
-- 适用范围：STEP-006 / docs/前后端联调/08-首批测试假数据落库清单.md
-- 用途：为 device-tree / realtime-init / ws/realtime 提供首轮主数据基线
-- 当前口径：device_variable 以 var_seq 为唯一业务键，TDengine 子表名由 device_code + '_' + var_seq 派生

USE device_watch;

/* ---------------------------------------------------------------------------
 * 一、基础字典
 * ------------------------------------------------------------------------- */

INSERT INTO comm_protocol (
    id, protocol, port, description, create_user, create_time
) VALUES
    (9901, 'SNAP7', 102, 'STEP-006 test protocol', 'coordinator', NOW()),
    (9902, 'GE SRTP', 18245, 'STEP-006 test protocol', 'coordinator', NOW()),
    (9903, 'OPC-UA', 4840, 'STEP-012 test protocol', 'coordinator', NOW())
ON DUPLICATE KEY UPDATE
    protocol = VALUES(protocol),
    port = VALUES(port),
    description = VALUES(description);

INSERT INTO device_type (
    id, type_code, type_name, description, create_user, create_time
) VALUES
    (9920, 'QC', '桥吊', 'STEP-006 test device type', 'coordinator', NOW()),
    (9921, 'ASC', '轨道吊', 'STEP-012 test device type', 'coordinator', NOW()),
    (9922, 'AGV', 'AGV', 'STEP-012 test device type', 'coordinator', NOW())
ON DUPLICATE KEY UPDATE
    type_code = VALUES(type_code),
    type_name = VALUES(type_name),
    description = VALUES(description);

INSERT INTO device_group (
    id, group_code, group_name, description, create_user, create_time
) VALUES
    (9910, 'QC', '桥吊', 'STEP-006 test device group', 'coordinator', NOW()),
    (9911, 'ASC', '轨道吊', 'STEP-012 test device group', 'coordinator', NOW()),
    (9912, 'AGV', 'AGV', 'STEP-012 test device group', 'coordinator', NOW())
ON DUPLICATE KEY UPDATE
    group_code = VALUES(group_code),
    group_name = VALUES(group_name),
    description = VALUES(description);

/* ---------------------------------------------------------------------------
 * 二、设备主数据
 * ------------------------------------------------------------------------- */

INSERT INTO device (
    id, device_code, device_name, type_id, group_id, status, comm_type,
    protocol_id, endpoint, endpoint_bkp, rack, slot, scan_cycle,
    create_user, create_time, update_user, update_time
) VALUES
    (991001, 'qc100', 'QC Device 100', 9920, 9910, 1, 'PLC', 9901,
        '127.0.0.1:102', NULL, 0, 1, 1000, 'coordinator', NOW(), 'coordinator', NOW()),
    (991002, 'asc7011', 'ASC Device 7011', 9921, 9911, 0, 'PLC', 9902,
        '127.0.0.1:18245', NULL, 0, 1, 1000, 'coordinator', NOW(), 'coordinator', NOW()),
    (991003, 'agv801', 'AGV Device 801', 9922, 9912, 1, 'PLC', 9903,
        'opc.tcp://127.0.0.1:4840', NULL, 0, 1, 1000, 'coordinator', NOW(), 'coordinator', NOW())
ON DUPLICATE KEY UPDATE
    device_name = VALUES(device_name),
    type_id = VALUES(type_id),
    group_id = VALUES(group_id),
    status = VALUES(status),
    comm_type = VALUES(comm_type),
    protocol_id = VALUES(protocol_id),
    endpoint = VALUES(endpoint),
    rack = VALUES(rack),
    slot = VALUES(slot),
    scan_cycle = VALUES(scan_cycle),
    update_user = VALUES(update_user),
    update_time = VALUES(update_time);

/* ---------------------------------------------------------------------------
 * 三、点位主数据（var_seq 纯序号；QC 默认主显示点已切到 30 / 11894 / 11900）
 * ------------------------------------------------------------------------- */

INSERT INTO device_variable (
    id, device_id, var_seq, var_name, io_type, var_type, var_address,
    var_offset, var_bit, description, scan_rate, is_alarm, is_storage,
    create_user, create_time, update_user, update_time
) VALUES
    (992001, 991001, 30, 'Input', 'I', 'BOOL', '%I101.0',
        101, 0, '控制合//+01F15/15CA10E', 1000, 1, 1, 'coordinator', NOW(), 'coordinator', NOW()),
    (992002, 991001, 11894, 'Main Hoist General', 'M', 'DINT', '%MD1304',
        1304, NULL, '主起升 位置值 （1=1mm)//', 100, 0, 1, 'coordinator', NOW(), 'coordinator', NOW()),
    (992003, 991001, 11900, 'Main Trolley General', 'M', 'DINT', '%MD1328',
        1328, NULL, '主小车 位置 （1=1mm)//', 100, 0, 1, 'coordinator', NOW(), 'coordinator', NOW()),
    (992004, 991002, 85, 'Run State', 'R', 'INT', '%R100',
        100, 0, 'STEP-006 stale state', 1000, 0, 1, 'coordinator', NOW(), 'coordinator', NOW()),
    (992005, 991002, 4200, 'Load', 'R', 'REAL', '%R101',
        101, 0, 'STEP-006 stale load', 1000, 0, 1, 'coordinator', NOW(), 'coordinator', NOW()),
    (992006, 991003, 100, 'Run State', 'R', 'INT', '%R200',
        200, 0, 'STEP-006 alarm device state', 1000, 0, 1, 'coordinator', NOW(), 'coordinator', NOW()),
    (992007, 991003, 200, 'Load', 'R', 'REAL', '%R201',
        201, 0, 'STEP-006 alarm value', 1000, 1, 1, 'coordinator', NOW(), 'coordinator', NOW())
ON DUPLICATE KEY UPDATE
    device_id = VALUES(device_id),
    var_seq = VALUES(var_seq),
    var_name = VALUES(var_name),
    io_type = VALUES(io_type),
    var_type = VALUES(var_type),
    var_address = VALUES(var_address),
    var_offset = VALUES(var_offset),
    var_bit = VALUES(var_bit),
    description = VALUES(description),
    scan_rate = VALUES(scan_rate),
    is_alarm = VALUES(is_alarm),
    is_storage = VALUES(is_storage),
    update_user = VALUES(update_user),
    update_time = VALUES(update_time);
