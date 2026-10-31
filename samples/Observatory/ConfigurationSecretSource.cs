using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;

using Themis.Core;

namespace Observatory;

/// <summary>
/// Answers the library's secret <c>connection</c> from the host's connection string
/// <c>Approvals</c>, as one version, and no other secret. A host that rotates its secrets
/// answers each version its store holds instead.
/// </summary>
internal sealed class ConfigurationSecretSource(IConfiguration configuration) : ISecretSource
{
    private const string Connection = "connection";

    public ValueTask<SecretVersions?> ReadAsync(string name, CancellationToken cancellationToken)
    {
        string? connection = string.Equals(name, Connection, StringComparison.Ordinal) ? configuration.GetConnectionString("Approvals") : null;
        return ValueTask.FromResult(connection is null ? null : new SecretVersions { Versions = [new SecretVersion { Version = 1, Value = Encoding.UTF8.GetBytes(connection) }] });
    }
}