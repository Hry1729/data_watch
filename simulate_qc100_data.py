import taosws
import time
import random
from datetime import datetime

# ================= 配置区域 =================
# 写入频率 (秒)，例如 0.5 表示半秒写入一次
WRITE_INTERVAL = 0.1 

# TDengine 连接配置
TD_DSN = 'ws://root:Hry19990910@127.0.0.1:6041'
DB_NAME = 'qc'
STABLE_NAME = 'qc100'

# 点位信息 (var_seq, name, min_val, max_val)
POINTS = [
    (30, 'Input', 0, 1), # 模拟 BOOL
    (11894, 'Main Hoist General', 0, 50), # 模拟高度
    (11900, 'Main Trolley General', 0, 150) # 模拟位置
]
# ===========================================

def simulate():
    conn = None
    try:
        conn = taosws.connect(TD_DSN)
        print(f"Connected to TDengine. Writing data to {DB_NAME}.{STABLE_NAME}_[30, 11894, 11900]")
        print(f"Current write interval: {WRITE_INTERVAL}s")
        
        print("\nStarting simulation... Press Ctrl+C to stop.")
        
        while True:
            for var_seq, name, min_v, max_v in POINTS:
                table_name = f"{DB_NAME}.{STABLE_NAME}_{var_seq}"
                
                # 生成随机值
                if var_seq == 30:
                    val = float(random.randint(0, 1))
                else:
                    val = random.uniform(float(min_v), float(max_v))
                
                # 执行写入
                sql = f"INSERT INTO {table_name} VALUES (NOW, {val})"
                try:
                    conn.execute(sql)
                except Exception as e:
                    print(f"Failed to write to {table_name}: {e}")
            
            time.sleep(WRITE_INTERVAL)
            
    except KeyboardInterrupt:
        print("\nSimulation stopped.")
    except Exception as e:
        print(f"Error: {e}")
    finally:
        if conn:
            conn.close()

if __name__ == "__main__":
    simulate()
