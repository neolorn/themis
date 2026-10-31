#Requires -Version 7.0
<#
.SYNOPSIS
    Checks that each criterion of build/gates/built-items.txt has a test named after it (05 APR-ENG-008).
.DESCRIPTION
    Each line of the file names one criterion, as "APR-REQ-004 AC2". A criterion has its test
    where a built test project lists a test whose method is named ITEM_ACn_Outcome,
    "APR_REQ_004_AC2_..." for that line. A file that names no criterion fails, since the
    gate would check nothing.
.PARAMETER Repository
    The repository to check; by default, this one.
#>
[CmdletBinding()]
param(
    [string] $Repository = (Join-Path $PSScriptRoot '..' '..')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$list = Join-Path $Repository 'build' 'gates' 'built-items.txt'
if (-not (Test-Path $list)) {
    throw 'build/gates/built-items.txt is missing.'
}

$lines = @(Get-Content -Path $list)
if ($lines.Count -eq 0) {
    Write-Host 'build/gates/built-items.txt names no criterion, so the gate would check nothing.'
    throw 'The gate checked nothing.'
}

$methods = @(& (Join-Path $PSScriptRoot 'list-tests.ps1') -Repository $Repository | ForEach-Object { $_.Method })

$failures = 0
$number = 0
foreach ($line in $lines) {
    $number++
    $criterion = [regex]::Match($line, '^(?<item>APR-[A-Z]+-\d{3}) (?<criterion>AC\d+)$')
    if (-not $criterion.Success) {
        Write-Host "built-items.txt line ${number}, ""$line"", does not name one criterion as ""APR-REQ-004 AC2""."
        $failures++
        continue
    }

    $prefix = $criterion.Groups['item'].Value.Replace('-', '_') + '_' + $criterion.Groups['criterion'].Value + '_'
    $pattern = '^' + [regex]::Escape($prefix) + '[A-Za-z0-9]+$'
    if (@($methods | Where-Object { $_ -cmatch $pattern }).Count -eq 0) {
        Write-Host "$line has no test named $prefix<Outcome>."
        $failures++
    }
}

if ($failures -gt 0) {
    throw "$failures line(s) of built-items.txt name no criterion with its test."
}

Write-Host "Each of the $number criteria of built-items.txt has its test."
