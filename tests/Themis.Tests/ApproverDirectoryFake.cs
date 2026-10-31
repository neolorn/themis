using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Themis.Core;

namespace Themis.Tests;

/// <summary>A directory that answers no one for every permission and refuses every check.</summary>
internal sealed class ApproverDirectoryFake : IApproverDirectory
{
    public ValueTask<IReadOnlyList<string>> ResolveAsync(string permission, ApproverQuery query, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult<IReadOnlyList<string>>([]);
    }

    public ValueTask<bool> CanApproveAsync(ApproverCheck check, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(false);
    }
}