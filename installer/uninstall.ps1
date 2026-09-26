[CmdletBinding()]
param([string]$InstallPath = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
$target = [IO.Path]::GetFullPath($InstallPath).TrimEnd('\')
if (Get-Process -Name 'ScreenshotBox' -ErrorAction SilentlyContinue) { throw 'Please quit ScreenshotBox from its tray menu before uninstalling.' }
$manifestPath = Join-Path $target '.installed-files.json'
if (-not (Test-Path $manifestPath -PathType Leaf)) { throw 'This folder was not installed by install.ps1. Use its registered uninstaller instead.' }
$registry = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ScreenshotBox'
if (-not (Test-Path $registry) -or (Get-ItemProperty $registry).InstallLocation.TrimEnd('\') -ne $target) {
    throw 'The registered installation directory does not match this uninstaller.'
}
$prefix = $target + '\'
$files = Get-Content $manifestPath -Raw | ConvertFrom-Json
foreach ($relative in $files) {
    $file = [IO.Path]::GetFullPath((Join-Path $target $relative))
    if (-not $file.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid installation manifest path.' }
}
# Do not remove a Run value now owned by a different installation.
$installedExe = Join-Path $target 'ScreenshotBox.exe'
$startupRoot = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser, [Microsoft.Win32.RegistryView]::Registry64)
try {
    $startupKey = $startupRoot.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Run', $true)
    try {
        if ($startupKey) {
            $current = $startupKey.GetValue('ScreenshotBox', $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
            $prefix = '"' + $installedExe + '"'
            if ($current -is [string] -and ($current.TrimStart().Equals($prefix, [StringComparison]::OrdinalIgnoreCase) -or $current.TrimStart().StartsWith($prefix + ' ', [StringComparison]::OrdinalIgnoreCase))) {
                $startupKey.DeleteValue('ScreenshotBox', $false)
            }
        }
    } finally { if ($startupKey) { $startupKey.Dispose() } }
} finally { $startupRoot.Dispose() }
$menu = Join-Path ([Environment]::GetFolderPath('Programs')) 'ScreenshotBox'
if (Test-Path $menu) { Remove-Item -LiteralPath $menu -Recurse -Force }
if (Test-Path $registry) { Remove-Item $registry -Recurse -Force }
foreach ($relative in $files) {
    $file = Join-Path $target $relative
    if (Test-Path $file -PathType Leaf) { Remove-Item -LiteralPath $file -Force }
}
Remove-Item -LiteralPath $manifestPath -Force
Get-ChildItem $target -Directory -Recurse | Sort-Object { $_.FullName.Length } -Descending | ForEach-Object {
    if (-not (Get-ChildItem $_.FullName -Force | Select-Object -First 1)) { Remove-Item -LiteralPath $_.FullName -Force }
}
if (-not (Get-ChildItem $target -Force | Select-Object -First 1)) { Remove-Item -LiteralPath $target -Force }
Write-Host 'ScreenshotBox has been removed. Data files not owned by the installer have been preserved.'
