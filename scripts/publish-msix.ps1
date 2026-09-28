param(
    [string]$Version = "1.0.0",
    [string]$PfxPath = $env:MSIX_PFX_PATH,
    [string]$PfxPassword = $env:MSIX_PFX_PASSWORD
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "Get-AssemblyFileVersion.ps1")

$Root = Split-Path -Parent $PSScriptRoot
$Packaging = Join-Path $Root "packaging"
$DistRoot = Join-Path $Root "dist"
$MsixVersion = Get-AssemblyFileVersion $Version
$Stamp = "AltTabPlus-$Version-win-x64"
$PayloadExe = Join-Path $DistRoot "$Stamp\AltTabPlus.exe"
$Stage = Join-Path $DistRoot "$Stamp-msix-stage"
$MsixPath = Join-Path $DistRoot "$Stamp.msix"
$CerPath = Join-Path $DistRoot "$Stamp.cer"
$BundleDir = Join-Path $DistRoot "$Stamp-msix"
$BundleZip = Join-Path $DistRoot "$Stamp-msix.zip"
$HashPath = Join-Path $DistRoot "$Stamp-msix.sha256"

function Find-SdkTool([string]$Name, [string[]]$Roots) {
    foreach ($root in $Roots) {
        if (-not (Test-Path $root)) {
            continue
        }

        $found = Get-ChildItem $root -Recurse -Filter $Name -ErrorAction SilentlyContinue |
            Where-Object { $_.Directory.Name -eq "x64" } |
            Sort-Object { $_.Directory.Parent.Name } -Descending |
            Select-Object -First 1
        if ($found) {
            return $found.FullName
        }
    }

    return $null
}

function Get-SdkBuildToolsRoot {
    $tools = Join-Path $DistRoot "sdk-buildtools"
    $existing = Find-SdkTool "makeappx.exe" @($tools)
    if ($existing) {
        return (Split-Path (Split-Path $existing -Parent) -Parent)
    }

    Write-Host "Windows SDK not on PATH — downloading Microsoft.Windows.SDK.BuildTools"
    New-Item -ItemType Directory -Path $tools -Force | Out-Null
    $zip = Join-Path $tools "Microsoft.Windows.SDK.BuildTools.zip"
    Invoke-WebRequest `
        -Uri "https://www.nuget.org/api/v2/package/Microsoft.Windows.SDK.BuildTools/10.0.26100.4188" `
        -OutFile $zip
    $extract = Join-Path $tools "pkg"
    if (Test-Path $extract) {
        Remove-Item $extract -Recurse -Force
    }
    Expand-Archive -Path $zip -DestinationPath $extract -Force
    return $extract
}

function Get-SdkTool([string]$Name) {
    $cmd = Get-Command $Name -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }

    $found = Find-SdkTool $Name @(
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin",
        "${env:ProgramFiles}\Windows Kits\10\bin",
        (Join-Path $DistRoot "sdk-buildtools")
    )
    if ($found) {
        return $found
    }

    $found = Find-SdkTool $Name @(Get-SdkBuildToolsRoot)
    if ($found) {
        return $found
    }

    throw "Windows SDK tool not found: $Name. Install the Windows 10/11 SDK."
}

function Escape-Xml([string]$Value) {
    return ($Value -replace "&", "&amp;" -replace "<", "&lt;" -replace ">", "&gt;" -replace '"', "&quot;")
}

function New-MsixLogo {
    param(
        [string]$Path,
        [int]$Width,
        [int]$Height
    )

    $bmp = New-Object System.Drawing.Bitmap $Width, $Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::FromArgb(24, 24, 27))

    $scale = [Math]::Min($Width, $Height) / 32.0
    $ox = ($Width - (32 * $scale)) / 2
    $oy = ($Height - (32 * $scale)) / 2
    $tile = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(48, 48, 54))
    $accent = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(96, 205, 255))
    $g.FillRectangle($tile, [float]($ox + 4 * $scale), [float]($oy + 7 * $scale), [float](11 * $scale), [float](18 * $scale))
    $g.FillRectangle($accent, [float]($ox + 17 * $scale), [float]($oy + 7 * $scale), [float](11 * $scale), [float](8 * $scale))
    $g.FillRectangle($tile, [float]($ox + 17 * $scale), [float]($oy + 17 * $scale), [float](11 * $scale), [float](8 * $scale))
    $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $tile.Dispose()
    $accent.Dispose()
    $g.Dispose()
    $bmp.Dispose()
}

