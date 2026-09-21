import threading
import time
from types import SimpleNamespace
from unittest.mock import MagicMock
import msgspec
import pytest
from storage.collector_outbox import CollectorRetryOutbox
from storage.durable_redis_writer import DurableRedisWriter


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
    assert [r[0] for r in q._conn.execute("SELECT ts_ms FROM collector_retry_outbox ORDER BY ts_ms")]==[1,2]
    assert q._conn.execute("PRAGMA synchronous").fetchone()[0]==2
    q.close()


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


class Pipeline:
    def __init__(self, persisted):self.persisted=persisted;self.calls=[]
    def __enter__(self):return self
    def __exit__(self,*args):pass
    def eval(self,*args):self.calls.append(args)
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
    assert b'1234' in pipe.calls[0][-1]
    w.close()
