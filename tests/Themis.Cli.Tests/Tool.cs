using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Xunit;

namespace Themis.Cli.Tests;

/// <summary>Runs the tool in the test's process, with the input a test gives.</summary>
internal static class Tool
{
    internal static async Task<ToolRun> RunAsync(string input, params string[] arguments)
    {
        using StringReader reader = new(input);
        await using StringWriter error = new();
        int exit = await Program.RunAsync(arguments.Length == 0 ? ["migrate"] : arguments, reader, error, TestContext.Current.CancellationToken);
        return new ToolRun(exit, error.ToString());
    }

    internal static string Input(string connection, params string[] roles)
    {
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("connection", connection);
            writer.WriteStartArray("applicationRoles");
            foreach (string role in roles)
            {
                writer.WriteStringValue(role);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>A run's exit code and what it wrote on standard error.</summary>
    internal sealed record ToolRun(int Exit, string Error)
    {
        /// <summary>The failure line's code and details.</summary>
        internal (string Code, IReadOnlyDictionary<string, string> Details) Failure()
        {
            Assert.EndsWith("\n", Error, System.StringComparison.Ordinal);
            Assert.Single(Error.Split('\n', System.StringSplitOptions.RemoveEmptyEntries));
            using JsonDocument line = JsonDocument.Parse(Error);
            Dictionary<string, string> details = [];
            foreach (JsonProperty detail in line.RootElement.GetProperty("details").EnumerateObject())
            {
                details[detail.Name] = detail.Value.GetString()!;
            }

            return (line.RootElement.GetProperty("code").GetString()!, details);
        }
    }
}