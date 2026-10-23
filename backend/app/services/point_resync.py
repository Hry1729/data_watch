"""点位存储映射校准（points resync）。

直接改库（INSERT/UPDATE device_variable、改 device_type.type_code）会绕过 App/API 的
配置链：不写 point_storage_mapping、不递增 device.variable_config_version、
列名可能带空格、测点组计数不前进。writer 因此找不到映射，整帧数据留在 PEL 不入库。

本模块按 device_variable 把一台设备的存储映射校准回一致状态：
  1. device_type.type_code 去空格；
  2. device_variable.td_column_name 去空格；
  3. 缺少分组/列名的有效点位补分配；
  4. 删除已不存在点位的孤儿映射（永久映射只保留仍存在的点，含逻辑删除的点）；
  5. 补齐/更正有效点位的 point_storage_mapping；
  6. 测点组 next_column_index / used_column_count 追上实际已用列；
  7. 有任何变更就递增测点组 config_version 与 device.variable_config_version：
     collector 热加载后新帧带新版本，writer 据此自动刷新映射。

check 模式只返回报告不改库；apply 模式在一个事务里完成。
"""
from __future__ import annotations

import re
from typing import TYPE_CHECKING, Any

from sqlalchemy import text

from app.core.exceptions import ApiError

if TYPE_CHECKING:  # pragma: no cover
    from app.services.catalog import CatalogService

_IDENT_SANITIZE_RE = re.compile(r"[^0-9a-z_]")
_IDENTIFIER_RE = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*$")
_COLUMN_SUFFIX_RE = re.compile(r"(\d+)$")


def _clean_type_code(value: Any) -> str:
    return _IDENT_SANITIZE_RE.sub("", str(value or "").strip().lower())


def _clean_column(value: Any) -> str:
    return str(value or "").strip()


