from pathlib import Path
import re

root = Path(r"D:\python\device_alarm\data_watch\outputs\多协议工业设备数据统一采集与实时监控\qa")
source = root / "交底书草稿_v2.md"
target = root / "交底书草稿_v3.md"

system_diagram = r'''```mermaid
flowchart TB
  subgraph R1[" "]
    direction LR
    A["异构工业设备<br/>寄存器型、节点型、传统组件型、外部推送"] --> B["协议适配与任务编排<br/>读块合并、节点批读、代理推送"] --> C["统一点状态<br/>设备、点、值、时间戳、质量码"]
  end
  subgraph R2[" "]
    direction LR
    D["多语义实时缓存<br/>短窗口、持久化消息流、最新状态"] --> E["快照与差分推送"] --> F["浏览器双缓冲<br/>当前值按点覆盖、趋势记录保序"]
  end
  subgraph R3[" "]
    direction LR
    H["任务快照与设备代际校验"] --> I["消费组恢复、批量入库与死信隔离"] --> G["列式流式表增量更新<br/>数值型与状态型分型展示"]
  end
  C --> D
  F --> G
  H -.约束.-> B
  H -.约束.-> D
  D --> I
```'''

text = source.read_text(encoding="utf-8")
match = re.search(r"```mermaid\s*\n.*?```", text, flags=re.S)
if match is None:
    raise RuntimeError("system mermaid block not found")
text = text[:match.start()] + system_diagram + text[match.end():]
target.write_text(text, encoding="utf-8")
print(target)
