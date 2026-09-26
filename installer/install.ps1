[CmdletBinding()]
param(
    [string]$SourcePath = (Split-Path -Parent $PSScriptRoot),
    [string]$Destination = (Join-Path $env:LOCALAPPDATA 'Programs\ScreenshotBox'),
    [switch]$NoAutoStart
)
$ErrorActionPreference = 'Stop'
$source = (Resolve-Path $SourcePath).Path
$target = [IO.Path]::GetFullPath($Destination)
$installedExe = Join-Path $target 'ScreenshotBox.exe'
$startupCommand = '"' + $installedExe + '" --background'
if (-not $NoAutoStart -and $startupCommand.Length -gt 260) {
    throw 'The startup command exceeds the Windows limit of 260 characters. Choose a shorter Destination or pass -NoAutoStart.'
}
if ($source.TrimEnd('\') -eq $target.TrimEnd('\')) { throw 'Run the installer from the extracted release folder, not the installed folder.' }
if ($target.StartsWith($source.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'The installation directory cannot be inside the extracted release folder.' }
if (-not (Test-Path (Join-Path $source 'ScreenshotBox.exe'))) { throw 'Extract the full Windows release ZIP before installing.' }
if (Get-Process -Name 'ScreenshotBox' -ErrorAction SilentlyContinue) { throw 'Please quit ScreenshotBox from its tray menu before installing or upgrading.' }
if ((Test-Path $target) -and (Get-ChildItem $target -Force | Select-Object -First 1) -and -not (Test-Path (Join-Path $target '.installed-files.json'))) {
    throw 'Choose an empty installation folder, or the existing folder installed by this script.'
}
New-Item -ItemType Directory -Path $target -Force | Out-Null
$checksumPath = Join-Path $source 'FILE-SHA256SUMS.txt'
if (-not (Test-Path $checksumPath)) { throw 'The official release file manifest is missing.' }
$installedFiles = @(Get-Content $checksumPath | ForEach-Object {
    if ($_ -notmatch '^([0-9a-fA-F]{64})  (.+)$') { throw 'Invalid release manifest.' }
    $relative = $Matches[2].Replace('/', '\')
    $file = [IO.Path]::GetFullPath((Join-Path $source $relative))
    if (-not $file.StartsWith($source.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid release path.' }
    if ((Get-FileHash $file -Algorithm SHA256).Hash -ne $Matches[1]) { throw "Release checksum mismatch: $relative" }
    $relative
}) + @('FILE-SHA256SUMS.txt')
foreach ($relative in $installedFiles) {
    $destinationFile = Join-Path $target $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destinationFile) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $source $relative) -Destination $destinationFile -Force
}
$installedFiles | ConvertTo-Json | Set-Content (Join-Path $target '.installed-files.json') -Encoding UTF8
$shell = New-Object -ComObject WScript.Shell
$menu = Join-Path ([Environment]::GetFolderPath('Programs')) 'ScreenshotBox'
New-Item -ItemType Directory -Path $menu -Force | Out-Null
$shortcut = $shell.CreateShortcut((Join-Path $menu 'ScreenshotBox.lnk'))
$shortcut.TargetPath = Join-Path $target 'ScreenshotBox.exe'
$shortcut.WorkingDirectory = $target
$shortcut.Description = 'ScreenshotBox - local screenshot library'
$shortcut.Save()
$registry = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ScreenshotBox'
New-Item $registry -Force | Out-Null
$uninstallScript = Join-Path $target 'installer\uninstall.ps1'
$powershell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
New-ItemProperty $registry -Name DisplayName -Value 'ScreenshotBox' -PropertyType String -Force | Out-Null
New-ItemProperty $registry -Name Publisher -Value 'ScreenshotBox contributors' -PropertyType String -Force | Out-Null
New-ItemProperty $registry -Name InstallLocation -Value $target -PropertyType String -Force | Out-Null
New-ItemProperty $registry -Name DisplayIcon -Value (Join-Path $target 'ScreenshotBox.exe') -PropertyType String -Force | Out-Null
New-ItemProperty $registry -Name UninstallString -Value ('"' + $powershell + '" -NoProfile -ExecutionPolicy Bypass -File "' + $uninstallScript + '"') -PropertyType String -Force | Out-Null
New-ItemProperty $registry -Name NoModify -Value 1 -PropertyType DWord -Force | Out-Null
New-ItemProperty $registry -Name NoRepair -Value 1 -PropertyType DWord -Force | Out-Null
$release = Get-Content (Join-Path $target 'release.json') -Raw | ConvertFrom-Json
New-ItemProperty $registry -Name DisplayVersion -Value $release.version -PropertyType String -Force | Out-Null
# Windows manages its own StartupApproved state; only the stable Run entry is changed.
$startupRoot = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser, [Microsoft.Win32.RegistryView]::Registry64)
try {
    $startupKey = if ($NoAutoStart) { $startupRoot.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Run', $true) } else { $startupRoot.CreateSubKey('Software\Microsoft\Windows\CurrentVersion\Run') }
    try {
        if ($startupKey) {
            if ($NoAutoStart) {
                $current = $startupKey.GetValue('ScreenshotBox', $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
                $prefix = '"' + $installedExe + '"'
                if ($current -is [string] -and ($current.TrimStart().Equals($prefix, [StringComparison]::OrdinalIgnoreCase) -or $current.TrimStart().StartsWith($prefix + ' ', [StringComparison]::OrdinalIgnoreCase))) {
                    $startupKey.DeleteValue('ScreenshotBox', $false)
                }
            } else {
                $startupKey.SetValue('ScreenshotBox', $startupCommand, [Microsoft.Win32.RegistryValueKind]::String)
            }
        }
    } finally { if ($startupKey) { $startupKey.Dispose() } }
} finally { $startupRoot.Dispose() }
Write-Host "Installed for this user: $target"
if (-not $NoAutoStart) { Write-Host 'Starts in the tray at sign-in. Disable it from Windows Settings or Task Manager.' }
Write-Host 'Your screenshot library is separate from the application and is preserved during upgrades.'
