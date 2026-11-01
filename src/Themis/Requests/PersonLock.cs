using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Themis.Operations;

namespace Themis.Requests;

/// <summary>
/// A person reference's lock (<c>01</c> APR-REQ-015; <c>04</c> APR-OPS-003): an erasure takes
/// it exclusive, and a create, an assignment or a delegation takes it shared for each person it
/// writes, so no reference is written while it is erased.
/// </summary>
internal static class PersonLock
{
    /// <summary>The text the lock's hash begins with.</summary>
    internal const string Text = "approvals-person";

    /// <summary>The lock's key: the first 8 bytes, read as a signed big-endian integer, of the SHA-256 of <see cref="Text"/>, a zero byte and <paramref name="person"/>.</summary>
    internal static long Key(string person)
    {
        byte[] text = [.. Encoding.UTF8.GetBytes(Text), 0, .. Encoding.UTF8.GetBytes(person)];
        return BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(text));
    }

    /// <summary>Takes the lock of <paramref name="person"/> exclusive in the transaction <paramref name="context"/> runs, waiting while another transaction holds it.</summary>
    internal static async Task TakeAsync(ApprovalsContext context, string person, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(@lock)", [new NpgsqlParameter("lock", Key(person))], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Takes the lock of each of <paramref name="persons"/> shared in the transaction <paramref name="context"/> runs, in the order of their keys.</summary>
    internal static async Task ShareAsync(ApprovalsContext context, IEnumerable<string?> persons, CancellationToken cancellationToken)
    {
        long[] keys = [.. persons.OfType<string>().Select(Key).Distinct().Order()];
        if (keys.Length > 0)
        {
            await context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock_shared(key) FROM unnest(@keys) WITH ORDINALITY AS taken(key, place) ORDER BY place", [new NpgsqlParameter("keys", keys)], cancellationToken).ConfigureAwait(false);
        }
    }
}