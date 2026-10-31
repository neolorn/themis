using System.Collections.Generic;
using System.Threading.Tasks;

using Xunit;

namespace Themis.Cli.Tests;

[Trait("Category", "unit")]
public sealed class MigrateInputTests
{
    [Theory]
    [InlineData("", null)]
    [InlineData("not json", null)]
    [InlineData("[]", null)]
    [InlineData("{\"connection\": \"Host=h\"} {}", null)]
    [InlineData("{\"connection\": \"Host=h\", \"connection\": \"Host=i\"}", null)]
    [InlineData("{}", "connection")]
    [InlineData("{\"Connection\": \"Host=h\"}", "connection")]
    [InlineData("{\"connection\": 5}", "connection")]
    [InlineData("{\"connection\": \"\"}", "connection")]
    [InlineData("{\"connection\": \"=x\"}", "connection")]
    [InlineData("{\"connection\": \"Host=h\", \"applicationRoles\": \"app\"}", "applicationRoles")]
    [InlineData("{\"connection\": \"Host=h\", \"applicationRoles\": null}", "applicationRoles")]
    [InlineData("{\"connection\": \"Host=h\", \"applicationRoles\": [5]}", "applicationRoles")]
    [InlineData("{\"connection\": \"Host=h\", \"applicationRoles\": [\"\"]}", "applicationRoles")]
    public async Task APR_OPS_002_AC3_InputThatDoesNotReadExitsOneWithInputInvalid(string input, string? member)
    {
        Tool.ToolRun run = await Tool.RunAsync(input);

        Assert.Equal(1, run.Exit);
        (string code, IReadOnlyDictionary<string, string> details) = run.Failure();
        Assert.Equal("approvals.migrate.inputinvalid", code);
        if (member is null)
        {
            Assert.Empty(details);
        }
        else
        {
            Assert.Equal(member, details["member"]);
        }
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("apply")]
    [InlineData("migrate,now")]
    [InlineData("Migrate")]
    public async Task Run_ArgumentsOtherThanMigrate_ExitsOne(string arguments)
    {
        Tool.ToolRun run = await Tool.RunAsync("{}", (arguments ?? string.Empty).Split(','));

        Assert.Equal(1, run.Exit);
        Assert.NotEmpty(run.Error);
    }
}