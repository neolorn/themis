using System;
using System.Threading.Tasks;

using Xunit;

namespace Themis.Repository.Tests;

/// <summary>
/// The sample host's gate (<c>05</c> APR-ENG-001, APR-ENG-012). Its passing run, which packs
/// the packages, builds the sample from them and runs its tests, is the gate's own run on this
/// repository; this test proves it checks something.
/// </summary>
[Trait("Category", "integration")]
public sealed class SampleHostGateTests
{
    [Fact]
    public async Task Gate_RepositoryWithoutTheSampleSolution_ChecksNothingAndFails()
    {
        using ScratchFolder folder = new();

        ProcessResult result = await Processes.GateAsync("gate-sample-host.ps1", folder.Path, TestContext.Current.CancellationToken, "-Repository", folder.Path);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("samples/Samples.sln is missing, so the gate would check nothing.", result.Output, StringComparison.Ordinal);
    }
}