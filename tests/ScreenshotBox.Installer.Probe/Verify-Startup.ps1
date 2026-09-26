[CmdletBinding()]
param(
    [string]$CompilerPath = '',
    [string]$OutputDirectory = ''
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not $CompilerPath) { $CompilerPath = Join-Path $repo '.tools\inno\ISCC.exe' }
if (-not (Test-Path -LiteralPath $CompilerPath)) { throw 'Pass -CompilerPath with the path to Inno Setup ISCC.exe.' }
$token = [Guid]::NewGuid().ToString('N')
$testValue = 'ScreenshotBox-StartupTest-' + $token
$appGuid = [Guid]::NewGuid().ToString().ToUpperInvariant()
$testAppId = '{' + $appGuid + '}_is1'
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repo ('artifacts\installer-startup-test-' + $token) }
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
[void][IO.Directory]::CreateDirectory($fixture)
$source = 'public static class StartupFixture { public static int Main(string[] args) { return args.Length == 1 && args[0] == "--background" ? 0 : 42; } }'
Add-Type -TypeDefinition $source -OutputAssembly (Join-Path $fixture 'ScreenshotBox.exe') -OutputType WindowsApplication
$original = Get-Content -LiteralPath (Join-Path $repo 'installer\ScreenshotBox.iss') -Raw
Check 'Production installer does not write StartupApproved' (-not [regex]::IsMatch($original, '(?im)^Root:.*StartupApproved'))
Check 'Startup task is selected by default' ([regex]::IsMatch($original, '(?m)^Name: "autostart";[^\r\n]*$') -and -not [regex]::IsMatch($original, '(?m)^Name: "autostart";[^\r\n]*unchecked'))
$script = $original.Replace('AppId={{45420B2B-8CAE-4AAD-AD09-C0D675AE541A}', ('AppId={{' + $appGuid + '}'))
$script = $script.Replace('AppName=ScreenshotBox', ('AppName=ScreenshotBox Startup Test ' + $token))
$script = $script.Replace('DefaultGroupName=ScreenshotBox', ('DefaultGroupName=ScreenshotBox Startup Test ' + $token))
$script = $script.Replace('ValueName: "ScreenshotBox"', ('ValueName: "' + $testValue + '"'))
$script = $script.Replace('SetupIconFile=..\assets\screenshotbox.ico', ('SetupIconFile=' + (Join-Path $repo 'assets\screenshotbox.ico')))
$script = $script.Replace('OutputDir=..\artifacts', ('OutputDir=' + $root))
$script = $script.Replace('OutputBaseFilename=ScreenshotBox-{#AppVersion}-win-x64-setup', 'OutputBaseFilename=startup-fixture-setup')
$scriptPath = Join-Path $root 'fixture.iss'
[IO.File]::WriteAllText($scriptPath, $script, [Text.UTF8Encoding]::new($true))
Check 'Fixture has isolated AppId and Run name' ($script.Contains('AppId={{' + $appGuid + '}') -and -not $script.Contains('ValueName: "ScreenshotBox"'))
$compile = Run-Native $CompilerPath @('/Qp', '/DAppVersion=0.1.4', ('/DPublishDir=' + $fixture), $scriptPath) 'compile'
Check 'Isolated installer compiles' ($compile -eq 0)
$setup = Join-Path $root 'startup-fixture-setup.exe'
$install = Join-Path $root '安装位置 空格\ScreenshotBox'
$baseArguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/CURRENTUSER', '/SP-', '/LANG=english')
function Install([string]$Directory, [string[]]$Extra, [string]$Name) {
    return Run-Native $setup ($baseArguments + @(('/DIR=' + $Directory), ('/LOG=' + (Join-Path $root ($Name + '.log')))) + $Extra) $Name
}
function Command([string]$Directory) { return '"' + (Join-Path $Directory 'ScreenshotBox.exe') + '" --background' }
$installed = $false
$sentinelWritten = $false
try {
    Check 'Default installation succeeds' ((Install $install @() 'install-default') -eq 0)
    $installed = $true
    Check 'Default registration quotes custom Unicode path and includes background flag' ((Read-Value $runPath $testValue) -ceq (Command $install))
    Check 'Fixture accepts background argument from installed path' ((Run-Native (Join-Path $install 'ScreenshotBox.exe') @('--background') 'fixture-launch') -eq 0)
    # This is an opaque preservation sentinel, not an implementation of Windows' approval encoding.
    $sentinel = [byte[]](3,0,0,0,17,34,51,68,85,102,119,136)
    $key = $hkcu.CreateSubKey($approvedPath)
    try { $key.SetValue($testValue, $sentinel, [Microsoft.Win32.RegistryValueKind]::Binary) } finally { $key.Dispose() }
    $sentinelWritten = $true
    function Approval-Preserved { return [Convert]::ToBase64String((Read-Value $approvedPath $testValue)) -ceq [Convert]::ToBase64String($sentinel) }
    Check 'Upgrade keeps selected startup task' ((Install $install @() 'upgrade-default') -eq 0 -and (Read-Value $runPath $testValue) -ceq (Command $install))
    Check 'Upgrade leaves opaque Windows approval value unchanged' (Approval-Preserved)
    Check 'Unchecking startup on upgrade removes the Run value' ((Install $install @('/MERGETASKS=!autostart') 'upgrade-disable') -eq 0 -and $null -eq (Read-Value $runPath $testValue))
    Check 'Disabled selection persists on the next upgrade' ((Install $install @() 'upgrade-still-disabled') -eq 0 -and $null -eq (Read-Value $runPath $testValue))
    Check 'Task removal does not clear Windows approval state' (Approval-Preserved)
    Check 'Selecting startup again registers the same value name' ((Install $install @('/MERGETASKS=autostart') 'upgrade-enable') -eq 0 -and (Read-Value $runPath $testValue) -ceq (Command $install))
    Check 'Reselecting task leaves Windows approval state unchanged' (Approval-Preserved)
    $padding = 235 - $root.Length - 1
    if ($padding -lt 1 -or $padding -gt 255) { throw 'Use a shorter OutputDirectory to test the startup command length boundary.' }
    $longInstall = Join-Path $root ('L' * $padding)
    Check 'Boundary fixture exceeds 260-character command but has a supported executable path' ((Command $longInstall).Length -gt 260 -and (Join-Path $longInstall 'ScreenshotBox.exe').Length -lt 260)
    Check 'Long startup command rejects installation' ((Install $longInstall @('/MERGETASKS=autostart') 'long-path-rejected') -ne 0)
    Check 'Rejected update leaves current Run registration unchanged' ((Read-Value $runPath $testValue) -ceq (Command $install))
    Check 'Long-path rejection explains the startup command limit' ((Get-Content -LiteralPath (Join-Path $root 'long-path-rejected.log') -Raw).Contains('260'))
    Check 'Long directory is accepted when startup is unselected' ((Install $longInstall @('/MERGETASKS=!autostart') 'long-path-no-startup') -eq 0 -and $null -eq (Read-Value $runPath $testValue))
    $install = $longInstall
    Check 'Disabled long-path installation preserves Windows approval state' (Approval-Preserved)
    $shortInstall = Join-Path $root '安装位置 空格\ScreenshotBox'
    Check 'A later selected-task upgrade refreshes the registered path' ((Install $shortInstall @('/MERGETASKS=autostart') 'short-path-enabled') -eq 0 -and (Read-Value $runPath $testValue) -ceq (Command $shortInstall))
    $install = $shortInstall
    $uninstall = Run-Native (Join-Path $install 'unins000.exe') @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', ('/LOG=' + (Join-Path $root 'uninstall.log'))) 'uninstall'
    Check 'Uninstall removes only the test Run value' ($uninstall -eq 0 -and $null -eq (Read-Value $runPath $testValue))
    $installed = $false
    Check 'Uninstall preserves the Windows approval value' (Approval-Preserved)
    Check 'All preexisting Run values remain unchanged' (Snapshot-Matches $beforeRun (Snapshot $runPath))
    Check 'All preexisting approval values remain unchanged' (Snapshot-Matches $beforeApproved (Snapshot $approvedPath))
}
finally {
    if ($installed -and (Test-Path -LiteralPath (Join-Path $install 'unins000.exe'))) {
        [void](Run-Native (Join-Path $install 'unins000.exe') @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') 'cleanup-uninstall')
    }
    # Cleanup is restricted to the randomized values created by this script.
    foreach ($path in @($runPath, $approvedPath)) {
        $key = $hkcu.OpenSubKey($path, $true)
        try { if ($key) { $key.DeleteValue($testValue, $false) } } finally { if ($key) { $key.Dispose() } }
    }
    $unchanged = (Snapshot-Matches $beforeRun (Snapshot $runPath)) -and (Snapshot-Matches $beforeApproved (Snapshot $approvedPath))
    $result = [ordered]@{
        version = '0.1.4'; test = 'Isolated installer startup registration'; testValueName = $testValue;
        independentAppId = $true; originalProductionRunPresent = $productionRunPresent;
        checks = $checks; preexistingStartupValuesUnchanged = $unchanged;
        testedTaskManagerInterface = $false; testedRealSignIn = $false;
        approvalEncoding = 'Opaque sentinel only; Windows format not decoded or written by production installer.'
    }
    [IO.File]::WriteAllText((Join-Path $root 'result.json'), ($result | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
    $hkcu.Dispose()
}
Write-Output ('Startup installer checks passed: ' + $checks.Count + '; result: ' + (Join-Path $root 'result.json'))
