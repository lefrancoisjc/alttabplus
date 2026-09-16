param(
    [string]$Version = "1.0.0",
    [ValidateSet("self-contained", "framework-dependent")]
    [string]$Mode = "self-contained",
    [switch]$SkipZip
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "Get-AssemblyFileVersion.ps1")
$Root = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $Root "src\AltTabPlus.csproj"
$DistRoot = Join-Path $Root "dist"
$Stamp = "AltTabPlus-$Version-win-x64"
if ($Mode -eq "framework-dependent") {
    $Stamp += "-fd"
}

$OutDir = Join-Path $DistRoot $Stamp
$ZipPath = Join-Path $DistRoot "$Stamp.zip"
$HashPath = Join-Path $DistRoot "$Stamp.sha256"

Get-Process AltTabPlus -ErrorAction SilentlyContinue | Stop-Process -Force

if (Test-Path $OutDir) {
    Remove-Item $OutDir -Recurse -Force
}

New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

$selfContained = $Mode -eq "self-contained"
Write-Host "Publishing $Stamp ($Mode)"

dotnet publish $Project `
    -c Release `
    -r win-x64 `
    --self-contained $selfContained `
    -p:Version=$Version `
    -p:AssemblyVersion="$(Get-AssemblyFileVersion $Version)" `
    -p:FileVersion="$(Get-AssemblyFileVersion $Version)" `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=$selfContained `
    -p:PublishReadyToRun=true `
    -p:DebugType=embedded `
    -p:DebugSymbols=true `
    -o $OutDir

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Get-ChildItem $OutDir -File | Where-Object { $_.Extension -in ".pdb", ".xml" } | Remove-Item -Force

$readme = @"
AltTabPlus $Version
===================

Run AltTabPlus.exe (64-bit Windows 10/11).
Settings are in the tray icon and in
%LOCALAPPDATA%\AltTabPlus\settings.json

The .NET 8 runtime is included in this build.
"@
if (-not $selfContained) {
    $readme = $readme -replace "The .NET 8 runtime is included in this build.", "Requires the .NET 8 Desktop runtime (x64): https://dotnet.microsoft.com/download/dotnet/8.0"
}

Set-Content -Path (Join-Path $OutDir "README.txt") -Value $readme.Trim() -Encoding UTF8
$license = Join-Path $Root "LICENSE"
if (Test-Path $license) {
    Copy-Item $license (Join-Path $OutDir "LICENSE.txt")
}

if (-not $SkipZip) {
    if (Test-Path $ZipPath) {
        Remove-Item $ZipPath -Force
    }

    Compress-Archive -Path (Join-Path $OutDir "*") -DestinationPath $ZipPath -CompressionLevel Optimal
    $hash = (Get-FileHash -Algorithm SHA256 $ZipPath).Hash.ToLowerInvariant()
    Set-Content -Path $HashPath -Value "$hash  $(Split-Path $ZipPath -Leaf)" -Encoding ASCII
    Write-Host "ZIP  $ZipPath"
    Write-Host "SHA  $hash"
}

$exe = Join-Path $OutDir "AltTabPlus.exe"
Write-Host "EXE  $exe"
Write-Host "OK   release $Version ready"
