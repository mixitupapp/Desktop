$script:LocationPushed = $false

function Fail {
    if ($script:LocationPushed) {
        Pop-Location | Out-Null
        $script:LocationPushed = $false
    }
    Write-Host ""
    Write-Host "Release build or packaging failed."
    exit 1
}

function Update-Csproj {
    param(
        [string]$FilePath,
        [string]$NewVersion
    )

    if (-not (Test-Path -LiteralPath $FilePath)) {
        Write-Host "Warning: File not found \"$FilePath\""
        return $false
    }

    $tempFile = "$FilePath.tmp"
    if (Test-Path -LiteralPath $tempFile) {
        Remove-Item -LiteralPath $tempFile -Force -ErrorAction SilentlyContinue
    }

    $reader = New-Object System.IO.StreamReader($FilePath, [System.Text.Encoding]::Default, $true)
    $null = $reader.Peek()
    $encoding = $reader.CurrentEncoding
    $writer = New-Object System.IO.StreamWriter($tempFile, $false, $encoding)
    $writer.NewLine = "`r`n"

    try {
        while (($line = $reader.ReadLine()) -ne $null) {
            if ($line -eq $null) {
                $writer.WriteLine()
                continue
            }

            if ($line.Contains("<AssemblyVersion>")) {
                $writer.WriteLine("    <AssemblyVersion>$NewVersion</AssemblyVersion>")
            } elseif ($line.Contains("<FileVersion>")) {
                $writer.WriteLine("    <FileVersion>$NewVersion</FileVersion>")
            } else {
                $writer.WriteLine($line)
            }
        }
    } finally {
        $writer.Close()
        $reader.Close()
    }

    Move-Item -LiteralPath $tempFile -Destination $FilePath -Force
    Write-Host "Updated $FilePath"
    return $true
}

function Update-AssemblyInfo {
    param(
        [string]$FilePath,
        [string]$NewVersion
    )

    if (-not (Test-Path -LiteralPath $FilePath)) {
        Write-Host "Warning: File not found \"$FilePath\""
        return $false
    }

    $tempFile = "$FilePath.tmp"
    if (Test-Path -LiteralPath $tempFile) {
        Remove-Item -LiteralPath $tempFile -Force -ErrorAction SilentlyContinue
    }

    $reader = New-Object System.IO.StreamReader($FilePath, [System.Text.Encoding]::Default, $true)
    $null = $reader.Peek()
    $encoding = $reader.CurrentEncoding
    $writer = New-Object System.IO.StreamWriter($tempFile, $false, $encoding)
    $writer.NewLine = "`r`n"

    try {
        while (($line = $reader.ReadLine()) -ne $null) {
            if ($line -eq $null) {
                $writer.WriteLine()
                continue
            }

            $trim = $line.TrimStart()
            if ($trim.StartsWith("//")) {
                $writer.WriteLine($line)
                continue
            }

            if ($line.Contains("AssemblyVersion(")) {
                $writer.WriteLine('[assembly: AssemblyVersion("' + $NewVersion + '")]')
            } elseif ($line.Contains("AssemblyFileVersion(")) {
                $writer.WriteLine('[assembly: AssemblyFileVersion("' + $NewVersion + '")]')
            } else {
                $writer.WriteLine($line)
            }
        }
    } finally {
        $writer.Close()
        $reader.Close()
    }

    Move-Item -LiteralPath $tempFile -Destination $FilePath -Force
    Write-Host "Updated $FilePath"
    return $true
}

