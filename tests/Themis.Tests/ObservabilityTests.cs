using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using Themis.Operations;

using Xunit;

namespace Themis.Tests;

[Trait("Category", "unit")]
public sealed class ObservabilityTests
{
    [Fact]
    public async Task AddThemis_Meter_PublishesTheReferencesInstrumentsUnderTheMeterThemis()
    {
        List<Instrument> published = [];
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, _) =>
        {
            if (instrument.Meter.Name == "Themis")
            {
                lock (published)
                {
                    published.Add(instrument);
                }
            }
        };
        listener.Start();
        await using TestHost host = TestHost.Build();

        Meter meter = host.Services.GetRequiredService<ApprovalsMetrics>().RequestsCreated.Meter;

        // Other tests' hosts publish meters of the same name at the same time; this host's alone count.
        Instrument[] own;
        lock (published)
        {
            own = [.. published.Where(instrument => ReferenceEquals(instrument.Meter, meter))];
        }

        IReadOnlyList<string>[] instruments = [.. Reference.Rows(7).Where(row => row[1] is "Counter" or "Observable gauge")];
        Assert.Equal(instruments.Select(row => row[0]).Order(StringComparer.Ordinal), own.Select(instrument => instrument.Name).Order(StringComparer.Ordinal));
        Assert.All(own, instrument => Assert.Equal(instruments.Single(row => row[0] == instrument.Name)[1] == "Counter" ? typeof(Counter<long>) : typeof(ObservableGauge<long>), instrument.GetType()));
    }

    [Fact]
    public async Task AddThemis_MeterFactoryTheHostRegisters_MakesTheMeter()
    {
        using MeterFactoryFake factory = new();
        await using TestHost host = TestHost.Build(before: services => services.AddSingleton<IMeterFactory>(factory));

        ApprovalsMetrics metrics = host.Services.GetRequiredService<ApprovalsMetrics>();

        Assert.Equal(["Themis"], factory.Made);
        Assert.Equal("Themis", metrics.RequestsCreated.Meter.Name);
    }

    [Fact]
    public async Task AddThemis_JobsEnabledByDefault_RegistersTheHealthCheckThemisWhichReportsHealthy()
    {
        await using TestHost host = TestHost.Build();

        HealthReport report = await host.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["themis"], report.Entries.Keys);
        Assert.Equal(HealthStatus.Healthy, report.Status);
    }

    [Theory]
    [InlineData("false")]
    [InlineData("False")]
    public async Task AddThemis_JobsDisabled_RegistersNoHealthCheck(string disabled)
    {
        await using TestHost host = TestHost.Build(new Dictionary<string, string?> { ["Themis:Jobs:Enabled"] = disabled });

        HealthCheckServiceOptions options = host.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        Assert.Empty(options.Registrations);
    }

    private sealed class MeterFactoryFake : IMeterFactory
    {
        private readonly List<Meter> meters = [];

        internal IReadOnlyList<string> Made => [.. meters.Select(meter => meter.Name)];

        public Meter Create(MeterOptions options)
        {
            Meter meter = new(options);
            meters.Add(meter);
            return meter;
        }

        public void Dispose()
        {
            foreach (Meter meter in meters)
            {
                meter.Dispose();
            }
        }
    }
}