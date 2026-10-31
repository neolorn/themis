#Requires -Version 7.0
<#
.SYNOPSIS
    Writes the release tag before Head, from which the commit-message gate checks a release tag
    and against which the release gate compares (05 APR-ENG-011, APR-ENG-012; 03 APR-LIB-006).
.DESCRIPTION
    A release tag is v<V>, V a final release X.Y.Z or a release candidate X.Y.Z-rc.N, each
    number without a leading zero and N counting from 1 (T-020); another tag is no release.
    Writes the release tag Head's parent reaches that is highest by Semantic Versioning 2.0.0's
    precedence, a release candidate below its final release and rc.10 above rc.9, or nothing
    where it reaches none: for the first release, whose gate checks the whole history.
.PARAMETER Repository
    The repository to read; by default, this one.
.PARAMETER Head
    The commit after whose parent the tags are read: a release tag's commit, or the commit a
    pull request's gates check.
.PARAMETER Final
    Reads the final releases alone, leaving every release candidate out: the newest final
    release, whose baseline the release gate compares with.
#>
[CmdletBinding()]
param(
    [string] $Repository = (Join-Path $PSScriptRoot '..' '..'),
    [Parameter(Mandatory)]
    [string] $Head,
    [switch] $Final
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$releasePattern = '^v(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(-rc\.(?<candidate>[1-9]\d*))?$'

$parent = & git -C $Repository rev-parse --verify --quiet "$Head^"
if ($LASTEXITCODE -ne 0) { return }

$tags = @(& git -C $Repository tag --list --merged $parent)
if ($LASTEXITCODE -ne 0) { throw "git tag --merged $parent failed." }

$releases = @(
    foreach ($tag in $tags) {
        $match = [regex]::Match($tag, $releasePattern)
        if (-not $match.Success) { continue }

        $candidate = $match.Groups['candidate']
        if ($Final -and $candidate.Success) { continue }

        [pscustomobject]@{
            Tag       = $tag
            Major     = [bigint]::Parse($match.Groups['major'].Value)
            Minor     = [bigint]::Parse($match.Groups['minor'].Value)
            Patch     = [bigint]::Parse($match.Groups['patch'].Value)
            # A final release ranks above each of its release candidates.
            IsFinal   = -not $candidate.Success
            Candidate = if ($candidate.Success) { [bigint]::Parse($candidate.Value) } else { [bigint]::Zero }
        }
    }
)

if ($releases.Count -gt 0) {
    $highest = $releases | Sort-Object -Descending -Property Major, Minor, Patch, IsFinal, Candidate | Select-Object -First 1
    Write-Output $highest.Tag
}
