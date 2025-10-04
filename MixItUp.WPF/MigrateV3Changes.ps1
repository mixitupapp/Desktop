param(
    [System.IO.DirectoryInfo]$RootDirectory
)

#NB: This script requires PowerShell 7.1 or later

$files = Get-ChildItem -Recurse -Path $RootDirectory -Include "*.xaml"
$resourceTypes = ('StaticResource', 'DynamicResource', 'Style')

foreach ($file in $files) {
    $fileContents = Get-Content $file -Encoding utf8BOM -Raw
    $fileLength = $fileContents.Length

    # Version 3 TextBlock style renames
    foreach($resourceType in $resourceTypes) {
        $fileContents = $fileContents -replace "\{$resourceType\ MaterialDesignTitleTextBlock}", "{$resourceType MaterialDesignHeadline6TextBlock}"
        $fileContents = $fileContents -replace "\{$resourceType\ MaterialDesignHeadlineTextBlock}", "{$resourceType MaterialDesignHeadline5TextBlock}"
        $fileContents = $fileContents -replace "\{$resourceType\ MaterialDesignSubheadingTextBlock}", "{$resourceType MaterialDesignSubtitle1TextBlock}"
        $fileContents = $fileContents -replace "\{$resourceType\ MaterialDesignDisplay4TextBlock}", "{$resourceType MaterialDesignHeadline1TextBlock}"
        $fileContents = $fileContents -replace "\{$resourceType\ MaterialDesignDisplay3TextBlock}", "{$resourceType MaterialDesignHeadline2TextBlock}"
        $fileContents = $fileContents -replace "\{$resourceType\ MaterialDesignDisplay2TextBlock}", "{$resourceType MaterialDesignHeadline3TextBlock}"
        $fileContents = $fileContents -replace "\{$resourceType\ MaterialDesignDisplay1TextBlock}", "{$resourceType MaterialDesignHeadline4TextBlock}"
    }

    if ($fileContents.Length -ne $fileLength) {
        Set-Content -Path $file -Value $fileContents -Encoding utf8BOM -NoNewline
    }
}

Write-Host "Version 3 migration complete!" -ForegroundColor Green