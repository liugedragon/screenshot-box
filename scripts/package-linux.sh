#!/usr/bin/env bash
set -euo pipefail
repo="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
version="${1:-0.1.5-linux.1}"
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+([-.][A-Za-z0-9.-]+)?$ ]] || { echo 'Invalid version.' >&2; exit 2; }
if [[ -n "${DOTNET:-}" ]]; then dotnet="$DOTNET"
elif [[ -x "$repo/.tools/dotnet-linux/dotnet" ]]; then dotnet="$repo/.tools/dotnet-linux/dotnet"
else dotnet="$(command -v dotnet)"; fi
name="ScreenshotBox-$version-linux-x64"
output="$repo/artifacts/$name"
archive="$output.tar.gz"
[[ ! -e "$output" && ! -e "$archive" && ! -e "$output.sha256" ]] || { echo 'Version output exists. Move it aside or select a new version.' >&2; exit 1; }
[[ -f "$repo/artifacts/native/linux-x64/libe_sqlite3.so" && -f "$repo/artifacts/native/linux-x64/sqlite-build-verification.json" ]] || { echo 'Build the pinned SQLite library first: bash scripts/build-linux-sqlite.sh' >&2; exit 1; }
mkdir -p "$repo/artifacts"
stage="$(mktemp -d "$repo/artifacts/.linux-package-XXXXXXXX")"
trap '[[ ! -d "$stage" ]] || rm -rf -- "$stage"' EXIT
project="$repo/src/ScreenshotBox.Linux/ScreenshotBox.Linux.csproj"
"$dotnet" restore "$project" --locked-mode --disable-parallel -m:1
"$dotnet" publish "$project" --configuration Release --runtime linux-x64 --self-contained true --no-restore --output "$stage" --nologo -m:1 \
  -p:PublishSingleFile=false -p:PublishTrimmed=false -p:PublishReadyToRun=false -p:UseAppHost=true -p:Version="$version"
# The NuGet SQLite binary requires newer glibc; ship the source build verified on Ubuntu 20.04.
cp -- "$repo/artifacts/native/linux-x64/libe_sqlite3.so" "$stage/libe_sqlite3.so"
if [[ -f "$stage/runtimes/linux-x64/native/libe_sqlite3.so" ]]; then cp -- "$repo/artifacts/native/linux-x64/libe_sqlite3.so" "$stage/runtimes/linux-x64/native/libe_sqlite3.so"; fi
rm -f -- "$stage/models/v5/latin_PP-OCRv5_rec_mobile_infer.onnx" "$stage/models/v5/ppocrv5_latin_dict.txt"
if [[ -d "$stage/runtimes" ]]; then
  find "$stage/runtimes" -mindepth 1 -maxdepth 1 -type d ! -name linux-x64 -exec rm -rf -- {} +
fi
required=(ScreenshotBox.Linux ScreenshotBox.Linux.dll ScreenshotBox.Linux.deps.json ScreenshotBox.Linux.runtimeconfig.json
  libcoreclr.so libhostfxr.so libhostpolicy.so libSkiaSharp.so libe_sqlite3.so libonnxruntime.so
  models/v5/ch_PP-OCRv5_mobile_det.onnx models/v5/ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx
  models/chinese/ch_PP-OCRv5_rec_mobile.onnx models/chinese/ppocrv5_dict.txt)
for relative in "${required[@]}"; do [[ -s "$stage/$relative" ]] || { echo "Missing publish dependency: $relative" >&2; exit 1; }; done
cp -a -- "$repo/licenses" "$repo/assets" "$repo/docs" "$stage/"
mkdir -p "$stage/installer/linux"
cp -- "$repo/installer/linux/install.sh" "$repo/installer/linux/uninstall.sh" "$stage/installer/linux/"
for relative in README.md README.en.md README.zh-CN.md CONTRIBUTING.md CONTRIBUTING.en.md CONTRIBUTING.zh-CN.md ROADMAP.md ROADMAP.en.md ROADMAP.zh-CN.md LICENSE; do
  [[ ! -f "$repo/$relative" ]] || cp -- "$repo/$relative" "$stage/"
done
for pair in 'ScreenshotBox.Linux linux' 'ScreenshotBox.Core core' 'ScreenshotBox.Ocr ocr'; do
  read -r project_name short_name <<< "$pair"
  cp -- "$repo/src/$project_name/packages.lock.json" "$stage/docs/$short_name-packages.lock.json"
done
cp -- "$repo/artifacts/native/linux-x64/sqlite-build-verification.json" "$stage/docs/linux-sqlite-build.json"
python3 - "$repo" "$stage" "$version" <<'PY'
import hashlib,json,pathlib,subprocess,sys,datetime
repo,stage,version=pathlib.Path(sys.argv[1]),pathlib.Path(sys.argv[2]),sys.argv[3]
models=json.loads((repo/'models/chinese/sources.json').read_text())
for source in models:
    file=stage/'models/chinese'/source['file']
    assert file.stat().st_size==source['bytes'] and hashlib.sha256(file.read_bytes()).hexdigest()==source['sha256'], source['file']
proof=json.loads((stage/'docs/linux-sqlite-build.json').read_text())
assert hashlib.sha256((stage/'libe_sqlite3.so').read_bytes()).hexdigest()==proof['librarySha256'],'SQLite binary differs from its build report'
commit=subprocess.check_output(['git','-C',str(repo),'rev-parse','HEAD'],text=True).strip()
dirty=bool(subprocess.check_output(['git','-C',str(repo),'status','--porcelain'],text=True).strip())
manifest={'version':version,'runtime':'linux-x64','selfContained':True,'status':'experimental X11 preview',
          'packagedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'sourceCommit':commit,'sourceDirty':dirty,
          'ocrModels':models,'sqlite':proof}
(stage/'release.json').write_text(json.dumps(manifest,indent=2)+'\n')
PY
find "$stage" -type d -exec chmod 755 {} +
find "$stage" -type f -exec chmod 644 {} +
[[ ! -f "$stage/createdump" ]] || chmod 755 "$stage/createdump"
chmod 755 "$stage/ScreenshotBox.Linux" "$stage/installer/linux/install.sh" "$stage/installer/linux/uninstall.sh"
(cd -- "$stage" && find . -type f ! -name FILE-SHA256SUMS.txt -print0 | LC_ALL=C sort -z | xargs -0 sha256sum | sed 's/  \.\//  /' > FILE-SHA256SUMS.txt)
(cd -- "$stage" && sha256sum --check --strict FILE-SHA256SUMS.txt >/dev/null)
mv -- "$stage" "$output"
tar -C "$repo/artifacts" -czf "$archive" "$name"
(cd -- "$repo/artifacts" && sha256sum "$name.tar.gz" > "$name.sha256")
printf 'Package directory: %s\nArchive: %s\n' "$output" "$archive"
