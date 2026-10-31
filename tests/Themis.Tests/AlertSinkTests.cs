using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Themis.Core;

using Xunit;

namespace Themis.Tests;

[Trait("Category", "unit")]
public sealed class AlertSinkTests
{
    [Fact]
    public async Task AddThemis_NoAlertSinkRegistered_TheLibrarysOwnLogsARaiseAtTheErrorLevel()
    {
        using LogCapture logs = new();
        await using TestHost host = TestHost.Build(logs: logs);
        IAlertSink sink = host.Services.GetRequiredService<IAlertSink>();

        await sink.RaiseAsync("job-stalled", "deadlines", new Dictionary<string, string> { ["job"] = "deadlines", ["since"] = "2031-02-03T04:05:06Z" }, TestContext.Current.CancellationToken);

        LogCapture.LogEntry entry = Assert.Single(logs.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal("Alert raised: job-stalled, scope deadlines (job deadlines, since 2031-02-03T04:05:06Z).", entry.Message);
    }

    [Fact]
    public async Task AddThemis_NoAlertSinkRegistered_TheLibrarysOwnLogsAClearAtTheErrorLevel()
    {
        using LogCapture logs = new();
        await using TestHost host = TestHost.Build(logs: logs);
        IAlertSink sink = host.Services.GetRequiredService<IAlertSink>();

        await sink.ClearAsync("observer-failing", null, TestContext.Current.CancellationToken);

        LogCapture.LogEntry entry = Assert.Single(logs.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal("Alert cleared: observer-failing, scope none.", entry.Message);
    }

    [Fact]
    public async Task AddThemis_AlertSinkRegisteredAfterTheCall_ReplacesTheLibrarysOwn()
    {
        AlertSinkFake fake = new();
        await using TestHost host = TestHost.Build(after: services => services.AddSingleton<IAlertSink>(fake));

        Assert.Same(fake, host.Services.GetRequiredService<IAlertSink>());
    }

    [Fact]
    public async Task AddThemis_AlertSinkRegisteredBeforeTheCall_IsKept()
    {
        AlertSinkFake fake = new();
        await using TestHost host = TestHost.Build(before: services => services.AddSingleton<IAlertSink>(fake));

        Assert.Same(fake, host.Services.GetRequiredService<IAlertSink>());
        Assert.Single(host.Services.GetServices<IAlertSink>());
    }

    private sealed class AlertSinkFake : IAlertSink
    {
        public ValueTask RaiseAsync(string condition, string? scope, IReadOnlyDictionary<string, string> details, CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask ClearAsync(string condition, string? scope, CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }
    }
}