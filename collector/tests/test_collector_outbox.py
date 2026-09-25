import sqlite3
import threading
import time
from types import SimpleNamespace
from unittest.mock import MagicMock
import msgspec
import pytest
from storage.collector_outbox import CollectorRetryOutbox
from storage.durable_redis_writer import DurableRedisWriter
from monitor.performance import PerformanceMetrics


def sample(ts=1234):
    return dict(device_code="D", group_code="g", device_type="qc", scan_rate_ms=100,
        ts_ms=ts, config_version=2, frame={"ts":ts,"values":{"1":"7"}},
        stream_values={"1":"7"}, stream_qualities={"1":192},
        latest_fields={"1":{"v":"7","t":ts,"q":192}})


def test_restart_and_retry_preserve_original_samples(tmp_path):
    path=tmp_path/"queue.db"
    q=CollectorRetryOutbox(path)
    q.record_failure(**sample())
    writer=MagicMock()
    writer.write_durable_frames.side_effect=RuntimeError("no fsync")
    with pytest.raises(RuntimeError): q.replay_pending(writer)
    assert q.count()==1
    q.close()
    q=CollectorRetryOutbox(path)
    writer.write_durable_frames.side_effect=None
    assert q.replay_pending(writer)==1
    row=writer.write_durable_frames.call_args.args[0][0]
    assert row["ts_ms"]==1234
    assert msgspec.json.decode(row["latest_fields_json"])["1"]["t"]==1234
    assert q.count()==0
    q.close()


def test_full_queue_refuses_new_data_without_eviction(tmp_path):
    q=CollectorRetryOutbox(tmp_path/"q.db",max_records=2)
    q.record_failure(**sample(1));q.record_failure(**sample(2))
    with pytest.raises(RuntimeError,match="full"):q.record_failure(**sample(3))
    assert q.count()==2
    assert [r[0] for r in q._conn.execute("SELECT ts_ms FROM collector_retry_outbox_v2 ORDER BY ts_ms")]==[1,2]
    assert q._conn.execute("PRAGMA synchronous").fetchone()[0]==2
    q.close()


# ----------------------------------------------------------------------
# 拒收缺口台账：不能只保住旧积压，必须留下可追溯的采样缺口
# ----------------------------------------------------------------------

def test_rejected_frame_is_recorded_as_a_traceable_gap(tmp_path):
    """队列触顶拒收时，必须留下设备/分组/周期/时间范围/数量/原因。"""
    q = CollectorRetryOutbox(tmp_path / "q.db", max_records=1)
    q.record_failure(**sample(1000))
    with pytest.raises(RuntimeError, match="full"):
        q.record_failure(**sample(2000))

    gaps = q.rejected_gaps()
    assert len(gaps) == 1
    gap = gaps[0]
    assert gap["device_code"] == "D"
    assert gap["group_code"] == "g"
    assert gap["device_type"] == "qc"
    assert gap["scan_rate_ms"] == 100
    assert gap["first_ts_ms"] == 2000 and gap["last_ts_ms"] == 2000
    assert gap["frames"] == 1
    assert "full" in gap["reason"]
    q.close()


def test_contiguous_rejections_merge_and_disjoint_ones_split(tmp_path):
    """连续拒收合并为一段；中间隔开（或恢复采集后）再拒收必须拆成独立事件。

    审查要求：不能只按"首次—末次"形成一个大区间。当前只有采样时间戳可用，
    因此按"间隔不超过约一个半采样周期"推断连续性——这是**时间推断**，
    不代表能精确知道每一帧是否缺失。
    """
    q = CollectorRetryOutbox(tmp_path / "q.db", max_records=1)
    q.record_failure(**sample(1000))          # 被接受，占满队列（本身不是缺口）
    assert q.rejected_gaps() == []

    # 100ms 周期：2000/2100/2200 相邻 → 同一段
    for ts in (2000, 2100, 2200):
        with pytest.raises(RuntimeError, match="full"):
            q.record_failure(**sample(ts))
    gaps = q.rejected_gaps()
    assert len(gaps) == 1, "相邻采样时间的连续拒收应合并为一段"
    assert gaps[0]["frames"] == 3
    assert gaps[0]["first_ts_ms"] == 2000 and gaps[0]["last_ts_ms"] == 2200

    # 间隔远大于容忍度 → 新的一段，不能并进上面
    with pytest.raises(RuntimeError, match="full"):
        q.record_failure(**sample(9000))
    gaps = q.rejected_gaps()
    assert len(gaps) == 2, "不连续的拒收必须拆成两个独立缺口事件"
    assert sorted(g["frames"] for g in gaps) == [1, 3]
    q.close()


