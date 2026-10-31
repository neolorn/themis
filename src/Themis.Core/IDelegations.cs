using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Themis.Core;

/// <summary>
/// Records, ends and lists delegations (<c>01</c> APR-REQ-008), each call in a transaction of
/// its own. Each call made before the host's start has run the library's startup checks throws
/// <see cref="InvalidOperationException"/>, and an argument outside its rule throws
/// <see cref="ArgumentException"/>, a fault of the calling code.
/// </summary>
public interface IDelegations
{
    /// <summary>Records <paramref name="delegation"/> and writes its <c>delegationcreated</c> event naming the delegate.</summary>
    /// <param name="delegation">The delegation.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The delegation's identifier; or <c>approvals.delegation.invalid</c>, naming the member, where the delegate is the lender, the end is not after the start or not after now, or the period passes 365 days.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="delegation"/> is null.</exception>
    /// <exception cref="ArgumentException">A person reference or a type key is outside its rule, or more than 64 types are named.</exception>
    /// <exception cref="InvalidOperationException">The startup checks have not run.</exception>
    ValueTask<Result<DelegationId>> CreateAsync(DelegationDefinition delegation, CancellationToken cancellationToken);

    /// <summary>Ends a live delegation now, recording <paramref name="actor"/> and the instant.</summary>
    /// <param name="delegation">The delegation.</param>
    /// <param name="actor">The person the host says ended it.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A success; or <c>approvals.delegation.unknown</c> where there is no such delegation, <c>approvals.delegation.ended</c> where it has ended.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="actor"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="actor"/> is outside its rule.</exception>
    /// <exception cref="InvalidOperationException">The startup checks have not run.</exception>
    ValueTask<Result> EndAsync(DelegationId delegation, string actor, CancellationToken cancellationToken);

    /// <summary>Lists the delegations from and to <paramref name="person"/> that have not ended, by their start.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The delegations.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="person"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="person"/> is outside its rule.</exception>
    /// <exception cref="InvalidOperationException">The startup checks have not run.</exception>
    ValueTask<IReadOnlyList<DelegationRecord>> ListAsync(string person, CancellationToken cancellationToken);
}