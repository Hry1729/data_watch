import asyncio
import json
import os
import time
import uuid
import dataclasses
import threading
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict, Optional

from app.core.config import get_settings
from app.core.time_utils import LOCAL_TZ


@dataclass
class ExportTask:
    task_id: str
    status: str  # "running", "completed", "failed"
    created_at: datetime
    progress: int = 0
    file_path: Optional[str] = None
    error: Optional[str] = None
    metadata: Dict[str, Any] = field(default_factory=dict)


class TaskLimitError(RuntimeError):
    """Raised when export concurrency or retained-task capacity is exhausted."""


class ExportTaskManager:
    _instance = None
    _lock = threading.Lock()

    def __new__(cls, tasks_file: Optional[str] = None):
        with cls._lock:
            if cls._instance is None:
                cls._instance = super(ExportTaskManager, cls).__new__(cls)
                cls._instance.tasks = {}
                cls._instance._tasks_lock = threading.Lock()
                cls._instance._cleanup_task = None
                cls._instance.tasks_file = tasks_file or str(Path(__file__).resolve().parents[3] / "data" / "exports" / "tasks.json")
                settings = get_settings()
                cls._instance.max_active_tasks = max(1, int(settings.export_max_active_tasks))
                cls._instance.max_active_tasks_per_user = max(
                    1, int(settings.export_max_active_tasks_per_user)
                )
                cls._instance.max_retained_tasks = max(1, int(settings.export_max_retained_tasks))
                cls._instance._load_tasks()
            return cls._instance

    def _load_tasks(self):
        if os.path.exists(self.tasks_file):
            try:
                with open(self.tasks_file, "r", encoding="utf-8") as f:
                    data = json.load(f)
                    for task_id, task_data in data.items():
                        if "created_at" in task_data:
                            if isinstance(task_data["created_at"], (int, float)):
                                task_data["created_at"] = datetime.fromtimestamp(task_data["created_at"], tz=timezone.utc)
                            elif isinstance(task_data["created_at"], str):
                                created_at = datetime.fromisoformat(task_data["created_at"])
                                # M10：created_at 统一为带时区时间。旧文件中不带时区的值按项目约定
                                # 视为 UTC+8（Asia/Shanghai），否则与 aware 时间相减会让清理协程崩溃。
                                if created_at.tzinfo is None:
                                    created_at = created_at.replace(tzinfo=LOCAL_TZ)
                                task_data["created_at"] = created_at
                        self.tasks[task_id] = ExportTask(**task_data)
            except Exception as e:
                print(f"Error loading tasks: {e}")
        # 服务启动时将残留的 running 任务标记为 failed，避免状态永久卡住
        interrupted = [t for t in self.tasks.values() if t.status == "running"]
        if interrupted:
            for t in interrupted:
                t.status = "failed"
                t.error = "服务重启中断"
        retained_changed = self._prune_retained_locked()
        if interrupted or retained_changed:
            self._save_tasks()

    def _save_tasks(self):
        try:
            os.makedirs(os.path.dirname(self.tasks_file) or ".", exist_ok=True)
            data = {}
            for task_id, task in self.tasks.items():
                task_dict = dataclasses.asdict(task)
                if isinstance(task.created_at, datetime):
                    task_dict["created_at"] = task.created_at.isoformat()
                data[task_id] = task_dict
            
            temp_file = self.tasks_file + ".tmp"
            with open(temp_file, "w", encoding="utf-8") as f:
                json.dump(data, f, ensure_ascii=False, indent=2)
            os.replace(temp_file, self.tasks_file)
        except Exception as e:
            print(f"Error saving tasks: {e}")

    def create_task(self, metadata: Optional[Dict[str, Any]] = None) -> str:
        task_metadata = dict(metadata or {})
        with self._tasks_lock:
            active_tasks = [task for task in self.tasks.values() if self._is_active(task)]
            if len(active_tasks) >= self.max_active_tasks:
                raise TaskLimitError("export global concurrency limit reached")
            owner_id = self._owner_id(task_metadata)
            if owner_id is not None:
                owner_active = sum(
                    self._owner_id(task.metadata) == owner_id
                    for task in active_tasks
                )
                if owner_active >= self.max_active_tasks_per_user:
                    raise TaskLimitError("export per-user concurrency limit reached")
            self._prune_retained_locked()
            task_id = str(uuid.uuid4())
            task = ExportTask(
                task_id=task_id,
                status="running",
                created_at=datetime.now(timezone.utc),
                metadata=task_metadata,
            )
            self.tasks[task_id] = task
            self._save_tasks()
        self._ensure_cleanup_started()
        return task_id

    def update_task(
        self,
        task_id: str,
        status: Optional[str] = None,
        progress: Optional[int] = None,
        file_path: Optional[str] = None,
        error: Optional[str] = None,
    ):
        with self._tasks_lock:
            if task_id in self.tasks:
                task = self.tasks[task_id]
                if status:
                    task.status = status
                if progress is not None:
                    task.progress = progress
                if file_path:
                    task.file_path = file_path
                if error:
                    task.error = error
                self._prune_retained_locked()
                self._save_tasks()

    def get_task(self, task_id: str) -> Optional[ExportTask]:
        with self._tasks_lock:
            return self.tasks.get(task_id)

    def _ensure_cleanup_started(self):
        if self._cleanup_task is None or self._cleanup_task.done():
            self._cleanup_task = asyncio.create_task(self._cleanup_loop())

    async def _cleanup_loop(self):
        while True:
            await asyncio.sleep(300)  # Clean up every 5 minutes
            self.perform_cleanup()
            
            with self._tasks_lock:
                if not self.tasks:
                    break
                
    def perform_cleanup(self):
        now = datetime.now(timezone.utc)
        # 任务在 24 小时（86400 秒）后过期
        with self._tasks_lock:
            expired_ids = [
                task_id
                for task_id, task in self.tasks.items()
                if not self._is_active(task)
                and (now - task.created_at).total_seconds() > 86400
            ]
            
            has_changes = False
            for task_id in expired_ids:
                task = self.tasks[task_id]
                if task.file_path and os.path.exists(task.file_path):
                    try:
                        os.remove(task.file_path)
                    except Exception as e:
                        print(f"Error removing exported file {task.file_path}: {e}")
                del self.tasks[task_id]
                has_changes = True

            has_changes = self._prune_retained_locked() or has_changes
                
            if has_changes:
                self._save_tasks()

    @staticmethod
    def _is_active(task: ExportTask) -> bool:
        return task.status in {"running", "pending", "processing"}

    @staticmethod
    def _owner_id(metadata: Dict[str, Any] | None) -> str | None:
        if not metadata:
            return None
        value = metadata.get("owner_id")
        if value is None or str(value).strip() == "":
            return None
        return str(value)

    def _prune_retained_locked(self) -> bool:
        terminal = [
            task
            for task in self.tasks.values()
            if not self._is_active(task)
        ]
        overflow = len(terminal) - self.max_retained_tasks
        if overflow <= 0:
            return False
        victims = sorted(terminal, key=lambda task: task.created_at)[:overflow]
        for task in victims:
            if task.file_path and os.path.exists(task.file_path):
                try:
                    os.remove(task.file_path)
                except OSError:
                    pass
            self.tasks.pop(task.task_id, None)
        return True


def get_export_task_manager() -> ExportTaskManager:
    return ExportTaskManager()