function Update-BuildExpirationHelper {
    param(
        [string]$FilePath,
        [bool]$Enabled,
        [DateTime]$ExpirationDate
    )

    if (-not (Test-Path -LiteralPath $FilePath)) {
        Write-Host "Warning: BuildExpirationHelper not found at \"$FilePath\""
        return $false
    }

    $year  = $ExpirationDate.Year
    $month = $ExpirationDate.Month
    $day   = $ExpirationDate.Day
    $enabledStr = if ($Enabled) { "true" } else { "false" }

    $tempFile = "$FilePath.tmp"
    if (Test-Path -LiteralPath $tempFile) {
        Remove-Item -LiteralPath $tempFile -Force -ErrorAction SilentlyContinue
    }

    $reader = New-Object System.IO.StreamReader($FilePath, [System.Text.Encoding]::Default, $true)
    $null = $reader.Peek()
    $encoding = $reader.CurrentEncoding
    $writer = New-Object System.IO.StreamWriter($tempFile, $false, $encoding)
    $writer.NewLine = "`r`n"

    try {
        while (($line = $reader.ReadLine()) -ne $null) {
            if ($line.Contains("private static readonly bool ENABLED")) {
                $writer.WriteLine("        private static readonly bool ENABLED = $enabledStr;  // Set to true to enable build expiration checking")
            } elseif ($line.Contains("private static readonly DateTime EXPIRATION_DATE")) {
                $writer.WriteLine("        private static readonly DateTime EXPIRATION_DATE = new DateTime($year, $month, $day); // (year, month, day) - Set expiration date here")
            } else {
                $writer.WriteLine($line)
            }
        }
    } finally {
        $writer.Close()
        $reader.Close()
    }

    Move-Item -LiteralPath $tempFile -Destination $FilePath -Force
    Write-Host "Updated $FilePath (ENABLED=$enabledStr, expires $($ExpirationDate.ToString('yyyy-MM-dd')))"
    return $true
}

function Prompt-YesNo {
    param(
        [string]$Prompt,
        [bool]$DefaultYes
    )

    while ($true) {
        $input = Read-Host $Prompt
        $input = $input -replace '"', ''
        if ($input -eq "") {
            return $DefaultYes
        }
        if ($input -match '^[Yy]$') {
            return $true
        }
        if ($input -match '^[Nn]$') {
            return $false
        }
    }
}

