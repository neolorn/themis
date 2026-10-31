using System;
using System.Security.Cryptography;
using System.Threading.Tasks;

using Npgsql;

using Xunit;

namespace Themis.Tests;

/// <summary>The tables of approval types and their versions, as the migrations make them, each test on a database of its own.</summary>
[Trait("Category", "integration")]
public sealed class TypeSchemaTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string InsertVersion = "INSERT INTO approvals.versions (key, version, definition, hash, published_at) VALUES ('purchase', @version, '{}', @hash, @at)";

    private static readonly byte[] Hash = SHA256.HashData("{}"u8);

    [Fact]
    public async Task Versions_NumberBelowOne_IsRefusedByTheDatabase()
    {
        PostgresException refused = await RefusedAsync(typeRecorded: true, version: 0, Hash);

        Assert.Equal((PostgresErrorCodes.CheckViolation, "ck_versions_version"), (refused.SqlState, refused.ConstraintName));
    }

    [Fact]
    public async Task Versions_HashOfAnotherLength_IsRefusedByTheDatabase()
    {
        PostgresException refused = await RefusedAsync(typeRecorded: true, version: 1, Hash[..31]);

        Assert.Equal((PostgresErrorCodes.CheckViolation, "ck_versions_hash"), (refused.SqlState, refused.ConstraintName));
    }

    [Fact]
    public async Task Versions_OfATypeNotRecorded_IsRefusedByTheDatabase()
    {
        PostgresException refused = await RefusedAsync(typeRecorded: false, version: 1, Hash);

        Assert.Equal((PostgresErrorCodes.ForeignKeyViolation, "fk_versions_types"), (refused.SqlState, refused.ConstraintName));
    }

    [Fact]
    public async Task Versions_TwoOfOneNumber_AreRefusedByTheDatabase()
    {
        PostgresException refused = await RefusedAsync(typeRecorded: true, version: 1, Hash, times: 2);

        Assert.Equal((PostgresErrorCodes.UniqueViolation, "pk_versions"), (refused.SqlState, refused.ConstraintName));
    }

    /// <summary>Inserts a version <paramref name="times"/> times, of a type recorded first where asked, and answers the refusal of the last insert.</summary>
    private async Task<PostgresException> RefusedAsync(bool typeRecorded, int version, byte[] hash, int times = 1)
    {
        (string name, string connection) = await postgres.NewMigratedDatabaseAsync();
        if (typeRecorded)
        {
            await postgres.RunAsync("INSERT INTO approvals.types (key, retired) VALUES ('purchase', false);", name);
        }

        await using NpgsqlConnection database = new(connection);
        await database.OpenAsync(TestContext.Current.CancellationToken);
        for (int insert = 1; insert < times; insert++)
        {
            await using NpgsqlCommand accepted = Insert(database, version, hash);
            await accepted.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await using NpgsqlCommand refused = Insert(database, version, hash);
        return await Assert.ThrowsAsync<PostgresException>(() => refused.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
    }

    private static NpgsqlCommand Insert(NpgsqlConnection database, int version, byte[] hash)
    {
        NpgsqlCommand insert = new(InsertVersion, database);
        insert.Parameters.AddWithValue("version", version);
        insert.Parameters.AddWithValue("hash", hash);
        insert.Parameters.AddWithValue("at", DateTimeOffset.UnixEpoch);
        return insert;
    }
}