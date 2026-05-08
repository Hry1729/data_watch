#!/usr/bin/env python3
"""
生成 AGV OPC DA 设备和点位配置 SQL

从 CSV 文件生成 MySQL INSERT 语句，用于配置 AGV801-AGV810 的 OPC DA 点位。
"""

import csv
import os
from pathlib import Path
from typing import List, Dict, Any


# 配置常量
AGV_DEVICES = ["agv801", "agv802", "agv803", "agv804", "agv805",
               "agv806", "agv807", "agv808", "agv809", "agv810"]
AGV_GROUPS = ["Control", "Fault", "ViewMain", "ViewPage1", "ViewPage2",
              "ViewPage3", "ViewPage4", "ViewPage5", "ViewPage6"]
CHANNEL = "OPD_Server"
OPC_SERVER = "ZPMC.OPCServer"

# OPC VM 地址（所有 AGV 共用同一个 VM）
OPC_VM_HOST = "172.27.18.221"

# ID 起始值
DEVICE_ID_START = 100000  # AGV 设备 ID 起始
VARIABLE_ID_START = 200000  # AGV 点位 ID 起始
DEVICE_TYPE_ID = 9922  # AGV 设备类型
DEVICE_GROUP_ID = 9912  # AGV 设备组
PROTOCOL_ID_OPCDA_SIDECAR = 9999  # opcda_sidecar 协议 ID（需要确认或创建）

# CSV 目录
CSV_DIR = Path(__file__).parent.parent / "sidecars" / "opcda_push_service" / "data" / "AGV801"


def parse_csv_file(csv_path: Path) -> List[Dict[str, Any]]:
    """解析 CSV 文件，返回点位列表"""
    points = []
    if not csv_path.exists():
        print(f"Warning: CSV file not found: {csv_path}")
        return points

    # 尝试多种编码
    encodings = ["utf-8", "gbk", "gb2312", "utf-16", "latin-1"]
    content = None

    for encoding in encodings:
        try:
            with open(csv_path, "r", encoding=encoding) as f:
                content = f.read()
            break
        except UnicodeDecodeError:
            continue

    if content is None:
        print(f"Warning: Cannot decode CSV file: {csv_path}")
        return points

    reader = csv.reader(content.strip().split("\n"))
    for row in reader:
        if len(row) >= 3:
            tag = row[0].strip('"')
            address = row[1].strip('"')
            data_type = row[2].strip('"')
            points.append({
                "tag": tag,
                "address": address,
                "data_type": data_type,
            })
    return points


def generate_item_id(device: str, group: str, tag: str) -> str:
    """生成完整的 OPC DA item_id"""
    return f"{CHANNEL}.{device.upper()}.{group}.{tag}"


def map_data_type(opc_type: str) -> str:
    """将 OPC DA 数据类型映射到 MySQL var_type"""
    type_map = {
        "Boolean": "BOOL",
        "Bool": "BOOL",
        "Int16": "INT",
        "Int32": "DINT",
        "Int64": "LINT",
        "Float": "REAL",
        "Double": "LREAL",
        "String": "STRING",
    }
    return type_map.get(opc_type, "REAL")