try {
    & tar --version 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "tar.exe is not available. This script requires Windows 10/11 with tar, or a compatible tar.exe in PATH."
        Fail
    }
} catch {
    Write-Host "tar.exe is not available. This script requires Windows 10/11 with tar, or a compatible tar.exe in PATH."
    Fail
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $scriptDir | Out-Null
$script:LocationPushed = $true

Push-Location (Join-Path $scriptDir "..") | Out-Null
$desktopDir = (Get-Location).Path
Pop-Location | Out-Null

Push-Location (Join-Path $desktopDir "..") | Out-Null
$repoRoot = (Get-Location).Path
Pop-Location | Out-Null

$publishingDir = Join-Path $repoRoot "Publishing"
if (-not (Test-Path -LiteralPath $publishingDir)) {
    New-Item -ItemType Directory -Path $publishingDir -ErrorAction Stop | Out-Null
}

$publishOutputDir = Join-Path $publishingDir "Published"

$eulaSource = Join-Path $repoRoot "Docs\Legal\Desktop\Embedded\End-User-License-Agreement.md"
if (-not (Test-Path -LiteralPath $eulaSource)) {
    Write-Host "EULA file not found at \"$eulaSource\"."
    Fail
}

$artifactRoot = Join-Path $repoRoot "FileService\artifacts"
if (-not (Test-Path -LiteralPath $artifactRoot)) {
    Write-Host "Artifact root not found at \"$artifactRoot\"."
    Fail
}

try {
    $now = Get-Date
    $releasedAt = $now.ToString("yyyy-MM-dd'T'HH:mm:ss") + ".000Z"
} catch {
    Write-Host "Failed to determine release date from system locale."
    Fail
}

$productKey = $null
while (-not $productKey) {
    $productInput = Read-Host "Select product to build: (W) MixItUp.WPF, (I) MixItUp.Installer: "
    $productInput = $productInput -replace '"', ''
    if ($productInput -match '^[Ww]$') {
        $productKey = "desktop"
    } elseif ($productInput -match '^[Ii]$') {
        $productKey = "installer"
    }
}

if ($productKey -eq "desktop") {
    $projectPath = Join-Path $desktopDir "MixItUp.WPF\MixItUp.WPF.csproj"
    $productName = "MixItUp.WPF"
    $productSlug = "mixitup-desktop"
    $productTitle = "Mix It Up Desktop"
    $outputDir = Join-Path $desktopDir "MixItUp.WPF\bin\Release"
} else {
    $projectPath = Join-Path $desktopDir "MixItUp.Installer\MixItUp.Installer.csproj"
    $productName = "MixItUp.Installer"
    $productSlug = "mixitup-desktop-installer"
    $productTitle = "Mix It Up Installer"
    $outputDir = Join-Path $desktopDir "MixItUp.Installer\bin\Release"
}

$releaseChannel = $null
$buildChannel = $null
while (-not $releaseChannel) {
    $channelInput = Read-Host "Select release channel: (P) Public, (R) Preview, (T) Test"
    $channelInput = $channelInput -replace '"', ''
    if ($channelInput -match '^[Pp]$') {
        $releaseChannel = "public"
        $buildChannel = "PUBLIC_BUILD"
        $vsConfig = "Public"
    } elseif ($channelInput -match '^[Rr]$') {
        $releaseChannel = "preview"
        $buildChannel = "PREVIEW_BUILD"
        $vsConfig = "Preview"
    } elseif ($channelInput -match '^[Tt]$') {
        $releaseChannel = "test"
        $buildChannel = "TEST_BUILD"
        $vsConfig = "Test"
    }
}

$expirationDays = $null
if ($releaseChannel -eq "test") {
    while ($null -eq $expirationDays) {
        $daysInput = Read-Host "Enter expiration window in days (1-7)"
        $daysInput = $daysInput -replace '"', ''
        if ($daysInput -match '^\d+$') {
            $daysInt = [int]$daysInput
            if ($daysInt -ge 1 -and $daysInt -le 7) {
                $expirationDays = $daysInt
            } else {
                Write-Host "Expiration days must be between 1 and 7."
            }
        } else {
            Write-Host "Please enter a valid number."
        }
    }
}

$releaseVersion = $null
while (-not $releaseVersion) {
    $releaseVersion = Read-Host "Enter release version (e.g. 1.4.0): "
    $releaseVersion = $releaseVersion -replace '"', ''
    if ($releaseVersion -eq "") {
        Write-Host "Release version is required."
        $releaseVersion = $null
    }
}

$assemblyVersion = "$releaseVersion.0"

$artifactDir = Join-Path $artifactRoot "$productSlug\windows-x64\$releaseChannel\$releaseVersion"
$cleanArtifactDir = $false
if (Test-Path -LiteralPath $artifactDir) {
    Write-Host "Target artifact directory already exists: \"$artifactDir\"."
    if (Prompt-YesNo "Delete and recreate this directory? (y/N): " $false) {
        Write-Host "Will remove existing artifact directory later..."
        $cleanArtifactDir = $true
    } else {
        Write-Host "User declined to replace the existing artifact directory."
        Fail
    }
}

$installerVersion = $null
$installerUrl = $null
if ($productKey -eq "desktop" -and $releaseChannel -ne "test") {
    while (-not $installerVersion) {
        $installerVersion = Read-Host "Enter installer version to reference in manifest (e.g. 0.5.0): "
        $installerVersion = $installerVersion -replace '"', ''
        if ($installerVersion -eq "") {
            Write-Host "Installer version is required when packaging the desktop build."
            $installerVersion = $null
        }
    }
    $installerUrl = "https://files.mixitupapp.com/apps/mixitup-desktop-installer/windows-x64/public/$installerVersion/MixItUp-Setup.exe"

    if (-not (Prompt-YesNo "Has the CHANGELOG.md been updated for the Desktop app? (Y/n): " $true)) {
        Write-Host "Please update the CHANGELOG.md before proceeding with the release."
        Fail
    }
}

Write-Host "Updating Assembly Versions to $assemblyVersion..."

if ($productKey -eq "desktop") {
    Update-Csproj (Join-Path $desktopDir "APIs\MixItUp.API\MixItUp.API.csproj") $assemblyVersion | Out-Null
    Update-Csproj (Join-Path $desktopDir "MixItUp.Base\MixItUp.Base.csproj") $assemblyVersion | Out-Null
    Update-Csproj (Join-Path $desktopDir "MixItUp.SignalR.Client\MixItUp.SignalR.Client.csproj") $assemblyVersion | Out-Null
    Update-Csproj (Join-Path $desktopDir "MixItUp.WPF\MixItUp.WPF.csproj") $assemblyVersion | Out-Null

    Update-AssemblyInfo (Join-Path $desktopDir "MixItUp.Reporter\Properties\AssemblyInfo.cs") $assemblyVersion | Out-Null
    Update-AssemblyInfo (Join-Path $desktopDir "MixItUp.Uninstaller\Properties\AssemblyInfo.cs") $assemblyVersion | Out-Null
    Update-AssemblyInfo (Join-Path $desktopDir "MixItUp.WPF\Properties\AssemblyInfo.cs") $assemblyVersion | Out-Null

    $expirationHelperPath = Join-Path $desktopDir "MixItUp.WPF\Util\BuildExpirationHelper.cs"
    if ($releaseChannel -eq "test") {
        $expirationDate = (Get-Date).AddDays($expirationDays)
        Update-BuildExpirationHelper $expirationHelperPath $true $expirationDate | Out-Null
    } else {
        Update-BuildExpirationHelper $expirationHelperPath $false (Get-Date) | Out-Null
    }
} else {
    Update-Csproj (Join-Path $desktopDir "MixItUp.Installer\MixItUp.Installer.csproj") $assemblyVersion | Out-Null
}

if ($cleanArtifactDir) {
    Remove-Item -LiteralPath $artifactDir -Recurse -Force -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $artifactDir) {
        Write-Host "Failed to remove existing artifact directory."
        Fail
    }
}

