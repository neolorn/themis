#Requires -Version 7.0
<#
.SYNOPSIS
    Builds the test projects of a repository and writes the tests each one lists (05 APR-ENG-008).
.DESCRIPTION
    The test projects are every project under tests/. Each is built in locked mode, then asked
    for its tests through Microsoft.Testing.Platform's listing, so a test is what the test
    framework runs, not a name in the source. Writes one object per test: its project, its
    full name, its method name and the values of its Category traits. The sample host's tests,
    a fourth kind, are in their own project outside tests/ (05 APR-ENG-001), so none is listed.
.PARAMETER Repository
    The repository whose test projects to list; by default, this one.
#>
[CmdletBinding()]
param(
    [string] $Repository = (Join-Path $PSScriptRoot '..' '..')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-Projects([string] $folder, [string] $filter) {
    if (Test-Path $folder) {
        Get-ChildItem -Path $folder -Recurse -File -Filter $filter |
            Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
    }
}

$projects = @(Get-Projects (Join-Path $Repository 'tests') '*.csproj' | Sort-Object FullName)

foreach ($project in $projects) {
    $build = dotnet build $project.FullName --configuration Release -p:RestoreLockedMode=true -nodeReuse:false 2>&1
    if ($LASTEXITCODE -ne 0) {
        $build | Write-Host
        throw "The build of $($project.BaseName) failed with exit code $LASTEXITCODE."
    }

    # A project with no test yet lists none, with exit code 8.
    $listing = @(dotnet run --project $project.FullName --configuration Release --no-build -- --list-tests json 2>&1)
    if ($LASTEXITCODE -notin 0, 8) {
        $listing | Write-Host
        throw "The test listing of $($project.BaseName) failed with exit code $LASTEXITCODE."
    }

    $start = [array]::FindIndex([string[]]$listing, [Predicate[string]] { param($line) $line -ceq '{' })
    if ($start -lt 0) {
        $listing | Write-Host
        throw "The test listing of $($project.BaseName) holds no JSON document."
    }

    $document = ($listing | Select-Object -Skip $start) -join "`n" | ConvertFrom-Json
    foreach ($test in $document.tests) {
        $traits = if ($test.PSObject.Properties['traits']) { @($test.traits) } else { @() }
        $categories = @($traits | Where-Object { $_.key -ceq 'Category' } | ForEach-Object { [string]$_.value })
        [pscustomobject]@{
            Project    = $project.BaseName
            Name       = "$($test.type.namespace).$($test.type.typeName).$($test.type.methodName)"
            Method     = [string]$test.type.methodName
            Categories = $categories
        }
    }
}
