using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Npgsql;

using Themis.Core;

namespace Themis.Operations;

/// <summary>Reads the secret <c>connection</c> when the host starts (<c>04</c> APR-OPS-008).</summary>
internal static class SecretCheck
{
    internal const string Connection = "connection";

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// The highest version of <c>connection</c>, held as bytes; a secret the source does not
    /// answer stops the start as unavailable, one that is not UTF-8 or not a connection string
    /// Npgsql reads as invalid, each naming it in <c>details.key</c>. The read is bounded by
    /// <paramref name="timeout"/>, <c>Themis:Timeout</c>, past which the source has not answered it
    /// (<c>03</c> APR-LIB-005).
    /// </summary>
    internal static async Task<ReadOnlyMemory<byte>> ConnectionAsync(ISecretSource secrets, TimeSpan timeout, TimeProvider clock, CancellationToken cancellationToken)
    {
        SecretVersions? read;
        try
        {
            read = await secrets.ReadAsync(Connection, cancellationToken).AsTask().WaitAsync(timeout, clock, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            throw ApprovalsStartup.Failure(Codes.SecretUnavailable, "key", Connection);
        }

        if (read is not { Versions.Count: > 0 })
        {
            throw ApprovalsStartup.Failure(Codes.SecretUnavailable, "key", Connection);
        }

        ReadOnlyMemory<byte> highest = read.Versions.MaxBy(version => version.Version)!.Value;
        if (!Reads(highest.Span))
        {
            throw ApprovalsStartup.Failure(Codes.SecretInvalid, "key", Connection);
        }

        return highest;
    }

    /// <summary>The connection string <paramref name="value"/> holds, which <see cref="Reads"/> has accepted.</summary>
    internal static string Text(ReadOnlySpan<byte> value)
    {
        return StrictUtf8.GetString(value);
    }

    /// <summary>
    /// Whether <paramref name="value"/> is UTF-8 and a connection string Npgsql reads: one it
    /// builds a data source from, which refuses a string naming no host; nothing is connected.
    /// </summary>
    private static bool Reads(ReadOnlySpan<byte> value)
    {
        try
        {
            using NpgsqlDataSource source = new NpgsqlDataSourceBuilder(StrictUtf8.GetString(value)).Build();
            return true;
        }
        catch (Exception invalid) when (invalid is DecoderFallbackException or ArgumentException or FormatException or InvalidCastException or OverflowException)
        {
            // A value Npgsql does not read is outside the secret's form; the caller answers it as such.
            return false;
        }
    }
}