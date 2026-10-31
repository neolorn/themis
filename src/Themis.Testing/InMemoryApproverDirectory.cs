using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Themis.Core;

namespace Themis.Testing;

/// <summary>
/// An <see cref="IApproverDirectory"/> a test fills: the persons granted each permission, in
/// the order granted, and the persons blocked (<c>03</c> APR-LIB-007). It is safe to use from
/// several threads.
/// </summary>
public sealed class InMemoryApproverDirectory : IApproverDirectory
{
    private readonly Lock gate = new();

    private readonly Dictionary<string, List<string>> granted = new(StringComparer.Ordinal);

    private readonly HashSet<string> blocked = new(StringComparer.Ordinal);

    private readonly List<ApproverCheck> checks = [];

    private Exception? failure;

    /// <summary>Every check asked, in the order asked.</summary>
    public IReadOnlyList<ApproverCheck> Checks
    {
        get
        {
            lock (gate)
            {
                return [.. checks];
            }
        }
    }

    /// <summary>Grants <paramref name="permission"/> to <paramref name="person"/>, after the persons it is granted to already.</summary>
    /// <param name="permission">The permission's name.</param>
    /// <param name="person">The person reference.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public void Grant(string permission, string person)
    {
        ArgumentNullException.ThrowIfNull(permission);
        ArgumentNullException.ThrowIfNull(person);
        lock (gate)
        {
            if (!granted.TryGetValue(permission, out List<string>? persons))
            {
                persons = [];
                granted.Add(permission, persons);
            }

            if (!persons.Contains(person, StringComparer.Ordinal))
            {
                persons.Add(person);
            }
        }
    }

    /// <summary>Revokes <paramref name="permission"/> from <paramref name="person"/>.</summary>
    /// <param name="permission">The permission's name.</param>
    /// <param name="person">The person reference.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public void Revoke(string permission, string person)
    {
        ArgumentNullException.ThrowIfNull(permission);
        ArgumentNullException.ThrowIfNull(person);
        lock (gate)
        {
            if (granted.TryGetValue(permission, out List<string>? persons))
            {
                persons.Remove(person);
            }
        }
    }

    /// <summary>Blocks <paramref name="person"/>: every check of them answers false.</summary>
    /// <param name="person">The person reference.</param>
    /// <exception cref="ArgumentNullException"><paramref name="person"/> is null.</exception>
    public void Block(string person)
    {
        ArgumentNullException.ThrowIfNull(person);
        lock (gate)
        {
            blocked.Add(person);
        }
    }

    /// <summary>Unblocks <paramref name="person"/>.</summary>
    /// <param name="person">The person reference.</param>
    /// <exception cref="ArgumentNullException"><paramref name="person"/> is null.</exception>
    public void Unblock(string person)
    {
        ArgumentNullException.ThrowIfNull(person);
        lock (gate)
        {
            blocked.Remove(person);
        }
    }

    /// <summary>Makes every call throw <paramref name="failure"/> while it is set; none sets the directory answering again.</summary>
    /// <param name="failure">The exception every call throws, or none.</param>
    public void FailWith(Exception? failure)
    {
        lock (gate)
        {
            this.failure = failure;
        }
    }

    /// <summary>Answers the persons granted <paramref name="permission"/>, in the order granted.</summary>
    /// <param name="permission">The permission's name.</param>
    /// <param name="query">The request and step asked for.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The persons.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public ValueTask<IReadOnlyList<string>> ResolveAsync(string permission, ApproverQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(permission);
        ArgumentNullException.ThrowIfNull(query);
        lock (gate)
        {
            ThrowIfFailing();
            return ValueTask.FromResult<IReadOnlyList<string>>(granted.TryGetValue(permission, out List<string>? persons) ? [.. persons] : []);
        }
    }

    /// <summary>
    /// Answers false for a blocked person, and otherwise true where <see cref="ApproverCheck.Via"/>
    /// is a person naming them, or a permission they hold, or where the check acts for another
    /// (a delegate needs no grant of their own).
    /// </summary>
    /// <param name="check">The check.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Whether the person may approve.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="check"/> is null.</exception>
    public ValueTask<bool> CanApproveAsync(ApproverCheck check, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(check);
        lock (gate)
        {
            checks.Add(check);
            ThrowIfFailing();
            bool able = !blocked.Contains(check.Person) && (check.ActingFor is not null || check.Via switch
            {
                ApproverReference.Person person => person.Reference == check.Person,
                ApproverReference.Permission permission => granted.TryGetValue(permission.Name, out List<string>? persons) && persons.Contains(check.Person, StringComparer.Ordinal),
                _ => false,
            });
            return ValueTask.FromResult(able);
        }
    }

    private void ThrowIfFailing()
    {
        if (failure is not null)
        {
            throw failure;
        }
    }
}