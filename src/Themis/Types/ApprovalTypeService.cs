using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Themis.Core;
using Themis.Operations;

namespace Themis.Types;

/// <summary>
/// Publishes, reads and retires approval types on the library's own connection, each publish
/// and retirement in one transaction under the type's lock (<c>02</c> APR-TYPE-001; <c>04</c>
/// APR-OPS-003; <c>05</c> APR-ENG-005).
/// </summary>
internal sealed partial class ApprovalTypeService(
    ApprovalsReady ready,
    ApprovalsDatabase database,
    IOptions<ApprovalsOptions> options,
    TimeProvider clock,
    ILogger<ApprovalTypeService> logger) : IApprovalTypes
{
    private static readonly Result<ApprovalTypeVersion> UnknownVersion = Result.Fail<ApprovalTypeVersion>(new Failure { Code = Codes.TypeUnknown });

    private static readonly Result UnknownType = Result.Fail(new Failure { Code = Codes.TypeUnknown });

    public async ValueTask<Result<PublishedVersion>> PublishAsync(ApprovalTypeDefinition definition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ready.ThrowIfNotReady();

        if (DefinitionRules.Invalid(definition, out byte[] form) is { } member)
        {
            Dictionary<string, string> details = new(StringComparer.Ordinal) { ["member"] = member };
            return Result.Fail<PublishedVersion>(new Failure { Code = Codes.TypeInvalid, Details = details.ToFrozenDictionary(StringComparer.Ordinal) });
        }

        byte[] hash = CanonicalForm.Hash(form);
        string text = Encoding.UTF8.GetString(form);
        string key = definition.Key;

        PublishedVersion published = await LibraryTransactions.RunAsync(
            database,
            options.Value,
            async (context, cancel) =>
            {
                await TypeLock.TakeAsync(context, key, cancel).ConfigureAwait(false);
                TypeState? type = await context.Types.SingleOrDefaultAsync(row => row.Key == key, cancel).ConfigureAwait(false);
                var latest = await context.Versions
                    .Where(row => row.Key == key)
                    .OrderByDescending(row => row.Version)
                    .Select(row => new { row.Version, row.Hash })
                    .FirstOrDefaultAsync(cancel)
                    .ConfigureAwait(false);

                if (type is null)
                {
                    type = new TypeState { Key = key };
                    context.Types.Add(type);
                }

                type.Retired = false;
                bool created = latest is null || !latest.Hash.AsSpan().SequenceEqual(hash);
                int version = latest is null ? 1 : created ? latest.Version + 1 : latest.Version;
                if (created)
                {
                    context.Versions.Add(new StoredVersion { Key = key, Version = version, Definition = text, Hash = hash, PublishedAt = clock.GetUtcNow() });
                }

                await context.SaveChangesAsync(cancel).ConfigureAwait(false);
                return new PublishedVersion { Key = key, Version = version, Created = created };
            },
            cancellationToken).ConfigureAwait(false);

        if (published.Created)
        {
            Created(published.Key, published.Version);
        }
        else
        {
            Unchanged(published.Key, published.Version);
        }

        return Result.Success(published);
    }

    public async ValueTask<Result<ApprovalTypeVersion>> ReadAsync(string key, int? version, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ready.ThrowIfNotReady();

        if (!TextRules.IsName(key))
        {
            return UnknownVersion;
        }

        ApprovalsContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            var found = await (
                from stored in context.Versions
                join type in context.Types on stored.Key equals type.Key
                where stored.Key == key && (version == null || stored.Version == version)
                orderby stored.Version descending
                select new { stored.Version, stored.Definition, stored.PublishedAt, type.Retired })
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (found is null)
            {
                return UnknownVersion;
            }

            return Result.Success(new ApprovalTypeVersion
            {
                Key = key,
                Version = found.Version,
                Definition = CanonicalForm.Read(Encoding.UTF8.GetBytes(found.Definition)),
                PublishedAt = found.PublishedAt,
                Retired = found.Retired,
            });
        }
    }

    public async ValueTask<Result> RetireAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ready.ThrowIfNotReady();

        if (!TextRules.IsName(key))
        {
            return UnknownType;
        }

        int? retired = await LibraryTransactions.RunAsync(
            database,
            options.Value,
            async (context, cancel) =>
            {
                await TypeLock.TakeAsync(context, key, cancel).ConfigureAwait(false);
                TypeState? type = await context.Types.SingleOrDefaultAsync(row => row.Key == key, cancel).ConfigureAwait(false);
                if (type is null)
                {
                    return (int?)null;
                }

                type.Retired = true;
                await context.SaveChangesAsync(cancel).ConfigureAwait(false);
                return await context.Versions.Where(row => row.Key == key).MaxAsync(row => row.Version, cancel).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        if (retired is not { } latest)
        {
            return UnknownType;
        }

        Retired(key, latest);
        return Result.Success();
    }

    [LoggerMessage(EventId = 1, EventName = "TypeVersionCreated", Level = LogLevel.Information, Message = "Approval type {Type} published: version {Version} created.")]
    private partial void Created(string type, int version);

    [LoggerMessage(EventId = 2, EventName = "TypeVersionUnchanged", Level = LogLevel.Information, Message = "Approval type {Type} published: version {Version} unchanged, its definition equal to the latest.")]
    private partial void Unchanged(string type, int version);

    [LoggerMessage(EventId = 3, EventName = "TypeRetired", Level = LogLevel.Information, Message = "Approval type {Type} retired at version {Version}.")]
    private partial void Retired(string type, int version);
}