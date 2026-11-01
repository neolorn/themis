using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Npgsql;

using Themis.Core;
using Themis.Operations;

namespace Themis.Requests;

/// <summary>
/// Records, ends and lists delegations (<c>01</c> APR-REQ-008), each call in a transaction of
/// the library's own. A delegation is live from its start until its end or until it is ended
/// earlier; an ending is recorded with its actor and instant, and no delegation is deleted.
/// </summary>
internal sealed class DelegationService(
    ApprovalsReady ready,
    ApprovalsDatabase database,
    IOptions<ApprovalsOptions> options,
    TimeProvider clock) : IDelegations
{
    /// <summary>The most types a delegation names (<c>01</c> APR-REQ-008).</summary>
    private const int MostTypes = 64;

    /// <summary>The longest period of a delegation (<c>01</c> APR-REQ-008).</summary>
    private static readonly TimeSpan LongestPeriod = TimeSpan.FromDays(365);

    public async ValueTask<Result<DelegationId>> CreateAsync(DelegationDefinition delegation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delegation);
        CheckReference(delegation.From, nameof(delegation));
        CheckReference(delegation.To, nameof(delegation));
        CheckReference(delegation.CreatedBy, nameof(delegation));
        if (delegation.Types is null || delegation.Types.Count > MostTypes || !delegation.Types.All(TextRules.IsName))
        {
            throw new ArgumentException("The types are none, more than 64, or hold a key that is not a name.", nameof(delegation));
        }

        ready.ThrowIfNotReady();
        DateTimeOffset now = clock.GetUtcNow();
        if (string.Equals(delegation.To, delegation.From, StringComparison.Ordinal))
        {
            return Invalid("to");
        }

        if (delegation.Ends <= delegation.Starts || delegation.Ends <= now || delegation.Ends - delegation.Starts > LongestPeriod)
        {
            return Invalid("ends");
        }

        Guid id = Guid.CreateVersion7(now);
        await LibraryTransactions.RunAsync(
            database,
            options.Value,
            async (context, cancel) =>
            {
                await PersonLock.ShareAsync(context, [delegation.From, delegation.To, delegation.CreatedBy], cancel).ConfigureAwait(false);
                context.Delegations.Add(new DelegationRow
                {
                    Id = id,
                    From = delegation.From,
                    To = delegation.To,
                    Starts = delegation.Starts,
                    Ends = delegation.Ends,
                    Types = [.. delegation.Types.Distinct(StringComparer.Ordinal)],
                    CreatedBy = delegation.CreatedBy,
                    CreatedAt = now,
                });
                await context.SaveChangesAsync(cancel).ConfigureAwait(false);
                context.Events.Add(new EventRow
                {
                    Id = Guid.CreateVersion7(now),
                    Sequence = 1,
                    Kind = EventKind.DelegationCreated,
                    At = now,
                    Actor = delegation.CreatedBy,
                    Delegation = id,
                    Notify = [delegation.To],
                });
                await context.SaveChangesAsync(cancel).ConfigureAwait(false);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
        return Result.Success(new DelegationId(id));
    }

    public async ValueTask<Result> EndAsync(DelegationId delegation, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        CheckReference(actor, nameof(actor));
        ready.ThrowIfNotReady();
        try
        {
            await LibraryTransactions.RunAsync(
                database,
                options.Value,
                async (context, cancel) =>
                {
                    DelegationRow row = await context.Delegations
                        .FromSqlRaw("SELECT * FROM approvals.delegations WHERE id = @id FOR UPDATE", new NpgsqlParameter("id", delegation.Value))
                        .SingleOrDefaultAsync(cancel)
                        .ConfigureAwait(false) ?? throw Refusal.Of(Codes.DelegationUnknown);
                    DateTimeOffset now = clock.GetUtcNow();
                    if (row.EndedAt is not null || row.Ends <= now)
                    {
                        throw Refusal.Of(Codes.DelegationEnded);
                    }

                    row.EndedAt = now;
                    row.EndedBy = actor;
                    await context.SaveChangesAsync(cancel).ConfigureAwait(false);
                    return true;
                },
                cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Refusal refusal)
        {
            return Result.Fail(refusal.Failure);
        }
    }

    public async ValueTask<IReadOnlyList<DelegationRecord>> ListAsync(string person, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(person);
        CheckReference(person, nameof(person));
        ready.ThrowIfNotReady();
        DateTimeOffset now = clock.GetUtcNow();
        ApprovalsContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            List<DelegationRow> rows = await context.Delegations.AsNoTracking()
                .Where(row => (row.From == person || row.To == person) && row.EndedAt == null && row.Ends > now)
                .OrderBy(row => row.Starts)
                .ThenBy(row => row.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            return [.. rows.Select(Record)];
        }
    }

    /// <summary>The record of a delegation's row.</summary>
    internal static DelegationRecord Record(DelegationRow row)
    {
        return new DelegationRecord
        {
            Delegation = new DelegationId(row.Id),
            From = row.From,
            To = row.To,
            Starts = row.Starts,
            Ends = row.Ends,
            Types = row.Types,
            CreatedBy = row.CreatedBy,
            CreatedAt = row.CreatedAt,
            EndedAt = row.EndedAt,
            EndedBy = row.EndedBy,
        };
    }

    private static Result<DelegationId> Invalid(string member)
    {
        return Result.Fail<DelegationId>(Refusal.Of(Codes.DelegationInvalid, "member", member).Failure);
    }

    private static void CheckReference(string? person, string argument)
    {
        if (!TextRules.IsReference(person))
        {
            throw new ArgumentException("A person reference is outside its rule.", argument);
        }
    }
}