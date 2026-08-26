import asyncio
import os
import shutil
import tempfile
import time
import unittest
import sys
from pathlib import Path

# 将 backend 目录添加到 sys.path
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..')))

from app.services.task_manager import (
    ExportTaskManager,
    ExportTask,
    TaskLimitError,
    get_export_task_manager,
)

class TestExportTaskManager(unittest.IsolatedAsyncioTestCase):
    def setUp(self):
        self.test_dir = tempfile.mkdtemp()
        self.tasks_file = os.path.join(self.test_dir, "tasks.json")
        
        # 我们将修补 ExportTaskManager 以使用此文件
        # 我们可以直接修补存储位置处的常量/属性
        self.original_init = ExportTaskManager.__init__
        
        ExportTaskManager._instance = None

    def tearDown(self):
        ExportTaskManager._instance = None
        shutil.rmtree(self.test_dir)

    async def test_persistence(self):
        # 使用特定的 tasks_file 路径实例化（我们需要添加此功能）
        manager = ExportTaskManager(tasks_file=self.tasks_file)
        
        # 创建一个任务
        task_id = manager.create_task({"type": "test"})
        manager.update_task(task_id, status="completed", progress=100)
        
        # 检查文件是否已创建
        self.assertTrue(os.path.exists(self.tasks_file), "Tasks JSON file should be created")
        
        # 重置单例以强制重新加载
        ExportTaskManager._instance = None
        
        # 重新加载并检查
        manager2 = ExportTaskManager(tasks_file=self.tasks_file)
        task = manager2.get_task(task_id)
        
        self.assertIsNotNone(task, "Task should be loaded from persistence")
        self.assertEqual(task.status, "completed")
        self.assertEqual(task.progress, 100)
        self.assertEqual(task.metadata["type"], "test")

    async def test_cleanup(self):
        manager = ExportTaskManager(tasks_file=self.tasks_file)
        task_id = manager.create_task({"type": "cleanup_test"})
        
        # 创建一个虚拟文件
        dummy_export_file = os.path.join(self.test_dir, "export.csv")
        with open(dummy_export_file, "w") as f:
            f.write("test")
            
        manager.update_task(task_id, status="completed", file_path=dummy_export_file)
        
        # 手动将 created_at 修改为超过 24 小时
        from datetime import datetime, timezone, timedelta
        manager.tasks[task_id].created_at = datetime.now(timezone.utc) - timedelta(hours=25)
        
        # 手动运行一次清理循环（我们可能需要调用内部逻辑或让它自行运行）
        manager.perform_cleanup()
        
        self.assertNotIn(task_id, manager.tasks, "Expired task should be removed from tasks dict")
        self.assertFalse(os.path.exists(dummy_export_file), "Exported file should be deleted")

    async def test_concurrency_limits_are_enforced_per_user_and_globally(self):
        manager = ExportTaskManager(tasks_file=self.tasks_file)
        manager.max_active_tasks = 2
        manager.max_active_tasks_per_user = 1

        first = manager.create_task({"owner_id": 7})
        with self.assertRaises(TaskLimitError):
            manager.create_task({"owner_id": 7})
        second = manager.create_task({"owner_id": 8})
        with self.assertRaises(TaskLimitError):
            manager.create_task({"owner_id": 9})

        manager.update_task(first, status="failed")
        released_per_user = manager.create_task({"owner_id": 7})
        self.assertIn(released_per_user, manager.tasks)
        manager.update_task(second, status="failed")
        released_global = manager.create_task({"owner_id": 9})
        self.assertIn(released_global, manager.tasks)

    async def test_retained_task_limit_removes_oldest_terminal_task(self):
        manager = ExportTaskManager(tasks_file=self.tasks_file)
        manager.max_retained_tasks = 1

        first = manager.create_task({"owner_id": 1})
        manager.update_task(first, status="completed")
        second = manager.create_task({"owner_id": 2})
        manager.update_task(second, status="completed")

        self.assertNotIn(first, manager.tasks)
        self.assertIn(second, manager.tasks)

if __name__ == '__main__':
    unittest.main()
