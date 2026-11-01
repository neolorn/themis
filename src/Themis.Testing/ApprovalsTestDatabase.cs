using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Themis.Operations;

namespace Themis.Testing;

/// <summary>Migrates a test's database as <c>themis migrate</c> does (<c>03</c> APR-LIB-007; <c>04</c> APR-OPS-002).</summary>
public static class ApprovalsTestDatabase
{
    /// <summary>Applies the release's pending migrations to the database <paramref name="connection"/> names, granting no role.</summary>
    /// <param name="connection">The connection string of the test's database.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes once the migrations are applied.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is null.</exception>
    /// <exception cref="InvalidOperationException">A migration fails, or the database cannot be reached; the message carries the code of <c>06</c> section 1 and its details.</exception>
    public static async ValueTask MigrateAsync(string connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        MigrationFailure? failure = await SchemaMigration.ApplyAsync(connection, [], [], cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            string details = string.Join(", ", failure.Details.Select(detail => string.Create(CultureInfo.InvariantCulture, $"{detail.Key} {detail.Value}")));
            throw new InvalidOperationException($"The test database's migration failed: {failure.Code} ({details}).");
        }
    }
}