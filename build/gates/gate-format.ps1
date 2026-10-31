#Requires -Version 7.0
<#
.SYNOPSIS
    Checks the solution's formatting against .editorconfig (05 APR-ENG-002).
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path
$solution = Join-Path $root 'Themis.sln'

dotnet restore $solution --locked-mode
if ($LASTEXITCODE -ne 0) {
    throw "The restore failed with exit code $LASTEXITCODE."
}

dotnet format $solution --verify-no-changes --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "dotnet format found changes to make (exit code $LASTEXITCODE)."
}
