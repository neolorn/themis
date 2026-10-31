using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Themis.Core;

namespace Themis.Requests;

/// <summary>
/// The calls into the host's directory a move makes, inside its transaction, each bounded by
/// <c>Themis:Timeout</c> (<c>03</c> APR-LIB-005): one that throws or does not answer within it
/// refuses the move with <c>approvals.directory.unavailable</c>, and a person reference outside
/// its rule is a fault of the host's code.
/// </summary>
internal sealed class DirectoryCalls(IApproverDirectory directory, TimeSpan timeout, TimeProvider clock)
{
    /// <summary>The persons holding <paramref name="permission"/> for <paramref name="query"/>.</summary>
    internal async Task<IReadOnlyList<string>> ResolveAsync(string permission, ApproverQuery query, CancellationToken cancellationToken)
    {
        IReadOnlyList<string>? persons = await CallAsync(() => directory.ResolveAsync(permission, query, cancellationToken).AsTask(), cancellationToken).ConfigureAwait(false);
        if (persons is null)
        {
            throw new InvalidOperationException("The approver directory (IApproverDirectory) answered no list of persons.");
        }

        foreach (string person in persons)
        {
            if (!TextRules.IsReference(person))
            {
                throw new InvalidOperationException("The approver directory (IApproverDirectory) answered a person reference outside its rule.");
            }
        }

        return persons;
    }

    /// <summary>Whether the directory answers <paramref name="check"/>'s person as able to approve.</summary>
    internal Task<bool> CanApproveAsync(ApproverCheck check, CancellationToken cancellationToken)
    {
        return CallAsync(() => directory.CanApproveAsync(check, cancellationToken).AsTask(), cancellationToken);
    }

    private async Task<T> CallAsync<T>(Func<Task<T>> call, CancellationToken cancellationToken)
    {
        try
        {
            return await call().WaitAsync(timeout, clock, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            throw Refusal.Of(Codes.DirectoryUnavailable);
        }
    }
}