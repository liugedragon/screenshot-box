[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$root = Join-Path $repo 'models\chinese'
$sources = Get-Content (Join-Path $root 'sources.json') -Raw | ConvertFrom-Json
New-Item -ItemType Directory -Path $root -Force | Out-Null
foreach ($source in $sources) {
    $path = Join-Path $root $source.file
    if ((Test-Path $path) -and (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant() -eq $source.sha256) { continue }
    $temp = $path + '.download'
    try {
        Invoke-WebRequest $source.url -OutFile $temp -UseBasicParsing
        if ((Get-FileHash $temp -Algorithm SHA256).Hash.ToLowerInvariant() -ne $source.sha256 -or (Get-Item $temp).Length -ne $source.bytes) { throw "Model verification failed: $($source.file)" }
        Move-Item $temp $path -Force
    } finally { if (Test-Path $temp) { Remove-Item $temp -Force } }
}
Write-Host 'Chinese model and matching dictionary verified.'
