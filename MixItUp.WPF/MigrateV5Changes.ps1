param(
    [System.IO.DirectoryInfo]$RootDirectory
)

#NB: This script requires PowerShell 7.1 or later

$files = Get-ChildItem -Recurse -Path $RootDirectory -Include "*.xaml"
$resourceTypes = ('StaticResource', 'DynamicResource', 'Style')

foreach ($file in $files) {
    $fileContents = Get-Content $file -Encoding utf8BOM -Raw
    $fileLength = $fileContents.Length

    # Version 5 ScrollBar renames
    foreach($resourceType in $resourceTypes) {
        $fileContents = $fileContents -replace "\{$resourceType\ MaterialDesignScrollBarThumbVertical}", "{$resourceType MaterialDesignScrollBarThumb}"
        $fileContents = $fileContents -replace "\{$resourceType\ MaterialDesignScrollBarThumbHorizontal}", "{$resourceType MaterialDesignScrollBarThumb}"
        
        # Version 5 TabControl rename (typo fix)
        $fileContents = $fileContents -replace "\{$resourceType\ MaterialDesignNavigatilRailTabControl}", "{$resourceType MaterialDesignNavigationRailTabControl}"
    }

    if ($fileContents.Length -ne $fileLength) {
        Set-Content -Path $file -Value $fileContents -Encoding utf8BOM -NoNewline
    }
}

Write-Host "Version 5 migration complete!" -ForegroundColor Green