param(
    [System.IO.DirectoryInfo]$RootDirectory
)

#NB: This script requires PowerShell 7.1 or later

$files = Get-ChildItem -Recurse -Path $RootDirectory -Include "*.xaml"
$resourceTypes = ('StaticResource', 'DynamicResource', 'Style')

foreach ($file in $files) {
    $fileContents = Get-Content $file -Encoding utf8BOM -Raw
    $fileLength = $fileContents.Length

    # Version 4 PasswordBox renames
    foreach($resourceType in $resourceTypes) {
        $fileContents = $fileContents -replace "\{$resourceType\ MaterialDesignFilledPasswordFieldPasswordBox}", "{$resourceType MaterialDesignFilledPasswordBox}"
        $fileContents = $fileContents -replace "\{$resourceType\ MaterialDesignOutlinedPasswordFieldPasswordBox}", "{$resourceType MaterialDesignOutlinedPasswordBox}"
        
        # Version 4 TextBox renames
        $fileContents = $fileContents -replace "\{$resourceType\ MaterialDesignFilledTextFieldTextBox}", "{$resourceType MaterialDesignFilledTextBox}"
        $fileContents = $fileContents -replace "\{$resourceType\ MaterialDesignOutlinedTextFieldTextBox}", "{$resourceType MaterialDesignOutlinedTextBox}"
    }

    # Version 4 Brush renames (these might not have braces)
    $fileContents = $fileContents -replace "ValidationErrorColor", "MaterialDesignValidationErrorColor"

    if ($fileContents.Length -ne $fileLength) {
        Set-Content -Path $file -Value $fileContents -Encoding utf8BOM -NoNewline
    }
}

Write-Host "Version 4 migration complete!" -ForegroundColor Green