#!/usr/bin/env python3
"""
重建 TDengine 超级表脚本

将 qc、asc、agv 数据库中的超级表重建为新的统一格式：
- 数据列: ts (TIMESTAMP), value (DOUBLE), quality (TINYINT)
- 标签: var_seq (INT)

注意：该脚本会 DROP STABLE，执行后会删除对应超级表下所有子表和历史数据。
"""

import taosws


def main():
    # 连接 TDengine
    conn = taosws.connect('ws://root:Hry19990910@127.0.0.1:6041')

    databases = ['qc', 'asc', 'agv']
    new_stable_schema = "(`ts` TIMESTAMP, `value` DOUBLE, `quality` TINYINT DEFAULT 0) TAGS (`var_seq` INT)"

    for db_name in databases:
        print(f"\n=== 处理数据库: {db_name} ===")

        # 获取现有超级表
        try:
            # asc 是 SQL 关键字，需要用反引号
            result = conn.query(f'SHOW `{db_name}`.STABLES')
            stables = [row[0] for row in result]
            print(f"现有超级表数量: {len(stables)}")
        except Exception as e:
            print(f"获取超级表失败: {e}")
            continue

        # 删除旧超级表并创建新超级表
        success_count = 0
        for stable_name in stables:
            try:
                # 删除旧超级表（会同时删除所有子表）
                conn.execute(f'DROP STABLE IF EXISTS `{db_name}`.`{stable_name}`')

                # 创建新超级表
                conn.execute(
                    f'CREATE STABLE IF NOT EXISTS `{db_name}`.`{stable_name}` '
                    f'{new_stable_schema}'
                )
                success_count += 1
                print(f"  重建: {stable_name}")
            except Exception as e:
                print(f"  失败: {stable_name} - {e}")

        print(f"成功重建: {success_count}/{len(stables)}")

        # 验证结果
        try:
            result = conn.query(f'SHOW `{db_name}`.STABLES')
            new_count = len(list(result))
            print(f"重建后超级表数量: {new_count}")
        except Exception as e:
            print(f"验证失败: {e}")

    conn.close()
    print("\n=== 完成 ===")


if __name__ == "__main__":
    main()
