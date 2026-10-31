using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Themis.Core;

namespace Themis.Testing;

/// <summary>An <see cref="ISecretSource"/> answering the secrets a test sets, and nothing until then (<c>03</c> APR-LIB-007).</summary>
public sealed class FixedSecretSource : ISecretSource
{
    private readonly Lock gate = new();

    private readonly Dictionary<string, SecretVersions> secrets = new(StringComparer.Ordinal);

    /// <summary>Sets the secret <paramref name="name"/> to <paramref name="versions"/>.</summary>
    /// <param name="name">The secret's name, such as <c>connection</c>.</param>
    /// <param name="versions">Its versions.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public void SetSecret(string name, SecretVersions versions)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(versions);
        lock (gate)
        {
            secrets[name] = versions;
        }
    }

    /// <summary>Removes the secret <paramref name="name"/>.</summary>
    /// <param name="name">The secret's name.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    public void RemoveSecret(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        lock (gate)
        {
            secrets.Remove(name);
        }
    }

    /// <summary>Answers the secret <paramref name="name"/>, or none where it is not set.</summary>
    /// <param name="name">The secret's name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Its versions, or none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    public ValueTask<SecretVersions?> ReadAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        lock (gate)
        {
            return ValueTask.FromResult(secrets.TryGetValue(name, out SecretVersions? versions) ? versions : null);
        }
    }
}