using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using Observatory;

using Themis;
using Themis.Core;
using Themis.Endpoints;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// The library with one call, and the seams only the host knows: where its secrets live, who
// may approve what, what it does with the events, and who each HTTP request acts for. The
// library publishes no type of its own, so the host publishes its two once the startup checks
// have passed.
builder.Services.AddThemis(builder.Configuration);
builder.Services.AddSingleton<ISecretSource, ConfigurationSecretSource>();
builder.Services.AddSingleton<IApproverDirectory, PermissionTable>();
builder.Services.AddSingleton<IApprovalObserver, EventLog>();
builder.Services.AddSingleton<ICurrentPerson, HeaderPerson>();
builder.Services.AddHostedService<TypePublisher>();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// The library's endpoints serve an approver's queue, decisions and delegations; the host's own
// create, cancel and complete its requests. A real host adds its authorization and
// antiforgery to the group the library's mapping answers.
WebApplication app = builder.Build();
app.MapThemisApprovals("/approvals");
app.MapObservatory();
await app.RunAsync().ConfigureAwait(false);