import hashlib
import json
import os
from pathlib import Path
import shutil
import stat
import subprocess
import tempfile
import unittest


SCRIPTS = Path(__file__).resolve().parent
SHELLS = [shell for shell in (shutil.which("powershell.exe"), shutil.which("pwsh")) if shell]
TEST_ROOT = SCRIPTS.parent / "artifacts" / "writer-double-click-20261014" / "file-tests"


@unittest.skipUnless(os.name == "nt" and SHELLS, "Windows launcher")
class WriterLauncherTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        assert TEST_ROOT.resolve().is_relative_to(SCRIPTS.parent.resolve())
        TEST_ROOT.mkdir(parents=True, exist_ok=True)

    def prepare(self, root):
        package = root / "中文 启动目录"
        package.mkdir()
        for name in ("writer_test_launcher.ps1", "writer_test.ps1", "diagnose_pipeline.ps1", "pipeline_diagnostics.py", "writer_data_check.py"):
            shutil.copyfile(SCRIPTS / name, package / name)
        settings = root / "配置 目录" / "settings.toml"
        settings.parent.mkdir()
        settings.write_bytes(b"[writer]\r\nconsumer_workers = 3\r\nbatch_count = 150\r\n# PRIVATE_FIXTURE\r\n")
        return package, settings, root / "D drive results"

    def run_launcher(self, shell, package, settings, output, action="check", stdin=None):
        environment = os.environ.copy()
        if Path(shell).name.lower() == "powershell.exe":
            environment["PSModulePath"] = str(Path(shell).parent / "Modules")
        return subprocess.run([
            shell, "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(package / "writer_test_launcher.ps1"),
            "-Action", action, "-SettingsPath", str(settings), "-OutputRoot", str(output),
        ], input=stdin, capture_output=True, timeout=30, env=environment)

    def test_permission_check_preserves_config_and_logs_only_to_output(self):
        for shell in SHELLS:
            with self.subTest(shell=shell), tempfile.TemporaryDirectory(dir=TEST_ROOT) as directory:
                package, settings, output = self.prepare(Path(directory))
                original = settings.read_bytes()
                result = self.run_launcher(shell, package, settings, output)
                self.assertEqual(result.returncode, 0, result.stderr.decode(errors="replace"))
                self.assertEqual(settings.read_bytes(), original)
                self.assertFalse(list(settings.parent.glob("writer-test-permission-*.tmp")))
                log = next((output / "operation-logs").glob("*.log")).read_text(encoding="utf-8")
                self.assertIn("elevated=", log)
                self.assertIn("acl=", log)
                self.assertNotIn("PRIVATE_FIXTURE", log)
                self.assertFalse((package / "operation-logs").exists())

    def test_unwritable_config_stops_without_creating_test_state(self):
        for shell in SHELLS:
            with self.subTest(shell=shell), tempfile.TemporaryDirectory(dir=TEST_ROOT) as directory:
                package, settings, output = self.prepare(Path(directory))
                original = settings.read_bytes()
                settings.chmod(stat.S_IREAD)
                try:
                    result = self.run_launcher(shell, package, settings, output)
                    self.assertNotEqual(result.returncode, 0)
                    self.assertEqual(settings.read_bytes(), original)
                    self.assertFalse(Path(str(settings) + ".writer-test-state.json").exists())
                    self.assertFalse(list(settings.parent.glob("writer-test-permission-*.tmp")))
                    log = next((output / "operation-logs").glob("*.log")).read_text(encoding="utf-8")
                    self.assertIn("hresult=", log)
                finally:
                    settings.chmod(stat.S_IWRITE)

    def test_menu_applies_profile_through_packaged_script(self):
        for shell in SHELLS:
            with self.subTest(shell=shell), tempfile.TemporaryDirectory(dir=TEST_ROOT) as directory:
                package, settings, output = self.prepare(Path(directory))
                launcher = package / "writer_test_launcher.ps1"
                text = launcher.read_text(encoding="utf-8-sig")
                # Exercise the elevated menu without opening a UAC dialog in the test.
                text = text.replace("$isAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)", "$isAdmin = $true")
                launcher.write_text(text, encoding="utf-8-sig")
                result = self.run_launcher(shell, package, settings, output, "menu", b"1\n0\n")
                self.assertEqual(result.returncode, 0, result.stderr.decode(errors="replace"))
                self.assertIn("consumer_workers = 4", settings.read_text())
                self.assertIn("batch_count = 200", settings.read_text())
                state = json.loads(Path(str(settings) + ".writer-test-state.json").read_text(encoding="utf-8"))
                self.assertEqual(state["last_profile_sha256"].lower(), hashlib.sha256(settings.read_bytes()).hexdigest())
                self.assertFalse((package / "operation-logs").exists())
                self.assertTrue(list((output / "operation-logs").glob("test-baseline-*.log")))

    def test_uac_request_keeps_unicode_and_space_paths_quoted(self):
        for shell in SHELLS:
            with self.subTest(shell=shell), tempfile.TemporaryDirectory(dir=TEST_ROOT) as directory:
                root = Path(directory)
                package, settings, output = self.prepare(root)
                launcher = package / "writer_test_launcher.ps1"
                recorded = root / "launch.json"
                text = launcher.read_text(encoding="utf-8-sig")
                text = text.replace("$isAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)", "$isAdmin = $false")
                # Record the launch request instead of starting a visible privileged process.
                stub = ("function Start-Process { param($FilePath,$ArgumentList,$Verb,$WindowStyle) "
                        "[IO.File]::WriteAllText('" + str(recorded).replace("'", "''") + "', "
                        "(@{file=$FilePath;args=$ArgumentList;verb=$Verb;window=$WindowStyle} | ConvertTo-Json), "
                        "(New-Object Text.UTF8Encoding($false))) }\n")
                text = text.replace("$identity = [Security.Principal.WindowsIdentity]::GetCurrent()", stub + "$identity = [Security.Principal.WindowsIdentity]::GetCurrent()")
                launcher.write_text(text, encoding="utf-8-sig")
                result = self.run_launcher(shell, package, settings, output, "menu")
                self.assertEqual(result.returncode, 0, result.stderr.decode(errors="replace"))
                request = json.loads(recorded.read_text(encoding="utf-8"))
                self.assertEqual(request["verb"], "RunAs")
                self.assertEqual(request["window"], "Normal")
                self.assertIn('"' + str(settings) + '"', request["args"])
                self.assertIn('"' + str(launcher) + '"', request["args"])
                self.assertFalse(Path(str(settings) + ".writer-test-state.json").exists())

    def test_cmd_starts_packaged_launcher_from_unicode_directory(self):
        with tempfile.TemporaryDirectory(dir=TEST_ROOT) as directory:
            package, settings, output = self.prepare(Path(directory))
            text = (SCRIPTS / "run_writer_test.cmd").read_text(encoding="ascii")
            # Use the diagnostic action to check CMD quoting without opening UAC.
            text = text.replace('"%~dp0writer_test_launcher.ps1"',
                                '"%~dp0writer_test_launcher.ps1" -Action check -SettingsPath "' + str(settings)
                                + '" -OutputRoot "' + str(output) + '"')
            entry = package / "run_writer_test.cmd"
            # The injected fixture paths contain Chinese; CMD reads them as UTF-8.
            entry.write_bytes(text.replace("\r\n", "\n").replace("\n", "\r\n").encode("utf-8"))
            environment = os.environ.copy()
            environment["PSModulePath"] = str(Path(shutil.which("powershell.exe")).parent / "Modules")
            result = subprocess.run([shutil.which("cmd.exe"), "/d", "/c", str(entry)],
                                    cwd=package, input=b"\n", capture_output=True, timeout=30, env=environment)
            self.assertEqual(result.returncode, 0, result.stderr.decode(errors="replace"))
            self.assertTrue(list((output / "operation-logs").glob("launcher-*.log")))


if __name__ == "__main__":
    unittest.main()
