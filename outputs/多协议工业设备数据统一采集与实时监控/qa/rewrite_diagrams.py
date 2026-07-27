from pathlib import Path
import re

root = Path(r"D:\python\device_alarm\data_watch\outputs\多协议工业设备数据统一采集与实时监控\qa")
source = root / "交底书草稿.md"
target = root / "交底书草稿_v2.md"

system_diagram = r'''```mermaid
flowchart TB
  A["异构工业设备<br/>寄存器型、节点型、传统组件型、外部推送"]
  B["协议适配与任务编排<br/>读块合并、节点批读、代理推送"]
  C["统一点状态<br/>设备、点、值、时间戳、质量码"]
  D["多语义实时缓存<br/>短窗口、持久化消息流、最新状态"]
  E["快照与差分推送"]
  F["浏览器双缓冲<br/>当前值按点覆盖、趋势记录保序"]
  G["列式流式表增量更新<br/>数值型与状态型分型展示"]
  H["任务快照与设备代际校验"]
  I["消费组恢复、批量入库与死信隔离"]
  A --> B --> C --> D --> E --> F --> G
  H -.约束.-> B
  H -.约束.-> D
  D --> I
```'''

flow_diagram = r'''```mermaid
flowchart LR
  subgraph P1["采集与统一"]
    direction TB
    S1["S1 读取设备与点配置"] --> S2["S2 按设备和周期建立任务"]
    S2 --> S3{"协议支持连续块读取？"}
    S3 -- "是" --> S4["S4 合并地址块并建偏移映射"]
    S3 -- "否" --> S5["S5 建立节点集合或推送映射"]
    S4 --> S6["S6 固化任务快照与代际号"]
    S5 --> S6
    S6 --> S7["S7 读取设备或接收推送"] --> S8["S8 转换统一点状态"]
    S8 --> S9{"S9 代际仍有效？"}
    S9 -- "否" --> S10["S10 丢弃旧代际结果"]
  end
  subgraph P2["缓存一致性"]
    direction TB
    S11["S11 写短窗口和消息流"] --> S12{"S12 前置写入成功？"}
    S12 -- "否" --> S13["S13 不刷新最新状态"]
    S12 -- "是" --> S14["S14 最后写最新状态"]
  end
  subgraph P3["状态传播"]
    direction TB
    S15{"S15 新建或重建订阅？"}
    S15 -- "是" --> S16["S16 发送快照并建基线"]
    S15 -- "否" --> S17["S17 计算差分并推送"]
  end
  subgraph P4["浏览器增量处理"]
    direction TB
    S18["S18 快照立即应用"]
    S19["S19 差分写入双缓冲"] --> S21{"S21 达到刷新周期？"}
    S21 -- "否" --> S19
    S21 -- "是" --> S22["S22 批量更新页面和趋势"]
    S18 --> S20["S20 增量更新列式流式表"]
    S22 --> S20 --> S23["S23 分型刷新视图"]
  end
  S9 -- "是" --> S11
  S14 --> S15
  S16 --> S18
  S17 --> S19
  S23 -.下一周期.-> S7
```'''

text = source.read_text(encoding="utf-8")
blocks = list(re.finditer(r"```mermaid\s*\n.*?```", text, flags=re.S))
if len(blocks) != 2:
    raise RuntimeError(f"expected 2 mermaid blocks, found {len(blocks)}")
text = text[:blocks[0].start()] + system_diagram + text[blocks[0].end():blocks[1].start()] + flow_diagram + text[blocks[1].end():]
target.write_text(text, encoding="utf-8")
print(target)
