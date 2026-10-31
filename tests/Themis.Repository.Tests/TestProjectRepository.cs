using System.IO;
using System.Threading.Tasks;

using Xunit;

namespace Themis.Repository.Tests;

/// <summary>
/// A repository under <c>tmp/</c> holding one test project, <c>tests/Probe.Tests</c>, restored
/// once, whose tests and listed criteria a test writes before it runs a gate on it.
/// </summary>
/// <remarks>
/// The repository's <c>.editorconfig</c> is copied to its root, so the probe's sources take
/// the analyser settings of <c>tests/</c>.
/// </remarks>
public sealed class TestProjectRepository : IAsyncLifetime
{
    private readonly ScratchFolder _folder = new();

    internal string Root => _folder.Path;

    internal void Write(string relativePath, string content)
    {
        _folder.Write(relativePath, content);
    }

    internal void WriteTests(string source)
    {
        _folder.Write(Path.Combine("tests", "Probe.Tests", "ProbeTests.cs"), source);
    }

    public async ValueTask InitializeAsync()
    {
        File.Copy(RepositoryRoot.Combine(".editorconfig"), Path.Combine(Root, ".editorconfig"));
        _folder.Write(Path.Combine("tests", "Probe.Tests", "Probe.Tests.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="xunit.v3" />
              </ItemGroup>

            </Project>
            """);
        ProcessResult restore = await Processes.DotnetAsync(Root, TestContext.Current.CancellationToken, "restore", Path.Combine(Root, "tests", "Probe.Tests", "Probe.Tests.csproj"));
        Assert.True(restore.ExitCode == 0, restore.Output);
    }

    public ValueTask DisposeAsync()
    {
        _folder.Dispose();
        return ValueTask.CompletedTask;
    }
}