try {
    New-Item -ItemType Directory -Path $artifactDir -Force -ErrorAction Stop | Out-Null
} catch {
    Fail
}

if ($releaseChannel -eq "test") {
    $doSign = $false
    Write-Host "Test builds are not signed."
} else {
    $doSign = Prompt-YesNo "Would you like to sign the build? (Y/n): " $true
}

Write-Host ""
Write-Host "==============================================================================="
Write-Host "Step 1: Building and Signing Release"
Write-Host "==============================================================================="

$signThumb = "A838AD3D9C00B4806F2FC4270269EA6060D021DC"
$signTool = Join-Path $scriptDir "SignTool\signtool.exe"
if ($doSign) {
    if (-not (Test-Path -LiteralPath $signTool)) {
        Write-Host "signtool.exe not found at: $signTool"
        Fail
    }
}

Write-Host "Cleaning Solution..."
& dotnet clean (Join-Path $desktopDir "mixer-mixitup.sln") -c $vsConfig
if ($LASTEXITCODE -ne 0) { Fail }

$installerExe = Join-Path $desktopDir "MixItUp.Installer\bin\Release\net48\MixItUp-Setup.exe"

if ($productKey -eq "desktop") {
    Write-Host "Publishing WPF Application..."
    if (Test-Path -LiteralPath $publishOutputDir) {
        Remove-Item -LiteralPath $publishOutputDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    New-Item -ItemType Directory -Path $publishOutputDir -Force | Out-Null
    & dotnet publish (Join-Path $desktopDir "MixItUp.WPF\MixItUp.WPF.csproj") -c $vsConfig -r win-x64 --self-contained -o $publishOutputDir -p:BuildChannel=$buildChannel
    if ($LASTEXITCODE -ne 0) { Fail }
} else {
    Write-Host "Building Installer..."
    & dotnet build (Join-Path $desktopDir "MixItUp.Installer\MixItUp.Installer.csproj") -c Release
    if ($LASTEXITCODE -ne 0) { Fail }

    if (-not (Test-Path -LiteralPath $installerExe)) {
        Write-Host "Installer executable not found at \"$installerExe\" after build."
        Fail
    }
}

if ($doSign) {
    Write-Host "Signing Binaries..."
    $filesToSign = @()
    if ($productKey -eq "desktop") {
        if (Test-Path -LiteralPath $installerExe) {
            $filesToSign += $installerExe
        }
        Push-Location $publishOutputDir | Out-Null
        $foundFiles = Get-ChildItem -Recurse -File -Include "MixItUp*.exe","MixItUp*.dll"
        Pop-Location | Out-Null
        if ($foundFiles) {
            $filesToSign += $foundFiles.FullName
        }
    } else {
        $filesToSign += $installerExe
    }

    if ($filesToSign.Count -eq 0) {
        Write-Host "No files found to sign."
        Fail
    }

    Write-Host "Signing $($filesToSign.Count) file(s)..."
    & $signTool sign /fd sha256 /sha1 $signThumb /tr http://ts.ssl.com /td sha256 /v @filesToSign
    if ($LASTEXITCODE -ne 0) { Fail }

    foreach ($file in $filesToSign) {
        & $signTool verify /pa $file
        if ($LASTEXITCODE -ne 0) { Fail }
    }
}

if ($productKey -eq "desktop") {
    Write-Host "Creating MixItUp.zip..."
    $zipPath = Join-Path $publishingDir "MixItUp.zip"
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
    }
    Push-Location $publishOutputDir | Out-Null
    & tar -a -c -f $zipPath *
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Failed to create archive."
        Pop-Location | Out-Null
        Fail
    }
    Pop-Location | Out-Null
}

