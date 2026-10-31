using System;

using Xunit;

namespace Themis.Repository.Tests;

/// <remarks>
/// The grants are read from each project's built assembly under <c>bin/Release/net10.0/</c>,
/// which the build of <c>Themis.sln</c> the gates run first leaves there; the sample host and
/// its test project, built only by their own gate, from the packed packages, are read from
/// their project files and sources.
/// </remarks>
[Trait("Category", "contract")]
public sealed class GrantTests
{
    [Fact]
    public void APR_ENG_001_AC1_GrantsFollowTheRule()
    {
        Assert.Empty(ReferenceRules.GrantViolations(RepositoryRoot.Path));
    }

    [Theory]
    [InlineData("samples/Observatory/Observatory.csproj", """<Project Sdk="Microsoft.NET.Sdk.Web"><ItemGroup><InternalsVisibleTo Include="Observatory.Tests" /></ItemGroup></Project>""")]
    [InlineData("samples/Observatory/Observatory.csproj", """<Project Sdk="Microsoft.NET.Sdk.Web"><ItemGroup><AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleToAttribute"><_Parameter1>Observatory.Tests</_Parameter1></AssemblyAttribute></ItemGroup></Project>""")]
    [InlineData("samples/Observatory/Grants.cs", """[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Observatory.Tests")]""")]
    public void APR_ENG_001_AC1_SampleProjectGrantingItsInternalsIsFound(string path, string content)
    {
        using RepositoryFixture fixture = new();
        fixture.Write("samples/Observatory/Observatory.csproj", """<Project Sdk="Microsoft.NET.Sdk.Web" />""");
        fixture.Write(path, content);

        Assert.Contains("samples/Observatory/Observatory.csproj grants its internals, which APR-ENG-001 allows no project of the sample host's solution.", ReferenceRules.GrantViolations(fixture.Root));
    }

    [Fact]
    public void GrantViolations_SampleProjectGrantingNothing_NeedsNoBuiltAssembly()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("samples/Observatory/Observatory.csproj", """<Project Sdk="Microsoft.NET.Sdk.Web" />""");
        fixture.Write("samples/Observatory/Program.cs", "System.Console.WriteLine();\n");

        Assert.DoesNotContain(ReferenceRules.GrantViolations(fixture.Root), violation => violation.StartsWith("samples/", StringComparison.Ordinal));
    }
}