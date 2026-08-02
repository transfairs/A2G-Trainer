<#
.SYNOPSIS
    Builds a version.json manifest describing the current release's assets, for a future in-app
    update check (or anything else that wants machine-readable release metadata without hitting the
    GitHub API).

.DESCRIPTION
    Writes two identical copies: a versioned snapshot (version-<tag>.json) and a constantly-named one
    (version.json) - the latter is what "releases/latest/download/version.json" resolves to, giving
    it the same kind of permalink as the Latest.zip bundle.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Tag,

    [Parameter(Mandatory = $true)]
    [string]$Repository,

    [Parameter(Mandatory = $true)]
    [string]$ZipShaPath,

    [string]$OutputDir = "dist"
)

$ErrorActionPreference = "Stop"

$version = $Tag -replace '^v', ''
$publishedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
$base = "https://github.com/$Repository/releases/download/$Tag"
$latestBase = "https://github.com/$Repository/releases/latest/download"

$zipShaLine = Get-Content -Path $ZipShaPath -Raw
$zipSha = ($zipShaLine -split '\s+')[0]

$manifest = [ordered]@{
    tag         = $Tag
    version     = $version
    publishedAt = $publishedAt
    assets      = [ordered]@{
        exe       = "$base/A2G-Trainer-XP-$Tag.exe"
        exe2007cd = "$base/A2G-Trainer-XP-$Tag-2007CD.exe"
        ct        = "$base/A2G-Trainer-XP-$Tag.ct"
        ctGog     = "$base/A2G-Trainer-XP-$Tag-GOG.ct"
        zip       = "$latestBase/A2G-Trainer-XP-Latest.zip"
    }
    sha256      = [ordered]@{
        zip = $zipSha
    }
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$json = $manifest | ConvertTo-Json -Depth 4

$versionedPath = Join-Path $OutputDir "version-$Tag.json"
$latestPath = Join-Path $OutputDir "version.json"
Set-Content -Path $versionedPath -Value $json -Encoding UTF8
Set-Content -Path $latestPath -Value $json -Encoding UTF8
Write-Host "Wrote $versionedPath and $latestPath"
