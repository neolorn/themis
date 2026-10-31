using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Themis.Core;

/// <summary>
/// Answers who holds a permission and who may approve, from the host's current data; the
/// library calls it inside the transaction of the move that needs it, each call bounded by
/// <c>Themis:Timeout</c> (<c>03</c> APR-LIB-005).
/// </summary>
public interface IApproverDirectory
{
    /// <summary>Resolves the persons holding <paramref name="permission"/> for the request of <paramref name="query"/>.</summary>
    /// <param name="permission">The permission, a name.</param>
    /// <param name="query">The request the permission is resolved for.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The persons' references, each within <c>00</c> section 1.5; one outside it is a fault of the host's code.</returns>
    ValueTask<IReadOnlyList<string>> ResolveAsync(string permission, ApproverQuery query, CancellationToken cancellationToken);

    /// <summary>Answers whether the person of <paramref name="check"/> may approve as it says.</summary>
    /// <param name="check">The person, the reference that named them, the lender they act for, and the request.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Whether the person may approve.</returns>
    ValueTask<bool> CanApproveAsync(ApproverCheck check, CancellationToken cancellationToken);
}