def test_gap_event_closes_when_collection_resumes(tmp_path):
    """采集恢复只关闭"缺口仍在扩大"，不代表缺失数据已补齐。"""
    q = CollectorRetryOutbox(tmp_path / "q.db", max_records=1)
    q.record_failure(**sample(1000))
    for ts in (2000, 2100):
        with pytest.raises(RuntimeError, match="full"):
            q.record_failure(**sample(ts))
    q.mark_collection_resumed(device_code="D", group_code="g", scan_rate_ms=100)

    gap = q.rejected_gaps()[0]
    assert gap["collection_resumed_at"] is not None
    # 关键：采集恢复 != 数据已补齐
    assert gap["backfilled_at"] is None
    assert gap["resolved_at"] is None

    # 恢复之后再拒收 → 新事件，不会并进已关闭的那段
    with pytest.raises(RuntimeError, match="full"):
        q.record_failure(**sample(9000))
    assert len(q.rejected_gaps()) == 2
    q.close()


def test_gap_ledger_reports_unavailable_instead_of_empty(tmp_path):
    """台账不可用时必须显式报出，不能返回一个无法区分原因的空结果。"""
    import sqlite3 as _sq
    from storage.reject_gap_log import RejectGapLog, RejectGapLogUnavailable

    db = tmp_path / "broken.sqlite"
    conn = _sq.connect(db)
    # 同名表但结构无法识别 → 初始化必须失败而不是静默降级
    conn.executescript("CREATE TABLE collector_rejected_gaps (something TEXT);")
    conn.commit()
    conn.close()

    log = RejectGapLog(db, run_id="r", session_id="s")
    assert log.available is False
    status = log.status()
    assert status["available"] is False and status["error"]
    with pytest.raises(RejectGapLogUnavailable):
        log.gaps()


def test_legacy_v1_gap_ledger_is_migrated_and_history_preserved(tmp_path):
    """旧版（无 run_id）台账必须显式迁移：保留历史，且不伪装成本次运行。

    CREATE TABLE IF NOT EXISTS 不会给已存在的同名表补列，
    旧版本升级时会直接初始化失败——这里用 v1 结构建库来覆盖这条路径。
    """
    import sqlite3 as _sq
    from storage.reject_gap_log import LEGACY_RUN_ID, RejectGapLog

    db = tmp_path / "legacy.rejects.sqlite"
    conn = _sq.connect(db)
    conn.executescript("""
        CREATE TABLE collector_rejected_gaps (
            device_code TEXT NOT NULL, group_code TEXT NOT NULL, device_type TEXT NOT NULL,
            scan_rate_ms INTEGER NOT NULL, reason TEXT NOT NULL,
            first_ts_ms INTEGER NOT NULL, last_ts_ms INTEGER NOT NULL, frames INTEGER NOT NULL,
            first_rejected_at INTEGER NOT NULL, last_rejected_at INTEGER NOT NULL,
            PRIMARY KEY (device_code, group_code, scan_rate_ms, reason));
    """)
    conn.execute("INSERT INTO collector_rejected_gaps VALUES "
                 "('DEV','g','dt',100,'full',1000,1500,5,111,222)")
    conn.commit()
    conn.close()

    q = CollectorRetryOutbox(tmp_path / "q.db")
    q._reject_log.close()
    q._reject_log = RejectGapLog(db, run_id="run-new", session_id="sess-new")
    status = q.reject_log_status()
    assert status["available"] is True
    assert status["schema_version"] == 2

    # 历史保留且标成 legacy-import，不会被当成当前告警
    assert q.rejected_gaps() == []
    history = q.rejected_gaps(run_id=LEGACY_RUN_ID)
    assert len(history) == 1
    assert history[0]["frames"] == 5
    assert history[0]["device_code"] == "DEV"
    assert q.rejected_gap_runs()[0]["run_id"] == LEGACY_RUN_ID
    q.close()


