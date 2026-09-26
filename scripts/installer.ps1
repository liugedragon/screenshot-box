[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+([-.][A-Za-z0-9.-]+)?$')][string]$Version = '0.1.2',
    [string]$CompilerPath = ''
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not $CompilerPath) {
    $localCompiler = Join-Path $repo '.tools\inno\ISCC.exe'
    if (Test-Path $localCompiler) { $CompilerPath = $localCompiler }
    else { $CompilerPath = (Get-Command ISCC.exe -ErrorAction Stop).Source }
}
$publish = Join-Path $repo "artifacts\ScreenshotBox-$Version-win-x64"
if (-not (Test-Path (Join-Path $publish 'ScreenshotBox.exe'))) { throw 'Run package.ps1 successfully before building the installer.' }
$output = Join-Path $repo "artifacts\ScreenshotBox-$Version-win-x64-setup.exe"
if (Test-Path $output) { throw 'Installer output already exists. Move it or choose a new version.' }
$arguments = @('/Q', "/DAppVersion=$Version", "/DPublishDir=$publish", (Join-Path $repo 'installer\ScreenshotBox.iss'))
$quoted = $arguments | ForEach-Object {
    $escaped = [regex]::Replace($_, '(\\*)"', '$1$1\"')
    $escaped = [regex]::Replace($escaped, '(\\+)$', '$1$1')
    '"' + $escaped + '"'
}
$process = Start-Process -FilePath $CompilerPath -ArgumentList ($quoted -join ' ') -NoNewWindow -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "Inno Setup failed: $($process.ExitCode)" }
if (-not (Test-Path $output)) { throw 'Inno Setup did not produce the expected installer.' }
"$((Get-FileHash $output -Algorithm SHA256).Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($output))" |
    Set-Content ($output + '.sha256') -Encoding ASCII
Write-Host "Installer: $output"
