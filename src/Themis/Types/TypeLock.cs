using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Themis.Operations;

namespace Themis.Types;

/// <summary>
/// A type's lock, the first lock a publish or a retirement takes in its transaction, so two of
/// them on one key run one after the other (<c>04</c> APR-OPS-003).
/// </summary>
internal static class TypeLock
{
    /// <summary>The text the lock's hash begins with (<c>06</c> section 7).</summary>
    internal const string Text = "approvals-type";

    /// <summary>The lock's key: the first 8 bytes, read as a signed big-endian integer, of the SHA-256 of <see cref="Text"/>, a zero byte and <paramref name="key"/>.</summary>
    internal static long Key(string key)
    {
        byte[] text = [.. Encoding.UTF8.GetBytes(Text), 0, .. Encoding.UTF8.GetBytes(key)];
        return BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(text));
    }

    /// <summary>Takes the lock of <paramref name="key"/> in the transaction <paramref name="context"/> runs, waiting while another transaction holds it.</summary>
    internal static async Task TakeAsync(ApprovalsContext context, string key, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(@lock)", [new NpgsqlParameter("lock", Key(key))], cancellationToken).ConfigureAwait(false);
    }
}