def test_gap_ledger_is_separate_from_the_full_main_queue(tmp_path):
    """缺口台账必须写在独立文件里：主队列满的正是它自己，记在同一个库里等于没记。"""
    q = CollectorRetryOutbox(tmp_path / "q.db", max_records=1)
    assert q._reject_path != q.path
    q.record_failure(**sample(1000))
    with pytest.raises(RuntimeError, match="full"):
        q.record_failure(**sample(2000))
    assert q._reject_path.exists(), "独立缺口台账文件必须真的被创建"
    # 主队列仍然只有 1 行（没有因为写缺口而动过它）
    assert q.count() == 1
    q.close()


def test_gap_ledger_does_not_grow_without_bound(tmp_path):
    """连续拒收聚合成一段，不随运行时长无限增长。"""
    q = CollectorRetryOutbox(tmp_path / "q.db", max_records=1)
    q.record_failure(**sample(1000))
    # 100ms 周期下的连续 100 次拒收
    for ts in range(2000, 2000 + 100 * 100, 100):
        with pytest.raises(RuntimeError, match="full"):
            q.record_failure(**sample(ts))
    gaps = q.rejected_gaps()
    assert len(gaps) == 1, "连续拒收应聚合成一段"
    assert gaps[0]["frames"] == 100
    q.close()


def test_accepting_a_frame_again_closes_the_open_gap(tmp_path):
    """再次成功接收同一 (设备,分组,周期) 时，缺口应自动标记"采集已恢复"。

    这只关闭"缺口仍在扩大"，**不**代表缺失数据已补齐。
    """
    q = CollectorRetryOutbox(tmp_path / "q.db", max_records=1)
    q.record_failure(**sample(1000))          # 接受，占满队列
    for ts in (2000, 2100):
        with pytest.raises(RuntimeError, match="full"):
            q.record_failure(**sample(ts))    # 队列满 → 拒收，产生缺口
    gap = q.rejected_gaps()[0]
    assert gap["frames"] == 2
    assert gap["collection_resumed_at"] is None, "仍在拒收期间不算恢复"

    # 腾出容量后再次成功接收 → 采集恢复
    q._conn.execute("DELETE FROM collector_retry_outbox_v2")
    q._count = 0
    q._payload_bytes = 0
    q.record_failure(**sample(3000))

    gap = q.rejected_gaps()[0]
    assert gap["collection_resumed_at"] is not None, "重新接收成功后应关闭缺口扩大标记"
    assert gap["backfilled_at"] is None, "采集恢复不得等同于缺失数据已补齐"
    q.close()


