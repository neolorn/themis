using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

namespace Themis.Operations;

/// <summary>Checks at the start that the database holds every migration of the release (<c>04</c> APR-OPS-002).</summary>
internal static class SchemaCheck
{
    /// <summary>The release's migrations, in the order they apply.</summary>
    internal static IReadOnlyList<string> Migrations { get; } = ReadMigrations();

    /// <summary>The first of the release's migrations the database lacks, or none; a later release's migrations are accepted.</summary>
    internal static async Task<string?> MissingAsync(ApprovalsDatabase database, CancellationToken cancellationToken)
    {
        ApprovalsContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            HashSet<string> applied = [.. await context.Database.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false)];
            return Migrations.FirstOrDefault(migration => !applied.Contains(migration));
        }
    }

    private static string[] ReadMigrations()
    {
        using ApprovalsContext context = ApprovalsContext.Create("Host=localhost");
        return [.. context.Database.GetMigrations()];
    }
}