using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Themis.Core;

using Xunit;

namespace Themis.Testing.Tests;

/// <summary>The testing package's registration and its jobs run on demand (<c>03</c> APR-LIB-007).</summary>
[Trait("Category", "unit")]
public sealed class TestingRegistrationTests
{
    [Fact]
    public async Task AddThemisTesting_AfterTheHostsSeams_ReplacesThemEachResolvableByItsOwnTypeAndBySeam()
    {
        SecretVersions connection = new() { Versions = [new SecretVersion { Version = 1, Value = Encoding.UTF8.GetBytes("Host=db") }] };
        ServiceCollection services = new();
        services.AddSingleton<IApproverDirectory, DirectoryFake>();

        services.AddThemisTesting(secrets => secrets.SetSecret("connection", connection));
        await using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Same(provider.GetRequiredService<InMemoryApproverDirectory>(), provider.GetRequiredService<IApproverDirectory>());
        Assert.Same(provider.GetRequiredService<RecordingObserver>(), provider.GetRequiredService<IApprovalObserver>());
        Assert.Same(provider.GetRequiredService<FixedSecretSource>(), provider.GetRequiredService<ISecretSource>());
        Assert.Same(connection, await provider.GetRequiredService<ISecretSource>().ReadAsync("connection", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddThemisTesting_SeamRegisteredAfterIt_ReplacesTheTestingOne()
    {
        ServiceCollection services = new();

        services.AddThemisTesting();
        services.AddSingleton<IApproverDirectory, DirectoryFake>();
        await using ServiceProvider provider = services.BuildServiceProvider();

        Assert.IsType<DirectoryFake>(provider.GetRequiredService<IApproverDirectory>());
    }

    [Fact]
    public async Task RunUntilIdleAsync_Rounds_RunDeadlinesThenObserverUntilARoundHandlesNothing()
    {
        JobsFake jobs = new(deadlines: [1, 0, 0], observer: [2, 1, 0]);

        int handled = await new TestJobs(jobs).RunUntilIdleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(4, handled);
        Assert.Equal([ApprovalJob.Deadlines, ApprovalJob.Observer, ApprovalJob.Deadlines, ApprovalJob.Observer, ApprovalJob.Deadlines, ApprovalJob.Observer], jobs.Ran);
    }

    [Fact]
    public void TestJobs_NoJobs_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new TestJobs(null!));
    }

    private sealed class DirectoryFake : IApproverDirectory
    {
        public ValueTask<IReadOnlyList<string>> ResolveAsync(string permission, ApproverQuery query, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<IReadOnlyList<string>>([]);
        }

        public ValueTask<bool> CanApproveAsync(ApproverCheck check, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(false);
        }
    }

    /// <summary>Jobs that answer the counts given for each job, in turn, and keep the order run.</summary>
    private sealed class JobsFake(int[] deadlines, int[] observer) : IApprovalJobs
    {
        private int deadlinesRun;

        private int observerRun;

        internal List<ApprovalJob> Ran { get; } = [];

        public ValueTask<int> RunAsync(ApprovalJob job, CancellationToken cancellationToken)
        {
            Ran.Add(job);
            return ValueTask.FromResult(job == ApprovalJob.Deadlines ? deadlines[deadlinesRun++] : observer[observerRun++]);
        }
    }
}