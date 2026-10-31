#Requires -Version 7.0
<#
.SYNOPSIS
    Checks the projects' layout, references and grants (05 APR-ENG-001, 03 APR-LIB-001).
.DESCRIPTION
    Runs the repository's tests of the layout, the project references and the grants of
    InternalsVisibleTo, which read every project of the repository.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

& (Join-Path $PSScriptRoot 'run-repository-tests.ps1') -Classes 'Themis.Repository.Tests.RepositoryLayoutTests', 'Themis.Repository.Tests.ProjectReferenceTests', 'Themis.Repository.Tests.GrantTests'
