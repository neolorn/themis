using System;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace Themis.Operations;

/// <summary>
/// The library's own connection to the host's database, opened at the start from the secret
/// <c>connection</c>, on which its own work runs (<c>04</c> APR-OPS-003).
/// </summary>
internal sealed class ApprovalsDatabase : IAsyncDisposable
{
    private NpgsqlDataSource? source;

    private DbContextOptions<ApprovalsContext>? options;

    /// <summary>The data source, once the start has opened it.</summary>
    internal NpgsqlDataSource Source => source ?? throw new InvalidOperationException("The library's database is used before the startup checks opened it.");

    /// <summary>Opens the data source on the connection string <paramref name="connection"/> holds, which is never kept but by the data source.</summary>
    internal void Open(ReadOnlyMemory<byte> connection)
    {
        source = new NpgsqlDataSourceBuilder(SecretCheck.Text(connection.Span)).Build();
        options = ApprovalsContext.Options(source);
    }

    /// <summary>A new context on the data source.</summary>
    internal ApprovalsContext Context()
    {
        return new ApprovalsContext(options ?? throw new InvalidOperationException("The library's database is used before the startup checks opened it."));
    }

    public ValueTask DisposeAsync()
    {
        return source?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}