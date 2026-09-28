$ErrorActionPreference = "Stop"
$Here = Split-Path -Parent $MyInvocation.MyCommand.Path
$Fr = (Get-Culture).TwoLetterISOLanguageName -eq "fr"

function Text([string]$En, [string]$FrText) {
    if ($Fr) { $FrText } else { $En }
}

$principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Start-Process -FilePath (Get-Process -Id $PID).Path -Verb RunAs -ArgumentList @(
        "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$PSCommandPath`""
    )
    exit
}

$msix = Get-ChildItem $Here -File -Filter "*.msix" | Select-Object -First 1
$cer = Get-ChildItem $Here -File -Filter "*.cer" | Select-Object -First 1
if (-not $msix -or -not $cer) {
    throw (Text "AltTabPlus.msix and AltTabPlus.cer must sit next to Install.ps1." "AltTabPlus.msix et AltTabPlus.cer doivent être à côté de Install.ps1.")
}

Unblock-File -Path $msix.FullName, $cer.FullName, $PSCommandPath -ErrorAction SilentlyContinue

$unlock = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock"
if (-not (Test-Path $unlock)) {
    New-Item -Path $unlock -Force | Out-Null
}
New-ItemProperty -Path $unlock -Name AllowAllTrustedApps -Value 1 -PropertyType DWord -Force | Out-Null

Import-Certificate -FilePath $cer.FullName -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
Add-AppxPackage -Path $msix.FullName -ForceUpdateFromAnyVersion

$alias = Join-Path $env:LOCALAPPDATA "Microsoft\WindowsApps\AltTabPlus.exe"
if (Test-Path $alias) {
    Start-Process $alias
}

Write-Host (Text "AltTabPlus is installed. Look for the tray icon." "AltTabPlus est installé. Cherche l'icône dans la barre des tâches.")
if (-not $env:CI) {
    Read-Host (Text "Press Enter to close" "Entrée pour fermer")
}
