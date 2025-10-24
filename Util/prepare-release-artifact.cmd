@echo off
setlocal EnableDelayedExpansion

set "EXITCODE=1"
set "SCRIPT_DIR=%~dp0"
pushd "%SCRIPT_DIR%" >nul

for /f "usebackq delims=" %%i in (`powershell -NoLogo -NoProfile -Command "(Resolve-Path '%SCRIPT_DIR%..').ProviderPath"`) do set "DESKTOP_DIR=%%i"
for /f "usebackq delims=" %%i in (`powershell -NoLogo -NoProfile -Command "(Resolve-Path '%DESKTOP_DIR%\..').ProviderPath"`) do set "REPO_ROOT=%%i"

set "EULA_SOURCE=!REPO_ROOT!\Docs\Legal\Desktop\Embedded\End-User-License-Agreement.md"
if not exist "!EULA_SOURCE!" (
    echo EULA file not found at "!EULA_SOURCE!".
    goto :fail
)

set "ARTIFACT_ROOT=!REPO_ROOT!\mixitupservices\src\FileService\artifacts"
if not exist "!ARTIFACT_ROOT!" (
    echo Artifact root not found at "!ARTIFACT_ROOT!".
    goto :fail
)

choice /c WI /m "Select product to build: (W) MixItUp.WPF, (I) MixItUp.Installer"
if errorlevel 2 (
    set "PRODUCT_KEY=installer"
) else (
    set "PRODUCT_KEY=desktop"
)

if "!PRODUCT_KEY!"=="desktop" (
    set "PROJECT_PATH=!DESKTOP_DIR!\MixItUp.WPF\MixItUp.WPF.csproj"
    set "PRODUCT_NAME=MixItUp.WPF"
    set "PRODUCT_SLUG=mixitup-desktop"
    set "PRODUCT_TITLE=Mix It Up Desktop"
    set "OUTPUT_DIR=!DESKTOP_DIR!\MixItUp.WPF\bin\Release"
) else (
    set "PROJECT_PATH=!DESKTOP_DIR!\MixItUp.Installer\MixItUp.Installer.csproj"
    set "PRODUCT_NAME=MixItUp.Installer"
    set "PRODUCT_SLUG=mixitup-desktop-installer"
    set "PRODUCT_TITLE=Mix It Up Installer"
    set "OUTPUT_DIR=!DESKTOP_DIR!\MixItUp.Installer\bin\Release"
)

:promptChannel
set /p RELEASE_CHANNEL=Enter release channel (e.g. public, preview): 
set "RELEASE_CHANNEL=!RELEASE_CHANNEL:"=!"
if "!RELEASE_CHANNEL!"=="" (
    echo Release channel is required.
    goto :promptChannel
)
set "PS_INPUT=!RELEASE_CHANNEL!"
for /f "usebackq delims=" %%i in (`powershell -NoLogo -NoProfile -Command "if ($env:PS_INPUT) { $env:PS_INPUT.Trim().ToLowerInvariant() } else { '' }"`) do set "RELEASE_CHANNEL=%%i"
set "PS_INPUT="

:promptVersion
set /p RELEASE_VERSION=Enter release version (e.g. 1.4.0): 
set "RELEASE_VERSION=!RELEASE_VERSION:"=!"
if "!RELEASE_VERSION!"=="" (
    echo Release version is required.
    goto :promptVersion
)
set "PS_INPUT=!RELEASE_VERSION!"
for /f "usebackq delims=" %%i in (`powershell -NoLogo -NoProfile -Command "if ($env:PS_INPUT) { $env:PS_INPUT.Trim() } else { '' }"`) do set "RELEASE_VERSION=%%i"
set "PS_INPUT="

set "ARTIFACT_DIR=!ARTIFACT_ROOT!\!PRODUCT_SLUG!\windows-x64\!RELEASE_CHANNEL!\!RELEASE_VERSION!"
if exist "!ARTIFACT_DIR!" (
    echo Target artifact directory already exists: "!ARTIFACT_DIR!".
    choice /c YN /m "Delete and recreate this directory? (Y/N)"
    if errorlevel 2 (
        echo User declined to replace the existing artifact directory.
        goto :fail
    )
    echo Removing existing artifact directory...
    rmdir /S /Q "!ARTIFACT_DIR!"
    if exist "!ARTIFACT_DIR!" (
        echo Failed to remove existing artifact directory.
        goto :fail
    )
)
mkdir "!ARTIFACT_DIR!"
if errorlevel 1 goto :fail

