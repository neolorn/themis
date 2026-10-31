#Requires -Version 7.0
<#
.SYNOPSIS
    Checks that each package's public API files are up to date with its public surface (03 APR-LIB-002).
.DESCRIPTION
    Each project under src/ holds PublicAPI.Shipped.txt and PublicAPI.Unshipped.txt, and its
    build reports none of the public API analysers' diagnostics. A repository with no project
    under src/ fails, since the gate would check nothing.
.PARAMETER Repository
    The repository to check; by default, this one.
#>
[CmdletBinding()]
param(
    [string] $Repository = (Join-Path $PSScriptRoot '..' '..')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projects = @(
    if (Test-Path (Join-Path $Repository 'src')) {
        Get-ChildItem -Path (Join-Path $Repository 'src') -Directory | Sort-Object Name
    }
)
if ($projects.Count -eq 0) {
    Write-Host 'src/ holds no project, so the gate would check nothing.'
    throw 'The gate checked nothing.'
}

$failures = [System.Collections.Generic.List[string]]::new()

foreach ($project in $projects) {
    foreach ($file in 'PublicAPI.Shipped.txt', 'PublicAPI.Unshipped.txt') {
        $path = Join-Path $project.FullName $file
        if (-not (Test-Path $path)) {
            $failures.Add("src/$($project.Name) has no $file")
        }
        elseif ((Get-Content -Path $path -TotalCount 1) -ne '#nullable enable') {
            $failures.Add("src/$($project.Name)/$file does not begin with #nullable enable")
        }
    }

    $output = dotnet build (Join-Path $project.FullName "$($project.Name).csproj") --configuration Release -p:RestoreLockedMode=true -nodeReuse:false 2>&1
    $code = $LASTEXITCODE
    $diagnostics = @($output | Select-String -Pattern '\bRS00\d\d\b' | ForEach-Object { $_.Line.Trim() } | Sort-Object -Unique)
    foreach ($diagnostic in $diagnostics) {
        $failures.Add($diagnostic)
    }
    if ($code -ne 0 -and $diagnostics.Count -eq 0) {
        $output | Write-Host
        $failures.Add("src/$($project.Name) does not build (exit code $code)")
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Host $_ }
    throw "$($failures.Count) public surface problem(s)."
}

Write-Host "Each package's public API files are up to date."
