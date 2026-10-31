#Requires -Version 7.0
<#
.SYNOPSIS
    Runs every gate script present under build/gates/ and reports each one's result (05 APR-ENG-012).
.DESCRIPTION
    The pipeline's gates job runs this script, and so does the implementer, on the commit a
    phase report names. The gates check the working tree, so the script refuses to run unless
    HEAD is Head and the working tree is HEAD's, with no tracked change and no untracked file
    outside the excluded paths. Each gate runs in its own PowerShell process; every gate runs
    even when one before it fails, and the script fails when any of them did, or when it finds
    no gate script to run.
.PARAMETER Base
    The commit after which the commit-message gate checks, none checking the whole history.
.PARAMETER Head
    The commit the gates check: the last commit the commit-message gate checks, and HEAD.
#>
[CmdletBinding()]
param(
    [string] $Base = '',
    [string] $Head = 'HEAD'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path

$current = & git -C $root rev-parse --verify --quiet 'HEAD^{commit}'
$expected = & git -C $root rev-parse --verify --quiet "$Head^{commit}"
if ($LASTEXITCODE -ne 0 -or -not $expected) {
    Write-Host "-Head $Head names no commit."
    throw 'The gates did not run.'
}
if ($current -ne $expected) {
    Write-Host "HEAD is $current, not -Head $Head ($expected), so the gates would not check -Head."
    throw 'The gates did not run.'
}

$changes = @(& git -C $root status --porcelain --untracked-files=all)
if ($LASTEXITCODE -ne 0) { throw 'git status failed.' }
if ($changes.Count -gt 0) {
    $changes | ForEach-Object { Write-Host $_ }
    Write-Host "The working tree differs from HEAD in $($changes.Count) path(s), so the gates would not check -Head alone."
    throw 'The gates did not run.'
}

$gates = @(Get-ChildItem -Path $PSScriptRoot -Filter 'gate-*.ps1' -File | Sort-Object Name)
if ($gates.Count -eq 0) {
    Write-Host 'build/gates/ holds no gate script, so nothing would be checked.'
    throw 'The gates did not run.'
}

$results = [System.Collections.Generic.List[pscustomobject]]::new()

foreach ($gate in $gates) {
    $arguments = @()
    if ($gate.Name -ceq 'gate-commit-messages.ps1') {
        $arguments = @('-Base', $Base, '-Head', $Head)
    }

    Write-Host "::group::$($gate.BaseName)"
    pwsh -NoProfile -NonInteractive -File $gate.FullName @arguments
    $code = $LASTEXITCODE
    Write-Host '::endgroup::'

    $result = if ($code -eq 0) { 'passed' } else { "failed (exit code $code)" }
    $results.Add([pscustomobject]@{ Gate = $gate.BaseName; Result = $result })
}

$results | Format-Table -AutoSize | Out-String | Write-Host

if (@($results | Where-Object { $_.Result -ne 'passed' }).Count -gt 0) {
    throw 'One or more gates failed.'
}