def test_history_frame_counter_is_idempotent_when_local_cleanup_fails(tmp_path):
    """Redis 确认成功但本地 DELETE 事务失败时，重放不得把同一批帧算两次。

    Redis 幂等（receipt 去重）不等于进程内计数器幂等：
    计数必须在"删除已提交"之后累加，否则实际唯一帧 100 而计数变 200。
    """
    q = CollectorRetryOutbox(tmp_path / "q.db")
    metrics = PerformanceMetrics()
    q._metrics = metrics
    for ts in (1, 2, 3):
        q.record_failure(**sample(ts))

    writer = MagicMock()

    class _Writer:
        pass

    # 第一次：Redis 侧"确认成功"（write_durable_frames 正常返回），
    # 但本地清理事务失败——用触发器让 DELETE 抛错。
    q._conn.execute(
        "CREATE TRIGGER fail_delete BEFORE DELETE ON collector_retry_outbox_v2 "
        "BEGIN SELECT RAISE(ABORT, 'simulated cleanup failure'); END")
    real_write = MagicMock()
    writer.write_durable_frames = real_write
    with pytest.raises(sqlite3.IntegrityError):
        q.replay_pending(writer)
    assert real_write.call_count == 1, "Redis 侧确实已确认过"
    assert q.count() == 3, "本地清理失败必须保留原帧"
    assert metrics.snapshot()["counters"].get("history_frames_confirmed", 0) == 0, \
        "清理未提交就不得计数"

    # 第二次：清掉故障，重放同一批帧，只能计一次 3 帧。
    q._conn.execute("DROP TRIGGER fail_delete")
    assert q.replay_pending(writer) == 3
    assert metrics.snapshot()["counters"]["history_frames_confirmed"] == 3
    assert q.count() == 0
    q.close()


def test_gap_ledger_survives_restart_and_history_stays_queryable(tmp_path):
    """缺口是历史事实：重启 Collector 不得清空台账，否则等于删掉审计线索。

    当前与历史靠 run_id 区分，不靠删除。
    """
    path = tmp_path / "q.db"
    q = CollectorRetryOutbox(path, max_records=1)
    q.record_failure(**sample(1000))
    with pytest.raises(RuntimeError, match="full"):
        q.record_failure(**sample(2000))
    first_run = q.rejected_gaps()
    assert len(first_run) == 1
    run_id = first_run[0]["run_id"]
    q.close()

    again = CollectorRetryOutbox(path, max_records=1)
    # 本次运行还没有缺口，所以默认查询为空……
    assert again.rejected_gaps() == []
    # ……但历史运行记录必须仍在，且可按 run_id 查回。
    runs = again.rejected_gap_runs()
    assert len(runs) == 1 and runs[0]["run_id"] == run_id
    history = again.rejected_gaps(run_id=run_id)
    assert len(history) == 1
    assert history[0]["frames"] == 1
    assert history[0]["first_ts_ms"] == 2000
    again.close()


def test_gap_records_are_separated_per_run(tmp_path):
    """同一设备在两次运行里各自成行，不会被合并成一个跨重启的大区间。

    注意主队列是持久化的：上一次运行的待发帧仍在库里，因此每次运行
    只放一帧、再触发一次拒收即可，无需假设库是空的。
    """
    path = tmp_path / "q.db"

    q1 = CollectorRetryOutbox(path, max_records=2)
    q1.record_failure(**sample(1000))
    assert q1._conn.execute(
        "SELECT COUNT(*) FROM collector_retry_outbox_v2").fetchone()[0] == 1
    # 直接把库占满，制造一次拒收
    q1.record_failure(**sample(1500))
    with pytest.raises(RuntimeError, match="full"):
        q1.record_failure(**sample(2000))
    run1 = q1.rejected_gap_runs()
    assert len(run1) == 1
    q1.close()

    q2 = CollectorRetryOutbox(path, max_records=2)
    # 上次运行的两帧仍在库中 → 本次一开始就已满，直接拒收
    with pytest.raises(RuntimeError, match="full"):
        q2.record_failure(**sample(9000))
    current = q2.rejected_gaps()
    assert len(current) == 1
    assert current[0]["first_ts_ms"] == 9000, "本次运行只反映本次缺口"
    assert len(q2.rejected_gap_runs()) == 2, "两次运行各留一条历史"
    q2.close()


def test_one_replay_owner_prevents_duplicate_concurrent_delivery(tmp_path):
    q=CollectorRetryOutbox(tmp_path/"q.db");q.record_failure(**sample())
    entered=threading.Event();release=threading.Event()
    writer=MagicMock()
    def send(rows): entered.set();assert release.wait(3)
    writer.write_durable_frames.side_effect=send
    thread=threading.Thread(target=lambda:q.replay_pending(writer));thread.start()
    assert entered.wait(3)
    assert q.replay_pending(writer)==0
    release.set();thread.join(3)
    writer.write_durable_frames.assert_called_once();q.close()


