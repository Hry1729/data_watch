import asyncio
import os
import shutil
import tempfile
import time
import unittest
import sys
from pathlib import Path

# Add backend directory to sys.path
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..')))

from app.services.task_manager import ExportTaskManager, ExportTask, get_export_task_manager

class TestExportTaskManager(unittest.IsolatedAsyncioTestCase):
    def setUp(self):
        self.test_dir = tempfile.mkdtemp()
        self.tasks_file = os.path.join(self.test_dir, "tasks.json")
        
        # We will patch ExportTaskManager to use this file
        # We can just patch the constant/attribute where it's stored
        self.original_init = ExportTaskManager.__init__
        
        ExportTaskManager._instance = None

    def tearDown(self):
        ExportTaskManager._instance = None
        shutil.rmtree(self.test_dir)

    async def test_persistence(self):
        # Instantiate with a specific tasks_file path (we will need to add this capability)
        manager = ExportTaskManager(tasks_file=self.tasks_file)
        
        # Create a task
        task_id = manager.create_task({"type": "test"})
        manager.update_task(task_id, status="completed", progress=100)
        
        # Check if file was created
        self.assertTrue(os.path.exists(self.tasks_file), "Tasks JSON file should be created")
        
        # Reset singleton to force reload
        ExportTaskManager._instance = None
        
        # Reload and check
        manager2 = ExportTaskManager(tasks_file=self.tasks_file)
        task = manager2.get_task(task_id)
        
        self.assertIsNotNone(task, "Task should be loaded from persistence")
        self.assertEqual(task.status, "completed")
        self.assertEqual(task.progress, 100)
        self.assertEqual(task.metadata["type"], "test")

    async def test_cleanup(self):
        manager = ExportTaskManager(tasks_file=self.tasks_file)
        task_id = manager.create_task({"type": "cleanup_test"})
        
        # Create a dummy file
        dummy_export_file = os.path.join(self.test_dir, "export.csv")
        with open(dummy_export_file, "w") as f:
            f.write("test")
            
        manager.update_task(task_id, file_path=dummy_export_file)
        
        # Manually alter created_at to be older than 24 hours
        from datetime import datetime, timezone, timedelta
        manager.tasks[task_id].created_at = datetime.now(timezone.utc) - timedelta(hours=25)
        
        # Run cleanup loop once manually (we might need to invoke internal logic or let it run)
        manager.perform_cleanup()
        
        self.assertNotIn(task_id, manager.tasks, "Expired task should be removed from tasks dict")
        self.assertFalse(os.path.exists(dummy_export_file), "Exported file should be deleted")

if __name__ == '__main__':
    unittest.main()
