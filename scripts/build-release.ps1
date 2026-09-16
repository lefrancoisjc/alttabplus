param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "Get-AssemblyFileVersion.ps1")
$Root = Split-Path -Parent $PSScriptRoot
$Sln = Join-Path $Root "AltTabPlus.sln"

Write-Host "Building Release x64 $Version"
$FileVersion = Get-AssemblyFileVersion $Version
dotnet build $Sln -c Release -p:Platform=x64 -p:Version=$Version -p:AssemblyVersion=$FileVersion -p:FileVersion=$FileVersion
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "OK  $(Join-Path $Root 'src\bin\x64\Release\net8.0-windows\AltTabPlus.exe')"
