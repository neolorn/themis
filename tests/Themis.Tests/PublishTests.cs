using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Npgsql;

using Themis.Core;
using Themis.Types;

using Xunit;

namespace Themis.Tests;

/// <summary>Publishing and reading approval types against a real database in a container, each test on a database of its own (<c>02</c> APR-TYPE-001).</summary>
[Trait("Category", "integration")]
public sealed class PublishTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly ApprovalTypeDefinition Changed = Definitions.Valid.WithStep(0, 0, step => step with { Mode = ApprovalMode.All });

    [Fact]
    public async Task APR_TYPE_001_AC1_PublishingADefinitionTwiceMakesOneVersionAndChangingOneStepsModeMakesVersion2()
    {
        await using TypeHost host = await TypeHost.StartAsync(postgres);

        Result<PublishedVersion> first = await host.PublishAsync(Definitions.Valid);
        Result<PublishedVersion> again = await host.PublishAsync(Definitions.Valid);
        Result<PublishedVersion> changed = await host.PublishAsync(Changed);

        Assert.Equal(new PublishedVersion { Key = "purchase.order", Version = 1, Created = true }, first.Value);
        Assert.Equal(new PublishedVersion { Key = "purchase.order", Version = 1, Created = false }, again.Value);
        Assert.Equal(new PublishedVersion { Key = "purchase.order", Version = 2, Created = true }, changed.Value);
        Assert.Equal([1, 2], (await host.VersionsAsync()).Select(version => version.Version));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_TYPE_001_AC3_TwoPublishesOfTwoDifferentDefinitionsOfOneKeyAtOnceMakeVersions1And2()
    {
        await using TypeHost host = await TypeHost.StartAsync(postgres);
        await using TypeHost.HeldLock held = await host.HoldTypeLockAsync("purchase.order");

        Task<Result<PublishedVersion>> one = Task.Run(() => host.PublishAsync(Definitions.Valid));
        Task<Result<PublishedVersion>> other = Task.Run(() => host.PublishAsync(Changed));
        await held.WaitForWaitersAsync(2);
        Assert.False(one.IsCompleted || other.IsCompleted);
        await held.ReleaseAsync();
        Result<PublishedVersion>[] published = await Task.WhenAll(one, other);

        Assert.All(published, result => Assert.True(result.Value!.Created));
        Assert.Equal([1, 2], published.Select(result => result.Value!.Version).Order());
        IReadOnlyList<(string Key, int Version, string Definition, byte[] Hash)> versions = await host.VersionsAsync();
        Assert.Equal([1, 2], versions.Select(version => version.Version));
        Assert.NotEqual(versions[0].Hash, versions[1].Hash);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_ENG_005_AC1_TransactionAbortedWith40001IsRunAgainAtMostRetriesTimes()
    {
        await using TypeHost host = await TypeHost.StartAsync(postgres, new Dictionary<string, string?> { ["Themis:Database:Retries"] = "3" });
        await AbortVersionInsertsAsync(host.Database, 3);

        Result<PublishedVersion> published = await host.PublishAsync(Definitions.Valid);

        Assert.True(published.IsSuccess);
        Assert.Equal(4L, await AbortCallsAsync(host.Connection));
        await AbortVersionInsertsAsync(host.Database, 4);
        Exception aborted = await Assert.ThrowsAnyAsync<Exception>(() => host.PublishAsync(Changed));
        Assert.Equal(PostgresErrorCodes.SerializationFailure, Assert.IsType<PostgresException>(aborted.GetBaseException()).SqlState);
        Assert.Equal(4L, await AbortCallsAsync(host.Connection));
        Assert.Equal([1], (await host.VersionsAsync()).Select(version => version.Version));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_ENG_005_AC2_PublishedDefinitionReadBackHoldsTheExactBytesWhoseHashItsVersionKeeps()
    {
        await using TypeHost host = await TypeHost.StartAsync(postgres);
        await host.PublishAsync(Definitions.Valid);

        (_, _, string definition, byte[] hash) = Assert.Single(await host.VersionsAsync());
        Result<ApprovalTypeVersion> read = await host.ReadAsync("purchase.order", 1);

        byte[] kept = Encoding.UTF8.GetBytes(definition);
        Assert.Equal(CanonicalForm.Write(Definitions.Valid), kept);
        Assert.Equal(SHA256.HashData(kept), hash);
        Assert.Equal(kept, CanonicalForm.Write(read.Value!.Definition));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task APR_TYPE_002_AC1_EachMemberOutsideTheTypesRulesRefusesThePublishNamingItAndWritesNoVersion()
    {
        await AssertRefusedAsync(InvalidDefinitions.TypeRules);
    }

    [Fact]
    public async Task APR_TYPE_004_AC1_EachMemberOutsideAStepsRulesRefusesThePublishNamingIt()
    {
        await AssertRefusedAsync(InvalidDefinitions.StepRules);
    }

    [Fact]
    public async Task APR_TYPE_004_AC2_PublishRefusesAtLeastWithoutRequiredRequiredWithAnotherModeRequiredOutside2To16AndOneAboveTheDistinctPersonsNamed()
    {
        await AssertRefusedAsync(InvalidDefinitions.RequiredRules);
    }

    [Fact]
    public async Task APR_REQ_009_AC5_PublishRefusesAReminderUnderAnHourOneNotShorterThanTheDeadlineAFourthAndTwoAlike()
    {
        await AssertRefusedAsync(InvalidDefinitions.ReminderRules);
    }

    [Fact]
    public async Task APR_TYPE_005_AC2_EachConditionOutsideItsRuleRefusesThePublishNamingIt()
    {
        await AssertRefusedAsync(InvalidDefinitions.ConditionRules);
    }

    [Fact]
    public async Task PublishAsync_CanonicalFormOf64KiB_IsPublishedAsItsForm()
    {
        await using TypeHost host = await TypeHost.StartAsync(postgres);
        ApprovalTypeDefinition largest = Definitions.OfSize(65_536);

        Result<PublishedVersion> published = await host.PublishAsync(largest);

        Assert.Equal((1, true), (published.Value!.Version, published.Value.Created));
        Assert.Equal(65_536, Encoding.UTF8.GetByteCount(Assert.Single(await host.VersionsAsync()).Definition));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task PublishAsync_CanonicalFormOver64KiBAndAMemberOutsideItsRule_IsRefusedNamingTheMember()
    {
        await using TypeHost host = await TypeHost.StartAsync(postgres);

        ApprovalTypeDefinition definition = Definitions.OfSize(70_000) with { Fallback = string.Empty };
        Assert.True(CanonicalForm.Write(definition).Length > 65_536);

        Result<PublishedVersion> refused = await host.PublishAsync(definition);

        Assert.Equal(("approvals.type.invalid", "fallback"), (refused.Failure?.Code, refused.Failure?.Details.GetValueOrDefault("member")));
        Assert.Empty(await host.VersionsAsync());
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task ReadAsync_NoVersionNamed_AnswersTheLatestAndAVersionNamedAnswersIt()
    {
        await using TypeHost host = await TypeHost.StartAsync(postgres);
        await host.PublishAsync(Definitions.Valid);
        await host.PublishAsync(Changed);

        ApprovalTypeVersion latest = (await host.ReadAsync("purchase.order")).Value!;
        ApprovalTypeVersion first = (await host.ReadAsync("purchase.order", 1)).Value!;

        Assert.Equal(("purchase.order", 2, false), (latest.Key, latest.Version, latest.Retired));
        Assert.Equal(CanonicalForm.Write(Changed), CanonicalForm.Write(latest.Definition));
        Assert.Equal(1, first.Version);
        Assert.Equal(ApprovalMode.AnyOne, first.Definition.Policies[0].Steps[0].Mode);
        Assert.True(first.PublishedAt <= latest.PublishedAt);
        Assert.Equal(TimeSpan.Zero, latest.PublishedAt.Offset);
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task ReadAsync_NoSuchTypeOrVersion_AnswersTypeUnknown()
    {
        await using TypeHost host = await TypeHost.StartAsync(postgres);
        await host.PublishAsync(Definitions.Valid);

        Result<ApprovalTypeVersion>[] reads = [await host.ReadAsync("purchase.other"), await host.ReadAsync("purchase.order", 2), await host.ReadAsync("purchase.order", 0), await host.ReadAsync("Purchase Order")];

        Assert.All(reads, read => Assert.Equal(("approvals.type.unknown", 0), (read.Failure?.Code, read.Failure?.Details.Count)));
        host.AssertNoLogHoldsAValueUsed();
    }

    [Fact]
    public async Task PublishAsync_EachPublish_IsLoggedWithTheKeyAndTheVersion()
    {
        await using TypeHost host = await TypeHost.StartAsync(postgres);

        await host.PublishAsync(Definitions.Valid);
        await host.PublishAsync(Definitions.Valid);
        await host.PublishAsync(Changed);

        string[] published = [.. host.Logs.Entries.Where(entry => entry.Category == "Themis.Types.ApprovalTypeService").Select(entry => entry.Message)];
        Assert.Equal(3, published.Length);
        Assert.All(published, message => Assert.Contains("purchase.order", message, StringComparison.Ordinal));
        Assert.Matches(@"version 1\b.*\bcreated\b", published[0]);
        Assert.Matches(@"version 1\b.*\bunchanged\b", published[1]);
        Assert.Matches(@"version 2\b.*\bcreated\b", published[2]);
        host.AssertNoLogHoldsAValueUsed();
    }

    /// <summary>Makes the next <paramref name="aborts"/> inserts of a version abort with 40001, counting every insert tried from now on.</summary>
    private async Task AbortVersionInsertsAsync(string database, int aborts)
    {
        await postgres.RunAsync(
            $"""
            DROP TRIGGER IF EXISTS abort_versions ON approvals.versions;
            DROP SEQUENCE IF EXISTS public.version_inserts;
            CREATE SEQUENCE public.version_inserts;
            CREATE OR REPLACE FUNCTION public.abort_versions() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF nextval('public.version_inserts') <= {aborts} THEN
                    RAISE EXCEPTION 'serialization failure' USING ERRCODE = '40001';
                END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER abort_versions BEFORE INSERT ON approvals.versions FOR EACH ROW EXECUTE FUNCTION public.abort_versions();
            """,
            database);
    }

    private static async Task<long> AbortCallsAsync(string connection)
    {
        await using NpgsqlConnection database = new(connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand read = new("SELECT last_value FROM public.version_inserts", database);
        return (long)(await read.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private async Task AssertRefusedAsync(IReadOnlyDictionary<string, (ApprovalTypeDefinition Definition, string Member)> rules)
    {
        await using TypeHost host = await TypeHost.StartAsync(postgres);

        foreach ((string rule, (ApprovalTypeDefinition definition, string member)) in rules)
        {
            Result<PublishedVersion> refused = await host.PublishAsync(definition);

            Assert.Equal((rule, "approvals.type.invalid", member, 1), (rule, refused.Failure?.Code, refused.Failure?.Details.GetValueOrDefault("member"), refused.Failure?.Details.Count));
        }

        Assert.Empty(await host.VersionsAsync());
        Assert.Equal(0L, await host.TypeCountAsync());
        host.AssertNoLogHoldsAValueUsed();
    }
}