#Requires -Version 7.0
<#
.SYNOPSIS
    Builds the solution with its analysers, every warning an error (05 APR-ENG-002, APR-ENG-004).
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path

dotnet build (Join-Path $root 'Themis.sln') --configuration Release -p:RestoreLockedMode=true -nodeReuse:false
if ($LASTEXITCODE -ne 0) {
    throw "The build failed with exit code $LASTEXITCODE."
}
