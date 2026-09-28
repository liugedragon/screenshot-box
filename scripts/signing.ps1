# Shared Windows release signing checks. Dot-source from the packaging scripts.
function Test-ReleaseSigningRequested {
    param(
        [string]$CertificateThumbprint,
        [string]$TimestampUrl,
        [bool]$RequireSignature
    )
    if (-not $CertificateThumbprint) {
        if ($RequireSignature) { throw '-RequireSignature requires -CertificateThumbprint.' }
        if ($TimestampUrl) { throw '-TimestampUrl requires -CertificateThumbprint.' }
        return $false
    }
    if ($CertificateThumbprint -notmatch '^[0-9a-fA-F]{40}$') {
        throw 'CertificateThumbprint must be exactly 40 hexadecimal characters (SHA-1 certificate thumbprint).'
    }
    if (-not $TimestampUrl) { throw 'Signing requires -TimestampUrl for an RFC 3161 time stamp.' }
    $uri = $null
    # This URL also appears in Inno's SignTool command. Keep it free of shell and Inno metacharacters.
    if (-not [Uri]::TryCreate($TimestampUrl, [UriKind]::Absolute, [ref]$uri) -or
        $uri.Scheme -notin @('http', 'https') -or $uri.UserInfo -or
        $TimestampUrl -cnotmatch '^https?://[A-Za-z0-9.-]+(?::[0-9]{1,5})?(?:/[A-Za-z0-9._~+/:=-]*)?$') {
        throw 'TimestampUrl must be a plain HTTP(S) host and path without credentials, queries, or command characters.'
    }
    return $true
}

function Get-ReleaseSigningCertificate {
    param([string]$CertificateThumbprint)
    $thumbprint = $CertificateThumbprint.ToUpperInvariant()
    foreach ($store in @('CurrentUser', 'LocalMachine')) {
        $path = "Cert:\$store\My\$thumbprint"
        if (-not (Test-Path -LiteralPath $path)) { continue }
        $certificate = Get-Item -LiteralPath $path
        if (-not $certificate.HasPrivateKey) { continue }
        if ($certificate.Subject -eq $certificate.Issuer) {
            throw 'A self-signed certificate cannot be used for a trusted release.'
        }
        $codeSigningOid = '1.3.6.1.5.5.7.3.3'
        $eku = $certificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.37' } | Select-Object -First 1
        if (-not $eku -or @($eku.EnhancedKeyUsages | Where-Object { $_.Value -eq $codeSigningOid }).Count -eq 0) {
            throw 'The selected certificate is not valid for code signing.'
        }
        $keyUsage = $certificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.15' } | Select-Object -First 1
        if ($keyUsage -and -not ($keyUsage.KeyUsages -band [Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature)) {
            throw 'The selected certificate does not permit digital signatures.'
        }
        $chain = New-Object System.Security.Cryptography.X509Certificates.X509Chain
        try {
            $chain.ChainPolicy.RevocationMode = [Security.Cryptography.X509Certificates.X509RevocationMode]::Online
            $chain.ChainPolicy.RevocationFlag = [Security.Cryptography.X509Certificates.X509RevocationFlag]::ExcludeRoot
            $chain.ChainPolicy.VerificationFlags = [Security.Cryptography.X509Certificates.X509VerificationFlags]::NoFlag
            [void]$chain.ChainPolicy.ApplicationPolicy.Add((New-Object System.Security.Cryptography.Oid -ArgumentList $codeSigningOid))
            if (-not $chain.Build($certificate) -or $chain.ChainElements.Count -lt 2) {
                $reason = ($chain.ChainStatus | ForEach-Object { $_.Status.ToString() }) -join ', '
                throw "The code-signing certificate does not have a valid CA chain: $reason"
            }
        }
        finally { $chain.Dispose() }
        return [pscustomobject]@{ Certificate = $certificate; Store = $store }
    }
    throw "No code-signing certificate with an accessible private key was found in CurrentUser or LocalMachine My: $thumbprint"
}

function Get-ReleaseSignTool {
    param([string]$SignToolPath)
    if ($SignToolPath) {
        $resolved = Resolve-Path -LiteralPath $SignToolPath -ErrorAction Stop
        return $resolved.Path
    }
    $command = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    foreach ($programFiles in @(${env:ProgramFiles(x86)}, $env:ProgramFiles)) {
        if (-not $programFiles) { continue }
        $root = Join-Path $programFiles 'Windows Kits\10\bin'
        if (-not (Test-Path -LiteralPath $root)) { continue }
        $candidate = Get-ChildItem -LiteralPath $root -Directory |
            Sort-Object Name -Descending |
            ForEach-Object { Join-Path $_.FullName 'x64\signtool.exe' } |
            Where-Object { Test-Path -LiteralPath $_ } |
            Select-Object -First 1
        if ($candidate) { return $candidate }
    }
    throw 'Windows SDK signtool.exe was not found. Pass -SignToolPath.'
}

function Assert-ReleaseSignature {
    param([string]$Path, [string]$CertificateThumbprint)
    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne 'Valid' -or -not $signature.SignerCertificate -or
        $signature.SignerCertificate.Thumbprint -ne $CertificateThumbprint -or
        -not $signature.TimeStamperCertificate) {
        throw "A valid, time-stamped Authenticode signature from the selected certificate is required: $Path ($($signature.Status))"
    }
}

function Invoke-ReleaseSignature {
    param(
        [string]$Path,
        [string]$CertificateThumbprint,
        [string]$TimestampUrl,
        [string]$SignToolPath,
        [string]$Store
    )
    $arguments = @('sign', '/sha1', $CertificateThumbprint, '/s', 'My')
    if ($Store -eq 'LocalMachine') { $arguments += '/sm' }
    $arguments += @('/fd', 'SHA256', '/tr', $TimestampUrl, '/td', 'SHA256', $Path)
    & $SignToolPath @arguments | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "SignTool failed with exit code $LASTEXITCODE`: $Path" }
    Assert-ReleaseSignature -Path $Path -CertificateThumbprint $CertificateThumbprint
}

function Get-InnoReleaseSignCommand {
    param(
        [string]$CertificateThumbprint,
        [string]$TimestampUrl,
        [string]$SignToolPath,
        [string]$Store
    )
    $machineStore = if ($Store -eq 'LocalMachine') { ' /sm' } else { '' }
    return ('$q{0}$q sign /sha1 {1} /s My{2} /fd SHA256 /tr {3} /td SHA256 $f' -f
        $SignToolPath, $CertificateThumbprint, $machineStore, $TimestampUrl)
}
