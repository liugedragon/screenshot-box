[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+([-.][A-Za-z0-9.-]+)?$')][string]$Version = '0.1.4',
    [string]$CompilerPath = '',
    [switch]$RequireSignature,
    [string]$CertificateThumbprint = '',
    [string]$TimestampUrl = '',
    [string]$SignToolPath = ''
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'signing.ps1')
$signRelease = Test-ReleaseSigningRequested -CertificateThumbprint $CertificateThumbprint -TimestampUrl $TimestampUrl -RequireSignature $RequireSignature.IsPresent
if ($SignToolPath -and -not $signRelease) { throw '-SignToolPath requires -CertificateThumbprint.' }
if ($signRelease) {
    $signing = Get-ReleaseSigningCertificate -CertificateThumbprint $CertificateThumbprint
    $SignToolPath = Get-ReleaseSignTool -SignToolPath $SignToolPath
    $CertificateThumbprint = $signing.Certificate.Thumbprint
}
$repo = Split-Path -Parent $PSScriptRoot
if (-not $CompilerPath) {
    $localCompiler = Join-Path $repo '.tools\inno\ISCC.exe'
    if (Test-Path $localCompiler) { $CompilerPath = $localCompiler }
    else { $CompilerPath = (Get-Command ISCC.exe -ErrorAction Stop).Source }
}
$publish = Join-Path $repo "artifacts\ScreenshotBox-$Version-win-x64"
$appExe = Join-Path $publish 'ScreenshotBox.exe'
if (-not (Test-Path $appExe)) { throw 'Run package.ps1 successfully before building the installer.' }
if ($signRelease) {
    Assert-ReleaseSignature -Path $appExe -CertificateThumbprint $CertificateThumbprint
    $manifest = Join-Path $publish 'FILE-SHA256SUMS.txt'
    $actual = (Get-FileHash -LiteralPath $appExe -Algorithm SHA256).Hash.ToLowerInvariant()
    $record = Get-Content -LiteralPath $manifest | Where-Object { $_ -match '^[0-9a-fA-F]{64}  ScreenshotBox\.exe$' }
    if (@($record).Count -ne 1 -or $record -ne "$actual  ScreenshotBox.exe") {
        throw 'The package checksum manifest does not match the signed ScreenshotBox.exe.'
    }
}
$output = Join-Path $repo "artifacts\ScreenshotBox-$Version-win-x64-setup.exe"
$hashPath = $output + '.sha256'
if ((Test-Path $output) -or (Test-Path $hashPath)) { throw 'Installer output already exists. Move it or choose a new version.' }
$signingOutputDir = if ($signRelease) { Join-Path $repo ('artifacts\.installer-' + [Guid]::NewGuid().ToString('N')) } else { '' }
$compiledOutput = if ($signRelease) { Join-Path $signingOutputDir ([IO.Path]::GetFileName($output)) } else { $output }
$arguments = @('/Q', "/DAppVersion=$Version", "/DPublishDir=$publish")
if ($signRelease) {
    $signCommand = Get-InnoReleaseSignCommand -CertificateThumbprint $CertificateThumbprint `
        -TimestampUrl $TimestampUrl -SignToolPath $SignToolPath -Store $signing.Store
    $arguments += @('/DSignedRelease=1', "--output-dir=$signingOutputDir", "--signtool=ScreenshotBoxRelease=$signCommand")
}
$arguments += (Join-Path $repo 'installer\ScreenshotBox.iss')
$quoted = $arguments | ForEach-Object {
    $escaped = [regex]::Replace($_, '(\\*)"', '$1$1\"')
    $escaped = [regex]::Replace($escaped, '(\\+)$', '$1$1')
    '"' + $escaped + '"'
}
$complete = $false
$promoted = $false
try {
    if ($signRelease) { New-Item -ItemType Directory -Path $signingOutputDir -Force | Out-Null }
    $process = Start-Process -FilePath $CompilerPath -ArgumentList ($quoted -join ' ') -NoNewWindow -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Inno Setup failed: $($process.ExitCode)" }
    if (-not (Test-Path $compiledOutput)) { throw 'Inno Setup did not produce the expected installer.' }
    if ($signRelease) {
        Assert-ReleaseSignature -Path $compiledOutput -CertificateThumbprint $CertificateThumbprint
        Move-Item -LiteralPath $compiledOutput -Destination $output
        $promoted = $true
    }
    "$((Get-FileHash $output -Algorithm SHA256).Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($output))" |
        Set-Content $hashPath -Encoding ASCII
    $complete = $true
    Write-Host "Installer: $output"
}
finally {
    # In a signing attempt, do not leave a failed or partially signed output looking publishable.
    if ($signRelease -and -not $complete) {
        Remove-Item -LiteralPath $hashPath -Force -ErrorAction SilentlyContinue
        if ($promoted) { Remove-Item -LiteralPath $output -Force -ErrorAction SilentlyContinue }
    }
    if ($signRelease -and (Test-Path -LiteralPath $signingOutputDir)) {
        Remove-Item -LiteralPath $signingOutputDir -Recurse -Force
    }
}
