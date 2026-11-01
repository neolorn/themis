using System;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using Themis.Core;

using Xunit;

namespace Themis.Endpoints.Tests;

/// <summary>What <c>MapThemisApprovals</c> checks and answers as it maps the endpoints (<c>03</c> APR-LIB-004, APR-LIB-005).</summary>
[Trait("Category", "unit")]
public sealed class MappingTests
{
    [Fact]
    public void APR_LIB_004_AC8_MappingWithoutCurrentPersonRegisteredThrowsSeamMissingNamingCurrentPerson()
    {
        using LogCapture logs = new();
        WebApplicationBuilder builder = EndpointsHost.Builder(logs);
        builder.Services.AddThemis(builder.Configuration);
        using WebApplication app = builder.Build();

        ApprovalsStartupException thrown = Assert.Throws<ApprovalsStartupException>(() => app.MapThemisApprovals(EndpointsHost.Prefix));

        Assert.Equal(("approvals.startup.seammissing", "currentPerson"), (thrown.Code, thrown.Details["key"]));
    }

    [Fact]
    public void MapThemisApprovals_CurrentPersonRegistered_AnswersTheGroupUnderThePrefix()
    {
        using LogCapture logs = new();
        WebApplicationBuilder builder = EndpointsHost.Builder(logs);
        builder.Services.AddThemis(builder.Configuration);
        builder.Services.AddSingleton<ICurrentPerson, HeaderPerson>();
        using WebApplication app = builder.Build();

        RouteGroupBuilder group = app.MapThemisApprovals(EndpointsHost.Prefix);

        Assert.NotNull(group);
        Assert.Throws<ArgumentNullException>(() => app.MapThemisApprovals(null!));
    }
}