Write-Host "Copying Installer..."
if (Test-Path -LiteralPath $installerExe) {
    try {
        Copy-Item -LiteralPath $installerExe -Destination (Join-Path $publishingDir "MixItUp-Setup.exe") -Force -ErrorAction Stop
    } catch {
        Fail
    }
} else {
    if ($productKey -eq "installer") {
        Write-Host "Installer executable not found at \"$installerExe\"."
        Fail
    } else {
        Write-Host "Installer executable not found at \"$installerExe\". Skipping copy."
    }
}

Write-Host ""
Write-Host "==============================================================================="
Write-Host "Step 2: Packaging Artifacts..."
Write-Host "==============================================================================="

$publishingDir = Join-Path $repoRoot "Publishing"
if (-not (Test-Path -LiteralPath $publishingDir)) {
    $fallbackPublishing = Join-Path $scriptDir "..\..\..\Publishing"
    if (Test-Path -LiteralPath $fallbackPublishing) {
        $publishingDir = (Resolve-Path -LiteralPath $fallbackPublishing).Path
    }
}

if (-not (Test-Path -LiteralPath $publishingDir)) {
    Write-Host "Publishing directory not found at \"$publishingDir\"."
    Write-Host "Please run BuildAndSignRelease.ps1 first."
    Fail
}

Write-Host "Packaging build output..."
if ($productKey -eq "desktop") {
    $packageFilename = "MixItUp-Desktop_$releaseVersion.zip"
    $packageSourceFile = Join-Path $publishingDir "MixItUp.zip"
    if (-not (Test-Path -LiteralPath $packageSourceFile)) {
        Write-Host "Pre-built package not found at \"$packageSourceFile\"."
        Fail
    }
    try {
        Copy-Item -LiteralPath $packageSourceFile -Destination (Join-Path $artifactDir $packageFilename) -Force -ErrorAction Stop
    } catch {
        Fail
    }
    $packagePath = Join-Path $artifactDir $packageFilename
} else {
    $packageFilename = "MixItUp-Setup.exe"
    $packageSourceFile = Join-Path $publishingDir "MixItUp-Setup.exe"
    if (-not (Test-Path -LiteralPath $packageSourceFile)) {
        Write-Host "Pre-built installer not found at \"$packageSourceFile\"."
        Fail
    }
    try {
        Copy-Item -LiteralPath $packageSourceFile -Destination (Join-Path $artifactDir $packageFilename) -Force -ErrorAction Stop
    } catch {
        Fail
    }
    $packagePath = Join-Path $artifactDir $packageFilename
}

