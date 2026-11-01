using System;
using System.Threading;
using System.Threading.Tasks;

namespace Themis.Core;

/// <summary>
/// Erases a person from the record (<c>01</c> APR-REQ-015), each call in a transaction of its
/// own. Each call made before the host's start has run the library's startup checks throws
/// <see cref="InvalidOperationException"/>, and an argument outside its rule throws
/// <see cref="ArgumentException"/>, a fault of the calling code.
/// </summary>
public interface IRecordRetention
{
    /// <summary>
    /// Replaces every reference to <paramref name="person"/> in the requests, assignments,
    /// decisions, delegations and events with one pseudonym drawn for the call, and clears the
    /// reasons the person wrote, at once.
    /// </summary>
    /// <param name="person">The person reference.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>
    /// The number of rows changed, 0 where nothing names the person, and the older versions that
    /// still name them; or, the first that holds, <c>approvals.retention.notgranted</c> where the
    /// session lacks the retention grant, <c>approvals.erasure.pending</c> where a pending request names
    /// the person or the person is the delegate of one of its open assignees,
    /// <c>approvals.erasure.typeversion</c>, naming the type, where the newest version of a type
    /// names the person.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="person"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="person"/> is outside its rule.</exception>
    /// <exception cref="InvalidOperationException">The startup checks have not run.</exception>
    ValueTask<Result<ErasureReceipt>> EraseAsync(string person, CancellationToken cancellationToken);
}