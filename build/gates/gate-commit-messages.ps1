#Requires -Version 7.0
<#
.SYNOPSIS
    Checks each commit's message, changelog line and released files (05 APR-ENG-011).
.DESCRIPTION
    Every commit after Base up to Head, save the merge commits the platform writes (committed
    by GitHub, noreply@github.com), has a Conventional Commits header of an allowed type and
    scope, a description that begins with a lower-case letter and has no full stop, and a first
    line of at most 72 characters; a body, if any, of dash fragments of at most 72 characters;
    and a footer, if any, of "Implements:" and "BREAKING CHANGE:" lines alone. A commit of type
    feat, fix or perf, or marked as breaking, adds its line under the changelog's Unreleased
    section, under "Added" while no release is tagged. No commit changes or deletes an existing
    PublicAPI.Shipped.txt or build/gates/release-baseline.json, and moving one deletes it;
    adding one is no change.
.PARAMETER Repository
    The repository to check; by default, this one.
.PARAMETER Base
    The commit after which to check; none checks the whole history up to Head.
.PARAMETER Head
    The last commit to check.
#>
[CmdletBinding()]
param(
    [string] $Repository = (Join-Path $PSScriptRoot '..' '..'),
    [string] $Base = '',
    [string] $Head = 'HEAD'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$types = 'feat|fix|refactor|perf|test|docs|build|ci|chore'
$scopes = 'core|engine|testing|cli|sample'
$headerPattern = "^(?<type>$types)(\((?<scope>$scopes)\))?(?<breaking>!)?: (?<description>\S(.*\S)?)$"
$footerPattern = '^(Implements: APR-[A-Z]+-\d{3}(, APR-[A-Z]+-\d{3})*|BREAKING CHANGE: \S.*)$'
$sectionTypes = 'Added|Changed|Deprecated|Removed|Fixed|Security'
$releaseTagPattern = '^v\d+\.\d+\.\d+$'
$platformCommitter = 'noreply@github.com'
$emptyTree = '4b825dc642cb6eb9a060e54bf8d69288fbee4904'

function Invoke-Git {
    $output = & git -C $Repository @args
    if ($LASTEXITCODE -ne 0) { throw "git $args failed." }
    $output
}

function Get-MessageProblems([string] $message) {
    $lines = @($message -split "`n" | ForEach-Object { $_.TrimEnd("`r") })
    while ($lines.Count -gt 0 -and $lines[-1] -eq '') { $lines = @($lines | Select-Object -SkipLast 1) }

    $header = $lines[0]
    $match = [regex]::Match($header, $headerPattern)
    if (-not $match.Success) {
        'the first line is not "type(scope): description" of an allowed type and scope'
        return
    }

    # A name the description mentions keeps its own spelling: "add the Npgsql connection".
    $description = $match.Groups['description'].Value
    if ($header.Length -gt 72) { 'the first line is longer than 72 characters' }
    if ($description -cnotmatch '^\p{Ll}') { 'the description does not begin with a lower-case letter' }
    if ($description.EndsWith('.')) { 'the description ends with a full stop' }

    $breaking = $match.Groups['breaking'].Success
    if ($lines.Count -gt 1) {
        if ($lines[1] -ne '') { 'the first line is not followed by a blank line' }

        $paragraphs = [System.Collections.Generic.List[string[]]]::new()
        $current = [System.Collections.Generic.List[string]]::new()
        foreach ($line in ($lines | Select-Object -Skip 2)) {
            if ($line -eq '') {
                if ($current.Count -gt 0) { $paragraphs.Add($current.ToArray()); $current.Clear() }
            }
            else { $current.Add($line) }
        }
        if ($current.Count -gt 0) { $paragraphs.Add($current.ToArray()) }

        if ($paragraphs.Count -gt 0) {
            $last = $paragraphs[$paragraphs.Count - 1]
            $isFooter = @($last | Where-Object { $_ -cnotmatch $footerPattern }).Count -eq 0
            if ($isFooter) {
                if (@($last | Where-Object { $_ -clike 'BREAKING CHANGE:*' }).Count -gt 0) { $breaking = $true }
                $paragraphs.RemoveAt($paragraphs.Count - 1)
            }

            foreach ($paragraph in $paragraphs) {
                foreach ($line in $paragraph) {
                    if ($line -notmatch '^- \S') { "the body line ""$line"" is not a dash fragment, and the footer holds only Implements: and BREAKING CHANGE: lines" }
                    elseif ($line.Length -gt 72) { "the body line ""$line"" is longer than 72 characters" }
                }
            }
        }
    }

    if ($match.Groups['type'].Value -in 'feat', 'fix', 'perf' -or $breaking) {
        [pscustomobject]@{ ChangelogRequired = $true }
    }
}

function Test-ChangelogLine([string] $commit) {
    $diff = @(& git -C $Repository show --format= --unified=0 --no-color $commit -- CHANGELOG.md)
    if ($LASTEXITCODE -ne 0) { throw "git show $commit failed." }

    $added = [System.Collections.Generic.List[int]]::new()
    $number = 0
    foreach ($line in $diff) {
        $hunk = [regex]::Match($line, '^@@ -\d+(,\d+)? \+(?<start>\d+)(,\d+)? @@')
        if ($hunk.Success) { $number = [int]$hunk.Groups['start'].Value; continue }
        if ($line.StartsWith('+++') -or $line.StartsWith('---')) { continue }
        if ($line.StartsWith('+')) {
            if ($line -match '^\+- \S') { $added.Add($number) }
            $number++
        }
        elseif (-not $line.StartsWith('-') -and $number -gt 0) { $number++ }
    }
    if ($added.Count -eq 0) { return $false }

    $changelog = @(& git -C $Repository show "${commit}:CHANGELOG.md")
    if ($LASTEXITCODE -ne 0) { return $false }
    $released = @(Invoke-Git tag --list --merged $commit | Where-Object { $_ -cmatch $releaseTagPattern }).Count -gt 0

    foreach ($lineNumber in $added) {
        $section = $null
        $subsection = $null
        for ($index = 0; $index -lt $lineNumber - 1; $index++) {
            $text = $changelog[$index]
            if ($text -match '^## ') { $section = $text; $subsection = $null }
            elseif ($text -match "^### (?<name>$sectionTypes)$") { $subsection = $Matches['name'] }
        }
        $underUnreleased = $section -eq '## [Unreleased]'
        $underSection = $null -ne $subsection -and ($released -or $subsection -eq 'Added')
        if ($underUnreleased -and $underSection) { return $true }
    }
    return $false
}

function Test-ReleasedFile([string] $path) {
    $path -ceq 'build/gates/release-baseline.json' -or ($path -split '/')[-1] -ceq 'PublicAPI.Shipped.txt'
}

# PublicAPI.Shipped.txt and the release baseline change only in a release commit, whose
# exemption comes with the release step (plan, phase 5). With renames off, a move is the
# deletion of the file it moves.
function Get-ReleasedFileProblems([string] $commit, [string] $parent) {
    $changes = @(Invoke-Git diff --name-status --no-renames $parent $commit)
    foreach ($change in $changes) {
        $fields = $change -split "`t"
        $status = $fields[0]
        $path = $fields[1]
        if ($status -eq 'D' -and (Test-ReleasedFile $path)) { "it deletes $path, which changes only in a release commit" }
        elseif ($status -in 'M', 'T' -and (Test-ReleasedFile $path)) { "it changes $path, which changes only in a release commit" }
    }
}

$range = if ($Base) { "$Base..$Head" } else { $Head }
$commits = @(Invoke-Git rev-list --reverse $range)
$failures = 0
$checked = 0

foreach ($commit in $commits) {
    $fields = (Invoke-Git log -1 --format='%P%x09%ce' $commit) -split "`t"
    $parents = @($fields[0] -split ' ' | Where-Object { $_ })
    if ($parents.Count -gt 1 -and $fields[1] -ceq $platformCommitter) { continue }
    $checked++

    $message = (Invoke-Git log -1 --format=%B $commit) -join "`n"
    $results = @(Get-MessageProblems $message)
    $problems = @($results | Where-Object { $_ -is [string] })
    if (@($results | Where-Object { $_ -isnot [string] }).Count -gt 0 -and -not (Test-ChangelogLine $commit)) {
        $problems += 'it changes behaviour and adds no line under the changelog''s Unreleased section'
    }

    $parent = if ($parents.Count -gt 0) { $parents[0] } else { $emptyTree }
    $problems += @(Get-ReleasedFileProblems $commit $parent)

    $subject = ($message -split "`n")[0]
    foreach ($problem in $problems) {
        Write-Host "$($commit.Substring(0, 12)) ""$subject"": $problem."
        $failures++
    }
}

if ($failures -gt 0) {
    throw "$failures problem(s) in the commits of $range."
}

Write-Host "$checked commit(s) of $range follow the commit-message, changelog and released-file rule."
