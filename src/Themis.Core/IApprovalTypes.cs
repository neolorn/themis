using System;
using System.Threading;
using System.Threading.Tasks;

namespace Themis.Core;

/// <summary>
/// Publishes, reads and retires a host's approval types, each as numbered, immutable versions
/// (<c>02</c> APR-TYPE-001). Each call made before the host's start has run the library's
/// startup checks throws <see cref="InvalidOperationException"/>.
/// </summary>
public interface IApprovalTypes
{
    /// <summary>
    /// Publishes <paramref name="definition"/> under the type's lock: a definition equal to the
    /// type's latest version changes nothing, and any other makes the next version, which new
    /// requests use. A retired type is made current again.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>
    /// The type's key, the version's number and whether this call made it; or the failure
    /// <c>approvals.type.invalid</c>, its detail <c>member</c> the path of the first member
    /// outside its rule (<c>02</c> APR-TYPE-002), nothing written.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The startup checks have not run.</exception>
    ValueTask<Result<PublishedVersion>> PublishAsync(ApprovalTypeDefinition definition, CancellationToken cancellationToken);

    /// <summary>Reads version <paramref name="version"/> of the type <paramref name="key"/>, or its latest where none is named.</summary>
    /// <param name="key">The type's key.</param>
    /// <param name="version">The version's number, or none for the latest.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The version, with the type's state now; or the failure <c>approvals.type.unknown</c> where there is no such type or version.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">The startup checks have not run.</exception>
    ValueTask<Result<ApprovalTypeVersion>> ReadAsync(string key, int? version, CancellationToken cancellationToken);

    /// <summary>
    /// Retires the type <paramref name="key"/> under its lock: a new request of it is refused,
    /// its open requests go on under their versions, and a later publish makes it current again.
    /// </summary>
    /// <param name="key">The type's key.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A success, the type retired; or the failure <c>approvals.type.unknown</c> where there is no such type.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">The startup checks have not run.</exception>
    ValueTask<Result> RetireAsync(string key, CancellationToken cancellationToken);
}