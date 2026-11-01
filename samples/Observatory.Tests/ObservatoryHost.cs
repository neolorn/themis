using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Themis.Core;
using Themis.Testing;

using Xunit;

namespace Observatory.Tests;

/// <summary>
/// The sample host started on a new migrated database through <c>Themis.Testing</c>: its
/// secrets fixed, its jobs run on demand by <see cref="Jobs"/>, its clock the test's, and its
/// observer the recording one, while its directory stays the sample's own table, registered
/// again after the testing package's, as any later registration replaces a seam. Every log
/// line of the host is kept, so a test can assert that none holds a value it used.
/// </summary>
internal sealed class ObservatoryHost : IAsyncDisposable
{
    /// <summary>The instant the host's clock starts at.</summary>
    internal static readonly DateTimeOffset Start = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

    private readonly WebApplicationFactory<Program> factory;

    private readonly HttpClient client;

    private ObservatoryHost(WebApplicationFactory<Program> factory, LogCapture logs, ClockFake clock)
    {
        this.factory = factory;
        client = factory.CreateClient();
        Logs = logs;
        Clock = clock;
        Jobs = new TestJobs(factory.Services.GetRequiredService<IApprovalJobs>());
    }

    internal LogCapture Logs { get; }

    internal ClockFake Clock { get; }

    internal TestJobs Jobs { get; }

    /// <summary>The recording observer, where the host keeps it in place of the sample's own.</summary>
    internal RecordingObserver Observer => factory.Services.GetRequiredService<RecordingObserver>();

    /// <summary>Starts the host on a new migrated database, its observer the sample's own where <paramref name="sampleObserver"/>.</summary>
    internal static async Task<ObservatoryHost> StartAsync(PostgresFixture postgres, bool sampleObserver = false)
    {
        string connection = await postgres.NewDatabaseAsync();
        await ApprovalsTestDatabase.MigrateAsync(connection, TestContext.Current.CancellationToken);
        SecretVersions secret = new() { Versions = [new SecretVersion { Version = 1, Value = Encoding.UTF8.GetBytes(connection) }] };
        LogCapture logs = new();
        ClockFake clock = new(Start);
        return new ObservatoryHost(new Factory(secret, logs, clock, sampleObserver), logs, clock);
    }

    /// <summary>Posts <paramref name="body"/> to <paramref name="path"/> as <paramref name="person"/> and answers the request created.</summary>
    internal async Task<Guid> CreateAsync(string path, object body, string person)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Post, path, person, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument created = await ReadAsync(response);
        return created.RootElement.GetProperty("request").GetGuid();
    }

    /// <summary>Decides <paramref name="request"/> as <paramref name="person"/>, for <paramref name="actingFor"/> where given, through the library's endpoints and answers the request's status after it.</summary>
    internal async Task<string> DecideAsync(Guid request, string person, string outcome, string? reason = null, string? actingFor = null)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"/approvals/requests/{request}/decisions", person, new { outcome, reason, actingFor });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument decided = await ReadAsync(response);
        return decided.RootElement.GetProperty("status").GetString()!;
    }

    /// <summary>The requests and steps of <paramref name="person"/>'s pending list, read through the library's endpoints.</summary>
    internal async Task<IReadOnlyList<(Guid Request, string StepName)>> PendingAsync(string person)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Get, "/approvals/pending", person);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument pending = await ReadAsync(response);
        return [.. pending.RootElement.GetProperty("items").EnumerateArray().Select(item => (item.GetProperty("request").GetGuid(), item.GetProperty("stepName").GetString()!))];
    }

    /// <summary>Sends <paramref name="body"/> to <paramref name="path"/> by <paramref name="method"/>, as <paramref name="person"/> where given.</summary>
    internal async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? person, object? body = null)
    {
        using HttpRequestMessage message = new(method, new Uri(path, UriKind.Relative));
        if (person is not null)
        {
            message.Headers.Add("X-Person", person);
        }

        if (body is not null)
        {
            message.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(message, TestContext.Current.CancellationToken);
    }

    /// <summary>The request's record, read through the library as a host's code would.</summary>
    internal async Task<RequestRecord> ReadAsync(Guid request)
    {
        AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        await using (scope)
        {
            Result<RequestRecord> read = await scope.ServiceProvider.GetRequiredService<IApprovals>().ReadAsync(new RequestId(request), TestContext.Current.CancellationToken);
            return read.Value ?? throw new InvalidOperationException($"The read was refused: {read.Failure?.Code}");
        }
    }

    /// <summary>Asserts that no log line of the host holds any of <paramref name="values"/> (<c>04</c> APR-OPS-005).</summary>
    internal void AssertNoLogHoldsAValueUsed(params string[] values)
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
        await factory.DisposeAsync();
    }

    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response)
    {
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The sample host with the testing package's seams in place of its own, but for its
    /// directory, and its observer where kept, which are registered again after them.
    /// </summary>
    private sealed class Factory(SecretVersions secret, LogCapture logs, ClockFake clock, bool sampleObserver) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
            builder.ConfigureTestServices(services =>
            {
                ServiceDescriptor directory = services.Last(descriptor => descriptor.ServiceType == typeof(IApproverDirectory));
                ServiceDescriptor observer = services.Last(descriptor => descriptor.ServiceType == typeof(IApprovalObserver));
                services.AddThemisTesting(secrets => secrets.SetSecret("connection", secret));
                services.Add(directory);
                if (sampleObserver)
                {
                    services.Add(observer);
                }

                services.AddSingleton<TimeProvider>(clock);
            });
        }
    }
}