def test_concurrent_accepts_share_one_sqlite_commit(tmp_path):
    q = CollectorRetryOutbox(tmp_path / "q.db")
    q._COMMIT_WINDOW_S = 0.05
    statements = []
    q._conn.set_trace_callback(statements.append)
    barrier = threading.Barrier(8)
    errors = []
    def submit(i):
        try:
            barrier.wait()
            q.record_failure(**sample(i))
        except BaseException as exc:
            errors.append(exc)
    threads = [threading.Thread(target=submit, args=(i,)) for i in range(8)]
    for thread in threads: thread.start()
    for thread in threads: thread.join(3)
    assert not any(thread.is_alive() for thread in threads)
    assert not errors
    assert q.count() == 8
    assert sum(sql == "COMMIT" for sql in statements) == 1
    q.close()


def test_failed_group_commit_rolls_back_all_rows(tmp_path):
    q = CollectorRetryOutbox(tmp_path / "q.db")
    q._COMMIT_WINDOW_S = 0.05
    q._conn.execute("CREATE TRIGGER reject_frame BEFORE INSERT ON collector_retry_outbox_v2 BEGIN SELECT RAISE(FAIL, 'insert blocked'); END")
    barrier = threading.Barrier(2)
    errors = []
    def submit(i):
        try:
            barrier.wait()
            q.record_failure(**sample(i))
        except Exception as exc:
            errors.append(str(exc))
    threads = [threading.Thread(target=submit, args=(i,)) for i in range(2)]
    for thread in threads: thread.start()
    for thread in threads: thread.join(3)
    assert len(errors) == 2
    assert all("insert blocked" in error for error in errors)
    assert q.count() == 0
    q._conn.execute("DROP TRIGGER reject_frame")
    q.record_failure(**sample(3))
    assert q.count() == 1
    q.close()


def test_close_drains_queued_accepts_and_wakes_waiters(tmp_path):
    path = tmp_path / "q.db"
    q = CollectorRetryOutbox(path)
    entered = threading.Event()
    release = threading.Event()
    original = q._commit_batch
    def blocked(batch):
        entered.set()
        assert release.wait(3)
        return original(batch)
    q._commit_batch = blocked
    results = []
    first = threading.Thread(target=lambda: results.append(q.record_failure(**sample(1))))
    first.start()
    assert entered.wait(3)
    second = threading.Thread(target=lambda: results.append(q.record_failure(**sample(2))))
    second.start()
    deadline = time.monotonic() + 3
    while q.stats()["waiting_writes"] == 0 and time.monotonic() < deadline: time.sleep(.001)
    assert q.stats()["waiting_writes"] == 1
    closer = threading.Thread(target=q.close)
    closer.start()
    deadline = time.monotonic() + 3
    while not q._closed and time.monotonic() < deadline: time.sleep(.001)
    with pytest.raises(RuntimeError, match="closed"):
        q.record_failure(**sample(3))
    release.set()
    for thread in (first, second, closer): thread.join(3)
    assert not any(thread.is_alive() for thread in (first, second, closer))
    assert len(results) == 2
    reopened = CollectorRetryOutbox(path)
    assert reopened.count() == 2
    reopened.close()


def test_post_commit_metric_failure_still_reports_accepted(tmp_path):
    metrics = MagicMock()
    metrics.record_timing.side_effect = RuntimeError("metric unavailable")
    q = CollectorRetryOutbox(tmp_path / "q.db", metrics=metrics)
    q.record_failure(**sample())
    assert q.count() == 1
    q.close()


def test_oversized_single_frame_is_explicitly_rejected(tmp_path):
    q = CollectorRetryOutbox(tmp_path / "q.db")
    q._MAX_SINGLE_FRAME_BYTES = 1024
    large = sample()
    large["frame"] = {"ts": 1234, "values": {"1": "x" * 2000}}
    with pytest.raises(RuntimeError, match="byte limit"):
        q.record_failure(**large)
    assert q.count() == 0
    q.close()


