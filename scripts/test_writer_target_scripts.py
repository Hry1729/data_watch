import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest


SCRIPTS = Path(__file__).resolve().parent
SHELLS = [shell for shell in (shutil.which("powershell.exe"), shutil.which("pwsh")) if shell]
TEST_ROOT = SCRIPTS.parent / "artifacts" / "writer-script-repair-20261014" / "file-tests"


@unittest.skipUnless(os.name == "nt" and SHELLS, "Windows PowerShell file operations")
class WriterTargetScriptTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        assert TEST_ROOT.resolve().is_relative_to(SCRIPTS.parent.resolve())
        TEST_ROOT.mkdir(parents=True, exist_ok=True)

    def prepare_package(self, root):
        package = root / "delivery"
        payload = package / "payload" / "writer"
        (payload / "_internal").mkdir(parents=True)
        (payload / "data-watch-writer.exe").write_bytes(b"new binary fixture")
        (payload / "_internal" / "dependency.dll").write_bytes(b"new dependency fixture")
        files = [{"path": p.relative_to(package).as_posix(), "sha256": hashlib.sha256(p.read_bytes()).hexdigest()}
                 for p in payload.rglob("*") if p.is_file()]
        (package / "release-manifest.json").write_text(json.dumps({"source_revision": "fixture", "files": files}))
        shutil.copyfile(SCRIPTS / "update_writer.ps1", package / "update_writer.ps1")
        target = root / "installation" / "writer"
        (target / "_internal").mkdir(parents=True)
        (target / "data-watch-writer.exe").write_bytes(b"original binary fixture")
        (target / "_internal" / "dependency.dll").write_bytes(b"original dependency fixture")
        (target / "local.txt").write_bytes(b"installation-local fixture")
        return package, target

    def run_script(self, shell, script, *arguments):
        environment = os.environ.copy()
        # Do not make Windows PowerShell load PowerShell 7's security module.
        if Path(shell).name.lower() == "powershell.exe":
            environment["PSModulePath"] = str(Path(shell).parent / "Modules")
        result = subprocess.run(
            [shell, "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(script), *arguments],
            stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=30, env=environment,
        )
        self.assertEqual(result.returncode, 0, result.stderr.decode(errors="replace"))
        return result

    def test_profiles_write_real_files_and_restore_original_bytes(self):
        for shell in SHELLS:
            for newline in ("\r\n", "\n"):
                with self.subTest(shell=shell, newline=repr(newline)), tempfile.TemporaryDirectory(dir=TEST_ROOT) as directory:
                    script = Path(directory) / "writer_test.ps1"
                    shutil.copyfile(SCRIPTS / "writer_test.ps1", script)
                    settings = Path(directory) / "配置 目录" / "settings.toml"
                    settings.parent.mkdir()
                    original = newline.join([
                        "# 中文配置", "[redis]", "stream_shard_count = 4", "[writer]",
                        "consumer_workers = 3", "batch_count = 150", "max_buffer_rows = 2000", "",
                    ]).encode("utf-8-sig")
                    settings.write_bytes(original)
                    for mode, workers, batch in (("baseline", 4, 200), ("workers8", 8, 200), ("batch400", 4, 400)):
                        self.run_script(shell, script, "-Mode", mode, "-SettingsPath", str(settings))
                        text = settings.read_text(encoding="utf-8-sig")
                        self.assertIn(f"consumer_workers = {workers}", text)
                        self.assertIn(f"batch_count = {batch}", text)
                        self.assertIn("max_buffer_rows = 2000", text)
                        self.assertIn("# 中文配置", text)
                        state = json.loads(Path(str(settings) + ".writer-test-state.json").read_text(encoding="utf-8-sig"))
                        self.assertEqual(state["last_profile_sha256"].lower(), hashlib.sha256(settings.read_bytes()).hexdigest())
                        self.assertEqual(Path(state["backup_path"]).read_bytes(), original)
                    self.run_script(shell, script, "-Mode", "restore", "-SettingsPath", str(settings))
                    self.assertEqual(settings.read_bytes(), original)
                    self.assertFalse(Path(str(settings) + ".writer-test-state.json").exists())

    def test_update_and_rollback_move_complete_directories(self):
        for shell in SHELLS:
            with self.subTest(shell=shell), tempfile.TemporaryDirectory(dir=TEST_ROOT) as directory:
                package, target = self.prepare_package(Path(directory))
                script = package / "update_writer.ps1"
                self.run_script(shell, script, "-WriterDirectory", str(target))
                self.assertEqual((target / "data-watch-writer.exe").read_bytes(), b"new binary fixture")
                self.assertEqual((target / "local.txt").read_bytes(), b"installation-local fixture")
                backups = list(target.parent.glob("writer.backup-*"))
                self.assertEqual(len(backups), 1)
                self.assertEqual((backups[0] / "data-watch-writer.exe").read_bytes(), b"original binary fixture")
                self.run_script(shell, script, "-WriterDirectory", str(target), "-RollbackBackup", str(backups[0]))
                self.assertEqual((target / "data-watch-writer.exe").read_bytes(), b"original binary fixture")
                self.assertEqual((target / "_internal" / "dependency.dll").read_bytes(), b"original dependency fixture")
                self.assertEqual(len(list(target.parent.glob("writer.before-rollback-*"))), 1)
                self.assertTrue(list((package / "operation-logs").glob("*.log")))

    def test_existing_state_after_failed_replace_can_resume(self):
        for shell in SHELLS:
            with self.subTest(shell=shell), tempfile.TemporaryDirectory(dir=TEST_ROOT) as directory:
                root = Path(directory)
                script = root / "writer_test.ps1"
                shutil.copyfile(SCRIPTS / "writer_test.ps1", script)
                settings = root / "settings.toml"
                original = b"[writer]\r\nconsumer_workers = 3\r\nbatch_count = 150\r\n"
                settings.write_bytes(original)
                backup = root / "settings.toml.writer-original-before-failure"
                backup.write_bytes(original)
                Path(str(settings) + ".writer-test-state.json").write_text(json.dumps({
                    "backup_path": str(backup), "last_profile_sha256": hashlib.sha256(original).hexdigest(),
                }))
                self.run_script(shell, script, "-Mode", "baseline", "-SettingsPath", str(settings))
                self.assertIn("consumer_workers = 4", settings.read_text())
                self.run_script(shell, script, "-Mode", "restore", "-SettingsPath", str(settings))
                self.assertEqual(settings.read_bytes(), original)

    def test_capture_reports_stopped_writer_without_changing_settings(self):
        for shell in SHELLS:
            with self.subTest(shell=shell), tempfile.TemporaryDirectory(dir=TEST_ROOT) as directory:
                root = Path(directory)
                script = root / "writer_test.ps1"
                shutil.copyfile(SCRIPTS / "writer_test.ps1", script)
                settings = root / "settings.toml"
                original = b"[writer]\r\nconsumer_workers = 4\r\nbatch_count = 200\r\n"
                settings.write_bytes(original)
                environment = os.environ.copy()
                if Path(shell).name.lower() == "powershell.exe":
                    environment["PSModulePath"] = str(Path(shell).parent / "Modules")
                result = subprocess.run([shell, "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(script),
                                         "-Mode", "capture", "-SettingsPath", str(settings)],
                                        capture_output=True, timeout=30, env=environment)
                self.assertNotEqual(result.returncode, 0)
                self.assertEqual(settings.read_bytes(), original)
                log = next((root / "operation-logs").glob("*.log")).read_text(encoding="utf-8")
                self.assertIn("Writer is stopped", log)

    def test_failed_stage_probe_leaves_original_installation_in_place(self):
        for shell in SHELLS:
            with self.subTest(shell=shell), tempfile.TemporaryDirectory(dir=TEST_ROOT) as directory:
                package, target = self.prepare_package(Path(directory))
                script = package / "update_writer.ps1"
                source = script.read_text(encoding="utf-8-sig")
                # A competing destination exercises a real failed filesystem rename.
                source = source.replace("Move-WriterDirectory $stage $ready",
                                        "New-Item -ItemType Directory -Path $ready | Out-Null\nMove-WriterDirectory $stage $ready")
                script.write_text(source, encoding="utf-8-sig")
                environment = os.environ.copy()
                if Path(shell).name.lower() == "powershell.exe":
                    environment["PSModulePath"] = str(Path(shell).parent / "Modules")
                result = subprocess.run([shell, "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(script),
                                         "-WriterDirectory", str(target)], capture_output=True, timeout=30, env=environment)
                self.assertNotEqual(result.returncode, 0)
                self.assertEqual((target / "data-watch-writer.exe").read_bytes(), b"original binary fixture")
                self.assertFalse(list(target.parent.glob("writer.backup-*")))
                logs = list((package / "operation-logs").glob("*.log"))
                self.assertEqual(len(logs), 1)
                self.assertIn("rename failed; hresult=", logs[0].read_text(encoding="utf-8"))

    def test_failed_final_switch_restores_original_directory(self):
        for shell in SHELLS:
            with self.subTest(shell=shell), tempfile.TemporaryDirectory(dir=TEST_ROOT) as directory:
                package, target = self.prepare_package(Path(directory))
                script = package / "update_writer.ps1"
                source = script.read_text(encoding="utf-8-sig")
                source = source.replace("try { Move-WriterDirectory $stage $target }", "try { throw 'Injected final rename failure' }")
                script.write_text(source, encoding="utf-8-sig")
                environment = os.environ.copy()
                if Path(shell).name.lower() == "powershell.exe":
                    environment["PSModulePath"] = str(Path(shell).parent / "Modules")
                result = subprocess.run([shell, "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(script),
                                         "-WriterDirectory", str(target)], capture_output=True, timeout=30, env=environment)
                self.assertNotEqual(result.returncode, 0)
                self.assertEqual((target / "data-watch-writer.exe").read_bytes(), b"original binary fixture")
                self.assertEqual((target / "_internal" / "dependency.dll").read_bytes(), b"original dependency fixture")
                log = next((package / "operation-logs").glob("*.log")).read_text(encoding="utf-8")
                self.assertIn("original Writer restored", log)


if __name__ == "__main__":
    unittest.main()
