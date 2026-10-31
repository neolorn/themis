#Requires -Version 7.0
<#
.SYNOPSIS
    Runs the unit, integration and contract tests, each kind on its own by its Category trait (05 APR-ENG-008).
.DESCRIPTION
    A test of tests/ that carries no Category trait, more than one, or a value other than
    the three kinds fails the gate before any test runs, since no run, or more than one,
    would take it. A run that finds no test of its kind passes: Microsoft.Testing.Platform's
    exit code 8, for no test run, is accepted. A repository with no test project fails, since
    the gate would run nothing.
.PARAMETER Repository
    The repository whose tests to run; by default, this one.
#>
[CmdletBinding()]
param(
    [string] $Repository = (Join-Path $PSScriptRoot '..' '..')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$kinds = 'unit', 'integration', 'contract'

# The integration tests run the scanner this step leaves under tmp/tools/.
& (Join-Path $PSScriptRoot 'fetch-secret-scanner.ps1') | Out-Null

$projects = @(
    if (Test-Path (Join-Path $Repository 'tests')) {
        Get-ChildItem -Path (Join-Path $Repository 'tests') -Filter '*.csproj' -Recurse -File |
            Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
            Sort-Object FullName
    }
)
if ($projects.Count -eq 0) {
    Write-Host 'tests/ holds no test project, so the gate would run nothing.'
    throw 'The gate ran nothing.'
}

$tests = @(& (Join-Path $PSScriptRoot 'list-tests.ps1') -Repository $Repository)

$unsorted = [System.Collections.Generic.List[string]]::new()
foreach ($test in $tests | Where-Object { $_.Project -in $projects.BaseName } | Sort-Object Name -Unique) {
    $categories = @($test.Categories)
    if ($categories.Count -eq 0) {
        $unsorted.Add("$($test.Name) carries no Category trait")
    }
    elseif ($categories.Count -gt 1) {
        $unsorted.Add("$($test.Name) carries more than one Category trait: $($categories -join ', ')")
    }
    elseif ($categories[0] -cnotin $kinds) {
        $unsorted.Add("$($test.Name) carries the Category $($categories[0]), which is none of $($kinds -join ', ')")
    }
}
if ($unsorted.Count -gt 0) {
    $unsorted | ForEach-Object { Write-Host "$_." }
    throw "$($unsorted.Count) test(s) no run of a kind would take once."
}

$failures = [System.Collections.Generic.List[string]]::new()
foreach ($kind in $kinds) {
    foreach ($project in $projects) {
        Write-Host "== $kind tests of $($project.BaseName)"
        dotnet run --project $project.FullName --configuration Release --no-build -- --filter-trait "Category=$kind" --ignore-exit-code 8
        if ($LASTEXITCODE -ne 0) {
            $failures.Add("$kind tests of $($project.BaseName) (exit code $LASTEXITCODE)")
        }
    }
}

if ($failures.Count -gt 0) {
    throw "Tests failed: $($failures -join '; ')."
}
