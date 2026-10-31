using System;

using Xunit;

namespace Themis.Repository.Tests;

[Trait("Category", "unit")]
public sealed class ScannerConfigurationTests
{
    private const string Extend = """
        title = "Secret scanning of the whole history"

        [extend]
        useDefault = true

        """;

    [Fact]
    public void APR_ENG_001_AC1_ScannerConfigurationAddsNoEntry()
    {
        Assert.Empty(ScannerConfigurationRules.Violations(RepositoryRoot.Path));
    }

    [Fact]
    public void APR_ENG_001_AC1_DefaultExtendedAloneIsAccepted()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("build/gates/gitleaks.toml", Extend);

        Assert.Empty(ScannerConfigurationRules.Violations(fixture.Root));
    }

    [Theory]
    [InlineData("[[allowlists]]\ndescription = \"RFC 8291 section 5\"\ncondition = \"AND\"\npaths = ['''^tests/Vectors\\.cs$''']\nregexes = ['''^vector$''']\n", "adds the table [[allowlists]], an entry the rule does not allow")]
    [InlineData("[[rules]]\nid = \"custom\"\nregex = '''x'''\n", "adds the table [[rules]], an entry the rule does not allow")]
    [InlineData("[allowlist]\npaths = ['''.*''']\n", "adds the table [allowlist], an entry the rule does not allow")]
    [InlineData("[[rules.allowlists]]\nregexes = ['''x''']\n", "adds the table [[rules.allowlists]], an entry the rule does not allow")]
    [InlineData("[extend]\nuseDefault = true\n", "holds more than one [extend] table")]
    [InlineData("not a key\n", "is not a key the rule allows once in its table")]
    public void APR_ENG_001_AC1_EntryAddedToTheConfigurationIsFound(string entry, string reason)
    {
        using RepositoryFixture fixture = new();
        fixture.Write("build/gates/gitleaks.toml", Extend + entry);

        Assert.Contains(ScannerConfigurationRules.Violations(fixture.Root), violation => violation.Contains(reason, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("[extend]\nuseDefault = false\n", "[extend] table holds more than useDefault = true")]
    [InlineData("[extend]\nuseDefault = true\ndisabledRules = [\"generic-api-key\"]\n", "[extend] table holds more than useDefault = true")]
    [InlineData("[extend]\npath = \"other.toml\"\n", "[extend] table holds more than useDefault = true")]
    [InlineData("title = \"No extension\"\n", "does not extend gitleaks' default")]
    [InlineData("title = \"Top entry\"\nversion = 2\n[extend]\nuseDefault = true\n", "sets version at its top, an entry the rule does not allow")]
    public void APR_ENG_001_AC1_ConfigurationThatDoesNotExtendTheDefaultAloneIsFound(string configuration, string reason)
    {
        using RepositoryFixture fixture = new();
        fixture.Write("build/gates/gitleaks.toml", configuration);

        Assert.Contains(ScannerConfigurationRules.Violations(fixture.Root), violation => violation.Contains(reason, StringComparison.Ordinal));
    }
}