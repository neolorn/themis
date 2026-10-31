using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Themis.Core;

using Xunit;

namespace Themis.Tests;

/// <summary>
/// A host's services with the library registered by <c>AddThemis</c>, started as .NET's host
/// starts its hosted services: every lifecycle service's <c>StartingAsync</c>, then every
/// hosted service's <c>StartAsync</c>, then every lifecycle service's <c>StartedAsync</c>.
/// </summary>
internal sealed class TestHost : IAsyncDisposable
{
    private readonly ServiceProvider provider;

    private TestHost(ServiceProvider provider, IConfigurationRoot configuration)
    {
        this.provider = provider;
        Configuration = configuration;
    }

    internal IServiceProvider Services => provider;

    /// <summary>The host's configuration, which a test may change while the host runs.</summary>
    internal IConfigurationRoot Configuration { get; }

    /// <summary>
    /// A host with <paramref name="configuration"/>, the services <paramref name="before"/>
    /// registers before <c>AddThemis</c> and <paramref name="after"/> after it, logging to
    /// <paramref name="logs"/> where given.
    /// </summary>
    internal static TestHost Build(
        IReadOnlyDictionary<string, string?>? configuration = null,
        Action<IServiceCollection>? before = null,
        Action<IServiceCollection>? after = null,
        LogCapture? logs = null)
    {
        IConfigurationRoot root = new ConfigurationBuilder().AddInMemoryCollection(configuration ?? new Dictionary<string, string?>()).Build();
        return Build(root, before, after, logs);
    }

    /// <summary>A host on <paramref name="configuration"/>, as <see cref="Build(IReadOnlyDictionary{string, string?}?, Action{IServiceCollection}?, Action{IServiceCollection}?, LogCapture?)"/> makes one.</summary>
    internal static TestHost Build(IConfigurationRoot configuration, Action<IServiceCollection>? before = null, Action<IServiceCollection>? after = null, LogCapture? logs = null)
    {
        ServiceCollection services = new();
        services.AddLogging(logging =>
        {
            if (logs is not null)
            {
                logging.AddProvider(logs);
            }
        });
        before?.Invoke(services);
        services.AddThemis(configuration);
        after?.Invoke(services);
        return new TestHost(services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }), configuration);
    }

    /// <summary>Registers the seams a host supplies: the secret source <paramref name="secrets"/>, a directory and an observer.</summary>
    internal static Action<IServiceCollection> Seams(ISecretSource secrets)
    {
        return services =>
        {
            services.AddSingleton(secrets);
            services.AddSingleton<IApproverDirectory, ApproverDirectoryFake>();
            services.AddSingleton<IApprovalObserver, ApprovalObserverFake>();
        };
    }

    /// <summary>Starts the host's hosted services as .NET's host does.</summary>
    internal async Task StartAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        IHostedService[] hosted = [.. provider.GetServices<IHostedService>()];
        foreach (IHostedLifecycleService service in hosted.OfType<IHostedLifecycleService>())
        {
            await service.StartingAsync(cancellationToken);
        }

        foreach (IHostedService service in hosted)
        {
            await service.StartAsync(cancellationToken);
        }

        foreach (IHostedLifecycleService service in hosted.OfType<IHostedLifecycleService>())
        {
            await service.StartedAsync(cancellationToken);
        }
    }

    /// <summary>Starts the host and answers the startup exception it stops with.</summary>
    internal async Task<ApprovalsStartupException> StartFailureAsync()
    {
        return await Assert.ThrowsAsync<ApprovalsStartupException>(StartAsync);
    }

    public ValueTask DisposeAsync()
    {
        return provider.DisposeAsync();
    }
}