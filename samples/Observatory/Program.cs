using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using Observatory;

using Themis;
using Themis.Core;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// The library with one call, and the seams only the host knows: where its secrets live, who
// may approve what, and what it does with the events. The library publishes no type of its
// own, so the host publishes its two once the startup checks have passed.
builder.Services.AddThemis(builder.Configuration);
builder.Services.AddSingleton<ISecretSource, ConfigurationSecretSource>();
builder.Services.AddSingleton<IApproverDirectory, PermissionTable>();
builder.Services.AddSingleton<IApprovalObserver, EventLog>();
builder.Services.AddHostedService<TypePublisher>();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

WebApplication app = builder.Build();
app.MapObservatory();
await app.RunAsync().ConfigureAwait(false);