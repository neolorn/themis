#Requires -Version 7.0
<#
.SYNOPSIS
    Writes the release tag before Head, from which the commit-message gate checks a release tag (05 APR-ENG-011, APR-ENG-012).
.DESCRIPTION
    A release tag is v<major>.<minor>.<patch>; another tag is no release. Writes the highest
    release tag Head's parent reaches, or nothing for the first release, whose gate checks the
    whole history.
.PARAMETER Repository
    The repository to read; by default, this one.
.PARAMETER Head
    The release tag's commit.
#>
[CmdletBinding()]
param(
    [string] $Repository = (Join-Path $PSScriptRoot '..' '..'),
    [Parameter(Mandatory)]
    [string] $Head
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$parent = & git -C $Repository rev-parse --verify --quiet "$Head^"
if ($LASTEXITCODE -ne 0) { return }

$tags = @(& git -C $Repository tag --list --merged $parent)
if ($LASTEXITCODE -ne 0) { throw "git tag --merged $parent failed." }

$releases = @($tags | Where-Object { $_ -cmatch '^v(\d+)\.(\d+)\.(\d+)$' } | Sort-Object -Descending { [version]$_.Substring(1) })
if ($releases.Count -gt 0) {
    Write-Output $releases[0]
}
