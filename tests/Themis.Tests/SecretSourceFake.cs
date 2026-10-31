using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Themis.Core;

namespace Themis.Tests;

/// <summary>A secret source answering the secrets a test sets, and nothing else; every name read is kept.</summary>
internal sealed class SecretSourceFake : ISecretSource
{
    private readonly ConcurrentDictionary<string, SecretVersions> secrets = new(StringComparer.Ordinal);

    private readonly ConcurrentQueue<string> read = new();

    /// <summary>The names read, in order.</summary>
    internal string[] Read => [.. read];

    /// <summary>A source answering <c>connection</c> as <paramref name="connection"/>, its one version.</summary>
    internal static SecretSourceFake WithConnection(string connection)
    {
        SecretSourceFake source = new();
        source.Set("connection", Encoding.UTF8.GetBytes(connection));
        return source;
    }

    /// <summary>Sets <paramref name="name"/> to the versions of <paramref name="values"/>, numbered from 1.</summary>
    internal void Set(string name, params byte[][] values)
    {
        SecretVersion[] versions = new SecretVersion[values.Length];
        for (int index = 0; index < values.Length; index++)
        {
            versions[index] = new SecretVersion { Version = index + 1, Value = values[index] };
        }

        secrets[name] = new SecretVersions { Versions = versions };
    }

    /// <summary>Sets <paramref name="name"/> to <paramref name="versions"/> as given.</summary>
    internal void Set(string name, SecretVersions versions)
    {
        secrets[name] = versions;
    }

    public ValueTask<SecretVersions?> ReadAsync(string name, CancellationToken cancellationToken)
    {
        read.Enqueue(name);
        return ValueTask.FromResult(secrets.TryGetValue(name, out SecretVersions? versions) ? versions : null);
    }
}