using System;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Themis.Core;
using Themis.Operations;

namespace Themis;

/// <summary>Registers the library in a host's services (<c>03</c> APR-LIB-003).</summary>
public static class ApprovalsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the library, binding the configuration section <c>Themis</c>. Every startup
    /// check (the configuration's, the seams', the secrets' and the schema's) runs when the host
    /// starts, before any hosted service's <c>StartAsync</c>, and a failure stops the start with
    /// an <see cref="ApprovalsStartupException"/> carrying its code. The library registers no
    /// approval type. A host replaces a seam by registering its own before or after the call.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <param name="configuration">The host's configuration, whose section <c>Themis</c> is the library's.</param>
    /// <returns><paramref name="services"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static IServiceCollection AddThemis(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        IConfigurationSection section = configuration.GetSection(ConfigurationRules.Section);
        services.AddOptions<ApprovalsOptions>().Bind(section);
        services.AddSingleton<IValidateOptions<ApprovalsOptions>, ApprovalsOptionsValidator>();
        services.AddSingleton(new ApprovalsConfiguration(section));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IAlertSink, LoggingAlertSink>();
        services.AddSingleton<ApprovalsReady>();
        services.AddSingleton<ApprovalsDatabase>();
        services.AddSingleton<IHostedService, ApprovalsStartup>();
        return services;
    }
}