using System;
using System.Threading.Tasks;

using DotNet.Testcontainers.Containers;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Testcontainers.PostgreSql;

using Themis.Operations;

using Xunit;

namespace Themis.Tests;

/// <summary>One PostgreSQL container for a test class, each test taking a database of its own in it.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:16.15").Build();

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await container.DisposeAsync();
    }

    /// <summary>Makes an empty database and answers its name and connection string.</summary>
    internal async Task<(string Name, string Connection)> NewDatabaseAsync()
    {
        string name = "test_" + Guid.NewGuid().ToString("N");
        await RunAsync($"CREATE DATABASE {name};");
        return (name, Connection(name));
    }

    /// <summary>Makes a database holding the release's migrations and answers its name and connection string.</summary>
    internal async Task<(string Name, string Connection)> NewMigratedDatabaseAsync()
    {
        (string name, string connection) = await NewDatabaseAsync();
        await using ApprovalsContext context = ApprovalsContext.Create(connection);
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        return (name, connection);
    }

    /// <summary>The superuser's connection string to <paramref name="database"/>.</summary>
    internal string Connection(string database)
    {
        return new NpgsqlConnectionStringBuilder(container.GetConnectionString()) { Database = database }.ConnectionString;
    }

    /// <summary>Runs <paramref name="script"/> through the container's <c>psql</c> as its superuser, in <paramref name="database"/> where given.</summary>
    internal async Task RunAsync(string script, string? database = null)
    {
        string connect = database is null ? string.Empty : $"\\connect {database}\n";
        ExecResult run = await container.ExecScriptAsync($"\\set ON_ERROR_STOP on\n{connect}{script}", TestContext.Current.CancellationToken);
        Assert.True(run.ExitCode == 0, run.Stderr);
    }
}