if ($releaseChannel -ne "test") {
    try {
        New-Item -ItemType File -Path (Join-Path $artifactDir "changelog.md") -Force -ErrorAction Stop | Out-Null
    } catch {
        Fail
    }

    try {
        Copy-Item -LiteralPath $eulaSource -Destination (Join-Path $artifactDir "eula.md") -Force -ErrorAction Stop
    } catch {
        Fail
    }

    $eulaVersion = ""
    $eulaVersionLine = $null
    try {
        $eulaLines = Get-Content -LiteralPath $eulaSource
        foreach ($line in $eulaLines) {
            if ($line.IndexOf("**Version:**", [System.StringComparison]::Ordinal) -ge 0) {
                $eulaVersionLine = $line
                break
            }
        }
    } catch {
    }

    if ($eulaVersionLine) {
        $eulaVersion = $eulaVersionLine.Replace("**Version:**", "")
        $eulaVersion = $eulaVersion.Replace("> ", "")
        $eulaVersion = $eulaVersion.TrimStart()
        while ($eulaVersion.EndsWith(" ")) {
            $eulaVersion = $eulaVersion.Substring(0, $eulaVersion.Length - 1)
        }
    }
    if ($eulaVersion -eq "") { $eulaVersion = "unknown" }
}

$packageSha = $null
$certOutput = & certutil -hashfile $packagePath SHA256
$skipFirst = $true
foreach ($line in $certOutput) {
    if ($skipFirst) {
        $skipFirst = $false
        continue
    }
    if (-not $packageSha) {
        $packageSha = $line -replace " ", ""
    }
}

$baseUrl = "https://files.mixitupapp.com/apps/$productSlug/windows-x64/$releaseChannel/$releaseVersion"
$eulaUrl = "$baseUrl/eula.md"
$changelogUrl = "$baseUrl/changelog.md"
$packageUrl = "$baseUrl/$packageFilename"

if ($productKey -ne "desktop") {
    $installerUrl = $packageUrl
}

Write-Host ""
Write-Host "==============================================================================="
Write-Host "Build, Sign, and Package Complete!"
Write-Host "==============================================================================="
Write-Host "Released At: $releasedAt"
Write-Host "Package: $packageFilename"
Write-Host "SHA256: $packageSha"
if ($releaseChannel -ne "test") { Write-Host "EULA Version: $eulaVersion" }
Write-Host ""
Write-Host "Please run: "
Write-Host "git commit -am \"Release $releaseVersion\""
Write-Host ""

if ($releaseChannel -ne "test") {
    $manifestPath = Join-Path $artifactDir "manifest.json"
    $manifestContent = @"
{
    "schemaVersion": "1.0.0",
    "product": "$productSlug",
    "version": "$releaseVersion",
    "channel": "$releaseChannel",
    "os": "windows",
    "arch": "x64",
    "releasedAt": "$releasedAt",
    "active": true,
    "mandatory": false,
    "eula": "$eulaUrl",
    "eulaVersion": "$eulaVersion",
    "changelog": "$changelogUrl",
    "package": "$packageUrl",
    "installer": "$installerUrl",
    "sha256": "$packageSha"
}
"@

    try {
        Set-Content -LiteralPath $manifestPath -Value $manifestContent -Encoding ASCII
    } catch {
        Fail
    }
}

Write-Host ""
Write-Host "Artifact prepared at: $artifactDir"
Write-Host "Package: $packagePath"
Write-Host "SHA256: $packageSha"
if ($releaseChannel -ne "test") { Write-Host "Populate changelog at: $artifactDir\changelog.md" }

if ($script:LocationPushed) {
    Pop-Location | Out-Null
    $script:LocationPushed = $false
}

exit 0
