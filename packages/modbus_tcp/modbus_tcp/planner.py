"""Modbus 读取计划与结果还原。"""

from __future__ import annotations

from typing import Any, Dict, Iterable, List, Mapping, Tuple

from drivers.base import ReadBlock

from .address import parse_address

_MAX_COUNT_BY_FC = {1: 2000, 3: 125}


def _item_value(item: Any, name: str, default: Any = None) -> Any:
    """同时兼容变量对象与字典输入。"""
    if isinstance(item, Mapping):
        return item.get(name, default)
    return getattr(item, name, default)


def _validate_count(raw_count: Any) -> int:
    """拒绝非法读取长度，避免静默把错误配置改成单点读取。"""
    if type(raw_count) is not int or raw_count <= 0:
        raise ValueError(f"Modbus count 必须是正整数，当前为 {raw_count!r}")
    return raw_count


def _address_with_start(address: str, area: str, start: int) -> str:
    """按原始地址风格生成分片块起始键。"""
    text = address.strip()
    if text.upper().startswith("HR"):
        return f"HR{start}"
    if text.upper().startswith("C") and not text.lower().startswith("coil:"):
        return f"C{start}"
    return f"{area}:{start}"


def build_read_plan(
    variables: Iterable[Any], *, unit_id: int = 1
) -> Tuple[List[ReadBlock], Dict[str, Dict[str, Any]]]:
    """构建受 Modbus FC 上限约束的读取块和 variable_id 结果映射。"""
    items: List[Dict[str, Any]] = []
    for variable in variables:
        variable_id = _item_value(variable, "variable_id")
        address = _item_value(variable, "address")
        data_type = str(_item_value(variable, "data_type", "")).upper()
        count = _validate_count(_item_value(variable, "count", 1))
        if not variable_id:
            raise ValueError("Modbus variable_id 不能为空")
        area, start = parse_address(address)
        if start < 0 or start > 65535:
            raise ValueError(
                f"Modbus 地址越界: {address!r} start={start}，合法范围为 0..65535"
            )
        if start + count > 65536:
            raise ValueError(
                f"Modbus 读取越界: {address!r} start={start} count={count}，"
                f"要求 start+count <= 65536"
            )
        expected_type = "BOOL" if area == "coil" else "WORD"
        if data_type != expected_type:
            raise ValueError(
                f"Modbus {area} 只支持 {expected_type}，变量 {variable_id!r} 当前为 {data_type!r}"
            )
        items.append(
            {
                "variable_id": variable_id,
                "address": address,
                "start": start,
                "count": count,
                "data_type": data_type,
                "area": area,
                "fc": 1 if area == "coil" else 3,
            }
        )

    blocks: List[ReadBlock] = []
    result_map: Dict[str, Dict[str, Any]] = {
        item["variable_id"]: {
            "protocol": "modbus_tcp",
            "address": item["address"],
            "start": item["start"],
            "count": item["count"],
            "data_type": item["data_type"],
            "slices": [],
        }
        for item in items
    }

    current: List[Dict[str, Any]] = []
    current_end: int | None = None

    def flush_group() -> None:
        """将连续变量组按功能码请求上限拆片，并记录每变量的切片绑定。"""
        nonlocal current, current_end
        if not current:
            return
        group_start = current[0]["start"]
        group_end = current_end
        assert group_end is not None
        limit = _MAX_COUNT_BY_FC[current[0]["fc"]]
        chunk_start = group_start
        while chunk_start < group_end:
            slice_count = min(limit, group_end - chunk_start)
            first = current[0]
            block_address = _address_with_start(
                first["address"], first["area"], chunk_start
            )
            block = ReadBlock(
                block_address, count=slice_count, data_type=first["data_type"]
            )
            block.start = chunk_start
            block.source_offset = chunk_start - group_start
            block.slice_count = slice_count
            blocks.append(block)
            chunk_end = chunk_start + slice_count
            for item in current:
                item_end = item["start"] + item["count"]
                overlap_start = max(chunk_start, item["start"])
                overlap_end = min(chunk_end, item_end)
                if overlap_start >= overlap_end:
                    continue
                result_map[item["variable_id"]]["slices"].append(
                    {
                        "block_address": block_address,
                        "source_offset": overlap_start - item["start"],
                        "slice_count": overlap_end - overlap_start,
                        "block_offset": overlap_start - chunk_start,
                    }
                )
            chunk_start = chunk_end
        current = []
        current_end = None

    for item in sorted(items, key=lambda value: (value["fc"], value["start"])):
        if (
            current
            and (
                item["fc"] != current[0]["fc"]
                or item["start"] != current_end
            )
        ):
            flush_group()
        current.append(item)
        current_end = item["start"] + item["count"]
    flush_group()
    return blocks, result_map


def decode_read_results(
    result: Mapping[str, Any], result_map: Mapping[str, Mapping[str, Any]]
) -> Dict[str, Any]:
    """依据结果映射还原为 variable_id → 标量或列表；缺失分片不伪造零值。"""
    decoded: Dict[str, Any] = {}
    for variable_id, binding in result_map.items():
        values: List[Any] = []
        complete = True
        for slice_binding in binding.get("slices", []):
            raw = result.get(slice_binding["block_address"])
            if raw is None:
                complete = False
                break
            part = raw if isinstance(raw, list) else [raw]
            slice_count = int(slice_binding["slice_count"])
            if len(part) < slice_count:
                complete = False
                break
            offset = int(slice_binding.get("block_offset", 0))
            if len(part) < offset + slice_count:
                complete = False
                break
            values.extend(part[offset:offset + slice_count])
        if not complete or len(values) != int(binding["count"]):
            continue
        decoded[variable_id] = values[0] if int(binding["count"]) == 1 else values
    return decoded


def plan_read_blocks(blocks: List[ReadBlock], unit_id: int = 1) -> List[Dict[str, Any]]:
    """将 ReadBlock 列表合并为 Modbus 读请求计划。"""
    grouped: Dict[tuple[int, int], List[dict]] = {}

    for block in blocks:
        area, start = parse_address(block.address)
        fc = 1 if area == "coil" else 3
        key = (unit_id, fc)
        count = _validate_count(block.count)
        grouped.setdefault(key, []).append(
            {
                "fc": fc,
                "start": start,
                "count": count,
                "address": block.address,
                "data_type": block.data_type,
            }
        )

    plan: List[Dict[str, Any]] = []
    for (uid, fc), items in grouped.items():
        items.sort(key=lambda item: item["start"])
        current: dict | None = None
        for item in items:
            if current is None:
                current = {
                    "unit_id": uid,
                    "fc": fc,
                    "start": item["start"],
                    "count": item["count"],
                    "items": [item],
                }
                continue

            expected_next = current["start"] + current["count"]
            limit = _MAX_COUNT_BY_FC[current["fc"]]
            if item["start"] == expected_next and current["count"] + item["count"] <= limit:
                current["count"] += item["count"]
                current["items"].append(item)
            else:
                plan.append(current)
                current = {
                    "unit_id": uid,
                    "fc": fc,
                    "start": item["start"],
                    "count": item["count"],
                    "items": [item],
                }
        if current is not None:
            plan.append(current)

    plan.sort(key=lambda entry: (entry["fc"], entry["start"]))
    return plan
