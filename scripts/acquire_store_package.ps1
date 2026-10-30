param(
    [Parameter(Mandatory = $true)]
    [string]$ProductId,
    [Parameter(Mandatory = $true)]
    [string]$ExpectedIdentity,
    [Parameter(Mandatory = $true)]
    [string]$ExpectedVersion,
    [Parameter(Mandatory = $true)]
    [string]$OutputPath,
    [Parameter(Mandatory = $true)]
    [string]$EvidencePath,
    [string]$ExpectedPublisher = "CN=CBF59877-21AD-4BC4-8F91-FE8DA520A138"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Test-MicrosoftDeliveryUri {
    param([Parameter(Mandatory = $true)][Uri]$Uri)

    return $Uri.Scheme -in @("http", "https") -and
        $Uri.Host -match '(^|\.)delivery\.mp\.microsoft\.com$'
}

if ($ProductId -notmatch '^(?:[A-Z0-9]{12}|XP[A-Z0-9]{12})$') {
    throw "Invalid Microsoft Store Product ID '$ProductId'."
}
if ($ExpectedVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    throw "Expected Store version must be four numeric components; got '$ExpectedVersion'."
}

$root = Split-Path -Parent $PSScriptRoot
$vendorScript = Join-Path $root "third_party\microsoft-store-package-downloader\scripts\download-store-package.ps1"
if (-not (Test-Path -LiteralPath $vendorScript)) {
    throw "Vendored Microsoft Store acquisition helper is missing: $vendorScript"
}

$destination = [IO.Path]::GetFullPath($OutputPath)
$evidenceDestination = [IO.Path]::GetFullPath($EvidencePath)
if ([IO.Path]::GetExtension($destination) -cne ".msix") {
    throw "Store acquisition output must use the canonical .msix extension."
}
if ([IO.Path]::GetExtension($evidenceDestination) -cne ".json") {
    throw "Store acquisition evidence must use the canonical .json extension."
}
if (Test-Path -LiteralPath $destination) {
    throw "Store acquisition output already exists; refusing to reuse stale bytes: $destination"
}
[IO.Directory]::CreateDirectory((Split-Path -Parent $evidenceDestination)) | Out-Null
if (Test-Path -LiteralPath $evidenceDestination) {
    throw "Store acquisition evidence already exists; refusing to reuse stale evidence: $evidenceDestination"
}
[IO.Directory]::CreateDirectory((Split-Path -Parent $destination)) | Out-Null

$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ("tapstrum-store-acquire-" + [guid]::NewGuid().ToString("N"))
[IO.Directory]::CreateDirectory($temporaryRoot) | Out-Null
try {
    try {
        $vendorOutput = & $vendorScript `
            -ProductId $ProductId `
            -Architecture "x64" `
            -OutputDirectory $temporaryRoot 2>&1
    } catch {
        $details = ($_ | Format-List * -Force | Out-String).Trim()
        throw "Microsoft Store endpoint acquisition failed.`n$details"
    }

    $manifestPath = Join-Path $temporaryRoot "package-manifest.json"
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        throw "Microsoft Store acquisition did not produce package-manifest.json."
    }
    $manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
    if ([string]$manifest.ProductId -cne $ProductId) {
        throw "Store manifest ProductId '$($manifest.ProductId)' does not match '$ProductId'."
    }
    if ([string]$manifest.PackageIdentityName -cne $ExpectedIdentity) {
        throw "Store manifest identity '$($manifest.PackageIdentityName)' does not match '$ExpectedIdentity'."
    }

    $escapedIdentity = [regex]::Escape($ExpectedIdentity)
    $escapedVersion = [regex]::Escape($ExpectedVersion)
    $expectedNamePattern = "^${escapedIdentity}_${escapedVersion}_(?<arch>x64|neutral)__(?<publisherId>[a-z0-9]+)\.msix$"
    $packageMatches = @(
        $manifest.Packages |
            Where-Object { [string]$_.FileName -match $expectedNamePattern }
    )
    if ($packageMatches.Count -ne 1) {
        throw "Expected exactly one '$ExpectedIdentity' Store MSIX for version '$ExpectedVersion', found $($packageMatches.Count)."
    }

    $selected = $packageMatches[0]
    $sourceUri = [Uri][string]$selected.SourceUrl
    if (-not (Test-MicrosoftDeliveryUri -Uri $sourceUri)) {
        throw "Store package source escaped the Microsoft delivery boundary: $sourceUri"
    }

    $sourcePath = Join-Path $temporaryRoot ([string]$selected.FileName)
    if (-not (Test-Path -LiteralPath $sourcePath)) {
        throw "Store manifest selected a package that is missing on disk: $sourcePath"
    }

    $signature = Get-AuthenticodeSignature -LiteralPath $sourcePath
    if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
        throw "Store acquisition signature status is '$($signature.Status)'."
    }
    if (-not $signature.SignerCertificate -or $signature.SignerCertificate.Subject -cne $ExpectedPublisher) {
        throw "Store acquisition signer does not match expected Publisher '$ExpectedPublisher'."
    }
    if ($signature.SignerCertificate.Issuer -notmatch '^CN=Microsoft Marketplace CA ') {
        throw "Store acquisition signer issuer '$($signature.SignerCertificate.Issuer)' is not a Microsoft Marketplace CA."
    }

    $sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $sourcePath).Hash
    $expectedSha256 = if ($selected.PSObject.Properties["Sha256"]) { [string]$selected.Sha256 } else { "" }
    if ($expectedSha256 -and $sha256 -cne $expectedSha256.ToUpperInvariant()) {
        throw "Acquired Store MSIX SHA-256 '$sha256' does not match manifest digest '$expectedSha256'."
    }

    $sha1 = (Get-FileHash -Algorithm SHA1 -LiteralPath $sourcePath).Hash # NOSONAR: Store acquisition metadata comparison
    $expectedSha1 = if ($selected.PSObject.Properties["Sha1"]) { [string]$selected.Sha1 } else { "" }
    if ($expectedSha1 -and $sha1 -cne $expectedSha1.ToUpperInvariant()) {
        throw "Acquired Store MSIX SHA-1 '$sha1' does not match manifest digest '$expectedSha1'."
    }

    Copy-Item -LiteralPath $sourcePath -Destination $destination -Force
    $evidence = [pscustomobject]@{
        ProductId = $ProductId
        Identity = $ExpectedIdentity
        Version = $ExpectedVersion
        Publisher = $ExpectedPublisher
        SourceFileName = [string]$selected.FileName
        SourceUrl = [string]$selected.SourceUrl
        SignerIssuer = $signature.SignerCertificate.Issuer
        SHA1 = $sha1
        SHA256 = $sha256
    }
    $evidence | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $evidenceDestination -Encoding UTF8
    $evidence
} finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}
