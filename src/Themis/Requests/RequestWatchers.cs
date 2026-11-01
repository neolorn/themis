using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using Themis.Core;
using Themis.Operations;

namespace Themis.Requests;

/// <summary>
/// The one internal query <c>Themis.Endpoints</c> asks, for the watcher check of its request
/// route: whether one of a request's events names a person in its <c>Notify</c> (<c>03</c>
/// APR-LIB-004; <c>05</c> APR-ENG-001). No public member answers it.
/// </summary>
internal sealed class RequestWatchers(ApprovalsReady ready, ApprovalsDatabase database)
{
    /// <summary>Whether an event of <paramref name="request"/> names <paramref name="person"/> in its <c>Notify</c>; false for no such request.</summary>
    internal async ValueTask<bool> NamesAsync(RequestId request, string person, CancellationToken cancellationToken)
    {
        ready.ThrowIfNotReady();
        ApprovalsContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            return await context.Events.AsNoTracking().AnyAsync(found => found.Request == request.Value && found.Notify.Contains(person), cancellationToken).ConfigureAwait(false);
        }
    }
}