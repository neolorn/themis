using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Themis.Core;

namespace Observatory;

/// <summary>
/// The observatory's directory, a small table of its own: who holds each permission. A step's
/// approver named by a permission is any of its holders, one named as a person is that person,
/// and a delegate deciding for a lender needs no permission of their own.
/// </summary>
internal sealed class PermissionTable : IApproverDirectory
{
    internal const string Schedule = "telescope.schedule";

    internal const string Direct = "telescope.direct";

    internal const string Deputise = "telescope.deputise";

    internal const string Lend = "instruments.lend";

    internal const string Keep = "instruments.keep";

    private static readonly FrozenDictionary<string, string[]> Holders = new Dictionary<string, string[]>
    {
        [Schedule] = ["scheduler-1", "scheduler-2"],
        [Direct] = ["director"],
        [Deputise] = ["deputy-director"],
        [Lend] = ["desk-1", "desk-2"],
        [Keep] = ["custodian"],
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public ValueTask<IReadOnlyList<string>> ResolveAsync(string permission, ApproverQuery query, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult<IReadOnlyList<string>>(Holders.TryGetValue(permission, out string[]? holders) ? holders : []);
    }

    public ValueTask<bool> CanApproveAsync(ApproverCheck check, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(check);
        bool can = check.ActingFor is not null || check.Via switch
        {
            ApproverReference.Person person => string.Equals(person.Reference, check.Person, StringComparison.Ordinal),
            ApproverReference.Permission permission => Holders.TryGetValue(permission.Name, out string[]? holders) && holders.Contains(check.Person, StringComparer.Ordinal),
            _ => false,
        };
        return ValueTask.FromResult(can);
    }
}