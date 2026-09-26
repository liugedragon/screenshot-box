[CmdletBinding()]
param(
    [string]$ExePath = '',
    [string]$OutputDirectory = '',
    [ValidateSet('en-US','zh-CN')][string[]]$Language = @('en-US','zh-CN')
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not $ExePath) { $ExePath = Join-Path $repo 'src\ScreenshotBox.App\bin\Release\net10.0-windows\win-x64\ScreenshotBox.exe' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repo ('artifacts\startup-test-' + [Guid]::NewGuid().ToString('N')) }
if (Test-Path $OutputDirectory) { throw 'Choose a new output directory to preserve previous results.' }
if (-not (Test-Path $ExePath)) { throw 'Build ScreenshotBox or provide -ExePath for an extracted release.' }
$settings = Join-Path $env:LOCALAPPDATA 'ScreenshotBox\settings.json'
$before = if (Test-Path $settings) { (Get-FileHash $settings -Algorithm SHA256).Hash } else { '' }
foreach ($locale in $Language) {
    $data = Join-Path $OutputDirectory $locale
    $arguments = @('--background','--startup-test','--startup-id',[Guid]::NewGuid().ToString('N'),'--language',$locale,'--data-dir',$data)
    $quoted = $arguments | ForEach-Object {
        $escaped = [regex]::Replace($_, '(\\*)"', '$1$1\"')
        $escaped = [regex]::Replace($escaped, '(\\+)$', '$1$1')
        '"' + $escaped + '"'
    }
    $start = New-Object System.Diagnostics.ProcessStartInfo
    $start.FileName = [IO.Path]::GetFullPath($ExePath)
    $start.Arguments = $quoted -join ' '
    $start.UseShellExecute = $false
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $start
    [void]$process.Start()
    if (-not $process.WaitForExit(30000)) {
        $process.Kill()
        throw 'The isolated startup test timed out.'
    }
    if ($process.ExitCode -ne 0) { throw "Startup test failed. See $data\self-test-error.txt" }
    $report = Get-Content (Join-Path $data 'startup-test.json') -Raw | ConvertFrom-Json
    $checks = @($report.PSObject.Properties | Where-Object { $_.Value -is [bool] })
    if (@($checks | Where-Object { -not $_.Value }).Count) { throw "Startup checks failed: $data" }
    Write-Host "$locale : $($checks.Count) checks passed. Report: $data\startup-test.json"
}
$after = if (Test-Path $settings) { (Get-FileHash $settings -Algorithm SHA256).Hash } else { '' }
if ($before -ne $after) { throw 'User settings changed during the test.' }
Write-Host 'User settings unchanged. Sign-in and Task Manager interaction are separate manual checks.'
