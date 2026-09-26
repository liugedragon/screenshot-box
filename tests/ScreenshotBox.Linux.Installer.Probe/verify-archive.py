#!/usr/bin/env python3
"""Check normalized permissions and payload integrity without running the app."""
import importlib.util
from pathlib import Path
import tarfile
import tempfile

repo = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("archive_linux", repo / "scripts/archive-linux.py")
archive_linux = importlib.util.module_from_spec(spec)
spec.loader.exec_module(archive_linux)
checks = []

with tempfile.TemporaryDirectory(prefix="screenshotbox-archive-probe-") as workspace:
    root = Path(workspace)
    package = root / "ScreenshotBox-fixture"
    package.mkdir(mode=0o777)
    for relative in ["ScreenshotBox.Linux", "createdump", "README.md", "licenses/LICENSE.txt", "installer/linux/install.sh", "installer/linux/uninstall.sh", "models/chinese/example.onnx"]:
        source = package / relative
        source.parent.mkdir(parents=True, exist_ok=True)
        source.write_bytes((relative + "\n").encode())
        source.chmod(0o777)
    archive = root / "fixture.tar.gz"
    archive_linux.create(package, archive)
    with tarfile.open(archive) as files:
        entries = files.getmembers()
        checks.append(all(p.mode == 0o755 for p in entries if p.isdir()))
        checks.append(all(p.mode == 0o755 for p in entries if p.name.split("/", 1)[-1] in archive_linux.EXECUTABLES))
        checks.append(all(p.mode == 0o644 for p in entries if p.isfile() and p.name.split("/", 1)[-1] not in archive_linux.EXECUTABLES))
        checks.append(all(p.uid == p.gid == 0 and not p.uname and not p.gname for p in entries))
        checks.append(files.extractfile(package.name + "/models/chinese/example.onnx").read() == b"models/chinese/example.onnx\n")
    checks.append(archive_linux.verify(package, archive) == len(entries))
    (package / "README.md").write_text("Changed after archiving")
    try:
        archive_linux.verify(package, archive)
        checks.append(False)
    except ValueError:
        checks.append(True)
    (package / "unexpected-link").symlink_to(root / "outside")
    try:
        archive_linux.create(package, root / "unsafe.tar.gz")
        checks.append(False)
    except ValueError:
        checks.append(not (root / "unsafe.tar.gz").exists())

assert all(checks), checks
print("Linux archive checks passed: " + str(len(checks)))
