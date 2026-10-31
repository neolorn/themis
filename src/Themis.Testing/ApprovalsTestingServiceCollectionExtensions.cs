using System;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using Themis.Core;
using Themis.Operations;

namespace Themis.Testing;

/// <summary>Registers the testing seams in a test's host (<c>03</c> APR-LIB-007).</summary>
public static class ApprovalsTestingServiceCollectionExtensions
{
    /// <summary>
    /// Registers one <see cref="InMemoryApproverDirectory"/>, one <see cref="RecordingObserver"/>
    /// and one <see cref="FixedSecretSource"/> as singletons, each resolvable by its own type and
    /// by its seam, in place of the host's, and removes the hosted jobs, so only a test's runs
    /// happen, whether it is called before or after <c>AddThemis</c>. A seam the host registers
    /// after it replaces the testing one.
    /// </summary>
    /// <param name="services">The test host's services.</param>
    /// <param name="configure">Runs on the <see cref="FixedSecretSource"/> at once, so a test sets <c>connection</c> before the host starts.</param>
    /// <returns><paramref name="services"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddThemisTesting(this IServiceCollection services, Action<FixedSecretSource>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        FixedSecretSource secrets = new();
        configure?.Invoke(secrets);

        services.RemoveAll<IApproverDirectory>();
        services.RemoveAll<IApprovalObserver>();
        services.RemoveAll<ISecretSource>();
        services.AddSingleton(new InMemoryApproverDirectory());
        services.AddSingleton<IApproverDirectory>(provider => provider.GetRequiredService<InMemoryApproverDirectory>());
        services.AddSingleton(new RecordingObserver());
        services.AddSingleton<IApprovalObserver>(provider => provider.GetRequiredService<RecordingObserver>());
        services.AddSingleton(secrets);
        services.AddSingleton<ISecretSource>(provider => provider.GetRequiredService<FixedSecretSource>());

        services.TryAddSingleton<HostedJobsOff>();
        foreach (ServiceDescriptor job in services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(HostedJob)).ToList())
        {
            services.Remove(job);
        }

        return services;
    }
}