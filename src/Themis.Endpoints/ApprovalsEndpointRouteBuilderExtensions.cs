using System;
using System.Collections.Generic;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using Themis.Core;

namespace Themis.Endpoints;

/// <summary>Maps the endpoints of an approver's queue, decisions and delegations in a host (<c>03</c> APR-LIB-004).</summary>
public static class ApprovalsEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Checks that <see cref="ICurrentPerson"/> is registered and maps the endpoints under
    /// <paramref name="prefix"/>: <c>GET pending</c>, <c>GET requests/{id}</c>,
    /// <c>POST requests/{id}/decisions</c>, <c>GET delegations</c>, <c>POST delegations</c> and
    /// <c>POST delegations/{id}/end</c>, each acting for the person the seam names.
    /// </summary>
    /// <param name="routes">The host's routes.</param>
    /// <param name="prefix">The prefix the endpoints are mapped under.</param>
    /// <returns>The group the endpoints are mapped in, to which the host adds its own authorization and antiforgery.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ApprovalsStartupException"><see cref="ICurrentPerson"/> is not registered: <c>approvals.startup.seammissing</c>, its <c>key</c> <c>currentPerson</c>.</exception>
    public static RouteGroupBuilder MapThemisApprovals(this IEndpointRouteBuilder routes, string prefix)
    {
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(prefix);
        if (routes.ServiceProvider.GetService<IServiceProviderIsService>() is not { } services || !services.IsService(typeof(ICurrentPerson)))
        {
            throw new ApprovalsStartupException(Codes.SeamMissing, new Dictionary<string, string>(StringComparer.Ordinal) { ["key"] = "currentPerson" });
        }

        RouteGroupBuilder group = routes.MapGroup(prefix);
        group.MapGet("/pending", ApprovalsRoutes.PendingAsync);
        group.MapGet("/requests/{id}", ApprovalsRoutes.RequestAsync);
        group.MapPost("/requests/{id}/decisions", ApprovalsRoutes.DecideAsync);
        group.MapGet("/delegations", ApprovalsRoutes.DelegationsAsync);
        group.MapPost("/delegations", ApprovalsRoutes.DelegateAsync);
        group.MapPost("/delegations/{id}/end", ApprovalsRoutes.EndAsync);
        return group;
    }
}