class Pipeline:
    def __init__(self, persisted):self.persisted=persisted;self.calls=[]
    def __enter__(self):return self
    def __exit__(self,*args):pass
    def eval(self,*args):self.calls.append(args)
    def evalsha(self,*args):self.calls.append(args)
    def execute_command(self,*args):self.command=args
    def execute(self):return [1]*len(self.calls)+[self.persisted]


def durable(tmp_path, persisted):
    client=MagicMock();pipe=Pipeline(persisted);client.pipeline.return_value=pipe
    config=SimpleNamespace(redis=SimpleNamespace(stream_shard_count=4,stream_maxlen=1))
    return DurableRedisWriter(client,config,outbox_path=tmp_path/"q.db"),pipe


def test_production_sink_does_not_delete_without_aof_confirmation(tmp_path):
    w,p=durable(tmp_path,[0,0])
    w.write_window_stream("D","qc","g",100,1234,2,{"ts":1234}, {"1":"7"},{"1":192})
    w.write_latest("D","g",{"1":{"v":"7","t":1234,"q":192}})
    assert w.outbox.count()==1
    with pytest.raises(RuntimeError,match="AOF"):w.outbox.replay_pending(w)
    assert w.outbox.count()==1
    assert p.command==("WAITAOF",1,0,2000)
    p.persisted=[1,0]
    assert w.outbox.replay_pending(w)==1
    assert w.outbox.count()==0
    w.close()


def test_startup_replays_backlog_without_new_samples(tmp_path):
    w,p=durable(tmp_path,[1,0]);w.outbox.record_failure(**sample())
    w.ping()
    deadline=time.monotonic()+3
    while w.outbox.count() and time.monotonic()<deadline:time.sleep(.01)
    assert w.outbox.count()==0
    w.close()


def test_http_split_write_path_is_locally_durable(tmp_path):
    w, pipe = durable(tmp_path, [1, 0])
    w.write_window("D", "g", 100, {"ts": 1234, "values": {"1": "7"}})
    w.write_stream("D", "qc", "g", 100, 1234, 2, {"1": "7"}, {"1": 192})
    w.write_latest("D", "g", {"1": {"v": "7", "t": 1234, "q": 192}})
    assert w.outbox.count() == 1
    assert w.outbox.replay_pending(w) == 1
    assert b'1234' in pipe.calls[0][-4]
    assert pipe.calls[0][-3:-1] == ("D", "g")
    w.close()


def test_aof_response_loss_keeps_original_receipt_identity(tmp_path):
    w, pipe = durable(tmp_path, [1, 0])
    w.outbox.record_failure(**sample())
    original_execute = pipe.execute
    first = True
    def lose_response():
        nonlocal first
        if first:
            first = False
            raise RuntimeError("WAITAOF reply lost after Redis execution")
        return original_execute()
    pipe.execute = lose_response
    with pytest.raises(RuntimeError, match="reply lost"):
        w.outbox.replay_pending(w)
    assert w.outbox.count() == 1
    first_receipt = pipe.calls[0][2]
    assert w.outbox.replay_pending(w) == 1
    assert pipe.calls[1][2] == first_receipt
    assert w.outbox.count() == 0
    w.close()


def test_fastlane_newer_update_survives_inflight_send(tmp_path):
    w, _ = durable(tmp_path, [1, 0])
    entered = threading.Event()
    release = threading.Event()
    sent = []
    def send(script, count, key, payload, device, group):
        sent.append(msgspec.json.decode(payload))
        if len(sent) == 1:
            entered.set()
            assert release.wait(3)
        return 1
    w.r.eval.side_effect = send
    w.ping()
    w._queue_latest("D", "g", {"1": {"v": "old", "t": 1, "q": 192}})
    assert entered.wait(3)
    w._queue_latest("D", "g", {"1": {"v": "new", "t": 2, "q": 192}})
    release.set()
    deadline = time.monotonic() + 3
    while (len(sent) < 2 or w._latest_pending) and time.monotonic() < deadline: time.sleep(.001)
    assert sent[0]["1"]["t"] == 1
    assert sent[1]["1"]["t"] == 2
    assert not w._latest_pending
    w.close()


