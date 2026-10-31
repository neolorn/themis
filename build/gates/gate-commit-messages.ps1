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
    section, under "Added" while no release, final or release candidate, is tagged. No commit
    changes or deletes an existing PublicAPI.Shipped.txt or build/gates/release-baseline.json,
    and moving one deletes it; adding one is no change. The release commit, "chore: release V" with V X.Y.Z or X.Y.Z-rc.N,
    is the exception: it changes no file but each PublicAPI.Shipped.txt, to its parent's entries
    and those of the PublicAPI.Unshipped.txt beside it less those marked *REMOVED*, each
    PublicAPI.Unshipped.txt, to "#nullable enable" alone, the release baseline, CHANGELOG.md,
    whose Unreleased entries move under "## [V] - YYYY-MM-DD" (the commit's date in UTC) below
    an empty Unreleased, and RELEASE-NOTES.md, which gains the section "## V"; only docs:
    commits changing nothing outside docs/ follow it, and a first line naming any other
    release fails.
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
# A release is a final release X.Y.Z or a release candidate X.Y.Z-rc.N, N from 1 (T-020).
$versionPattern = '(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-rc\.[1-9]\d*)?'
$releaseTagPattern = "^v$versionPattern$"
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

# PublicAPI.Shipped.txt and the release baseline change only in a release commit, which
# Get-ReleaseCommitProblems checks instead. With renames off, a move is the deletion of the
# file it moves.
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

# The release commit of 05 APR-ENG-011: "chore: release V", and nothing else that names a release.
$releaseHeaderPattern = "^chore: release (?<version>$versionPattern)$"
$releaseClaimPattern = "^(?<type>$types)(\([^)]*\))?!?: release v?\d"
$releaseOutsideTheRule = 'the first line names a release outside "chore: release V", V being X.Y.Z or X.Y.Z-rc.N with N from 1'

# The lines of the file at path in commit, the blank lines at its end left out, or none where it does not hold the file.
function Get-FileLines([string] $commit, [string] $path) {
    & git -C $Repository cat-file -e "${commit}:$path" 2>$null
    if ($LASTEXITCODE -ne 0) { return $null }
    $lines = @((Invoke-Git show "${commit}:$path") | ForEach-Object { $_.TrimEnd("`r") })
    while ($lines.Count -gt 0 -and $lines[-1] -eq '') { $lines = @($lines | Select-Object -SkipLast 1) }
    , [string[]] $lines
}

function Test-SameLines([string[]] $actual, [string[]] $expected) {
    $null -ne $actual -and $actual.Count -eq $expected.Count -and @(for ($index = 0; $index -lt $expected.Count; $index++) { if ($actual[$index] -cne $expected[$index]) { $index } }).Count -eq 0
}

function Test-ReleaseCommitFile([string] $path) {
    $path -cin 'CHANGELOG.md', 'RELEASE-NOTES.md', 'build/gates/release-baseline.json' -or ($path -split '/')[-1] -cin 'PublicAPI.Shipped.txt', 'PublicAPI.Unshipped.txt'
}

# A release commit changes no file but its own, each as its value gives. The release baseline's
# content is the release's own set, which the release gate reads and checks (03 APR-LIB-006).
function Get-ReleaseCommitProblems([string] $commit, [string] $parent, [string] $version) {
    foreach ($change in @(Invoke-Git diff --name-only --no-renames $parent $commit)) {
        if (-not (Test-ReleaseCommitFile $change)) { "it changes $change, which a release commit does not change" }
    }

    $listed = @(Invoke-Git ls-tree -r --name-only $parent) + @(Invoke-Git ls-tree -r --name-only $commit)
    $folders = @($listed | Where-Object { ($_ -split '/')[-1] -cin 'PublicAPI.Shipped.txt', 'PublicAPI.Unshipped.txt' } | ForEach-Object { $_.Substring(0, $_.LastIndexOf('/') + 1) } | Sort-Object -Unique -CaseSensitive)
    foreach ($folder in $folders) {
        $before = @((Get-FileLines $parent "${folder}PublicAPI.Shipped.txt") | Where-Object { $_ -and $_ -cne '#nullable enable' })
        $unshipped = @((Get-FileLines $parent "${folder}PublicAPI.Unshipped.txt") | Where-Object { $_ -and $_ -cne '#nullable enable' })
        $removed = @($unshipped | Where-Object { $_.StartsWith('*REMOVED*', [StringComparison]::Ordinal) } | ForEach-Object { $_.Substring('*REMOVED*'.Length) })
        $entries = [System.Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal)
        foreach ($entry in @($before) + @($unshipped | Where-Object { -not $_.StartsWith('*REMOVED*', [StringComparison]::Ordinal) })) { [void] $entries.Add($entry) }
        foreach ($entry in $removed) { [void] $entries.Remove($entry) }

        if (-not (Test-SameLines (Get-FileLines $commit "${folder}PublicAPI.Shipped.txt") (@('#nullable enable') + @($entries)))) {
            "${folder}PublicAPI.Shipped.txt is not its parent's entries and those of PublicAPI.Unshipped.txt beside it, less those it marks *REMOVED*, in ordinal order after #nullable enable"
        }
        if (-not (Test-SameLines (Get-FileLines $commit "${folder}PublicAPI.Unshipped.txt") @('#nullable enable'))) {
            "${folder}PublicAPI.Unshipped.txt holds more than #nullable enable"
        }
    }

    $date = ([DateTimeOffset]::Parse((Invoke-Git log -1 --format=%aI $commit), [Globalization.CultureInfo]::InvariantCulture)).UtcDateTime.ToString('yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
    $heading = "## [$version] - $date"
    $changelog = Get-FileLines $parent 'CHANGELOG.md'
    $unreleased = if ($null -eq $changelog) { -1 } else { [Array]::IndexOf($changelog, '## [Unreleased]') }
    $expected = if ($unreleased -lt 0) { $null } else { @($changelog[0..$unreleased]) + @('', $heading) + @($changelog | Select-Object -Skip ($unreleased + 1)) }
    if ($null -eq $expected -or -not (Test-SameLines (Get-FileLines $commit 'CHANGELOG.md') $expected)) {
        "CHANGELOG.md does not move the Unreleased entries under ""$heading"" below an empty ""## [Unreleased]"""
    }

    if (@((Get-FileLines $commit 'RELEASE-NOTES.md') | Where-Object { $_ -ceq "## $version" }).Count -ne 1) {
        "RELEASE-NOTES.md has no section headed ""## $version"""
    }
}

# In its pull request, only docs: commits that change nothing outside docs/ follow a release
# commit, so the tag on it carries everything the pull request built.
function Test-DocsOnly([string] $commit, [string] $parent, [string] $message) {
    $match = [regex]::Match(($message -split "`n")[0], $headerPattern)
    $match.Success -and $match.Groups['type'].Value -ceq 'docs' -and @(Invoke-Git diff --name-only --no-renames $parent $commit | Where-Object { -not $_.StartsWith('docs/', [StringComparison]::Ordinal) }).Count -eq 0
}

$range = if ($Base) { "$Base..$Head" } else { $Head }
$commits = @(Invoke-Git rev-list --reverse $range)
$failures = 0
$checked = 0
$released = $null

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
    $subject = ($message -split "`n")[0]
    $release = [regex]::Match($subject, $releaseHeaderPattern)
    if ($null -ne $released -and -not (Test-DocsOnly $commit $parent $message)) {
        $problems += "it follows the release commit of $released and is not a docs: commit changing only docs/"
    }

    if ($release.Success) {
        $released = $release.Groups['version'].Value
        $problems += @(Get-ReleaseCommitProblems $commit $parent $released)
        $problems += @(Get-ReleasedFileProblems $commit $parent | Where-Object { $_ -clike 'it deletes *' })
    }
    else {
        if ($subject -cmatch $releaseClaimPattern) { $problems += $releaseOutsideTheRule }
        $problems += @(Get-ReleasedFileProblems $commit $parent)
    }

    foreach ($problem in $problems) {
        Write-Host "$($commit.Substring(0, 12)) ""$subject"": $problem."
        $failures++
    }
}

if ($failures -gt 0) {
    throw "$failures problem(s) in the commits of $range."
}

Write-Host "$checked commit(s) of $range follow the commit-message, changelog and released-file rule."
