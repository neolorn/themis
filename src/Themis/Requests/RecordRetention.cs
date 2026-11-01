using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Npgsql;

using NpgsqlTypes;

using Themis.Core;
using Themis.Operations;
using Themis.Types;

namespace Themis.Requests;

/// <summary>
/// Erases a person from the record (<c>01</c> APR-REQ-015), in a transaction of the library's
/// own: every reference to the person in the requests, assignments, decisions, delegations and
/// events becomes one pseudonym drawn for the call, <c>erased:</c> and 128 random bits in
/// base64url, so the person's rows stay linked to each other and to no one, and the reasons the
/// person wrote are cleared. The steps hold no person. Published versions are never changed.
/// </summary>
internal sealed class RecordRetention(
    ApprovalsReady ready,
    ApprovalsDatabase database,
    IOptions<ApprovalsOptions> options,
    ApprovalsMetrics metrics,
    TimeProvider clock) : IRecordRetention
{
    /// <summary>The prefix of a pseudonym.</summary>
    internal const string Prefix = "erased:";

    /// <summary>
    /// Whether the person takes part in a pending request: its requester or subject, holding an
    /// assignment of it or having decided on it, or the delegate, by a delegation that has not
    /// ended, of a person holding an open assignment of its current step.
    /// </summary>
    private const string Pending = """
        SELECT EXISTS (
            SELECT 1 FROM approvals.requests r
            WHERE r.status = 'pending' AND (
                r.requester = @person OR r.subject = @person
                OR EXISTS (SELECT 1 FROM approvals.assignments a WHERE a.request = r.id AND a.person = @person)
                OR EXISTS (SELECT 1 FROM approvals.decisions d WHERE d.request = r.id AND d.decider = @person)
                OR EXISTS (
                    SELECT 1 FROM approvals.assignments a
                    JOIN approvals.delegations g ON g.from_person = a.person
                    WHERE a.request = r.id AND a.step = r.step AND a.decision IS NULL
                        AND g.to_person = @person AND g.ended_at IS NULL AND g.ends > @now))) AS "Value"
        """;

    /// <summary>Locks, <c>FOR UPDATE</c> and in identifier order, each request the erasure changes: the requests whose own row, assignments, decisions or events name the person.</summary>
    private const string Locked = """
        SELECT r.id FROM approvals.requests r
        WHERE @person IN (r.requester, r.subject, r.closed_by, r.closed_for)
            OR EXISTS (SELECT 1 FROM approvals.assignments a WHERE a.request = r.id AND (a.person = @person OR a.via = @via))
            OR EXISTS (SELECT 1 FROM approvals.decisions d WHERE d.request = r.id AND @person IN (d.decider, d.acting_for))
            OR EXISTS (SELECT 1 FROM approvals.events e WHERE e.request = r.id AND (@person IN (e.actor, e.acting_for) OR @person = ANY(e.notify)))
        ORDER BY r.id
        FOR UPDATE
        """;

    /// <summary>The rewrites, each changing a row once however many of its columns name the person.</summary>
    private static readonly string[] Rewrites =
    [
        """
        UPDATE approvals.requests SET
            requester = CASE WHEN requester = @person THEN @alias ELSE requester END,
            subject = CASE WHEN subject = @person THEN @alias ELSE subject END,
            closed_by = CASE WHEN closed_by = @person THEN @alias ELSE closed_by END,
            closed_for = CASE WHEN closed_for = @person THEN @alias ELSE closed_for END,
            reason = CASE WHEN closed_by = @person THEN NULL ELSE reason END
        WHERE requester = @person OR subject = @person OR closed_by = @person OR closed_for = @person
        """,
        """
        UPDATE approvals.assignments SET
            person = CASE WHEN person = @person THEN @alias ELSE person END,
            via = CASE WHEN via = @via THEN @aliasvia ELSE via END
        WHERE person = @person OR via = @via
        """,
        """
        UPDATE approvals.decisions SET
            decider = CASE WHEN decider = @person THEN @alias ELSE decider END,
            acting_for = CASE WHEN acting_for = @person THEN @alias ELSE acting_for END,
            reason = CASE WHEN decider = @person THEN NULL ELSE reason END
        WHERE decider = @person OR acting_for = @person
        """,
        """
        UPDATE approvals.delegations SET
            from_person = CASE WHEN from_person = @person THEN @alias ELSE from_person END,
            to_person = CASE WHEN to_person = @person THEN @alias ELSE to_person END,
            created_by = CASE WHEN created_by = @person THEN @alias ELSE created_by END,
            ended_by = CASE WHEN ended_by = @person THEN @alias ELSE ended_by END
        WHERE from_person = @person OR to_person = @person OR created_by = @person OR ended_by = @person
        """,
        """
        UPDATE approvals.events SET
            actor = CASE WHEN actor = @person THEN @alias ELSE actor END,
            acting_for = CASE WHEN acting_for = @person THEN @alias ELSE acting_for END,
            notify = array_replace(notify, @person, @alias)
        WHERE actor = @person OR acting_for = @person OR @person = ANY(notify)
        """,
    ];

    public async ValueTask<Result<ErasureReceipt>> EraseAsync(string person, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(person);
        if (!TextRules.IsReference(person))
        {
            throw new ArgumentException("A person reference is outside its rule.", nameof(person));
        }

        ready.ThrowIfNotReady();
        try
        {
            ErasureReceipt receipt = await LibraryTransactions.RunAsync(database, options.Value, (context, cancel) => EraseInAsync(context, person, cancel), cancellationToken).ConfigureAwait(false);
            if (receipt.Changed > 0)
            {
                metrics.Erasures.Add(1);
            }

            return Result.Success(receipt);
        }
        catch (Refusal refusal)
        {
            return Result.Fail<ErasureReceipt>(refusal.Failure);
        }
    }

    /// <summary>Whether <paramref name="definition"/> names <paramref name="person"/>: as a fallback, a watcher, an approver or a step's escalation.</summary>
    internal static bool Names(ApprovalTypeDefinition definition, string person)
    {
        bool Is(ApproverReference? approver) => approver is ApproverReference.Person { Reference: var reference } && string.Equals(reference, person, StringComparison.Ordinal);
        return string.Equals(definition.Fallback, person, StringComparison.Ordinal)
            || definition.Watchers.Any(Is)
            || definition.Policies.SelectMany(policy => policy.Steps).Any(step =>
                string.Equals(step.Fallback, person, StringComparison.Ordinal) || step.Approvers.Any(Is) || Is(step.Deadline?.EscalateTo));
    }

    private static string Pseudonym()
    {
        return Prefix + Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));
    }

    /// <summary>The versions that name the person, the newest of a type first refused, and the older ones answered.</summary>
    private static async Task<List<TypeVersionReference>> KeptVersionsAsync(ApprovalsContext context, string person, CancellationToken cancellationToken)
    {
        List<StoredVersion> versions = await context.Versions.AsNoTracking().OrderBy(row => row.Key).ThenBy(row => row.Version).ToListAsync(cancellationToken).ConfigureAwait(false);
        List<TypeVersionReference> kept = [];
        foreach (IGrouping<string, StoredVersion> type in versions.GroupBy(row => row.Key, StringComparer.Ordinal))
        {
            int newest = type.Max(row => row.Version);
            foreach (StoredVersion version in type.Where(row => Names(CanonicalForm.Read(Encoding.UTF8.GetBytes(row.Definition)), person)))
            {
                if (version.Version == newest)
                {
                    throw Refusal.Of(Codes.ErasureTypeVersion, "type", type.Key);
                }

                kept.Add(new TypeVersionReference { Type = version.Key, Version = version.Version });
            }
        }

        return kept;
    }

    private async Task<ErasureReceipt> EraseInAsync(ApprovalsContext context, string person, CancellationToken cancellationToken)
    {
        if (!await RetentionGrant.HeldForErasureAsync(context, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"{Codes.RetentionNotGranted}: the application's role holds no DELETE on the request and delegation tables, or no UPDATE on their person and reason columns; grant it the role {RetentionGrant.Role}.");
        }

        await PersonLock.TakeAsync(context, person, cancellationToken).ConfigureAwait(false);
        await context.Database.ExecuteSqlRawAsync(Locked, [new NpgsqlParameter("person", person), new NpgsqlParameter("via", CanonicalForm.WriteApprover(new ApproverReference.Person(person)))], cancellationToken).ConfigureAwait(false);
        bool pending = await context.Database
            .SqlQueryRaw<bool>(Pending, new NpgsqlParameter("person", person), new NpgsqlParameter("now", NpgsqlDbType.TimestampTz) { Value = clock.GetUtcNow().UtcDateTime })
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
        if (pending)
        {
            throw Refusal.Of(Codes.ErasurePending);
        }

        List<TypeVersionReference> kept = await KeptVersionsAsync(context, person, cancellationToken).ConfigureAwait(false);
        string alias = Pseudonym();
        string via = CanonicalForm.WriteApprover(new ApproverReference.Person(person));
        string aliasVia = CanonicalForm.WriteApprover(new ApproverReference.Person(alias));
        int changed = 0;
        foreach (string rewrite in Rewrites)
        {
            changed += await context.Database.ExecuteSqlRawAsync(
                rewrite,
                [new NpgsqlParameter("person", person), new NpgsqlParameter("alias", alias), new NpgsqlParameter("via", via), new NpgsqlParameter("aliasvia", aliasVia)],
                cancellationToken).ConfigureAwait(false);
        }

        return new ErasureReceipt { Changed = changed, KeptVersions = kept };
    }
}