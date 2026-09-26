[CmdletBinding()]
param(
    [string]$OutputDirectory = ''
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$token = [Guid]::NewGuid().ToString('N')
$testValue = 'ScreenshotBox-ScriptStartupTest-' + $token
$appGuid = [Guid]::NewGuid().ToString().ToUpperInvariant()
$testAppId = $testValue
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repo ('artifacts\script-startup-test-' + $token) }
$root = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $root) { throw 'Choose a new empty output directory.' }
$runPath = 'Software\Microsoft\Windows\CurrentVersion\Run'
$approvedPath = 'Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run'
$uninstallPath = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\' + $testAppId
$view = [Microsoft.Win32.RegistryView]::Registry64
$hkcu = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser, $view)
function Read-Value([string]$Path, [string]$Name) {
    $key = $hkcu.OpenSubKey($Path)
    try { if ($key -and ($key.GetValueNames() -contains $Name)) { return ,$key.GetValue($Name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames) } }
    finally { if ($key) { $key.Dispose() } }
    return $null
}
function Snapshot([string]$Path) {
    $values = @{}
    $key = $hkcu.OpenSubKey($Path)
    try {
        if ($key) {
            foreach ($name in $key.GetValueNames()) {
                if ($name -eq $testValue) { continue }
                $value = $key.GetValue($name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
                $encoded = if ($value -is [byte[]]) { [Convert]::ToBase64String($value) } else { ConvertTo-Json -InputObject $value -Compress }
                $values[$name] = $key.GetValueKind($name).ToString() + ':' + $encoded
            }
        }
    } finally { if ($key) { $key.Dispose() } }
    return $values
}
function Snapshot-Matches($Expected, $Actual) {
    if ($Expected.Count -ne $Actual.Count) { return $false }
    foreach ($name in $Expected.Keys) { if (-not $Actual.ContainsKey($name) -or $Expected[$name] -cne $Actual[$name]) { return $false } }
    return $true
}
# Only randomized values and AppIds are written. Never restore snapshots over existing user values.
foreach ($path in @($runPath, $approvedPath)) {
    if ($null -ne (Read-Value $path $testValue)) { throw 'The randomized test value already exists; no changes made.' }
}
$uninstallKey = $hkcu.OpenSubKey($uninstallPath)
if ($uninstallKey) { $uninstallKey.Dispose(); throw 'The randomized test installation already exists; no changes made.' }
$beforeRun = Snapshot $runPath
$beforeApproved = Snapshot $approvedPath
$productionRunPresent = $null -ne (Read-Value $runPath 'ScreenshotBox')
[void][IO.Directory]::CreateDirectory($root)
$checks = [Collections.Generic.List[object]]::new()
function Check([string]$Name, [bool]$Passed) {
    $checks.Add([pscustomobject]@{ name = $Name; passed = $Passed })
    if (-not $Passed) { throw ('Check failed: ' + $Name) }
}
function Run-Native([string]$Path, [string[]]$Arguments, [string]$LogName) {
    $quoted = $Arguments | ForEach-Object {
        $escaped = [regex]::Replace($_, '(\\*)"', '$1$1\"')
        $escaped = [regex]::Replace($escaped, '(\\+)$', '$1$1')
        '"' + $escaped + '"'
    }
    $info = New-Object Diagnostics.ProcessStartInfo
    $info.FileName = $Path; $info.Arguments = $quoted -join ' '; $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    $info.StandardOutputEncoding = [Text.Encoding]::UTF8; $info.StandardErrorEncoding = [Text.Encoding]::UTF8
    $process = New-Object Diagnostics.Process
    $process.StartInfo = $info
    [void]$process.Start()
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit(30000)) {
        # Only terminate the exact child started by this invocation, never a process-name match.
        $ownedId = $process.Id
        if (-not $process.HasExited) { $process.Kill(); [void]$process.WaitForExit(5000) }
        throw ('Owned test process timed out and was terminated; PID ' + $ownedId)
    }
    [IO.File]::WriteAllText((Join-Path $root ($LogName + '.out.txt')), $stdoutTask.Result)
    [IO.File]::WriteAllText((Join-Path $root ($LogName + '.err.txt')), $stderrTask.Result)
    return $process.ExitCode
}
$fixture = Join-Path $root 'fixture'
[void][IO.Directory]::CreateDirectory((Join-Path $fixture 'installer'))
$source = 'public static class ScriptStartupFixture { public static int Main(string[] args) { return 0; } }'
Add-Type -TypeDefinition $source -OutputAssembly (Join-Path $fixture 'ScreenshotBox.exe') -OutputType WindowsApplication
foreach ($fileName in @('install.ps1', 'uninstall.ps1')) {
    $original = Get-Content -LiteralPath (Join-Path $repo ('installer\' + $fileName)) -Raw
    Check ($fileName + ' does not write StartupApproved') (-not [regex]::IsMatch($original, '(?im)^[^#\r\n]*["'']Software\\.*StartupApproved'))
    # Isolate only registry identity, Start menu group, default path and process guard.
    $clone = $original.Replace("'ScreenshotBox'", ("'" + $testValue + "'"))
    $clone = $clone.Replace('Uninstall\ScreenshotBox', ('Uninstall\' + $testValue))
    $clone = $clone.Replace('Programs\ScreenshotBox', ('Programs\' + $testValue))
    [IO.File]::WriteAllText((Join-Path $fixture ('installer\' + $fileName)), $clone, [Text.UTF8Encoding]::new($true))
}
[IO.File]::WriteAllText((Join-Path $fixture 'release.json'), '{"version":"0.1.4"}', [Text.UTF8Encoding]::new($false))
$manifest = @('ScreenshotBox.exe', 'release.json', 'installer/install.ps1', 'installer/uninstall.ps1') | ForEach-Object {
    (Get-FileHash -LiteralPath (Join-Path $fixture $_) -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_
}
[IO.File]::WriteAllLines((Join-Path $fixture 'FILE-SHA256SUMS.txt'), $manifest, [Text.UTF8Encoding]::new($false))
$powershell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
function Install([string]$Directory, [bool]$DisableStartup, [string]$Name) {
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $fixture 'installer\install.ps1'), '-SourcePath', $fixture, '-Destination', $Directory)
    if ($DisableStartup) { $arguments += '-NoAutoStart' }
    return Run-Native $powershell $arguments $Name
}
function Uninstall([string]$Directory, [string]$Name) {
    return Run-Native $powershell @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $fixture 'installer\uninstall.ps1'), '-InstallPath', $Directory) $Name
}
function Command([string]$Directory) { return '"' + (Join-Path $Directory 'ScreenshotBox.exe') + '" --background' }
function Set-TestRun([string]$Value) {
    $key = $hkcu.CreateSubKey($runPath)
    try { $key.SetValue($testValue, $Value, [Microsoft.Win32.RegistryValueKind]::String) } finally { $key.Dispose() }
}
$sentinel = [byte[]](3,0,0,0,17,34,51,68,85,102,119,136)
function Approval-Preserved { return [Convert]::ToBase64String((Read-Value $approvedPath $testValue)) -ceq [Convert]::ToBase64String($sentinel) }
$install = Join-Path $root '脚本安装 空格\ScreenshotBox'
$installedDirectories = [Collections.Generic.List[string]]::new()
try {
    $key = $hkcu.CreateSubKey($approvedPath)
    try { $key.SetValue($testValue, $sentinel, [Microsoft.Win32.RegistryValueKind]::Binary) } finally { $key.Dispose() }
    Check 'Default script installation succeeds' ((Install $install $false 'default-install') -eq 0)
    $installedDirectories.Add($install)
    Check 'Default script installation registers quoted Unicode path and background argument' ((Read-Value $runPath $testValue) -ceq (Command $install))
    Check 'Default installation preserves Windows approval value' (Approval-Preserved)
    Check 'NoAutoStart update succeeds and removes its own Run value' ((Install $install $true 'disable-update') -eq 0 -and $null -eq (Read-Value $runPath $testValue))
    $otherInstall = Join-Path $root 'another installation'
    Set-TestRun (Command $otherInstall)
    Check 'NoAutoStart preserves a Run value owned by another installation' ((Install $install $true 'disable-other-owner') -eq 0 -and (Read-Value $runPath $testValue) -ceq (Command $otherInstall))
    Check 'Uninstall preserves a Run value owned by another installation' ((Uninstall $install 'uninstall-other-owner') -eq 0 -and (Read-Value $runPath $testValue) -ceq (Command $otherInstall))
    $installedDirectories.Remove($install) | Out-Null
    Check 'Uninstall preserves opaque Windows approval state' (Approval-Preserved)
    Check 'Reinstallation registers the same stable Run name' ((Install $install $false 'reinstall') -eq 0 -and (Read-Value $runPath $testValue) -ceq (Command $install))
    $installedDirectories.Add($install)
    [IO.File]::WriteAllText((Join-Path $install 'user-created.txt'), 'User file that is not in the release manifest.')
    Check 'Uninstall removes the Run value that matches its own executable' ((Uninstall $install 'uninstall-own-registration') -eq 0 -and $null -eq (Read-Value $runPath $testValue))
    $installedDirectories.Remove($install) | Out-Null
    Check 'Uninstall preserves files outside the installed manifest' (Test-Path -LiteralPath (Join-Path $install 'user-created.txt'))
    $padding = 235 - $root.Length - 1
    if ($padding -lt 1 -or $padding -gt 255) { throw 'Use a shorter OutputDirectory to test the startup command length boundary.' }
    $longInstall = Join-Path $root ('L' * $padding)
    Check 'Boundary fixture exceeds command limit without exceeding executable path limit' ((Command $longInstall).Length -gt 260 -and (Join-Path $longInstall 'ScreenshotBox.exe').Length -lt 260)
    Check 'Long startup command is rejected before writing installation files' ((Install $longInstall $false 'long-path-rejected') -ne 0 -and -not (Test-Path -LiteralPath $longInstall))
    Check 'Script rejection states the length limit and NoAutoStart option' ((Get-Content -LiteralPath (Join-Path $root 'long-path-rejected.err.txt') -Raw).Contains('260') -and (Get-Content -LiteralPath (Join-Path $root 'long-path-rejected.err.txt') -Raw).Contains('NoAutoStart'))
    Check 'Long path installation works with NoAutoStart' ((Install $longInstall $true 'long-path-no-startup') -eq 0 -and $null -eq (Read-Value $runPath $testValue))
    $installedDirectories.Add($longInstall)
    Check 'Long path uninstalls successfully' ((Uninstall $longInstall 'long-path-uninstall') -eq 0)
    $installedDirectories.Remove($longInstall) | Out-Null
    Check 'All script operations preserve Windows approval value' (Approval-Preserved)
    Check 'All preexisting Run values remain unchanged' (Snapshot-Matches $beforeRun (Snapshot $runPath))
    Check 'All preexisting approval values remain unchanged' (Snapshot-Matches $beforeApproved (Snapshot $approvedPath))
}
finally {
    foreach ($directory in $installedDirectories) {
        if (Test-Path -LiteralPath (Join-Path $directory '.installed-files.json')) { [void](Uninstall $directory 'cleanup-uninstall') }
    }
    foreach ($path in @($runPath, $approvedPath)) {
        $key = $hkcu.OpenSubKey($path, $true)
        try { if ($key) { $key.DeleteValue($testValue, $false) } } finally { if ($key) { $key.Dispose() } }
    }
    $unchanged = (Snapshot-Matches $beforeRun (Snapshot $runPath)) -and (Snapshot-Matches $beforeApproved (Snapshot $approvedPath))
    $result = [ordered]@{
        version = '0.1.4'; test = 'Isolated script startup registration'; testValueName = $testValue;
        independentAppId = $true; originalProductionRunPresent = $productionRunPresent;
        checks = $checks; preexistingStartupValuesUnchanged = $unchanged;
        testedTaskManagerInterface = $false; testedRealSignIn = $false;
        approvalEncoding = 'Opaque sentinel only; Windows format not decoded or written by production scripts.'
    }
    [IO.File]::WriteAllText((Join-Path $root 'result.json'), ($result | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
    $hkcu.Dispose()
}
Write-Output ('Startup script checks passed: ' + $checks.Count + '; result: ' + (Join-Path $root 'result.json'))