async def resync_point_storage(
    service: "CatalogService",
    device_code: str,
    *,
    apply: bool,
    actor: str,
) -> dict[str, Any]:
    session = service.session
    code = str(device_code or "").strip()
    if not code:
        raise ApiError(400, "deviceCode is required")

    device = (
        await session.execute(
            text(
                """
                SELECT d.id AS device_id, d.device_code, d.type_id,
                       COALESCE(dt.type_code, '') AS type_code
                FROM device AS d
                LEFT JOIN device_type AS dt ON dt.id = d.type_id
                WHERE TRIM(d.device_code) = :code
                LIMIT 1
                """
            ),
            {"code": code},
        )
    ).mappings().first()
    if device is None:
        raise ApiError(404, f"设备 {code} 不存在")

    device_id = int(device["device_id"])
    real_code = str(device["device_code"])
    raw_type_code = str(device["type_code"] or "")
    type_code = _clean_type_code(raw_type_code)
    if not type_code:
        raise ApiError(400, f"设备 {real_code} 没有有效的设备类型编码")

    report: dict[str, Any] = {
        "deviceCode": real_code,
        "applied": False,
        "typeCodeFixed": None,
        "trimmedColumns": [],
        "allocated": [],
        "inserted": [],
        "updated": [],
        "orphansRemoved": [],
        "groupCountersFixed": [],
        "conflicts": [],
        "configVersionBumped": False,
    }

    if raw_type_code != raw_type_code.strip() or raw_type_code.strip().lower() != type_code:
        report["typeCodeFixed"] = {"from": raw_type_code, "to": raw_type_code.strip()}

    points = (
        await session.execute(
            text(
                """
                SELECT dv.id AS point_id, dv.point_group_id, dv.group_index,
                       dv.td_column_name, COALESCE(dv.var_type, '') AS var_type,
                       COALESCE(dv.deleted, 0) AS deleted,
                       dpg.group_code
                FROM device_variable AS dv
                LEFT JOIN device_point_group AS dpg ON dpg.id = dv.point_group_id
                WHERE dv.device_id = :device_id
                ORDER BY dv.id
                """
            ),
            {"device_id": device_id},
        )
    ).mappings().all()

    mapping_available = await service._point_storage_mapping_available()
    mappings: dict[int, dict[str, Any]] = {}
    if mapping_available:
        for row in (
            await session.execute(
                text(
                    """
                    SELECT point_id, device_code, device_type_code, stable_name,
                           child_table, group_code, td_column_name, var_type
                    FROM point_storage_mapping
                    WHERE device_code = :device_code
                    """
                ),
                {"device_code": real_code},
            )
        ).mappings().all():
            mappings[int(row["point_id"])] = dict(row)

    point_ids = {int(p["point_id"]) for p in points}
    for point_id in sorted(set(mappings) - point_ids):
        report["orphansRemoved"].append(point_id)

    # 列名占用表：有效点 + 逻辑删除点 + 仍保留的映射（孤儿除外），防止补分配撞列。
    used_columns: dict[str, int] = {}
    for p in points:
        col = _clean_column(p["td_column_name"])
        if col:
            used_columns.setdefault(col, int(p["point_id"]))
    for point_id, m in mappings.items():
        if point_id in point_ids:
            used_columns.setdefault(_clean_column(m["td_column_name"]), point_id)

    planned: list[dict[str, Any]] = []
    for p in points:
        point_id = int(p["point_id"])
        raw_col = p["td_column_name"]
        col = _clean_column(raw_col)
        if raw_col is not None and col and col != raw_col:
            report["trimmedColumns"].append({"pointId": point_id, "from": raw_col, "to": col})
        if int(p["deleted"] or 0):
            continue
        if col and not _IDENTIFIER_RE.match(col):
            report["conflicts"].append({"pointId": point_id, "reason": f"列名不合法：{raw_col!r}，请手工修正"})
            continue
        group_code = str(p["group_code"] or "").strip().lower()
        if not col or not group_code:
            report["allocated"].append({"pointId": point_id, "groupCode": group_code or "g001"})
            planned.append({"point": p, "allocate": True, "group_code": group_code or "g001"})
            continue
        planned.append({"point": p, "allocate": False, "group_code": group_code, "column": col})

    for item in planned:
        if item["allocate"]:
            continue
        p = item["point"]
        point_id = int(p["point_id"])
        owner = used_columns.get(item["column"])
        if owner is not None and owner != point_id:
            report["conflicts"].append(
                {"pointId": point_id, "reason": f"列名 {item['column']} 已被点位 {owner} 占用"}
            )
            item["skip"] = True
            continue
        desired = _desired_mapping(real_code, type_code, item["group_code"], item["column"], p["var_type"])
        current = mappings.get(point_id)
        if current is None:
            report["inserted"].append(point_id)
            item["action"] = "insert"
        elif any(str(current.get(k) or "") != str(v) for k, v in desired.items()):
            report["updated"].append(point_id)
            item["action"] = "update"
        item["desired"] = desired

    group_fixes = await _plan_group_counters(session, device_id, type_code)
    report["groupCountersFixed"] = [
        {"groupCode": g["group_code"], "nextColumnIndex": g["next_index"]} for g in group_fixes
    ]

    changed = any(
        report[key]
        for key in ("typeCodeFixed", "trimmedColumns", "allocated", "inserted", "updated",
                    "orphansRemoved", "groupCountersFixed")
    )
    if not apply or not changed:
        return report

    try:
        if report["typeCodeFixed"]:
            await session.execute(
                text("UPDATE device_type SET type_code = TRIM(type_code) WHERE id = :type_id"),
                {"type_id": int(device["type_id"])},
            )
        if report["trimmedColumns"]:
            await session.execute(
                text(
                    """
                    UPDATE device_variable
                    SET td_column_name = TRIM(td_column_name)
                    WHERE device_id = :device_id AND td_column_name <> TRIM(td_column_name)
                    """
                ),
                {"device_id": device_id},
            )
        if mapping_available and report["orphansRemoved"]:
            from sqlalchemy import bindparam

            await session.execute(
                text(
                    "DELETE FROM point_storage_mapping WHERE device_code = :device_code AND point_id IN :ids"
                ).bindparams(bindparam("ids", expanding=True)),
                {"device_code": real_code, "ids": report["orphansRemoved"]},
            )
        for fix in group_fixes:
            await session.execute(
                text(
                    """
                    UPDATE device_point_group
                    SET next_column_index = :next_index,
                        used_column_count = GREATEST(used_column_count, :used),
                        config_version = config_version + 1,
                        update_time = NOW()
                    WHERE id = :group_id
                    """
                ),
                {"next_index": fix["next_index"], "used": fix["next_index"] - 1, "group_id": fix["group_id"]},
            )
        for item in planned:
            p = item["point"]
            point_id = int(p["point_id"])
            if item["allocate"]:
                slot = await service._allocate_point_slot_for_update(
                    device_id=device_id, group_code=item["group_code"], actor=actor, type_code=type_code
                )
                await session.execute(
                    text(
                        """
                        UPDATE device_variable
                        SET point_group_id = :point_group_id, group_index = :group_index,
                            td_column_name = :td_column_name
                        WHERE id = :point_id
                        """
                    ),
                    {**{k: slot[k] for k in ("point_group_id", "group_index", "td_column_name")},
                     "point_id": point_id},
                )
                await service._mark_point_created(
                    point_group_id=slot["point_group_id"], device_id=device_id, actor=actor
                )
                for entry in report["allocated"]:
                    if entry["pointId"] == point_id:
                        entry["tdColumnName"] = slot["td_column_name"]
                        entry["groupCode"] = slot["group_code"]
                if mapping_available:
                    await session.execute(
                        text("DELETE FROM point_storage_mapping WHERE point_id = :point_id"),
                        {"point_id": point_id},
                    )
                    await service._insert_point_storage_mapping(
                        point_id=point_id, device_code=real_code, device_type_code=type_code,
                        point_slot=slot, var_type=str(p["var_type"] or ""),
                    )
                continue
            if not mapping_available or item.get("skip") or "action" not in item:
                continue
            desired = item["desired"]
            if item["action"] == "insert":
                await service._insert_point_storage_mapping(
                    point_id=point_id, device_code=real_code, device_type_code=type_code,
                    point_slot={"group_code": desired["group_code"], "td_column_name": desired["td_column_name"]},
                    var_type=desired["var_type"],
                )
            else:
                await session.execute(
                    text(
                        """
                        UPDATE point_storage_mapping
                        SET device_code = :device_code, device_type_code = :device_type_code,
                            stable_name = :stable_name, child_table = :child_table,
                            group_code = :group_code, td_column_name = :td_column_name,
                            var_type = :var_type
                        WHERE point_id = :point_id
                        """
                    ),
                    {**desired, "point_id": point_id},
                )
        # stream entry 带的是测点组 config_version：一并递增，writer 收到新版本帧时自动重载映射。
        await session.execute(
            text(
                """
                UPDATE device_point_group
                SET config_version = config_version + 1, update_time = NOW()
                WHERE device_id = :device_id
                """
            ),
            {"device_id": device_id},
        )
        await service._bump_device_variable_config(device_id)
        await session.commit()
    except Exception:
        await session.rollback()
        raise

    report["applied"] = True
    report["configVersionBumped"] = True
    return report


