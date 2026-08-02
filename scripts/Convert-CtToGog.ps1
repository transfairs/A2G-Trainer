<#
.SYNOPSIS
    Derives a GOG-addressed Cheat Engine table from the original A2G-Trainer-XP.ct.

.DESCRIPTION
    Every <Address>anstoss2.exe+HEX</Address> entry is rewritten to <ModuleName>+<HEX + OffsetHex>,
    matching the same gogOffset (0x3140) and run.exe module detection the trainer itself applies at
    runtime (see Model/Settings.cs and Controller/ProcessController.cs). <Offset> entries (pointer-chain
    offsets applied after dereferencing) are left untouched - they don't shift between builds.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$InputPath,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [string]$ModuleName = "run.exe",

    [string]$OffsetHex = "3140"
)

$ErrorActionPreference = "Stop"

$content = Get-Content -Path $InputPath -Raw -Encoding UTF8
$gogOffset = [Convert]::ToInt64($OffsetHex, 16)
$addressCount = 0

$pattern = '(?<tag><Address>)anstoss2\.exe\+(?<hex>[0-9A-Fa-f]+)(?<close></Address>)'

$converted = [System.Text.RegularExpressions.Regex]::Replace($content, $pattern, {
    param($match)
    $script:addressCount++
    $newValue = [Convert]::ToInt64($match.Groups['hex'].Value, 16) + $gogOffset
    "$($match.Groups['tag'].Value)$ModuleName+$($newValue.ToString('X'))$($match.Groups['close'].Value)"
})

if ($addressCount -eq 0) {
    throw "No 'anstoss2.exe+<hex>' addresses were found in '$InputPath' - refusing to write an unchanged copy."
}

Set-Content -Path $OutputPath -Value $converted -Encoding UTF8 -NoNewline
Write-Host "Converted $addressCount address(es) from '$InputPath' to '$OutputPath' (module=$ModuleName, +0x$OffsetHex)."