function Get-SigningCertificate {
    if (-not $PfxPath -and $env:MSIX_PFX_BASE64) {
        $PfxPath = Join-Path $env:TEMP "alttabplus-msix.pfx"
        [IO.File]::WriteAllBytes($PfxPath, [Convert]::FromBase64String($env:MSIX_PFX_BASE64))
    }

    if (-not $PfxPath) {
        $localPfx = Join-Path $Packaging "AltTabPlus.pfx"
        if (Test-Path $localPfx) {
            $PfxPath = $localPfx
        }
    }

    $flags = [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::Exportable -bor
        [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::PersistKeySet

    if ($PfxPath) {
        if (-not (Test-Path $PfxPath)) {
            throw "PFX not found: $PfxPath"
        }

        if ($PfxPassword) {
            $secure = ConvertTo-SecureString $PfxPassword -AsPlainText -Force
            return [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($PfxPath, $secure, $flags)
        }

        return [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($PfxPath, $null, $flags)
    }

    $existing = Get-ChildItem Cert:\CurrentUser\My -ErrorAction SilentlyContinue |
        Where-Object {
            $_.FriendlyName -eq "AltTabPlus MSIX" -and
            $_.HasPrivateKey -and
            $_.NotAfter -gt (Get-Date).AddDays(1)
        } |
        Select-Object -First 1
    if ($existing) {
        Write-Host "Signing with existing CurrentUser cert $($existing.Thumbprint)"
        return $existing
    }

    Write-Host "No PFX provided — creating a self-signed CN=AltTabPlus certificate"
    return New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject "CN=AltTabPlus" `
        -FriendlyName "AltTabPlus MSIX" `
        -KeyUsage DigitalSignature `
        -HashAlgorithm SHA256 `
        -CertStoreLocation Cert:\CurrentUser\My `
        -NotAfter (Get-Date).AddYears(5) `
        -KeyExportPolicy Exportable
}

function Sign-MsixPackage([string]$Path, [string]$SignTool, $Certificate) {
    $tempPfx = Join-Path $env:TEMP "alttabplus-sign.pfx"
    $tempPass = [guid]::NewGuid().ToString("N")
    $secure = ConvertTo-SecureString $tempPass -AsPlainText -Force
    Export-PfxCertificate -Cert $Certificate -FilePath $tempPfx -Password $secure | Out-Null

    try {
        & $SignTool sign /fd SHA256 /td SHA256 /f $tempPfx /p $tempPass /tr http://timestamp.digicert.com $Path
        if ($LASTEXITCODE -eq 0) {
            return
        }

        & $SignTool sign /fd SHA256 /f $tempPfx /p $tempPass $Path
        if ($LASTEXITCODE -ne 0) {
            throw "Could not sign $Path"
        }
    }
    finally {
        Remove-Item $tempPfx -Force -ErrorAction SilentlyContinue
    }
}

Add-Type -AssemblyName System.Drawing

Get-Process AltTabPlus -ErrorAction SilentlyContinue | Stop-Process -Force

if (-not (Test-Path $PayloadExe)) {
    Write-Host "Publishing payload $Stamp"
    & (Join-Path $PSScriptRoot "publish-release.ps1") -Version $Version -SkipZip
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

$cert = Get-SigningCertificate
Write-Host "MSIX publisher $($cert.Subject)"

if (Test-Path $Stage) {
    Remove-Item $Stage -Recurse -Force
}

$assets = Join-Path $Stage "Assets"
New-Item -ItemType Directory -Path $assets -Force | Out-Null
Copy-Item $PayloadExe (Join-Path $Stage "AltTabPlus.exe")

New-MsixLogo (Join-Path $assets "StoreLogo.png") 50 50
New-MsixLogo (Join-Path $assets "Square44x44Logo.png") 44 44
New-MsixLogo (Join-Path $assets "Square71x71Logo.png") 71 71
New-MsixLogo (Join-Path $assets "Square150x150Logo.png") 150 150
New-MsixLogo (Join-Path $assets "Wide310x150Logo.png") 310 150
New-MsixLogo (Join-Path $assets "SplashScreen.png") 620 300

$manifest = Get-Content (Join-Path $Packaging "AppxManifest.xml") -Raw
$manifest = $manifest.Replace("__PUBLISHER__", (Escape-Xml $cert.Subject))
$manifest = $manifest.Replace("__VERSION__", $MsixVersion)
$utf8 = New-Object System.Text.UTF8Encoding $false
[IO.File]::WriteAllText((Join-Path $Stage "AppxManifest.xml"), $manifest, $utf8)

$makeappx = Get-SdkTool "makeappx.exe"
$signtool = Join-Path (Split-Path $makeappx) "signtool.exe"
if (-not (Test-Path $signtool)) {
    throw "signtool.exe not next to $makeappx"
}

if (Test-Path $MsixPath) {
    Remove-Item $MsixPath -Force
}

Write-Host "Packing $MsixPath"
& $makeappx pack /d $Stage /p $MsixPath /o
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Sign-MsixPackage $MsixPath $signtool $cert
Export-Certificate -Cert $cert -FilePath $CerPath | Out-Null

if (Test-Path $BundleDir) {
    Remove-Item $BundleDir -Recurse -Force
}
New-Item -ItemType Directory -Path $BundleDir -Force | Out-Null
Copy-Item $MsixPath (Join-Path $BundleDir (Split-Path $MsixPath -Leaf))
Copy-Item $CerPath (Join-Path $BundleDir (Split-Path $CerPath -Leaf))
Copy-Item (Join-Path $Packaging "Install.ps1") (Join-Path $BundleDir "Install.ps1")

$readme = @"
AltTabPlus $Version (MSIX)
==========================

1. Right-click Install.ps1 → Run with PowerShell (administrator).
2. Windows imports the publisher certificate, then installs the package.
3. The tray icon starts after setup.

Sideloading of trusted apps is turned on for this machine if it was off.
To uninstall: Settings → Apps → AltTabPlus, or:
  Get-AppxPackage *AltTabPlus* | Remove-AppxPackage

Settings stay in %LOCALAPPDATA%\AltTabPlus\settings.json
"@
Set-Content -Path (Join-Path $BundleDir "README.txt") -Value $readme.Trim() -Encoding UTF8

if (Test-Path $BundleZip) {
    Remove-Item $BundleZip -Force
}
Compress-Archive -Path (Join-Path $BundleDir "*") -DestinationPath $BundleZip -CompressionLevel Optimal
$hash = (Get-FileHash -Algorithm SHA256 $BundleZip).Hash.ToLowerInvariant()
Set-Content -Path $HashPath -Value "$hash  $(Split-Path $BundleZip -Leaf)" -Encoding ASCII

Write-Host "MSIX $MsixPath"
Write-Host "CER  $CerPath"
Write-Host "ZIP  $BundleZip"
Write-Host "SHA  $hash"
Write-Host "OK   MSIX $MsixVersion ready"
