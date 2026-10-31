#Requires -Version 7.0
<#
.SYNOPSIS
    Checks the projects' layout and references (05 APR-ENG-001, 03 APR-LIB-001).
.DESCRIPTION
    Runs the repository's tests of the layout and the project references, which read every
    project of the repository.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

& (Join-Path $PSScriptRoot 'run-repository-tests.ps1') -Classes 'Themis.Repository.Tests.RepositoryLayoutTests', 'Themis.Repository.Tests.ProjectReferenceTests'
