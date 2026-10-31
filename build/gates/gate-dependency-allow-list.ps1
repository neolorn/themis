#Requires -Version 7.0
<#
.SYNOPSIS
    Checks the packages and local tools against the allow-list of 05 APR-ENG-009.
.DESCRIPTION
    Runs the repository's tests of the allow-list, which read every project, the central
    package versions and the tool manifest.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

& (Join-Path $PSScriptRoot 'run-repository-tests.ps1') -Classes 'Themis.Repository.Tests.PackageAllowListTests'
