using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace Themis.Repository.Tests;

/// <remarks>
/// Each test builds a copy of <c>Themis.Core</c>'s project and public API files under
/// <c>tmp/</c>, so the copy takes the repository's settings and analysers as the project does.
/// </remarks>
[Trait("Category", "integration")]
public sealed class PublicSurfaceBuildTests
{
    /// <summary>The files of <c>Themis.Core</c> a probe copies: its project and lock file, whose analyser wiring it tests.</summary>
    private static readonly IReadOnlyList<string> Copied = ["Themis.Core.csproj", "packages.lock.json"];

    /// <summary>The public API files a probe starts from, listing nothing, since it copies none of the sources.</summary>
    private static readonly IReadOnlyList<string> SurfaceFiles = ["PublicAPI.Shipped.txt", "PublicAPI.Unshipped.txt"];

    private const string EmptySurface = "#nullable enable\n";

    [Fact]
    public async Task APR_LIB_002_AC1_AddingAPublicTypeWithoutItsPublicApiFileFailsTheBuild()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        ProbeProject probe = CopyCore(folder);

        ProcessResult unchanged = await probe.BuildAsync(cancellationToken);
        probe.WriteSource("Probe.cs", """
            namespace Themis.Core;

            /// <summary>A public type no public API file lists.</summary>
            public sealed class Probe
            {
            }
            """);
        ProcessResult added = await probe.BuildAsync(cancellationToken);

        Assert.True(unchanged.ExitCode == 0, unchanged.Output);
        Assert.NotEqual(0, added.ExitCode);
        Assert.Contains("RS0016", added.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APR_LIB_002_AC1_RemovingAPublicTypeWithoutItsPublicApiFileFailsTheBuild()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        ProbeProject probe = CopyCore(folder);
        probe.WriteSource("PublicAPI.Unshipped.txt", "#nullable enable\nThemis.Core.Removed\n");

        ProcessResult removed = await probe.BuildAsync(cancellationToken);

        Assert.NotEqual(0, removed.ExitCode);
        Assert.Contains("RS0017", removed.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APR_ENG_004_AC1_PublicMemberWithoutDocumentationFailsTheBuild()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        ProbeProject probe = CopyCore(folder);
        probe.WriteSource("PublicAPI.Unshipped.txt", "#nullable enable\nThemis.Core.Probe\nThemis.Core.Probe.Probe() -> void\n");
        probe.WriteSource("Probe.cs", """
            namespace Themis.Core;

            public sealed class Probe
            {
            }
            """);

        ProcessResult undocumented = await probe.BuildAsync(cancellationToken);

        Assert.NotEqual(0, undocumented.ExitCode);
        Assert.Contains("error CS1591", undocumented.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("RS0016", undocumented.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APR_LIB_002_AC1_PublicSurfaceGateFailsOnAPublicTypeNoFileLists()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        foreach (string file in Copied)
        {
            folder.Write(Path.Combine("src", Packages.Core, file), await File.ReadAllTextAsync(RepositoryRoot.Combine("src", Packages.Core, file), cancellationToken));
        }

        foreach (string file in SurfaceFiles)
        {
            folder.Write(Path.Combine("src", Packages.Core, file), EmptySurface);
        }

        ProcessResult unchanged = await Processes.GateAsync("gate-public-surface.ps1", folder.Path, cancellationToken, "-Repository", folder.Path);
        folder.Write(Path.Combine("src", Packages.Core, "Probe.cs"), """
            namespace Themis.Core;

            /// <summary>A public type no public API file lists.</summary>
            public sealed class Probe
            {
            }
            """);
        ProcessResult added = await Processes.GateAsync("gate-public-surface.ps1", folder.Path, cancellationToken, "-Repository", folder.Path);

        Assert.True(unchanged.ExitCode == 0, unchanged.Output);
        Assert.NotEqual(0, added.ExitCode);
        Assert.Contains("RS0016", added.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PublicSurfaceGate_NoProjectUnderSrc_Fails()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        folder.Write("tests/Themis.Tests/Themis.Tests.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");

        ProcessResult gate = await Processes.GateAsync("gate-public-surface.ps1", folder.Path, cancellationToken, "-Repository", folder.Path);

        Assert.NotEqual(0, gate.ExitCode);
        Assert.Contains("src/ holds no project, so the gate would check nothing", gate.Output, StringComparison.Ordinal);
    }

    private static ProbeProject CopyCore(ScratchFolder folder)
    {
        ProbeProject probe = new(folder, Packages.Core);
        foreach (string file in Copied)
        {
            probe.WriteSource(file, File.ReadAllText(RepositoryRoot.Combine("src", Packages.Core, file)));
        }

        foreach (string file in SurfaceFiles)
        {
            probe.WriteSource(file, EmptySurface);
        }

        return probe;
    }
}