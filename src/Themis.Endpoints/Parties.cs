using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Themis.Core;
using Themis.Requests;

namespace Themis.Endpoints;

/// <summary>
/// Whether a person is a party to a request, to whom <c>GET requests/{id}</c> answers its record
/// (<c>03</c> APR-LIB-004): its requester or subject; an assignee of it, open or decided; a
/// delegate of one of its assignees through a delegation that covers its type and is live at
/// the call; or a watcher, whom one of its events names in <c>Notify</c>.
/// </summary>
internal static class Parties
{
    internal static async ValueTask<bool> IsPartyAsync(RequestRecord record, string person, IServiceProvider services, CancellationToken cancellationToken)
    {
        if (string.Equals(record.Requester, person, StringComparison.Ordinal) || string.Equals(record.Subject, person, StringComparison.Ordinal))
        {
            return true;
        }

        HashSet<string> assignees = new(record.Steps.SelectMany(step => step.Assignments).Select(assignment => assignment.Person), StringComparer.Ordinal);
        if (assignees.Contains(person))
        {
            return true;
        }

        DateTimeOffset now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        IReadOnlyList<DelegationRecord> delegations = await services.GetRequiredService<IDelegations>().ListAsync(person, cancellationToken).ConfigureAwait(false);
        if (delegations.Any(delegation => string.Equals(delegation.To, person, StringComparison.Ordinal) && assignees.Contains(delegation.From) && Covers(delegation, record.Type, now)))
        {
            return true;
        }

        return await services.GetRequiredService<RequestWatchers>().NamesAsync(record.Request, person, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Whether <paramref name="delegation"/> covers <paramref name="type"/> and is live at <paramref name="now"/>: started, not ended, and before its end.</summary>
    private static bool Covers(DelegationRecord delegation, string type, DateTimeOffset now)
    {
        return delegation.EndedAt is null
            && delegation.Starts <= now
            && now < delegation.Ends
            && (delegation.Types.Count == 0 || delegation.Types.Contains(type, StringComparer.Ordinal));
    }
}