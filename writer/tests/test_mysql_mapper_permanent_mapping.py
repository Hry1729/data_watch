from __future__ import annotations

from mysql_mapper import MysqlMapper, _SQL


class FakeCursor:
    def __init__(self, rows):
        self.rows = rows
        self.executions = []

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc, tb):
        return False

    def execute(self, query, params):
        self.executions.append((query, params))

    def fetchall(self):
        return self.rows


class FakeConnection:
    def __init__(self, rows):
        self.cursor_instance = FakeCursor(rows)
        self.ping_calls = 0

    def ping(self, reconnect=True):
        self.ping_calls += 1

    def cursor(self, cursor_type):
        return self.cursor_instance


def mapping_row() -> dict:
    return {
        "var_id": 123,
        "td_column_name": "p0001",
        "var_type": "FLOAT",
        "group_code": "g001",
        "device_code": "QC100",
        "device_type_code": "qc",
        "stable_name": "sdqc",
        "child_table": "QC100",
    }


def mapping_row_new_point() -> dict:
    row = mapping_row()
    row["var_id"] = 124
    row["td_column_name"] = "p0002"
    return row


def test_query_reads_immutable_mapping_without_active_filters() -> None:
    assert "FROM point_storage_mapping" in _SQL
    assert "device_variable" not in _SQL
    assert "deleted" not in _SQL
    assert "enable" not in _SQL


def test_old_config_versions_share_permanent_mapping_cache() -> None:
    connection = FakeConnection([mapping_row()])
    mapper = MysqlMapper(connection)

    current = mapper.get_or_load("QC100", "g001", 12)
    stale = mapper.get_or_load("QC100", "g001", 7)

    assert current is stale
    assert mapper.get_cached("QC100", "g001") is current
    assert stale.var_map == {"123": "p0001"}
    assert stale.stable == "sdqc"
    assert stale.child_table == "QC100"
    assert len(connection.cursor_instance.executions) == 1


def test_published_mapping_snapshot_is_replaced_not_mutated_in_place() -> None:
    """已发布的映射快照不得被原地修改（reload 必须换对象）。

    consumer 的快路径用对象身份判断"已算好的转换结果是否还对得上当前映射"。
    若 reload 改成在旧 dict 上 clear()/update()，身份不变但内容已变，
    旧转换结果就会被误判为有效——新增点位会漏写或写错列。
    """
    connection = FakeConnection([mapping_row()])
    mapper = MysqlMapper(connection)

    first = mapper.get_or_load("QC100", "g001", 12)
    published = first.var_map
    snapshot = dict(published)

    connection.cursor_instance.rows = [mapping_row(), mapping_row_new_point()]
    refreshed = mapper.reload("QC100", "g001", 13)

    assert refreshed is not first
    assert refreshed.var_map is not published, "reload 必须发布新的 var_map 对象"
    assert published == snapshot, "已发布的映射快照不得被原地增删"
    assert refreshed.var_map == {"123": "p0001", "124": "p0002"}
    assert mapper.get_cached("QC100", "g001") is refreshed

    # 再 reload 一次同样换对象，旧快照内容保持冻结
    connection.cursor_instance.rows = [mapping_row()]
    again = mapper.reload("QC100", "g001", 14)
    assert again.var_map is not refreshed.var_map
    assert refreshed.var_map == {"123": "p0001", "124": "p0002"}
    assert again.var_map == {"123": "p0001"}


def test_reload_refreshes_same_group_after_new_permanent_mapping() -> None:
    connection = FakeConnection([mapping_row()])
    mapper = MysqlMapper(connection)

    first = mapper.get_or_load("QC100", "g001", 12)
    connection.cursor_instance.rows = [mapping_row(), mapping_row_new_point()]
    refreshed = mapper.reload("QC100", "g001", 13)

    assert first is not refreshed
    assert refreshed is not None
    assert refreshed.var_map == {"123": "p0001", "124": "p0002"}
    assert mapper.get_cached("QC100", "g001") is refreshed
    assert len(connection.cursor_instance.executions) == 2