def test_fastlane_restores_latest_from_unconfirmed_outbox(tmp_path):
    path = tmp_path / "q.db"
    q = CollectorRetryOutbox(path)
    q.record_failure(**sample())
    q.close()
    client = MagicMock()
    pipe = Pipeline([0, 0])
    client.pipeline.return_value = pipe
    config = SimpleNamespace(redis=SimpleNamespace(stream_shard_count=4, stream_maxlen=1))
    w = DurableRedisWriter(client, config, outbox_path=path)
    w.ping()
    deadline = time.monotonic() + 3
    while client.eval.call_args is None and time.monotonic() < deadline: time.sleep(.001)
    assert client.eval.call_args is not None
    assert msgspec.json.decode(client.eval.call_args.args[3])["1"]["t"] == 1234
    assert w.outbox.count() == 1
    w.close()


def test_replay_reads_more_than_old_fifty_frame_limit(tmp_path):
    q = CollectorRetryOutbox(tmp_path / "q.db")
    q._COMMIT_WINDOW_S = 0
    for i in range(100): q.record_failure(**sample(i))
    writer = MagicMock()
    assert q.replay_pending(writer) == 100
    assert len(writer.write_durable_frames.call_args.args[0]) == 100
    assert q.count() == 0
    q.close()


def test_deferred_sqlite_commit_failure_rolls_back_before_acceptance(tmp_path):
    q = CollectorRetryOutbox(tmp_path / "q.db")
    q._conn.execute("PRAGMA foreign_keys=ON")
    q._conn.execute("CREATE TABLE parent(id INTEGER PRIMARY KEY)")
    q._conn.execute("CREATE TABLE child(parent_id INTEGER REFERENCES parent(id) DEFERRABLE INITIALLY DEFERRED)")
    q._conn.execute("CREATE TRIGGER fail_at_commit AFTER INSERT ON collector_retry_outbox_v2 BEGIN INSERT INTO child(parent_id) VALUES (42); END")
    with pytest.raises(sqlite3.IntegrityError, match="FOREIGN KEY"):
        q.record_failure(**sample())
    assert q.count() == 0
    assert q._conn.execute("SELECT COUNT(*) FROM collector_retry_outbox_v2").fetchone()[0] == 0
    assert q._conn.execute("SELECT COUNT(*) FROM child").fetchone()[0] == 0
    q._conn.execute("DROP TRIGGER fail_at_commit")
    q.record_failure(**sample(2))
    assert q.count() == 1
    q.close()


def test_fastlane_merge_keeps_newer_point_timestamp(tmp_path):
    w, _ = durable(tmp_path, [1, 0])
    w._queue_latest("D", "g", {"1": {"v": "new", "t": 2, "q": 192}})
    w._queue_latest("D", "g", {"1": {"v": "old", "t": 1, "q": 0}})
    w._queue_latest("D", "g", {"1": {"v": "tie", "t": 2, "q": 0}})
    assert w._latest_pending[("D", "g")].fields["1"]["v"] == "new"
    w.close()


def test_redis_replay_splits_encoded_payload_before_aof(tmp_path):
    w, _ = durable(tmp_path, [1, 0])
    from uuid import uuid4
    big = "x" * 850_000
    def row(ts):
        return dict(id=str(uuid4()), device_code="D", device_type="qc", group_code="g",
                    scan_rate_ms=100, ts_ms=ts, config_version=2,
                    stream_values_json=msgspec.json.encode({"1": big}),
                    stream_qualities_json=msgspec.json.encode({"1": 192}),
                    latest_fields_json=msgspec.json.encode({"1": {"v": big, "t": ts, "q": 192}}),
                    frame_json=msgspec.json.encode({"ts": ts, "values": {"1": big}}))
    w.r.pipeline.side_effect = lambda transaction=False: Pipeline([1, 0])
    w.write_durable_frames([row(1), row(2)])
    assert w.r.pipeline.call_count == 1
    w.close()


