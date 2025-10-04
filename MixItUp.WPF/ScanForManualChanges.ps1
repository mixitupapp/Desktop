param(
    [System.IO.DirectoryInfo]$RootDirectory
)

#NB: This script requires PowerShell 7.1 or later

$files = Get-ChildItem -Recurse -Path $RootDirectory -Include "*.xaml"
$issuesFound = @()

# Patterns to search for that need manual review
$patterns = @{
    "DialogClosingEventArgs.Content" = "V3: Should use DialogClosingEventArgs.Session.Content instead"
    "MaterialDataGridComboBoxColumn" = "V3: Should be renamed to DataGridComboBoxColumn"
    "DialogHost.DialogTheme" = "V4: Default behavior changed to 'Inherit'"
    "PopupBox.PopupMode" = "V4: Default changed from 'MouseOverEager' to 'Click'"
    "HintAssist.FloatingOffset" = "V4: Default changed to (0, -16) from (1, -16)"
    "SecondaryAccentBrush" = "V4: Removed - needs replacement"
    "SecondaryAccentForegroundBrush" = "V4: Removed - needs replacement"
    "ShadowDepth" = "V5: Removed - needs replacement"
    "ShadowAssist.ShadowEdges" = "V5: Removed - needs replacement"
    "DrawerHostOpenMode.Model" = "V5: Removed - use different mode"
    "RatingBarButton.IsWithinSelectedValue" = "V5: Property removed"
    "Card[^>]*VerticalAlignment\s*=" = "V3: Card default VerticalAlignment changed to Stretch"
    "Flipper[^>]*VerticalAlignment\s*=" = "V3: Flipper default VerticalAlignment changed to Stretch"
}

Write-Host "`nScanning for manual changes needed..." -ForegroundColor Cyan
Write-Host "=" * 60 -ForegroundColor Cyan

foreach ($file in $files) {
    $fileContents = Get-Content $file -Encoding utf8BOM -Raw
    $relativePath = $file.FullName.Replace($RootDirectory.FullName, "").TrimStart("\")
    
    $fileHasIssues = $false
    
    foreach ($pattern in $patterns.GetEnumerator()) {
        if ($fileContents -match $pattern.Key) {
            if (-not $fileHasIssues) {
                $issuesFound += [PSCustomObject]@{
                    File = $relativePath
                    Issues = @()
                }
                $fileHasIssues = $true
            }
            
            # Find line numbers
            $lines = $fileContents -split "`n"
            $lineNumbers = @()
            for ($i = 0; $i -lt $lines.Count; $i++) {
                if ($lines[$i] -match $pattern.Key) {
                    $lineNumbers += ($i + 1)
                }
            }
            
            $issuesFound[-1].Issues += [PSCustomObject]@{
                Pattern = $pattern.Key
                Description = $pattern.Value
                Lines = $lineNumbers -join ", "
            }
        }
    }
}

# Display results
if ($issuesFound.Count -eq 0) {
    Write-Host "`nNo manual changes detected! ✓" -ForegroundColor Green
} else {
    Write-Host "`nFound $($issuesFound.Count) file(s) that need manual review:`n" -ForegroundColor Yellow
    
    foreach ($issue in $issuesFound) {
        Write-Host "FILE: $($issue.File)" -ForegroundColor Yellow
        foreach ($detail in $issue.Issues) {
            Write-Host "  Line(s) $($detail.Lines): $($detail.Description)" -ForegroundColor White
            Write-Host "    Pattern: $($detail.Pattern)" -ForegroundColor Gray
        }
        Write-Host ""
    }
    
    Write-Host "=" * 60 -ForegroundColor Cyan
    Write-Host "NEXT STEP: Share the file paths above and I'll help you fix them!" -ForegroundColor Cyan
}

# Export to file for easy reference
$outputFile = Join-Path $RootDirectory "ManualChangesNeeded.txt"
if ($issuesFound.Count -gt 0) {
    $issuesFound | ForEach-Object {
        "FILE: $($_.File)"
        $_.Issues | ForEach-Object {
            "  Line(s) $($_.Lines): $($_.Description)"
            "    Pattern: $($_.Pattern)"
        }
        ""
    } | Out-File -FilePath $outputFile -Encoding UTF8
    
    Write-Host "Results also saved to: $outputFile" -ForegroundColor Green
}