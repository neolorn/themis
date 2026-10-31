using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Themis.Operations;

namespace Themis.Requests;

/// <summary>
/// Runs an operation on a host's open transaction (<c>03</c> APR-LIB-008): on its connection,
/// under a savepoint taken before the first statement, released where the operation succeeds
/// and rolled back to where it fails, so nothing of a refused operation stays on the
/// transaction. The library never commits, rolls back or runs the transaction again.
/// </summary>
internal static class HostTransactions
{
    private const string Savepoint = "themis_operation";

    /// <summary>The host's transaction as an open <c>NpgsqlTransaction</c>, or an <see cref="ArgumentException"/>.</summary>
    internal static NpgsqlTransaction Check(DbTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        if (transaction is not NpgsqlTransaction host)
        {
            throw new ArgumentException("The transaction is not an NpgsqlTransaction.", nameof(transaction));
        }

        if (host.Connection is null)
        {
            throw new ArgumentException("The transaction has ended.", nameof(transaction));
        }

        return host;
    }

    /// <summary>Runs <paramref name="work"/> on <paramref name="transaction"/> under a savepoint.</summary>
    internal static async Task<T> RunAsync<T>(NpgsqlTransaction transaction, Func<ApprovalsContext, CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        NpgsqlConnection connection = transaction.Connection ?? throw new ArgumentException("The transaction has ended.", nameof(transaction));
        ApprovalsContext context = ApprovalsContext.OnConnection(connection);
        await using (context.ConfigureAwait(false))
        {
            await context.Database.UseTransactionAsync(transaction, cancellationToken).ConfigureAwait(false);
            try
            {
                await transaction.SaveAsync(Savepoint, cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException ended)
            {
                throw new ArgumentException("The transaction has ended.", nameof(transaction), ended);
            }

            T result;
            try
            {
                result = await work(context, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(Savepoint, CancellationToken.None).ConfigureAwait(false);
                throw;
            }

            await transaction.ReleaseAsync(Savepoint, cancellationToken).ConfigureAwait(false);
            return result;
        }
    }
}