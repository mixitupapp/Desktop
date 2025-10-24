@echo off
setlocal

set "SIGNTHUMB=A838AD3D9C00B4806F2FC4270269EA6060D021DC"

if not defined SIGNTOOL (
	set "SIGNTOOL=C:\Program Files (x86)\Microsoft Visual Studio\Shared\NuGetPackages\microsoft.windows.sdk.buildtools\10.0.26100.1742\bin\10.0.26100.0\x64\signtool.exe"
	if not exist "%SIGNTOOL%" set "SIGNTOOL=C:\Program Files (x86)\Microsoft SDKs\ClickOnce\SignTool\signtool.exe"
)

if not exist "%SIGNTOOL%" (
	echo signtool.exe not found. Set SIGNTOOL to the full path.
	exit /b 1
)

msbuild /t:Clean,Build /property:Configuration=Release ..\mixer-mixitup.sln

"%SIGNTOOL%" sign /fd sha256 /sha1 %SIGNTHUMB% /tr http://ts.ssl.com /td sha256 /v "..\MixItUp.Installer\bin\Release\MixItUp-Setup.exe" "..\MixItUp.WPF\bin\Release\MixItUp.exe" "..\MixItUp.WPF\bin\Release\MixItUp.Reporter.exe" "..\MixItUp.WPF\bin\Release\MixItUp.API.dll" "..\MixItUp.WPF\bin\Release\MixItUp.Base.dll" "..\MixItUp.WPF\bin\Release\MixItUp.SignalR.Client.dll"
"%SIGNTOOL%" verify /pa "..\MixItUp.Installer\bin\Release\MixItUp-Setup.exe" "..\MixItUp.WPF\bin\Release\MixItUp.exe" "..\MixItUp.WPF\bin\Release\MixItUp.Reporter.exe" "..\MixItUp.WPF\bin\Release\MixItUp.API.dll" "..\MixItUp.WPF\bin\Release\MixItUp.Base.dll" "..\MixItUp.WPF\bin\Release\MixItUp.SignalR.Client.dll"

"%SIGNTOOL%" sign /fd sha256 /sha1 %SIGNTHUMB% /tr http://ts.ssl.com /td sha256 /v "..\MixItUp.Installer\bin\Debug\MixItUp-Setup.exe" "..\MixItUp.WPF\bin\Debug\MixItUp.exe" "..\MixItUp.WPF\bin\Debug\MixItUp.Reporter.exe" "..\MixItUp.WPF\bin\Debug\MixItUp.API.dll" "..\MixItUp.WPF\bin\Debug\MixItUp.Base.dll" "..\MixItUp.WPF\bin\Debug\MixItUp.SignalR.Client.dll"
"%SIGNTOOL%" verify /pa "..\MixItUp.Installer\bin\Debug\MixItUp-Setup.exe" "..\MixItUp.WPF\bin\Debug\MixItUp.exe" "..\MixItUp.WPF\bin\Debug\MixItUp.Reporter.exe" "..\MixItUp.WPF\bin\Debug\MixItUp.API.dll" "..\MixItUp.WPF\bin\Debug\MixItUp.Base.dll" "..\MixItUp.WPF\bin\Debug\MixItUp.SignalR.Client.dll"

endlocal

powershell -NoLogo -NoProfile -Command Compress-Archive -Path "..\MixItUp.WPF\bin\Release\*" -DestinationPath "..\..\MixItUp.zip"
copy ..\MixItUp.WPF\Changelog.html ..\..