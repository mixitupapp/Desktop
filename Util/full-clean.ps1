$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Push-Location $scriptDir | Out-Null

Push-Location (Join-Path $scriptDir "..") | Out-Null
$desktopDir = (Get-Location).Path
Pop-Location | Out-Null

$projects = @(
    "MixItUp.Base",
    "MixItUp.Installer",
    "MixItUp.Reporter",
    "MixItUp.SignalR.Client",
    "MixItUp.Uninstaller",
    "MixItUp.WPF"
)

Write-Host "Running dotnet clean..."
& dotnet clean (Join-Path $desktopDir "mixer-mixitup.sln")

Write-Host ""
Write-Host "Removing bin and obj directories..."

foreach ($proj in $projects) {
    foreach ($subDir in @("bin", "obj")) {
        $path = Join-Path $desktopDir "$proj\$subDir"
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Recurse -Force -ErrorAction SilentlyContinue
            Write-Host "  Removed: $proj\$subDir"
        }
    }
}

Write-Host ""
Write-Host "Done."

Pop-Location | Out-Null
