"""高-1 回归：边缘模式下 HTTP Push / OPC DA 推送必须与 executor 主路径同一契约。

修复前 ingest 路径走 write_window → write_stream → write_latest 三步：
- EdgeOutboxWriter.write_stream 只用 stream 点构造 latest_fields，随即 commit_frame；
- 紧接着的 write_latest 没有 pending 帧，只写本地 latest（第二次 IPC）；
- STRING / is_storage=0 点因此永远不进上传载荷的 latest_values；
- 整帧全是 STRING 点时连 commit_frame 都不调用，中心完全看不到这一帧。

现在 ingest 也用 write_window_stream + write_latest 配对：一帧一次 commit_frame，latest 带全部点。
"""

from core.config import CollectorConfig, DeviceConfig, VariableConfig
from data_queue.types import DataPoint
from monitor.ingest_server import _IngestHTTPServer, _IngestRequestHandler
from storage.edge_outbox_writer import EdgeOutboxWriter


class _RecordingStore:
    def __init__(self) -> None:
        self.commits: list[dict] = []
        self.local_latest: list[tuple] = []

    def commit_frame(self, **kwargs) -> None:
        self.commits.append(kwargs)

    def write_local_latest(self, device_code, group_code, fields) -> None:
        self.local_latest.append((device_code, group_code, fields))


def _variable(variable_id, data_type, is_storage, var_seq):
    return VariableConfig(
        variable_id=variable_id,
        device_id="AGV801",
        address=f"OPD_Server.AGV801.ViewMain.{variable_id}",
        data_type=data_type,
        scan_rate=0.1,
        is_storage=is_storage,
        var_seq=var_seq,
        group_code="g001",
        group_config_version=7,
    )


def _server(writer, variables):
    config = CollectorConfig(
        devices=[
            DeviceConfig(
                device_id="AGV801",
                device_type="AGV",
                protocol="opcda_sidecar",
                ip="127.0.0.1",
            )
        ],
        variables=variables,
    )
    return _IngestHTTPServer(
        ("127.0.0.1", 0),
        _IngestRequestHandler,
        ingest_path="/ingest",
        api_key=None,
        max_batch_size=100,
        redis_writer=writer,
        realtime_config=config,
    )


def test_edge_ingest_commits_one_frame_with_full_latest():
    store = _RecordingStore()
    server = _server(
        EdgeOutboxWriter(store),
        [
            _variable("real", "REAL", True, 1),
            _variable("text", "STRING", True, 2),
            _variable("nostore", "REAL", False, 3),
        ],
    )
    try:
        accepted, errors = server.write_points_to_redis([
            DataPoint("AGV801", "real", 1.5, quality=0),
            DataPoint("AGV801", "text", "hello", quality=0),
            DataPoint("AGV801", "nostore", 9.0, quality=0),
        ])
    finally:
        server.server_close()

    assert errors == []
    assert accepted == 3
    assert len(store.commits) == 1, "一帧只允许一次 commit_frame（一次 IPC）"
    assert store.local_latest == [], "latest 必须随帧一起提交，不再单独写本地 latest"
    commit = store.commits[0]
    assert commit["values"] == {"real": "1.5"}
    assert set(commit["latest_fields"]) == {"real", "text", "nostore"}
    assert commit["latest_fields"]["text"]["v"] == "hello"
    assert commit["config_version"] == 7


def test_edge_ingest_string_only_frame_still_reaches_center():
    store = _RecordingStore()
    server = _server(EdgeOutboxWriter(store), [_variable("text", "STRING", True, 1)])
    try:
        accepted, errors = server.write_points_to_redis([
            DataPoint("AGV801", "text", "hello", quality=0),
        ])
    finally:
        server.server_close()

    assert errors == []
    assert accepted == 1
    assert store.local_latest == []
    assert len(store.commits) == 1
    assert store.commits[0]["values"] == {}
    assert store.commits[0]["latest_fields"]["text"]["v"] == "hello"
