#Requires -Version 7.0
<#
.SYNOPSIS
    Scans every commit of every ref for secrets with gitleaks (07 APR-THR-003).
.DESCRIPTION
    No allow-list but gitleaks' default reaches the scan (05 APR-ENG-001): an inline
    gitleaks:allow marker silences nothing, the .gitleaksignore of the working directory is
    not read, no baseline is given, and a .gitleaksignore the repository tracks or holds at
    its root, which gitleaks reads whatever its flags say, fails the scan.
.PARAMETER Repository
    The repository to scan; by default, this one.
#>
[CmdletBinding()]
param(
    [string] $Repository = (Join-Path $PSScriptRoot '..' '..')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path
$scanner = & (Join-Path $PSScriptRoot 'fetch-secret-scanner.ps1')
$configuration = Join-Path $PSScriptRoot 'gitleaks.toml'

$ignoreLists = [System.Collections.Generic.List[string]]::new()
$tracked = @(& git -C $Repository ls-files --cached --full-name -- ':(glob)**/.gitleaksignore')
if ($LASTEXITCODE -ne 0) { throw "git ls-files failed in $Repository." }
foreach ($path in $tracked) {
    $ignoreLists.Add("$path is a .gitleaksignore the repository tracks")
}
if ((Test-Path -LiteralPath (Join-Path $Repository '.gitleaksignore')) -and '.gitleaksignore' -notin $tracked) {
    $ignoreLists.Add(".gitleaksignore sits at the scanned repository's root")
}
if ($ignoreLists.Count -gt 0) {
    $ignoreLists | ForEach-Object { Write-Host "$_; no finding is silenced but by gitleaks' default configuration." }
    Write-Host "$($ignoreLists.Count) .gitleaksignore file(s) would silence findings."
    throw 'The secret scan refused to run.'
}

# gitleaks reads the .gitleaksignore of the folder this names, which holds no file.
$noIgnoreList = Join-Path $root 'tmp' 'secret-scan' ([guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $noIgnoreList | Out-Null
try {
    & $scanner git $Repository --config $configuration --log-opts=--all --ignore-gitleaks-allow --gitleaks-ignore-path $noIgnoreList --redact --verbose --no-banner --exit-code 1
    $code = $LASTEXITCODE
}
finally {
    Remove-Item -Recurse -Force -LiteralPath $noIgnoreList
}

if ($code -ne 0) {
    Write-Host "The secret scan failed with exit code $code."
    throw 'The secret scan failed.'
}
