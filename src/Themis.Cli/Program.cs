using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Themis.Operations;

namespace Themis.Cli;

/// <summary>The command <c>themis</c>, whose one command, <c>migrate</c>, applies the release's migrations (<c>04</c> APR-OPS-002).</summary>
internal static class Program
{
    private const string Usage = "Usage: themis migrate, reading {\"connection\": \"...\", \"applicationRoles\": [\"...\"], \"retentionRoles\": [\"...\"]} on standard input.";

    private static async Task<int> Main(string[] args)
    {
        using StreamReader input = new(Console.OpenStandardInput(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return await RunAsync(args, input, Console.Error, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs the command <paramref name="arguments"/> name, reading its input from
    /// <paramref name="input"/>; answers 0 on success, and on a failure 1, having written one
    /// JSON line on <paramref name="error"/>, <c>{ "code", "details" }</c>.
    /// </summary>
    internal static async Task<int> RunAsync(IReadOnlyList<string> arguments, TextReader input, TextWriter error, CancellationToken cancellationToken)
    {
        if (arguments is not ["migrate"])
        {
            await error.WriteLineAsync(Usage).ConfigureAwait(false);
            return 1;
        }

        string text = await input.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        MigrationFailure? failure = MigrationInput.Read(text, out MigrationInput? read) ?? await SchemaMigration.ApplyAsync(read!.Connection, read.ApplicationRoles, read.RetentionRoles, cancellationToken).ConfigureAwait(false);
        if (failure is null)
        {
            return 0;
        }

        await error.WriteAsync(Line(failure) + "\n").ConfigureAwait(false);
        return 1;
    }

    /// <summary>The failure as one line of JSON, written without the reflection-based serializer.</summary>
    private static string Line(MigrationFailure failure)
    {
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("code", failure.Code);
            writer.WriteStartObject("details");
            foreach (KeyValuePair<string, string> detail in failure.Details)
            {
                writer.WriteString(detail.Key, detail.Value);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}