def generate_sql():
    """生成 SQL 脚本"""
    output_lines = []

    # 文件头
    output_lines.append("-- AGV OPC DA 设备和点位配置")
    output_lines.append("-- 生成时间: 自动生成")
    output_lines.append("-- 用途: 对接 OPC DA sidecar 推送数据")
    output_lines.append("")

    # 1. 确保 opcda_sidecar 协议存在
    output_lines.append("-- 确保 opcda_sidecar 协议存在")
    output_lines.append(f"""
INSERT INTO comm_protocol (id, protocol, port, description)
VALUES ({PROTOCOL_ID_OPCDA_SIDECAR}, 'opcda_sidecar', NULL, 'OPC DA Sidecar HTTP Push')
ON DUPLICATE KEY UPDATE
    protocol = VALUES(protocol),
    description = VALUES(description);
""")

    # 2. 生成设备配置
    output_lines.append("-- AGV 设备配置")
    output_lines.append("INSERT INTO device (")
    output_lines.append("    id, device_code, device_name, type_id, group_id, status,")
    output_lines.append("    protocol_id, endpoint, rack, slot, scan_cycle,")
    output_lines.append("    create_user, create_time, update_user, update_time")
    output_lines.append(") VALUES")

    device_values = []
    for i, device_code in enumerate(AGV_DEVICES):
        device_id = DEVICE_ID_START + i
        device_name = f"AGV Device {device_code.upper()}"
        # endpoint 存储 OPC VM 地址，用于标识设备来源和聚合
        device_values.append(
            f"    ({device_id}, '{device_code}', '{device_name}', {DEVICE_TYPE_ID}, "
            f"{DEVICE_GROUP_ID}, 1, {PROTOCOL_ID_OPCDA_SIDECAR}, "
            f"'{OPC_VM_HOST}', 0, 1, 500, "
            f"'agv_setup', NOW(), 'agv_setup', NOW())"
        )

    output_lines.append(",\n".join(device_values))
    output_lines.append("ON DUPLICATE KEY UPDATE")
    output_lines.append("    device_name = VALUES(device_name),")
    output_lines.append("    protocol_id = VALUES(protocol_id),")
    output_lines.append("    endpoint = VALUES(endpoint),")
    output_lines.append("    scan_cycle = VALUES(scan_cycle),")
    output_lines.append("    update_user = VALUES(update_user),")
    output_lines.append("    update_time = VALUES(update_time);")
    output_lines.append("")

    # 3. 收集所有点位
    all_points = []
    var_seq_counter = 1

    for device_idx, device_code in enumerate(AGV_DEVICES):
        device_id = DEVICE_ID_START + device_idx

        for group in AGV_GROUPS:
            csv_path = CSV_DIR / f"{group}.csv"
            points = parse_csv_file(csv_path)

            for point in points:
                item_id = generate_item_id(device_code, group, point["tag"])
                var_type = map_data_type(point["data_type"])

                all_points.append({
                    "id": VARIABLE_ID_START + len(all_points),
                    "device_id": device_id,
                    "var_seq": var_seq_counter,
                    "var_name": f"{group}_{point['tag']}",
                    "var_type": var_type,
                    "var_address": item_id,  # 关键：存储完整的 item_id
                    "description": f"AGV {device_code} {group} {point['tag']}",
                    "tag": point["tag"],
                    "group": group,
                })
                var_seq_counter += 1

    # 4. 生成点位配置 SQL（分批）
    output_lines.append("-- AGV 点位配置")
    output_lines.append(f"-- 共 {len(all_points)} 个点位")
    output_lines.append("")

    batch_size = 1000
    for batch_start in range(0, len(all_points), batch_size):
        batch = all_points[batch_start:batch_start + batch_size]

        output_lines.append("INSERT INTO device_variable (")
        output_lines.append("    id, device_id, var_seq, var_name, io_type, var_type, var_address,")
        output_lines.append("    scan_rate, is_alarm, is_storage,")
        output_lines.append("    create_user, create_time, update_user, update_time")
        output_lines.append(") VALUES")

        var_values = []
        for p in batch:
            var_values.append(
                f"    ({p['id']}, {p['device_id']}, {p['var_seq']}, '{p['var_name']}', "
                f"'I', '{p['var_type']}', '{p['var_address']}', "
                f"500, 0, 1, 'agv_setup', NOW(), 'agv_setup', NOW())"
            )

        output_lines.append(",\n".join(var_values))
        output_lines.append("ON DUPLICATE KEY UPDATE")
        output_lines.append("    var_name = VALUES(var_name),")
        output_lines.append("    var_type = VALUES(var_type),")
        output_lines.append("    var_address = VALUES(var_address),")
        output_lines.append("    is_storage = VALUES(is_storage),")
        output_lines.append("    update_user = VALUES(update_user),")
        output_lines.append("    update_time = VALUES(update_time);")
        output_lines.append("")

    # 5. 生成验证查询
    output_lines.append("-- 验证配置")
    output_lines.append("SELECT d.device_code, COUNT(v.id) as point_count")
    output_lines.append("FROM device d")
    output_lines.append("LEFT JOIN device_variable v ON d.id = v.device_id")
    output_lines.append("WHERE d.device_code LIKE 'agv%'")
    output_lines.append("GROUP BY d.device_code;")
    output_lines.append("")
    output_lines.append("-- 验证 item_id 映射")
    output_lines.append("SELECT id, device_id, var_seq, var_name, var_address")
    output_lines.append("FROM device_variable")
    output_lines.append("WHERE device_id >= {DEVICE_ID_START} AND device_id < {DEVICE_ID_START + len(AGV_DEVICES)}")
    output_lines.append("LIMIT 10;")

    return "\n".join(output_lines)


def main():
    """主函数"""
    sql = generate_sql()

    output_path = Path(__file__).parent / "agv_opcda_config.sql"
    with open(output_path, "w", encoding="utf-8") as f:
        f.write(sql)

    print(f"SQL 脚本已生成: {output_path}")
    print(f"设备数量: {len(AGV_DEVICES)}")
    print(f"设备 ID 范围: {DEVICE_ID_START} - {DEVICE_ID_START + len(AGV_DEVICES) - 1}")

    # 统计点位数量
    total_points = 0
    for group in AGV_GROUPS:
        csv_path = CSV_DIR / f"{group}.csv"
        points = parse_csv_file(csv_path)
        total_points += len(points)

    total_points *= len(AGV_DEVICES)
    print(f"预估点位总数: {total_points}")


if __name__ == "__main__":
    main()
