using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Themis.Core;

namespace Observatory;

/// <summary>
/// Publishes the observatory's two approval types when the host starts, after the library's
/// startup checks; a type unchanged since its last publish keeps its version.
/// </summary>
internal sealed partial class TypePublisher(IServiceScopeFactory scopes, ILogger<TypePublisher> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        AsyncServiceScope scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            IApprovalTypes types = scope.ServiceProvider.GetRequiredService<IApprovalTypes>();
            foreach (ApprovalTypeDefinition definition in ObservatoryTypes.All)
            {
                Result<PublishedVersion> published = await types.PublishAsync(definition, cancellationToken).ConfigureAwait(false);
                if (published.Value is not { } version)
                {
                    throw new InvalidOperationException($"The type {definition.Key} was refused: {published.Failure?.Code}.");
                }

                Published(logger, version.Key, version.Version, version.Created);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The type {Key} is at version {Version}, created by this start: {Created}.")]
    private static partial void Published(ILogger logger, string key, int version, bool created);
}