[CmdletBinding()]
param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$localSdk = Join-Path $repo '.tools\dotnet\dotnet.exe'
$dotnet = if (Test-Path $localSdk) { $localSdk } else { (Get-Command dotnet -ErrorAction Stop).Source }
$app = Join-Path $repo 'src\ScreenshotBox.App\ScreenshotBox.App.csproj'
$tests = Join-Path $repo 'tests\ScreenshotBox.Core.Tests\ScreenshotBox.Core.Tests.csproj'
function Invoke-Dotnet([string[]]$Arguments) {
    # App.csproj pins win-x64; do not force that RID onto the neutral Core project.
    # Start-Process also waits correctly when invoked through Windows/WSL interop.
    $quoted = $Arguments | ForEach-Object {
        $escaped = [regex]::Replace($_, '(\\*)"', '$1$1\"')
        $escaped = [regex]::Replace($escaped, '(\\+)$', '$1$1')
        '"' + $escaped + '"'
    }
    $process = Start-Process -FilePath $dotnet -ArgumentList ($quoted -join ' ') -NoNewWindow -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "dotnet command failed ($($process.ExitCode)): $($Arguments -join ' ')" }
}
Push-Location $repo
try {
    Invoke-Dotnet -Arguments @('restore', $app, '--locked-mode')
    Invoke-Dotnet -Arguments @('restore', $tests, '--locked-mode')
    Invoke-Dotnet -Arguments @('build', $app, '--configuration', $Configuration, '--no-restore')
    Invoke-Dotnet -Arguments @('test', $tests, '--configuration', $Configuration, '--no-restore', '--verbosity', 'minimal')
}
finally { Pop-Location }
