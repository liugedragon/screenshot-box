[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+([-.][A-Za-z0-9.-]+)?$')][string]$Version = '0.1.3',
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$localSdk = Join-Path $repo '.tools\dotnet\dotnet.exe'
$dotnet = if (Test-Path $localSdk) { $localSdk } else { (Get-Command dotnet -ErrorAction Stop).Source }
$artifacts = Join-Path $repo 'artifacts'
$name = "ScreenshotBox-$Version-win-x64"
$destination = Join-Path $artifacts $name
$zipPath = Join-Path $artifacts "$name.zip"
$stage = Join-Path $artifacts ('.package-' + [Guid]::NewGuid().ToString('N'))
$app = Join-Path $repo 'src\ScreenshotBox.App\ScreenshotBox.App.csproj'
function Invoke-Dotnet([string[]]$Arguments) {
    # App.csproj pins win-x64; do not force that RID onto the neutral Core project.
    # Quote native Windows arguments, including paths with spaces and trailing backslashes.
    $quoted = $Arguments | ForEach-Object {
        $escaped = [regex]::Replace($_, '(\\*)"', '$1$1\"')
        $escaped = [regex]::Replace($escaped, '(\\+)$', '$1$1')
        '"' + $escaped + '"'
    }
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $dotnet
    $startInfo.Arguments = $quoted -join ' '
    $startInfo.UseShellExecute = $false
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    # Wait only for dotnet, not its persistent compiler-server descendants.
    [void]$process.Start()
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "dotnet command failed ($($process.ExitCode)): $($Arguments -join ' ')" }
}
if ((Test-Path $destination) -or (Test-Path $zipPath)) { throw 'Version output already exists. Choose a new version or move the previous output first.' }
Push-Location $repo
try {
    if (-not $SkipBuild) { & (Join-Path $PSScriptRoot 'build.ps1') -Configuration Release }
    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    Invoke-Dotnet -Arguments @('restore', $app, '--locked-mode')
    Invoke-Dotnet -Arguments @('publish', $app, '--configuration', 'Release',
        '--self-contained', 'true', '--no-restore', '--output', $stage,
        '-p:PublishSingleFile=false', '-p:PublishTrimmed=false', '-p:PublishReadyToRun=false',
        '-p:UseAppHost=true', "-p:Version=$Version")

    # ONNX Runtime imports these Microsoft CRT binaries; .NET's runtime does not supply them.
    $crtRoot = Join-Path $repo 'native\vc-runtime-x64'
    $crtSources = Get-Content (Join-Path $crtRoot 'sources.json') -Raw | ConvertFrom-Json
    foreach ($source in $crtSources.files) {
        $file = Join-Path $crtRoot $source.file
        if ((Get-FileHash $file -Algorithm SHA256).Hash.ToLowerInvariant() -ne $source.sha256) { throw "VC runtime checksum mismatch: $($source.file)" }
        $signature = Get-AuthenticodeSignature $file
        if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Microsoft Corporation') { throw "VC runtime signature invalid: $($source.file)" }
        Copy-Item $file $stage
    }

    # RapidOcrNet copies its defaults AFTER Publish. Only these unused Latin files are removed.
    foreach ($relative in @('models\v5\latin_PP-OCRv5_rec_mobile_infer.onnx', 'models\v5\ppocrv5_latin_dict.txt')) {
        $file = Join-Path $stage $relative
        if (Test-Path $file) { Remove-Item -LiteralPath $file }
    }
    $runtimeRoot = Join-Path $stage 'runtimes'
    if (Test-Path $runtimeRoot) {
        Get-ChildItem $runtimeRoot -Directory | Where-Object { $_.Name -ne 'win-x64' } | Remove-Item -Recurse -Force
    }
    Get-ChildItem $stage -Recurse -File | Where-Object { $_.Extension -in @('.so', '.dylib') } | Remove-Item -Force

    $required = @('ScreenshotBox.exe', 'ScreenshotBox.dll', 'ScreenshotBox.deps.json', 'ScreenshotBox.runtimeconfig.json',
        'coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll', 'PresentationFramework.dll', 'PresentationNative_cor3.dll',
        'RapidOcrNet.dll', 'Wpf.Ui.dll', 'Microsoft.Data.Sqlite.dll', 'onnxruntime.dll', 'onnxruntime_providers_shared.dll', 'libSkiaSharp.dll', 'e_sqlite3.dll',
        'msvcp140.dll', 'msvcp140_1.dll', 'vcruntime140.dll', 'vcruntime140_1.dll',
        'models\v5\ch_PP-OCRv5_mobile_det.onnx', 'models\v5\ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx',
        'models\chinese\ch_PP-OCRv5_rec_mobile.onnx', 'models\chinese\ppocrv5_dict.txt')
    foreach ($relative in $required) {
        $file = Join-Path $stage $relative
        if (-not (Test-Path $file -PathType Leaf) -or (Get-Item $file).Length -eq 0) { throw "Required publish dependency is missing: $relative" }
    }
    $sources = Get-Content (Join-Path $repo 'models\chinese\sources.json') -Raw | ConvertFrom-Json
    foreach ($source in $sources) {
        $file = Join-Path $stage ('models\chinese\' + $source.file)
        $actual = (Get-FileHash $file -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -ne $source.sha256 -or (Get-Item $file).Length -ne $source.bytes) { throw "OCR model checksum mismatch: $($source.file)" }
    }
    Copy-Item (Join-Path $repo 'licenses') $stage -Recurse
    Copy-Item (Join-Path $repo 'installer') $stage -Recurse
    foreach ($relative in @('README.md', 'README.zh-CN.md', 'README.en.md', 'CONTRIBUTING.md', 'CONTRIBUTING.zh-CN.md', 'CONTRIBUTING.en.md', 'ROADMAP.md', 'ROADMAP.zh-CN.md', 'ROADMAP.en.md', 'LICENSE')) {
        $source = Join-Path $repo $relative
        if (Test-Path $source) { Copy-Item $source $stage }
    }
    Copy-Item (Join-Path $repo 'assets') $stage -Recurse
    New-Item -ItemType Directory -Path (Join-Path $stage 'docs') -Force | Out-Null
    $docsRoot = Join-Path $repo 'docs'
    Get-ChildItem $docsRoot -Recurse -File | Where-Object { $_.Extension -in @('.md', '.png', '.txt', '.json') } | ForEach-Object {
        $relative = $_.FullName.Substring($docsRoot.Length + 1)
        $destinationFile = Join-Path (Join-Path $stage 'docs') $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $destinationFile) -Force | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $destinationFile
    }
    Copy-Item (Join-Path $repo 'src\ScreenshotBox.App\packages.lock.json') (Join-Path $stage 'docs\app-packages.lock.json')
    Copy-Item (Join-Path $repo 'src\ScreenshotBox.Core\packages.lock.json') (Join-Path $stage 'docs\core-packages.lock.json')
    Copy-Item (Join-Path $crtRoot 'sources.json') (Join-Path $stage 'docs\vc-runtime-sources.json')

    $manifest = @{
        version = $Version; runtime = 'win-x64'; selfContained = $true
        packagedUtc = [DateTime]::UtcNow.ToString('O'); ocrModels = $sources; vcRuntime = $crtSources
    }
    $manifest | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $stage 'release.json') -Encoding UTF8
    $fileHashes = Get-ChildItem $stage -Recurse -File | Sort-Object FullName | ForEach-Object {
        $relative = $_.FullName.Substring($stage.Length + 1).Replace('\', '/')
        "$((Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant())  $relative"
    }
    $fileHashes | Set-Content (Join-Path $stage 'FILE-SHA256SUMS.txt') -Encoding ASCII
    Move-Item $stage $destination
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($destination, $zipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)
    "$((Get-FileHash $zipPath -Algorithm SHA256).Hash.ToLowerInvariant())  $name.zip" |
        Set-Content (Join-Path $artifacts "$name.sha256") -Encoding ASCII
    Write-Host "Package: $zipPath"
    Write-Host 'Packaging succeeded. Real offline Windows smoke testing is still required before publishing.'
}
finally {
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
    Pop-Location
}
