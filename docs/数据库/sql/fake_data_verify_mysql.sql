-- Executable MySQL verify script for STEP-007
USE device_watch;
SELECT id, protocol, port FROM comm_protocol WHERE id IN (9901, 9902, 9903) ORDER BY id;
SELECT id, group_code, group_name FROM device_group WHERE id IN (9910, 9911, 9912) ORDER BY id;
SELECT id, type_code, type_name FROM device_type WHERE id IN (9920, 9921, 9922) ORDER BY id;
SELECT id, device_code, device_name, status, protocol_id, endpoint FROM device WHERE id IN (991001, 991002, 991003) ORDER BY id;
SELECT id, device_id, var_seq, var_name, io_type, var_type, var_address, is_alarm, is_storage, CONCAT((SELECT device_code FROM device WHERE device.id = device_variable.device_id), '_', var_seq) AS td_subtable_name FROM device_variable WHERE id IN (992001, 992002, 992003, 992004, 992005, 992006, 992007) ORDER BY id;
