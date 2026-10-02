param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version
)

$projectPath = Join-Path $PSScriptRoot "..\ScannerDisabler.csproj"
$content = [System.IO.File]::ReadAllText($projectPath)
$match = [System.Text.RegularExpressions.Regex]::Match($content, '<Version>(\d+\.\d+\.\d+)</Version>')
if (-not $match.Success) {
    throw "Could not find a semantic <Version> element in ScannerDisabler.csproj."
}

if ($match.Groups[1].Value -eq $Version) {
    Write-Host "Penguin Anti-Scan is already version $Version"
    exit 0
}

$updated = [System.Text.RegularExpressions.Regex]::Replace(
    $content,
    '<Version>\d+\.\d+\.\d+</Version>',
    "<Version>$Version</Version>",
    1)

$utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($projectPath, $updated, $utf8WithoutBom)
Write-Host "Penguin Anti-Scan version updated to $Version"
