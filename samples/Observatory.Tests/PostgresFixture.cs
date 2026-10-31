using System;
using System.Data.Common;
using System.Threading.Tasks;

using DotNet.Testcontainers.Containers;

using Testcontainers.PostgreSql;

using Xunit;

namespace Observatory.Tests;

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

    /// <summary>Makes an empty database and answers the superuser's connection string to it.</summary>
    internal async Task<string> NewDatabaseAsync()
    {
        string name = "sample_" + Guid.NewGuid().ToString("N");
        ExecResult run = await container.ExecScriptAsync($"\\set ON_ERROR_STOP on\nCREATE DATABASE {name};", TestContext.Current.CancellationToken);
        Assert.True(run.ExitCode == 0, run.Stderr);
        DbConnectionStringBuilder connection = new() { ConnectionString = container.GetConnectionString() };
        connection["Database"] = name;
        return connection.ConnectionString;
    }
}