echo Building !PRODUCT_NAME! (Release)...
msbuild "!PROJECT_PATH!" /t:Rebuild /p:Configuration=Release
if errorlevel 1 goto :fail

call :SignReleaseBinaries
if errorlevel 1 goto :fail

echo Packaging build output...
if "!PRODUCT_KEY!"=="desktop" (
    set "PACKAGE_FILENAME=MixItUp-Desktop_!RELEASE_VERSION!.zip"
    set "PACKAGE_SOURCE_DIR=!DESKTOP_DIR!\MixItUp.WPF\bin\Release"
    if not exist "!PACKAGE_SOURCE_DIR!" (
        echo Build output not found at "!PACKAGE_SOURCE_DIR!".
        goto :fail
    )
    set "PACKAGE_PATH=!ARTIFACT_DIR!\!PACKAGE_FILENAME!"
    set "PS_PACKAGE_SOURCE=!PACKAGE_SOURCE_DIR!"
    set "PS_PACKAGE_DEST=!PACKAGE_PATH!"
    powershell -NoLogo -NoProfile -Command "Compress-Archive -Path (Join-Path $env:PS_PACKAGE_SOURCE '*') -DestinationPath $env:PS_PACKAGE_DEST -Force"
    if errorlevel 1 goto :fail
    set "PS_PACKAGE_SOURCE="
    set "PS_PACKAGE_DEST="
) else (
    set "PACKAGE_FILENAME=MixItUp-Setup.exe"
    set "PACKAGE_SOURCE_FILE=!OUTPUT_DIR!\MixItUp-Setup.exe"
    if not exist "!PACKAGE_SOURCE_FILE!" (
        echo Installer binary not found at "!PACKAGE_SOURCE_FILE!".
        goto :fail
    )
    copy /Y "!PACKAGE_SOURCE_FILE!" "!ARTIFACT_DIR!\!PACKAGE_FILENAME!" >nul
    if errorlevel 1 goto :fail
    set "PACKAGE_PATH=!ARTIFACT_DIR!\!PACKAGE_FILENAME!"
)

type nul > "!ARTIFACT_DIR!\changelog.md"
if errorlevel 1 goto :fail

copy /Y "!EULA_SOURCE!" "!ARTIFACT_DIR!\eula.md" >nul
if errorlevel 1 goto :fail

set "EULA_VERSION=unknown"
for /f "usebackq delims=" %%i in (`powershell -NoLogo -NoProfile -Command "^$line = (Get-Content '!EULA_SOURCE!') ^| Where-Object { ^$_ -match '\*\*Version:\*\*\s*(\S+)' } ^| Select-Object -First 1; if (^$line) { ^$line -match '\*\*Version:\*\*\s*(\S+)'; Write-Output ^$matches[1] }"`) do (
    if not "%%i"=="" set "EULA_VERSION=%%i"
)

for /f "usebackq delims=" %%i in (`powershell -NoLogo -NoProfile -Command "(Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss.fffZ')"`) do set "RELEASED_AT=%%i"
for /f "usebackq delims=" %%i in (`powershell -NoLogo -NoProfile -Command "(Get-FileHash -Path '!PACKAGE_PATH!' -Algorithm SHA256).Hash.ToLower()"`) do set "PACKAGE_SHA=%%i"

set "BASE_URL=https://files.mixitupapp.com/apps/!PRODUCT_SLUG!/windows-x64/!RELEASE_CHANNEL!/!RELEASE_VERSION!"
set "EULA_URL=!BASE_URL!/eula.md"
set "CHANGELOG_URL=!BASE_URL!/changelog.md"
set "PACKAGE_URL=!BASE_URL!/!PACKAGE_FILENAME!"
set "INSTALLER_URL="

if "!PRODUCT_KEY!"=="desktop" (
    call :PromptInstallerVersion
) else (
    set "INSTALLER_URL=!PACKAGE_URL!"
)

