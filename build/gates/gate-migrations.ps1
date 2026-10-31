#Requires -Version 7.0
<#
.SYNOPSIS
    Applies the release's migrations twice in a row to an empty database, the second applying nothing (05 APR-ENG-012; 04 APR-OPS-002).
.DESCRIPTION
    Starts a PostgreSQL container of Image with a password made for the run, makes an empty
    database in it, and runs themis migrate on it twice. The gate fails when either run does
    not exit 0, when the first leaves no migration in approvals.__migrations_history, or when
    the second changes that history or the schema the first left. The container is removed
    however the gate ends.
.PARAMETER Repository
    The repository whose Themis.Cli to run; by default, this one.
.PARAMETER Image
    The PostgreSQL image to start.
.PARAMETER NoBuild
    Runs Themis.Cli as last built in Release instead of building it first.
#>
[CmdletBinding()]
param(
    [string] $Repository = (Join-Path $PSScriptRoot '..' '..'),
    [string] $Image = 'postgres:16.15',
    [switch] $NoBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$project = Join-Path $Repository 'src' 'Themis.Cli' 'Themis.Cli.csproj'
if (-not (Test-Path $project)) {
    throw 'src/Themis.Cli/Themis.Cli.csproj is missing.'
}

& docker version --format '{{.Server.Version}}' | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw 'Docker is not available, so no database can be started for the migrations.'
}

if (-not $NoBuild) {
    dotnet build $project --configuration Release -p:RestoreLockedMode=true -nodeReuse:false
    if ($LASTEXITCODE -ne 0) {
        throw "Themis.Cli did not build (exit code $LASTEXITCODE)."
    }
}

function Invoke-Psql([string] $Container, [string] $Database, [string] $Statement) {
    $output = & docker exec $Container psql --username postgres --dbname $Database --no-align --tuples-only --set ON_ERROR_STOP=1 --command $Statement
    if ($LASTEXITCODE -ne 0) {
        throw "psql failed on $Database (exit code $LASTEXITCODE)."
    }
    return @($output)
}

function Get-State([string] $Container) {
    $history = Invoke-Psql $Container 'gate' 'SELECT "MigrationId" FROM approvals.__migrations_history ORDER BY 1'
    $schema = & docker exec $Container pg_dump --username postgres --dbname gate --schema-only --restrict-key gate
    if ($LASTEXITCODE -ne 0) {
        throw "pg_dump failed (exit code $LASTEXITCODE)."
    }
    return [pscustomobject]@{ History = @($history | Where-Object { $_ }); Schema = ($schema -join "`n") }
}

function Invoke-Migrate([string] $Connection, [int] $Run) {
    $document = @{ connection = $Connection; applicationRoles = @() } | ConvertTo-Json -Compress
    $document | dotnet run --project $project --configuration Release --no-build --no-launch-profile -- migrate
    if ($LASTEXITCODE -ne 0) {
        throw "themis migrate's run $Run exited $LASTEXITCODE."
    }
}

$password = [Guid]::NewGuid().ToString('N')
$container = & docker run --detach --rm --env "POSTGRES_PASSWORD=$password" --publish '127.0.0.1::5432' $Image
if ($LASTEXITCODE -ne 0 -or -not $container) {
    throw "The container of $Image did not start."
}

try {
    $ready = $false
    for ($attempt = 0; $attempt -lt 120 -and -not $ready; $attempt++) {
        & docker exec $container pg_isready --host localhost --username postgres | Out-Null
        $ready = $LASTEXITCODE -eq 0
        if (-not $ready) { Start-Sleep -Milliseconds 500 }
    }
    if (-not $ready) {
        throw 'The database did not become ready within a minute.'
    }

    Invoke-Psql $container 'postgres' 'CREATE DATABASE gate' | Out-Null
    $port = ((& docker port $container '5432/tcp') | Select-Object -First 1).Split(':')[-1]
    $connection = "Host=127.0.0.1;Port=$port;Username=postgres;Password=$password;Database=gate"

    Invoke-Migrate $connection 1
    $first = Get-State $container
    if ($first.History.Count -eq 0) {
        throw 'The first run left no migration in approvals.__migrations_history.'
    }
    Write-Host "The first run applied $($first.History.Count) migration(s): $($first.History -join ', ')."

    Invoke-Migrate $connection 2
    $second = Get-State $container
    if (($second.History -join "`n") -cne ($first.History -join "`n")) {
        throw "The second run changed the history: $($second.History -join ', ')."
    }
    if ($second.Schema -cne $first.Schema) {
        throw 'The second run changed the schema the first left.'
    }
    Write-Host 'The second run applied nothing.'
}
finally {
    & docker rm --force $container | Out-Null
}
