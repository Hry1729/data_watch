#!/usr/bin/env python3
"""CLI 技术演示 - 5 分钟理解 CLI 原理"""
import sys
import json
import argparse

def main():
    # 1. 解析命令行参数
    parser = argparse.ArgumentParser(description='Data Watch CLI Demo')
    parser.add_argument('command', choices=['devices', 'points'])
    parser.add_argument('--status', help='过滤状态')
    parser.add_argument('--format', default='json', choices=['json', 'table'])

    args = parser.parse_args()

    # 2. 执行业务逻辑（这里模拟，实际会调用 Backend API）
    if args.command == 'devices':
        data = {
            "devices": [
                {"code": "PLC001", "name": "1号PLC", "status": "online"},
                {"code": "PLC002", "name": "2号PLC", "status": "offline"}
            ]
        }
        if args.status:
            data["devices"] = [d for d in data["devices"] if d["status"] == args.status]
    else:
        data = {"points": []}

    # 3. 输出结果到 stdout（AI 会读取这个）
    if args.format == 'json':
        print(json.dumps(data, ensure_ascii=False, indent=2))
    else:
        # 表格格式（人类友好）
        for device in data.get("devices", []):
            print(f"{device['code']}\t{device['name']}\t{device['status']}")

    # 4. 返回退出码
    return 0

if __name__ == '__main__':
    try:
        sys.exit(main())
    except Exception as e:
        # 错误信息输出到 stderr（不污染 stdout 的 JSON）
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)
