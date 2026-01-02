$ErrorActionPreference = "Stop"

$signThumb = "A838AD3D9C00B4806F2FC4270269EA6060D021DC"
$signTool = "C:\Program Files (x86)\Microsoft Visual Studio\Shared\NuGetPackages\microsoft.windows.sdk.buildtools\10.0.26100.1742\bin\10.0.26100.0\x64\signtool.exe"

if (-not (Test-Path $signTool)) {
    $signTool = "C:\Program Files (x86)\Microsoft SDKs\ClickOnce\SignTool\signtool.exe"
}

if (-not (Test-Path $signTool)) {
    Write-Error "signtool.exe not found. Set SIGNTOOL to the full path."
    exit 1
}

# 1. Build the entire solution (ensures Installer and other projects are built)
Write-Host "Building Solution..."
dotnet build ..\mixer-mixitup.sln -c Release

# 2. Publish the WPF application to a dedicated folder
$publishDir = "..\..\..\Publishing\Published"
if (Test-Path $publishDir) {
    Remove-Item -Path $publishDir -Recurse -Force
}
Write-Host "Publishing WPF Application..."
dotnet publish ..\MixItUp.WPF\MixItUp.WPF.csproj -c Release -r win-x64 --self-contained -o $publishDir

# 3. Sign the binaries
Write-Host "Signing Binaries..."
$filesToSign = @(
    "$publishDir\MixItUp.exe",
    "$publishDir\MixItUp.Reporter.exe",
    "$publishDir\MixItUp.API.dll",
    "$publishDir\MixItUp.Base.dll",
    "$publishDir\MixItUp.SignalR.Client.dll",
    "..\MixItUp.Installer\bin\Release\net48\MixItUp-Setup.exe"
)

# Filter existing files to avoid errors in signtool
$existingFiles = $filesToSign | Where-Object { Test-Path $_ }
$missingFiles = $filesToSign | Where-Object { -not (Test-Path $_) }

if ($missingFiles) {
    Write-Warning "The following files were not found and will not be signed:"
    $missingFiles | ForEach-Object { Write-Warning "  $_" }
}

if ($existingFiles) {
    Write-Host "Signing all files in a single batch..."
    & $signTool sign /fd sha256 /sha1 $signThumb /tr http://ts.ssl.com /td sha256 /v $existingFiles
    & $signTool verify /pa $existingFiles
}

# 4. Package the release
Write-Host "Packaging Release..."
$zipPath = "..\..\..\Publishing\MixItUp.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}
# Zip only the published application files (excluding the installer)
Compress-Archive -Path "$publishDir\*" -DestinationPath $zipPath

# Copy the signed installer to the Publishing directory
$installerSource = "..\MixItUp.Installer\bin\Release\net48\MixItUp-Setup.exe"
$installerDest = "..\..\..\Publishing\MixItUp-Setup.exe"
if (Test-Path $installerSource) {
    Copy-Item -Path $installerSource -Destination $installerDest -Force
    Write-Host "Installer copied to $installerDest"
} else {
    Write-Warning "Installer not found at $installerSource"
}

Write-Host "Build and Release Process Complete!"
