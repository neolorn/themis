using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Themis.Core;

namespace Themis.Operations;

/// <summary>
/// Runs the startup checks in <see cref="StartingAsync"/>, which the host calls on every
/// lifecycle service before any hosted service's <c>StartAsync</c>, so they complete before a
/// host's own hosted service starts or a request is served (<c>03</c> APR-LIB-003): the
/// configuration's, then the seams', the secrets' and the schema's. A failure stops the start
/// with an <see cref="ApprovalsStartupException"/> carrying its code.
/// </summary>
internal sealed partial class ApprovalsStartup(
    ApprovalsConfiguration configuration,
    IOptions<ApprovalsOptions> options,
    ApprovalsReady ready,
    ApprovalsDatabase database,
    TimeProvider clock,
    IServiceProvider services,
    ILogger<ApprovalsStartup> logger) : IHostedLifecycleService
{
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        try
        {
            await CheckAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ApprovalsStartupException failure)
        {
            StartFailed(failure.Code, Describe(failure.Details));
            throw;
        }

        ready.Mark();
        Started();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StartedAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StoppingAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StoppedAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    internal static ApprovalsStartupException Failure(string code, string detail, string value)
    {
        return new ApprovalsStartupException(code, new Dictionary<string, string>(StringComparer.Ordinal) { [detail] = value });
    }

    private async Task CheckAsync(CancellationToken cancellationToken)
    {
        IConfigurationSection section = configuration.Section;
        if (ConfigurationRules.Invalid(path => section[path]) is { } key)
        {
            throw Failure(Codes.ConfigurationInvalid, "key", key.Name);
        }

        // Read once, here, so every key takes effect at the start (04 APR-OPS-001).
        ApprovalsOptions current = options.Value;
        if (SeamCheck.Missing(services.GetRequiredService<IServiceProviderIsService>(), current) is { } seam)
        {
            throw Failure(Codes.SeamMissing, "key", seam);
        }

        AsyncServiceScope scope = services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            ISecretSource secrets = scope.ServiceProvider.GetRequiredService<ISecretSource>();
            database.Open(await SecretCheck.ConnectionAsync(secrets, current.Timeout, clock, cancellationToken).ConfigureAwait(false));
        }
    }

    private static string Describe(IReadOnlyDictionary<string, string> details)
    {
        return details.Count == 0 ? "none" : string.Join(", ", details.OrderBy(detail => detail.Key, StringComparer.Ordinal).Select(detail => detail.Key + " " + detail.Value));
    }

    [LoggerMessage(EventId = 1, EventName = "StartChecked", Level = LogLevel.Information, Message = "The library's startup checks passed.")]
    private partial void Started();

    [LoggerMessage(EventId = 2, EventName = "StartFailed", Level = LogLevel.Error, Message = "The library's startup checks failed: {Code} ({Details}).")]
    private partial void StartFailed(string code, string details);
}