def _desired_mapping(device_code: str, type_code: str, group_code: str, column: str, var_type: Any) -> dict[str, str]:
    return {
        "device_code": device_code,
        "device_type_code": type_code,
        "stable_name": f"sd{type_code}",
        "child_table": device_code,
        "group_code": group_code,
        "td_column_name": column,
        "var_type": str(var_type or ""),
    }


async def _plan_group_counters(session, device_id: int, type_code: str) -> list[dict[str, Any]]:
    """直接 SQL 加点不会推进 next_column_index，下次 App 新增就会生成重复列名。"""
    groups = (
        await session.execute(
            text(
                """
                SELECT id, group_code, next_column_index
                FROM device_point_group
                WHERE device_id = :device_id
                """
            ),
            {"device_id": device_id},
        )
    ).mappings().all()
    if not groups:
        return []
    rows = (
        await session.execute(
            text(
                """
                SELECT point_group_id, group_index, td_column_name
                FROM device_variable
                WHERE device_id = :device_id AND point_group_id IS NOT NULL
                """
            ),
            {"device_id": device_id},
        )
    ).mappings().all()
    highest: dict[int, int] = {}
    for row in rows:
        group_id = int(row["point_group_id"])
        index = int(row["group_index"] or 0)
        col = _clean_column(row["td_column_name"]).lower()
        if col.startswith(type_code):
            match = _COLUMN_SUFFIX_RE.search(col[len(type_code):])
            if match:
                index = max(index, int(match.group(1)))
        highest[group_id] = max(highest.get(group_id, 0), index)
    fixes: list[dict[str, Any]] = []
    for group in groups:
        group_id = int(group["id"])
        next_index = highest.get(group_id, 0) + 1
        if int(group["next_column_index"] or 1) < next_index:
            fixes.append({"group_id": group_id, "group_code": group["group_code"], "next_index": next_index})
    return fixes