> "!ARTIFACT_DIR!\manifest.json" (
    echo {
    echo     "schemaVersion": "1.0.0",
    echo     "product": "!PRODUCT_SLUG!",
    echo     "version": "!RELEASE_VERSION!",
    echo     "channel": "!RELEASE_CHANNEL!",
    echo     "os": "windows",
    echo     "arch": "x64",
    echo     "releasedAt": "!RELEASED_AT!",
    echo     "active": true,
    echo     "mandatory": false,
    echo     "eula": "!EULA_URL!",
    echo     "eulaVersion": "!EULA_VERSION!",
    echo     "changelog": "!CHANGELOG_URL!",
    echo     "package": "!PACKAGE_URL!",
    echo     "installer": "!INSTALLER_URL!",
    echo     "sha256": "!PACKAGE_SHA!"
    echo }
)
if errorlevel 1 goto :fail

echo.
echo Artifact prepared at: !ARTIFACT_DIR!
echo Package: !PACKAGE_PATH!
echo SHA256: !PACKAGE_SHA!
echo Populate changelog at: !ARTIFACT_DIR!\changelog.md

set "EXITCODE=0"
goto :done

:PromptInstallerVersion
set "INSTALLER_VERSION="
:promptInstallerLoop
set /p INSTALLER_VERSION=Enter installer version to reference in manifest (e.g. 0.5.0): 
set "INSTALLER_VERSION=!INSTALLER_VERSION:"=!"
if "!INSTALLER_VERSION!"=="" (
    echo Installer version is required when packaging the desktop build.
    goto :promptInstallerLoop
)
set "PS_INPUT=!INSTALLER_VERSION!"
for /f "usebackq delims=" %%i in (`powershell -NoLogo -NoProfile -Command "if ($env:PS_INPUT) { $env:PS_INPUT.Trim() } else { '' }"`) do set "INSTALLER_VERSION=%%i"
set "PS_INPUT="
set "INSTALLER_URL=https://files.mixitupapp.com/apps/mixitup-desktop-installer/windows-x64/public/!INSTALLER_VERSION!/MixItUp-Setup.exe"
exit /b 0

:SignReleaseBinaries
if not defined SIGNTHUMB set "SIGNTHUMB=A838AD3D9C00B4806F2FC4270269EA6060D021DC"

if not defined SIGNTOOL (
    set "SIGNTOOL=C:\Program Files (x86)\Microsoft Visual Studio\Shared\NuGetPackages\microsoft.windows.sdk.buildtools\10.0.26100.1742\bin\10.0.26100.0\x64\signtool.exe"
    if not exist "%SIGNTOOL%" set "SIGNTOOL=C:\Program Files (x86)\Microsoft SDKs\ClickOnce\SignTool\signtool.exe"
)

if not exist "!SIGNTOOL!" (
    echo signtool.exe not found. Set SIGNTOOL to the full path.
    exit /b 1
)

set "SIGN_TARGETS="

if "!PRODUCT_KEY!"=="desktop" (
    set "WPF_RELEASE_DIR=!DESKTOP_DIR!\MixItUp.WPF\bin\Release"
    for %%f in (MixItUp.exe MixItUp.Reporter.exe MixItUp.API.dll MixItUp.Base.dll MixItUp.SignalR.Client.dll) do (
        if not exist "!WPF_RELEASE_DIR!\%%f" (
            echo Required binary not found for signing: "!WPF_RELEASE_DIR!\%%f".
            exit /b 1
        )
        set "SIGN_TARGETS=!SIGN_TARGETS! ""!WPF_RELEASE_DIR!\%%f"""
    )
) else (
    set "INSTALLER_RELEASE=!DESKTOP_DIR!\MixItUp.Installer\bin\Release\MixItUp-Setup.exe"
    if not exist "!INSTALLER_RELEASE!" (
        echo Required installer binary not found for signing: "!INSTALLER_RELEASE!".
        exit /b 1
    )
    set "SIGN_TARGETS=""!INSTALLER_RELEASE!"""
)

if "!SIGN_TARGETS!"=="" (
    echo No binaries collected for signing.
    exit /b 1
)

echo Signing release binaries...
"%SIGNTOOL%" sign /fd sha256 /sha1 %SIGNTHUMB% /tr http://ts.ssl.com /td sha256 /v !SIGN_TARGETS!
if errorlevel 1 (
    echo Failed to sign release binaries.
    exit /b 1
)

echo Verifying signatures...
"%SIGNTOOL%" verify /pa !SIGN_TARGETS!
if errorlevel 1 (
    echo Failed to verify signatures.
    exit /b 1
)

exit /b 0

:fail
echo.
echo Release packaging failed.

:done
popd >nul
endlocal & exit /b %EXITCODE%
