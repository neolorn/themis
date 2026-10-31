using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Themis.Core;

namespace Themis.Tests;

/// <summary>A host's own hosted service that publishes a type and creates a request of it in its <c>StartAsync</c> (<c>03</c> APR-LIB-003).</summary>
internal sealed class CreatingService : IHostedService
{
    private IServiceProvider? services;

    /// <summary>The create's answer, once <c>StartAsync</c> has run.</summary>
    internal Result<CreatedRequest>? Created { get; private set; }

    /// <summary>This service, resolving the library's services from <paramref name="provider"/>.</summary>
    internal CreatingService With(IServiceProvider provider)
    {
        services = provider;
        return this;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        AsyncServiceScope scope = (services ?? throw new InvalidOperationException("The service was not given its provider.")).CreateAsyncScope();
        await using (scope)
        {
            await scope.ServiceProvider.GetRequiredService<IApprovalTypes>().PublishAsync(Requests.Type(Requests.Step("review", ApprovalMode.AnyOne, Requests.Person(Requests.A))), cancellationToken);
            Created = await scope.ServiceProvider.GetRequiredService<IApprovals>().CreateAsync(Requests.Request(), cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}