def test_restart_scan_has_fixed_rowid_ceiling(tmp_path):
    q = CollectorRetryOutbox(tmp_path / "q.db")
    q.record_failure(**sample(1))
    q.record_failure(**sample(2))
    scan = q.iter_pending_latest(batch_size=1)
    first = next(scan)
    q.record_failure(**sample(3))
    remaining = list(scan)
    assert len([first] + remaining) == 2
    q.close()


def test_fastlane_failed_group_does_not_starve_other_groups(tmp_path):
    w, _ = durable(tmp_path, [1, 0])
    delivered = threading.Event()
    def send(script, count, key, payload, device, group):
        if group == "bad":
            raise RuntimeError("bad latest payload")
        delivered.set()
        return 1
    w.r.eval.side_effect = send
    w._queue_latest("D", "bad", {"1": {"v": "1", "t": 1, "q": 192}})
    w._queue_latest("D", "good", {"1": {"v": "1", "t": 1, "q": 192}})
    w.ping()
    assert delivered.wait(3)
    assert ("D", "bad") in w._latest_pending
    w.close()


def test_outbox_telemetry_failure_does_not_stop_history_replay(tmp_path):
    w, _ = durable(tmp_path, [1, 0])
    w.outbox.record_failure(**sample())
    w._metrics.set_gauge = MagicMock(side_effect=RuntimeError("metric down"))
    w.ping()
    deadline = time.monotonic() + 3
    while w.outbox.count() and time.monotonic() < deadline: time.sleep(.001)
    assert w.outbox.count() == 0
    w.close()


def test_redis_window_budget_is_checked_before_each_pipeline(tmp_path):
    w, _ = durable(tmp_path, [1, 0])
    w._MAX_REDIS_WINDOW_BYTES = 2400
    for ts in range(1, 5):
        w.outbox.record_failure(**sample(ts))
    rows = w.outbox._conn.execute(
        "SELECT * FROM collector_retry_outbox_v2 ORDER BY rowid"
    ).fetchall()
    pipes = []
    def make_pipe(transaction=False):
        pipe = Pipeline([1, 0])
        pipes.append(pipe)
        return pipe
    w.r.pipeline.side_effect = make_pipe
    w.write_durable_frames(rows)
    assert len(pipes) >= 2
    for pipe in pipes:
        assert pipe.command == ("WAITAOF", 1, 0, 2000)
        estimated = sum(
            40 + sum(len(arg if isinstance(arg, bytes) else str(arg).encode()) + 64
                     for arg in call[1:])
            for call in pipe.calls
        )
        assert estimated <= w._MAX_REDIS_WINDOW_BYTES
    w.close()


def test_wide_frames_share_bounded_sqlite_commits(tmp_path):
    q = CollectorRetryOutbox(tmp_path / "wide.db")
    q._COMMIT_WINDOW_S = 0.05
    observed = []
    original = q._commit_batch
    def inspect(batch):
        observed.append(sum(item.payload_bytes for item in batch))
        return original(batch)
    q._commit_batch = inspect
    barrier = threading.Barrier(8)
    errors = []
    def submit(ts):
        row = sample(ts)
        row["frame"] = {"ts": ts, "blob": "x" * 250_000}
        try:
            barrier.wait()
            q.record_failure(**row)
        except BaseException as exc:
            errors.append(exc)
    threads = [threading.Thread(target=submit, args=(ts,)) for ts in range(8)]
    for thread in threads:
        thread.start()
    for thread in threads:
        thread.join(5)
    assert not errors
    assert not any(thread.is_alive() for thread in threads)
    assert q.count() == 8
    assert len(observed) >= 2
    assert max(observed) <= q._MAX_COMMIT_BYTES
    q.close()
