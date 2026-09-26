#!/usr/bin/env bash
# Build the pinned SQLite native library on the oldest supported Ubuntu/glibc.
# Requirements: GCC, curl, Python 3, and binutils. Outputs stay outside Git.
set -euo pipefail
repo="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
output="${1:-$repo/artifacts/native/linux-x64}"
cache="$repo/.tools/sqlite-linux"
archive="$cache/sqlite-amalgamation-3530300.zip"
mkdir -p "$cache" "$output"
if [[ ! -f "$archive" ]]; then
  curl --fail --location --retry 2 --connect-timeout 20 \
    'https://sqlite.org/2026/sqlite-amalgamation-3530300.zip' -o "$archive.partial"
  mv "$archive.partial" "$archive"
fi
python3 - "$archive" "$cache" <<'PY'
import hashlib, pathlib, sys, zipfile
archive, destination = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
expected_zip = 'd45c688a8cb23f68611a894a756a12d7eb6ab6e9e2468ca70adbeab3808b5ab9'
expected_c = '28e484abdaa43630e34040ef6ed92be973a1ad54107803d8af5145b889c23ed7'
if hashlib.sha3_256(archive.read_bytes()).hexdigest() != expected_zip:
    raise SystemExit('SQLite archive SHA3-256 mismatch; refusing to compile.')
with zipfile.ZipFile(archive) as z:
    for name in ['sqlite3.c', 'sqlite3.h']:
        content = z.read('sqlite-amalgamation-3530300/' + name)
        if name == 'sqlite3.c' and hashlib.sha3_256(content).hexdigest() != expected_c:
            raise SystemExit('SQLite source SHA3-256 mismatch; refusing to compile.')
        (destination / name).write_bytes(content)
PY
flags=(
  -O2 -fPIC -shared -pthread
  -DSQLITE_ENABLE_COLUMN_METADATA -DSQLITE_ENABLE_FTS3
  -DSQLITE_ENABLE_FTS3_PARENTHESIS -DSQLITE_ENABLE_FTS4 -DSQLITE_ENABLE_FTS5
  -DSQLITE_ENABLE_GEOPOLY -DSQLITE_ENABLE_MATH_FUNCTIONS
  -DSQLITE_ENABLE_PREUPDATE_HOOK -DSQLITE_ENABLE_RTREE -DSQLITE_ENABLE_SESSION
  -DSQLITE_ENABLE_SNAPSHOT -DSQLITE_THREADSAFE=1
  -DSQLITE_DEFAULT_FOREIGN_KEYS=1 -DSQLITE_DEFAULT_RECURSIVE_TRIGGERS=1
)
"${CC:-gcc}" "${flags[@]}" "$cache/sqlite3.c" -ldl -lm -o "$output/libe_sqlite3.so.partial"
python3 - "$output/libe_sqlite3.so.partial" "$output/sqlite-build-verification.json" <<'PY'
import ctypes, hashlib, json, pathlib, platform, re, subprocess, sys
library, report_path = pathlib.Path(sys.argv[1]).resolve(), pathlib.Path(sys.argv[2])
lib = ctypes.CDLL(str(library))
lib.sqlite3_libversion.restype = ctypes.c_char_p
lib.sqlite3_sourceid.restype = ctypes.c_char_p
version, source = lib.sqlite3_libversion().decode(), lib.sqlite3_sourceid().decode()
expected = '2026-06-26 20:14:12 d4c0e51e4aeb96955b99185ab9cde75c339e2c29c3f3f12428d364a10d782c62'
assert version == '3.53.3', version
assert source == expected, source
for symbol in ['sqlite3_open_v2', 'sqlite3_prepare_v2', 'sqlite3_create_function_v2',
               'sqlite3_backup_init', 'sqlite3_column_origin_name', 'sqlite3_preupdate_hook',
               'sqlite3session_create', 'sqlite3_snapshot_get']:
    getattr(lib, symbol)
lib.sqlite3_open.argtypes = [ctypes.c_char_p, ctypes.POINTER(ctypes.c_void_p)]
lib.sqlite3_exec.argtypes = [ctypes.c_void_p, ctypes.c_char_p, ctypes.c_void_p,
                           ctypes.c_void_p, ctypes.POINTER(ctypes.c_char_p)]
lib.sqlite3_close.argtypes = [ctypes.c_void_p]
callback_type = ctypes.CFUNCTYPE(ctypes.c_int, ctypes.c_void_p, ctypes.c_int,
                                ctypes.POINTER(ctypes.c_char_p), ctypes.POINTER(ctypes.c_char_p))
results = []
@callback_type
def collect(context, count, values, names):
    results.append([values[i].decode() if values[i] is not None else None for i in range(count)])
    return 0
db, error = ctypes.c_void_p(), ctypes.c_char_p()
assert lib.sqlite3_open(b':memory:', ctypes.byref(db)) == 0
try:
    checks = """CREATE VIRTUAL TABLE f USING fts5(text);
        INSERT INTO f VALUES('hello sqlite'); SELECT count(*) FROM f WHERE f MATCH 'hello';
        SELECT json_extract('{"order":42}', '$.order');
        SELECT sqrt(9); CREATE VIRTUAL TABLE r USING rtree(id,x1,x2,y1,y2);"""
    assert lib.sqlite3_exec(db, checks.encode(), collect, None, ctypes.byref(error)) == 0, error.value
    assert results == [['1'], ['42'], ['3.0']], results
finally:
    lib.sqlite3_close(db)
symbols = subprocess.check_output(['readelf', '--version-info', str(library)], text=True)
versions = sorted(set(re.findall(r'GLIBC_(\d+\.\d+(?:\.\d+)?)', symbols)),
                  key=lambda v: tuple(map(int, v.split('.'))))
maximum = versions[-1]
if tuple(map(int, maximum.split('.'))) > (2, 31):
    raise SystemExit('Build on Ubuntu 20.04 or an equivalent glibc <= 2.31 toolchain; required GLIBC_' + maximum)
report = {'sqliteVersion':version, 'sourceId':source, 'sourceSha3_256':
          '28e484abdaa43630e34040ef6ed92be973a1ad54107803d8af5145b889c23ed7',
          'librarySha256':hashlib.sha256(library.read_bytes()).hexdigest(),
          'bytes':library.stat().st_size, 'maximumRequiredGlibc':maximum,
          'hostGlibc':platform.libc_ver(), 'compiler':subprocess.check_output(['gcc','--version'], text=True).splitlines()[0],
          'fts5':True, 'json':True, 'math':True, 'rtree':True, 'requiredExports':True}
report_path.write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report, indent=2))
PY
mv "$output/libe_sqlite3.so.partial" "$output/libe_sqlite3.so"
