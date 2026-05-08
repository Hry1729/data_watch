-- OPC DA 中心采集协议初始化 SQL
-- 在系统中注册 opcda_center 协议

-- 1. 新增协议（使用 protocol_id 范围 9000-9999）
INSERT INTO comm_protocol (id, protocol, port, description)
VALUES (9001, 'opcda_center', 9090, 'OPC DA Center Collection via Proxy')
ON DUPLICATE KEY UPDATE
    protocol = VALUES(protocol),
    port = VALUES(port),
    description = VALUES(description);

-- 2. 查看当前设备使用的协议
-- SELECT d.device_code, d.device_id, cp.protocol, d.endpoint
-- FROM device d
-- JOIN comm_protocol cp ON d.protocol_id = cp.id
-- WHERE d.device_code LIKE 'agv%';

-- 3. 切换设备使用中心采集协议（示例）
-- UPDATE device SET protocol_id = 9001
-- WHERE device_code = 'agv801' AND protocol_id = 9999;

-- 4. 配置设备 endpoint 为 OPC VM 地址（示例）
-- UPDATE device SET endpoint = '172.27.18.221'
-- WHERE device_code = 'agv801';

-- 5. 验证配置
-- SELECT
--     d.device_code,
--     d.device_id,
--     cp.protocol,
--     d.endpoint as opc_vm_host,
--     d.connection_config
-- FROM device d
-- JOIN comm_protocol cp ON d.protocol_id = cp.id
-- WHERE d.device_code LIKE 'agv%';
