#Requires -Version 7.0
<#
.SYNOPSIS
    Builds the sample host from the packages packed from the same commit, checks its format
    and runs its tests (05 APR-ENG-001, APR-ENG-002, APR-ENG-012).
.DESCRIPTION
    Packs every package of src/ into artifacts/packages/ at a version of the commit's own,
    0.0.0-sample.g<commit>, and builds samples/Samples.sln at that version, which MSBuild reads
    as ThemisVersion from the environment, so the sample takes the packages of the commit the
    gates check and no other. The restore reads a package folder of the gate's own under
    tmp/, from which the library's packages of earlier runs are removed first, so a package
    packed again at the same version is never read from a cache. The sample's code must pass
    dotnet format --verify-no-changes, and its tests, a kind of their own, must pass.
.PARAMETER Repository
    The repository whose sample host to check; by default, this one.
#>
[CmdletBinding()]
param(
    [string] $Repository = (Join-Path $PSScriptRoot '..' '..')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path $Repository).Path
$solution = Join-Path $root 'samples' 'Samples.sln'
if (-not (Test-Path $solution)) {
    Write-Host 'samples/Samples.sln is missing, so the gate would check nothing.'
    throw 'The gate checked nothing.'
}

$commit = & git -C $root rev-parse --short=12 HEAD
if ($LASTEXITCODE -ne 0) { throw 'git rev-parse failed.' }
$version = "0.0.0-sample.g$commit"

& (Join-Path $PSScriptRoot 'pack-packages.ps1') -Version $version -Output (Join-Path $root 'artifacts' 'packages')

$packages = Join-Path $root 'tmp' 'sample-packages'
New-Item -ItemType Directory -Force -Path $packages | Out-Null
Get-ChildItem -Path $packages -Directory | Where-Object { $_.Name -like 'themis*' } | Remove-Item -Recurse -Force
$env:NUGET_PACKAGES = $packages
$env:ThemisVersion = $version

dotnet build $solution --configuration Release -nodeReuse:false
if ($LASTEXITCODE -ne 0) { throw "The build of samples/Samples.sln at $version failed with exit code $LASTEXITCODE." }

dotnet format $solution --verify-no-changes --no-restore
if ($LASTEXITCODE -ne 0) { throw "samples/Samples.sln does not pass dotnet format --verify-no-changes (exit code $LASTEXITCODE)." }

$failures = [System.Collections.Generic.List[string]]::new()
foreach ($project in @(Get-ChildItem -Path (Join-Path $root 'samples') -Filter '*.Tests.csproj' -Recurse -File | Where-Object { $_.FullName -notmatch '[\/](bin|obj)[\/]' } | Sort-Object FullName)) {
    Write-Host "== tests of $($project.BaseName)"
    dotnet run --project $project.FullName --configuration Release --no-build
    if ($LASTEXITCODE -ne 0) {
        $failures.Add("$($project.BaseName) (exit code $LASTEXITCODE)")
    }
}

if ($failures.Count -gt 0) {
    throw "The sample host's tests failed: $($failures -join '; ')."
}

Write-Host "The sample host built from the packages at $version, and its tests passed."
