param(
    [string]$Password,
    [string]$OutDir
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
if (-not $OutDir) {
    $OutDir = Join-Path $Root "packaging"
}

New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
$pfx = Join-Path $OutDir "AltTabPlus.pfx"
$cer = Join-Path $OutDir "AltTabPlus.cer"

$cert = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject "CN=AltTabPlus" `
    -FriendlyName "AltTabPlus MSIX" `
    -KeyUsage DigitalSignature `
    -HashAlgorithm SHA256 `
    -CertStoreLocation Cert:\CurrentUser\My `
    -NotAfter (Get-Date).AddYears(5) `
    -KeyExportPolicy Exportable

if (-not $Password) {
    $Password = [guid]::NewGuid().ToString("N")
}

$secure = ConvertTo-SecureString $Password -AsPlainText -Force
Export-PfxCertificate -Cert $cert -FilePath $pfx -Password $secure | Out-Null
Export-Certificate -Cert $cert -FilePath $cer | Out-Null

$b64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($pfx))
Write-Host "PFX  $pfx"
Write-Host "CER  $cer"
Write-Host "PASS $Password"
Write-Host ""
Write-Host "Add these GitHub Actions secrets so every release keeps the same identity:"
Write-Host "  MSIX_PFX_BASE64  (the Base64 of the PFX)"
Write-Host "  MSIX_PFX_PASSWORD"
Write-Host ""
Write-Host "MSIX_PFX_BASE64="
Write-Host $b64
