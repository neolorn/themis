using System;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace Themis.Repository.Tests;

[Trait("Category", "integration")]
public sealed class TestsGateTests(TestProjectRepository repository) : IClassFixture<TestProjectRepository>
{
    [Fact]
    public async Task APR_ENG_008_AC3_TestsOfOneKindEachRunAndPass()
    {
        ProcessResult gate = await RunAsync("""[Trait("Category", "unit")]""", TestContext.Current.CancellationToken);

        Assert.True(gate.ExitCode == 0, gate.Output);
        Assert.Contains("== unit tests of Probe.Tests", gate.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", "Probe.Tests.ProbeTests.Method_Scenario_Outcome carries no Category trait")]
    [InlineData("""[Trait("Category", "unit")][Trait("Category", "integration")]""", "Probe.Tests.ProbeTests.Method_Scenario_Outcome carries more than one Category trait")]
    [InlineData("""[Trait("Category", "slow")]""", "Probe.Tests.ProbeTests.Method_Scenario_Outcome carries the Category slow, which is none of unit, integration, contract")]
    [InlineData("""[Trait("Category", "Unit")]""", "Probe.Tests.ProbeTests.Method_Scenario_Outcome carries the Category Unit, which is none of unit, integration, contract")]
    [InlineData("""[Trait("Kind", "unit")]""", "Probe.Tests.ProbeTests.Method_Scenario_Outcome carries no Category trait")]
    public async Task APR_ENG_008_AC3_TestWithoutExactlyOneKindFailsTheTestsGate(string traits, string reason)
    {
        ArgumentNullException.ThrowIfNull(traits);
        ArgumentNullException.ThrowIfNull(reason);

        ProcessResult gate = await RunAsync(traits, TestContext.Current.CancellationToken);

        Assert.NotEqual(0, gate.ExitCode);
        Assert.Contains(reason, gate.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("== unit tests of Probe.Tests", gate.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestsGate_NoTestProject_Fails()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        folder.Write("src/Themis.Core/Themis.Core.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");

        ProcessResult gate = await Processes.GateAsync("gate-tests.ps1", folder.Path, cancellationToken, "-Repository", folder.Path);

        Assert.NotEqual(0, gate.ExitCode);
        Assert.Contains("tests/ holds no test project, so the gate would run nothing", gate.Output, StringComparison.Ordinal);
    }

    private Task<ProcessResult> RunAsync(string traits, CancellationToken cancellationToken)
    {
        repository.WriteTests($$"""
            using Xunit;

            namespace Probe.Tests;

            public sealed class ProbeTests
            {
                [Fact]
                {{traits}}
                public void Method_Scenario_Outcome()
                {
                }
            }

            """);
        return Processes.GateAsync("gate-tests.ps1", repository.Root, cancellationToken, "-Repository", repository.Root);
    }
}