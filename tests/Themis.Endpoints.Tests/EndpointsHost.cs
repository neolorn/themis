using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Themis.Core;
using Themis.Testing;

using Xunit;

namespace Themis.Endpoints.Tests;

/// <summary>
/// An ASP.NET Core host on a test server and a new migrated database, with the library, the
/// testing package's seams, the test's clock and <see cref="HeaderPerson"/>, which maps the
/// endpoints under <see cref="Prefix"/>. Every log line of the host is kept, so a test can
/// assert that none holds a value it used.
/// </summary>
internal sealed class EndpointsHost : IAsyncDisposable
{
    internal const string Prefix = "/approvals";

    /// <summary>The instant the host's clock starts at.</summary>
    internal static readonly DateTimeOffset Start = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

    private readonly WebApplication app;

    private readonly HttpClient client;

    private EndpointsHost(WebApplication app, LogCapture logs, ClockFake clock)
    {
        this.app = app;
        client = app.GetTestClient();
        Logs = logs;
        Clock = clock;
    }

    internal LogCapture Logs { get; }

    internal ClockFake Clock { get; }

    internal IServiceProvider Services => app.Services;

    internal InMemoryApproverDirectory Directory => app.Services.GetRequiredService<InMemoryApproverDirectory>();

    internal HeaderPerson Person => app.Services.GetRequiredService<HeaderPerson>();

    /// <summary>Starts the host on a new migrated database.</summary>
    internal static async Task<EndpointsHost> StartAsync(PostgresFixture postgres)
    {
        string connection = await postgres.NewDatabaseAsync();
        await ApprovalsTestDatabase.MigrateAsync(connection, TestContext.Current.CancellationToken);
        SecretVersions secret = new() { Versions = [new SecretVersion { Version = 1, Value = Encoding.UTF8.GetBytes(connection) }] };
        LogCapture logs = new();
        ClockFake clock = new(Start);
        WebApplicationBuilder builder = Builder(logs);
        builder.Services.AddThemis(builder.Configuration);
        builder.Services.AddThemisTesting(secrets => secrets.SetSecret("connection", secret));
        builder.Services.AddSingleton<TimeProvider>(clock);
        builder.Services.AddSingleton<HeaderPerson>();
        builder.Services.AddSingleton<ICurrentPerson>(provider => provider.GetRequiredService<HeaderPerson>());
        WebApplication app = builder.Build();
        app.MapThemisApprovals(Prefix);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return new EndpointsHost(app, logs, clock);
    }

    /// <summary>A web application's builder on a test server, logging to <paramref name="logs"/>.</summary>
    internal static WebApplicationBuilder Builder(LogCapture logs)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.Logging.AddProvider(logs);
        return builder;
    }

    /// <summary>Runs <paramref name="call"/> on a service of a scope of its own, as a host's code would.</summary>
    internal async Task<T> CallAsync<TService, T>(Func<TService, ValueTask<T>> call)
        where TService : notnull
    {
        AsyncServiceScope scope = app.Services.CreateAsyncScope();
        await using (scope)
        {
            return await call(scope.ServiceProvider.GetRequiredService<TService>());
        }
    }

    /// <summary>Publishes <paramref name="definition"/>.</summary>
    internal async Task PublishAsync(ApprovalTypeDefinition definition)
    {
        Result<PublishedVersion> published = await CallAsync<IApprovalTypes, Result<PublishedVersion>>(types => types.PublishAsync(definition, TestContext.Current.CancellationToken));
        Assert.True(published.IsSuccess, published.Failure?.Code);
    }

    /// <summary>Creates <paramref name="request"/> and answers its identifier.</summary>
    internal async Task<RequestId> CreateAsync(ApprovalRequest request)
    {
        Result<CreatedRequest> created = await CallAsync<IApprovals, Result<CreatedRequest>>(approvals => approvals.CreateAsync(request, TestContext.Current.CancellationToken));
        return created.Value?.Request ?? throw new InvalidOperationException($"The create was refused: {created.Failure?.Code}");
    }

    /// <summary>Records <paramref name="delegation"/> and answers its identifier.</summary>
    internal async Task<DelegationId> DelegateAsync(DelegationDefinition delegation)
    {
        Result<DelegationId> created = await CallAsync<IDelegations, Result<DelegationId>>(delegations => delegations.CreateAsync(delegation, TestContext.Current.CancellationToken));
        return created.IsSuccess ? created.Value : throw new InvalidOperationException($"The delegation was refused: {created.Failure?.Code}");
    }

    /// <summary>Reads the request's record through the library.</summary>
    internal async Task<RequestRecord> ReadAsync(RequestId request)
    {
        Result<RequestRecord> read = await CallAsync<IApprovals, Result<RequestRecord>>(approvals => approvals.ReadAsync(request, TestContext.Current.CancellationToken));
        return read.Value ?? throw new InvalidOperationException($"The read was refused: {read.Failure?.Code}");
    }

    /// <summary>Sends <paramref name="body"/> to <paramref name="path"/> under the prefix by <paramref name="method"/>, as <paramref name="person"/> where given, the body as <paramref name="contentType"/>.</summary>
    internal async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? person, string? body = null, string contentType = "application/json")
    {
        using HttpRequestMessage message = new(method, new Uri(Prefix + path, UriKind.Relative));
        if (person is not null)
        {
            message.Headers.Add(HeaderPerson.Header, person);
        }

        if (body is not null)
        {
            message.Content = new StringContent(body, Encoding.UTF8, contentType);
        }

        return await client.SendAsync(message, TestContext.Current.CancellationToken);
    }

    /// <summary>Sends as <see cref="SendAsync"/> does and answers the status and the body read as JSON, none where it is empty.</summary>
    internal async Task<(int Status, JsonElement? Body)> AnswerAsync(HttpMethod method, string path, string? person, string? body = null, string contentType = "application/json")
    {
        using HttpResponseMessage response = await SendAsync(method, path, person, body, contentType);
        string text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        if (text.Length == 0)
        {
            return ((int)response.StatusCode, null);
        }

        using JsonDocument document = JsonDocument.Parse(text);
        return ((int)response.StatusCode, document.RootElement.Clone());
    }

    /// <summary>Asserts that no log line of the host holds any of <paramref name="values"/> (<c>04</c> APR-OPS-005).</summary>
    internal void AssertNoLogHoldsAValueUsed(IEnumerable<string> values)
    {
        Assert.NotEmpty(Logs.Entries);
        foreach (LogCapture.LogEntry entry in Logs.Entries)
        {
            Assert.DoesNotContain(values, value => entry.Message.Contains(value, StringComparison.Ordinal));
        }
    }

    public async ValueTask DisposeAsync()
    {
        client.Dispose();
        await app.StopAsync(TestContext.Current.CancellationToken);
        await app.DisposeAsync();
    }
}