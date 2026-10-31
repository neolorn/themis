using System.Threading;
using System.Threading.Tasks;

namespace Themis.Core;

/// <summary>
/// Is told each event, by the <c>observer</c> job alone, outside any transaction, in order for
/// each request and at least once (<c>01</c> APR-REQ-014).
/// </summary>
public interface IApprovalObserver
{
    /// <summary>Observes <paramref name="approvalEvent"/>.</summary>
    /// <param name="approvalEvent">The event.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>
    /// A task whose completion takes the event; one that throws, or runs past
    /// <c>Themis:Timeout</c>, leaves it to be told again.
    /// </returns>
    ValueTask ObserveAsync(ApprovalEvent approvalEvent, CancellationToken cancellationToken);
}