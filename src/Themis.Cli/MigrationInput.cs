using System;
using System.Collections.Generic;
using System.Text.Json;

using Themis.Operations;

namespace Themis.Cli;

/// <summary>
/// The document <c>themis migrate</c> reads, <c>{ "connection", "applicationRoles", "retentionRoles" }</c>, the
/// roles optional (<c>04</c> APR-OPS-002): members case-sensitive, an unknown one ignored, a
/// document that does not read answering no member and <c>details.member</c> naming the member
/// at fault otherwise.
/// </summary>
/// <param name="Connection">The connection string.</param>
/// <param name="ApplicationRoles">The roles granted the use of the schema.</param>
/// <param name="RetentionRoles">The logins granted the role <c>approvals_retention</c>.</param>
internal sealed record MigrationInput(string Connection, IReadOnlyList<string> ApplicationRoles, IReadOnlyList<string> RetentionRoles)
{
    private static readonly JsonDocumentOptions Options = new() { AllowDuplicateProperties = false };

    /// <summary>Reads <paramref name="text"/>; answers none and the input, or the failure where it does not read.</summary>
    internal static MigrationFailure? Read(string text, out MigrationInput? input)
    {
        input = null;
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text, Options);
        }
        catch (JsonException)
        {
            return Invalid(null);
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Invalid(null);
            }

            if (!root.TryGetProperty("connection", out JsonElement connection) || connection.ValueKind != JsonValueKind.String || connection.GetString() is not { Length: > 0 } read)
            {
                return Invalid("connection");
            }

            List<string>? roles = Roles(root, "applicationRoles");
            if (roles is null)
            {
                return Invalid("applicationRoles");
            }

            List<string>? logins = Roles(root, "retentionRoles");
            if (logins is null)
            {
                return Invalid("retentionRoles");
            }

            input = new MigrationInput(read, roles, logins);
            return null;
        }
    }

    /// <summary>The names of the member <paramref name="member"/>, none where it is absent; or null where it is not an array of names that are not empty.</summary>
    private static List<string>? Roles(JsonElement root, string member)
    {
        List<string> roles = [];
        if (!root.TryGetProperty(member, out JsonElement named))
        {
            return roles;
        }

        if (named.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (JsonElement role in named.EnumerateArray())
        {
            if (role.ValueKind != JsonValueKind.String || role.GetString() is not { Length: > 0 } name)
            {
                return null;
            }

            roles.Add(name);
        }

        return roles;
    }

    private static MigrationFailure Invalid(string? member)
    {
        Dictionary<string, string> details = new(StringComparer.Ordinal);
        if (member is not null)
        {
            details["member"] = member;
        }

        return new MigrationFailure(Codes.MigrateInputInvalid, details);
    }
}