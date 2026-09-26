#!/usr/bin/env python3
"""Create and verify a Linux archive independently of host filesystem modes."""
import argparse
import hashlib
import os
from pathlib import Path
import tarfile
import tempfile


EXECUTABLES = {
    "ScreenshotBox.Linux", "createdump",
    "installer/linux/install.sh", "installer/linux/uninstall.sh",
}


def expected_mode(relative, is_directory):
    return 0o755 if is_directory or relative in EXECUTABLES else 0o644


def normalize(entry):
    relative = entry.name.split("/", 1)[1] if "/" in entry.name else ""
    entry.mode = expected_mode(relative, entry.isdir())
    entry.uid = entry.gid = 0
    entry.uname = entry.gname = ""
    return entry


def verify(directory, archive):
    directory, archive = Path(directory), Path(archive)
    expected = {directory.name: directory}
    expected.update({directory.name + "/" + p.relative_to(directory).as_posix(): p
                     for p in directory.rglob("*")})
    seen = set()
    with tarfile.open(archive, "r:gz") as package:
        for entry in package:
            if entry.name not in expected or entry.name in seen:
                raise ValueError("Unexpected or duplicate archive entry: " + entry.name)
            source = expected[entry.name]
            if source.is_symlink() or not (entry.isdir() or entry.isfile()):
                raise ValueError("Symlinks and special files cannot be packaged: " + entry.name)
            relative = source.relative_to(directory).as_posix() if source != directory else ""
            if entry.isdir() != source.is_dir() or entry.mode != expected_mode(relative, source.is_dir()):
                raise ValueError("Incorrect archive type or permissions: " + entry.name)
            if entry.uid != 0 or entry.gid != 0 or entry.uname or entry.gname:
                raise ValueError("Archive contains host owner metadata: " + entry.name)
            if entry.isfile():
                extracted = package.extractfile(entry)
                digest = hashlib.sha256()
                for chunk in iter(lambda: extracted.read(1024 * 1024), b""):
                    digest.update(chunk)
                if digest.digest() != hashlib.sha256(source.read_bytes()).digest():
                    raise ValueError("Archive content differs: " + entry.name)
            seen.add(entry.name)
    if seen != set(expected):
        raise ValueError("Archive is missing release files")
    return len(seen)


def create(directory, archive):
    directory, archive = Path(directory), Path(archive)
    if not directory.is_dir() or directory.is_symlink():
        raise ValueError("Release directory must be a regular directory")
    if archive.exists():
        raise FileExistsError("Archive already exists: " + str(archive))
    for source in directory.rglob("*"):
        if source.is_symlink() or not (source.is_file() or source.is_dir()):
            raise ValueError("Unsupported release file: " + str(source))
    descriptor, temporary_name = tempfile.mkstemp(prefix=".linux-archive-", suffix=".tar.gz", dir=archive.parent)
    os.close(descriptor)
    temporary = Path(temporary_name)
    try:
        with tarfile.open(temporary, "w:gz", compresslevel=9) as package:
            package.add(directory, arcname=directory.name, filter=normalize)
        entries = verify(directory, temporary)
        temporary.replace(archive)
        return entries
    finally:
        if temporary.exists():
            temporary.unlink()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("archive", type=Path)
    parser.add_argument("--verify", action="store_true")
    args = parser.parse_args()
    count = verify(args.directory, args.archive) if args.verify else create(args.directory, args.archive)
    print("Archive entries verified: " + str(count))
