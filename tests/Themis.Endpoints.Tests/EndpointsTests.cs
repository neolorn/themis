using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using Themis.Core;

using Xunit;

using static Themis.Endpoints.Tests.Approvals;

namespace Themis.Endpoints.Tests;

/// <summary>
/// The endpoints of <c>Themis.Endpoints</c> over HTTP, on a test server, against a real database
/// in a container, each test on a database of its own (<c>03</c> APR-LIB-004).
/// </summary>
[Trait("Category", "integration")]
public sealed class EndpointsTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Approve = """{"outcome":"approve"}""";

    private static readonly DateTimeOffset Start = EndpointsHost.Start;

    [Fact]
    public async Task APR_LIB_004_AC1_CallWithNoPersonAnswers401OnEveryRoute()
    {
        await using EndpointsHost host = await EndpointsHost.StartAsync(postgres);
        await host.PublishAsync(Type());
        RequestId request = await host.CreateAsync(Request());
        DelegationId delegation = await host.DelegateAsync(Delegation(A, Deputy, Start));
        string body = $$"""{"to":"{{Deputy}}","starts":"{{Start:O}}","ends":"{{Start.AddDays(1):O}}"}""";

        int[] statuses =
        [
            (await host.AnswerAsync(HttpMethod.Get, "/pending", person: null)).Status,
            (await host.AnswerAsync(HttpMethod.Get, $"/requests/{request.Value}", person: null)).Status,
            (await host.AnswerAsync(HttpMethod.Post, $"/requests/{request.Value}/decisions", person: null, Approve)).Status,
            (await host.AnswerAsync(HttpMethod.Get, "/delegations", person: null)).Status,
            (await host.AnswerAsync(HttpMethod.Post, "/delegations", person: null, body)).Status,
            (await host.AnswerAsync(HttpMethod.Post, $"/delegations/{delegation.Value}/end", person: null)).Status,
        ];

        Assert.Equal([401, 401, 401, 401, 401, 401], statuses);
        Assert.Empty((await host.ReadAsync(request)).Decisions);
        Assert.Single(await host.CallAsync<IDelegations, System.Collections.Generic.IReadOnlyList<DelegationRecord>>(delegations => delegations.ListAsync(A, TestContext.Current.CancellationToken)));
        host.AssertNoLogHoldsAValueUsed(Unloggable);
    }

    [Fact]
    public async Task APR_LIB_004_AC2_PendingListsThePersonsOwnRequestsAndThoseTheyHoldThroughADelegation()
    {
        await using EndpointsHost host = await EndpointsHost.StartAsync(postgres);
        await host.PublishAsync(Type());
        await host.DelegateAsync(Delegation(B, Deputy, Start));
        RequestId request = await host.CreateAsync(Request());

        (int ownStatus, JsonElement? own) = await host.AnswerAsync(HttpMethod.Get, "/pending?size=5", A);
        (_, JsonElement? delegated) = await host.AnswerAsync(HttpMethod.Get, "/pending", Deputy);
        (_, JsonElement? none) = await host.AnswerAsync(HttpMethod.Get, "/pending", Stranger);

        Assert.Equal(200, ownStatus);
        JsonElement item = Assert.Single(own!.Value.GetProperty("items").EnumerateArray());
        Assert.Equal((request.Value.ToString(), Key, 1, "review"), (item.GetProperty("request").GetString(), item.GetProperty("type").GetString(), item.GetProperty("step").GetInt32(), item.GetProperty("stepName").GetString()));
        Assert.Equal(B, Assert.Single(delegated!.Value.GetProperty("items").EnumerateArray()).GetProperty("actingFor").GetString());
        Assert.Empty(none!.Value.GetProperty("items").EnumerateArray());
        host.AssertNoLogHoldsAValueUsed(Unloggable);
    }

    [Fact]
    public async Task APR_LIB_004_AC2_DecisionIsThePersonsOwnOrForTheLenderTheyHoldADelegationFrom()
    {
        await using EndpointsHost host = await EndpointsHost.StartAsync(postgres);
        await host.PublishAsync(Type());
        await host.DelegateAsync(Delegation(B, Deputy, Start));
        RequestId request = await host.CreateAsync(Request());

        (int ownStatus, JsonElement? own) = await host.AnswerAsync(HttpMethod.Post, $"/requests/{request.Value}/decisions", A, Approve);
        host.Clock.Advance(TimeSpan.FromMinutes(1));
        (int lentStatus, JsonElement? lent) = await host.AnswerAsync(HttpMethod.Post, $"/requests/{request.Value}/decisions", Deputy, $$"""{"outcome":"approve","reason":"{{Reason}}","actingFor":"{{B}}"}""");

        Assert.Equal((200, "pending", 200, "approved"), (ownStatus, own!.Value.GetProperty("status").GetString(), lentStatus, lent!.Value.GetProperty("status").GetString()));
        RequestRecord record = await host.ReadAsync(request);
        Assert.Equal([(A, (string?)null, (string?)null), (Deputy, B, Reason)], record.Decisions.Select(decision => (decision.Decider, decision.ActingFor, decision.Reason)));
        Assert.Equal(record.Decisions[0].Decision.Value.ToString(), own.Value.GetProperty("decision").GetString());
        host.AssertNoLogHoldsAValueUsed(Unloggable);
    }

    [Fact]
    public async Task APR_LIB_004_AC2_RequestReadAnswersTheRecordToEachPartyInTheLibrarysJson()
    {
        await using EndpointsHost host = await EndpointsHost.StartAsync(postgres);
        await host.PublishAsync(Type());
        await host.DelegateAsync(Delegation(B, Deputy, Start, Key));
        RequestId request = await host.CreateAsync(Request());
        await host.AnswerAsync(HttpMethod.Post, $"/requests/{request.Value}/decisions", A, Approve);

        string[] parties = [Requester, Subject, A, B, Deputy, Watcher];
        (int Status, JsonElement? Body)[] answers = await Task.WhenAll(parties.Select(party => host.AnswerAsync(HttpMethod.Get, $"/requests/{request.Value}", party)));

        Assert.All(answers, answer => Assert.Equal(200, answer.Status));
        JsonElement record = answers[0].Body!.Value;
        Assert.Equal((request.Value.ToString(), Key, "pending", Requester, Subject), (record.GetProperty("request").GetString(), record.GetProperty("type").GetString(), record.GetProperty("status").GetString(), record.GetProperty("requester").GetString(), record.GetProperty("subject").GetString()));
        JsonElement amount = record.GetProperty("snapshot").GetProperty("amount");
        Assert.Equal(("decimalNumber", 1250.5m), (amount.GetProperty("case").GetString(), amount.GetProperty("value").GetDecimal()));
        JsonElement step = Assert.Single(record.GetProperty("steps").EnumerateArray());
        Assert.Equal(("all", "current"), (step.GetProperty("mode").GetString(), step.GetProperty("status").GetString()));
        Assert.Equal(["approver", "approver"], step.GetProperty("assignments").EnumerateArray().Select(assignment => assignment.GetProperty("origin").GetString()));
        Assert.Equal("approve", Assert.Single(record.GetProperty("decisions").EnumerateArray()).GetProperty("outcome").GetString());
        host.AssertNoLogHoldsAValueUsed(Unloggable);
    }

    [Fact]
    public async Task APR_LIB_004_AC2_DelegationIsCreatedFromThePersonListedAndEndedWithThePersonItsActor()
    {
        await using EndpointsHost host = await EndpointsHost.StartAsync(postgres);
        string body = $$"""{"from":"{{Stranger}}","createdBy":"{{Stranger}}","to":"{{Deputy}}","starts":"{{Start:O}}","ends":"{{Start.AddDays(3):O}}","types":["{{Key}}"]}""";

        (int created, JsonElement? id) = await host.AnswerAsync(HttpMethod.Post, "/delegations", A, body);
        (int listed, JsonElement? list) = await host.AnswerAsync(HttpMethod.Get, "/delegations", A);
        (_, JsonElement? strangers) = await host.AnswerAsync(HttpMethod.Get, "/delegations", Stranger);
        (int ended, _) = await host.AnswerAsync(HttpMethod.Post, $"/delegations/{id!.Value.GetString()}/end", A);

        Assert.Equal((201, 200, 204), (created, listed, ended));
        JsonElement delegation = Assert.Single(list!.Value.EnumerateArray());
        Assert.Equal((id.Value.GetString(), A, Deputy, A, Key), (delegation.GetProperty("delegation").GetString(), delegation.GetProperty("from").GetString(), delegation.GetProperty("to").GetString(), delegation.GetProperty("createdBy").GetString(), Assert.Single(delegation.GetProperty("types").EnumerateArray()).GetString()));
        Assert.Empty(strangers!.Value.EnumerateArray());
        DelegationRecord record = (await host.CallAsync<IApprovals, PersonExport>(approvals => approvals.ExportAsync(A, TestContext.Current.CancellationToken))).Delegations.Single();
        Assert.Equal((A, (DateTimeOffset?)Start), (record.EndedBy, record.EndedAt));
        host.AssertNoLogHoldsAValueUsed(Unloggable);
    }

    [Fact]
    public async Task APR_LIB_004_AC3_RequestOfWhichThePersonIsNoPartyAnswers404AsAnUnknownOneDoes()
    {
        await using EndpointsHost host = await EndpointsHost.StartAsync(postgres);
        await host.PublishAsync(Type());
        await host.DelegateAsync(Delegation(A, "person-othertype-1", Start, OtherKey));
        await host.DelegateAsync(Delegation(A, "person-later-2", Start.AddDays(1)));
        DelegationId ended = await host.DelegateAsync(Delegation(A, "person-ended-3", Start));
        await host.CallAsync<IDelegations, Result>(delegations => delegations.EndAsync(ended, A, TestContext.Current.CancellationToken));
        await host.DelegateAsync(Delegation(A, "person-expired-4", Start.AddHours(-228)));
        await host.DelegateAsync(Delegation(Stranger, "person-notassignee-5", Start));
        RequestId request = await host.CreateAsync(Request());
        host.Clock.Advance(TimeSpan.FromDays(1) - TimeSpan.FromSeconds(1));

        string[] strangers = [Stranger, "person-othertype-1", "person-later-2", "person-ended-3", "person-expired-4", "person-notassignee-5"];
        (int Status, JsonElement? Body)[] answers = await Task.WhenAll(strangers.Select(stranger => host.AnswerAsync(HttpMethod.Get, $"/requests/{request.Value}", stranger)));
        (int Status, JsonElement? Body) unknown = await host.AnswerAsync(HttpMethod.Get, $"/requests/{Guid.CreateVersion7()}", A);

        Assert.All(answers.Append(unknown), answer => Assert.Equal((404, "approvals.request.unknown"), (answer.Status, answer.Body!.Value.GetProperty("code").GetString())));
        host.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(200, (await host.AnswerAsync(HttpMethod.Get, $"/requests/{request.Value}", "person-later-2")).Status);
        host.AssertNoLogHoldsAValueUsed(Unloggable);
    }

    [Fact]
    public async Task APR_LIB_004_AC4_FailuresAnswerTheirStatusWithCodeAndDetailsAndTheDirectorysFailures503And500WithNone()
    {
        await using EndpointsHost host = await EndpointsHost.StartAsync(postgres);
        await host.PublishAsync(Type());
        await host.PublishAsync(LargeType());
        foreach (int holder in Enumerable.Range(0, 201))
        {
            host.Directory.Grant(Finance, $"person-finance-{holder}");
        }

        RequestId request = await host.CreateAsync(Request());
        RequestId large = await host.CreateAsync(Request(LargeKey, "order-large-5b1e"));
        await host.DelegateAsync(Delegation(B, A, Start));
        DelegationId delegation = await host.DelegateAsync(Delegation(A, Deputy, Start));
        await host.CallAsync<IDelegations, Result>(delegations => delegations.EndAsync(delegation, A, TestContext.Current.CancellationToken));
        string decide = $"/requests/{request.Value}/decisions";

        (int, JsonElement?)[] answers =
        [
            await host.AnswerAsync(HttpMethod.Post, $"/requests/{Guid.CreateVersion7()}/decisions", A, Approve),
            await host.AnswerAsync(HttpMethod.Post, decide, Stranger, Approve),
            await host.AnswerAsync(HttpMethod.Post, decide, A, """{"outcome":"reject"}"""),
            await host.AnswerAsync(HttpMethod.Post, "/delegations", A, $$"""{"to":"{{A}}","starts":"{{Start:O}}","ends":"{{Start.AddDays(1):O}}"}"""),
            await host.AnswerAsync(HttpMethod.Post, $"/delegations/{delegation.Value}/end", A),
            await host.AnswerAsync(HttpMethod.Post, decide, A, Approve),
            await host.AnswerAsync(HttpMethod.Post, decide, A, $$"""{"outcome":"approve","actingFor":"{{B}}"}"""),
            await host.AnswerAsync(HttpMethod.Post, $"/requests/{large.Value}/decisions", A, Approve),
        ];
        host.Directory.FailWith(new InvalidOperationException("The directory is down."));
        (int, JsonElement?) unavailable = await host.AnswerAsync(HttpMethod.Post, decide, B, Approve);

        Assert.Equal(
            [
                (404, "approvals.request.unknown", ""),
                (403, "approvals.decision.notassigned", ""),
                (400, "approvals.decision.reasonrequired", ""),
                (400, "approvals.delegation.invalid", "member=to"),
                (409, "approvals.delegation.ended", ""),
                (200, null, null),
                (409, "approvals.decision.duplicate", ""),
                (500, null, null),
                (503, null, null),
            ],
            answers.Append(unavailable).Select(Problem));
        Assert.Equal(RequestStatus.Pending, (await host.ReadAsync(large)).Status);
        host.AssertNoLogHoldsAValueUsed(Unloggable);
    }

    [Fact]
    public async Task APR_LIB_004_AC6_EachRouteAnswersItsSuccessStatusAndACreatedDelegationNoLocation()
    {
        await using EndpointsHost host = await EndpointsHost.StartAsync(postgres);
        await host.PublishAsync(Type());
        RequestId request = await host.CreateAsync(Request());
        string body = $$"""{"to":"{{Deputy}}","starts":"{{Start:O}}","ends":"{{Start.AddDays(1):O}}"}""";

        using HttpResponseMessage created = await host.SendAsync(HttpMethod.Post, "/delegations", A, body);
        string id = JsonSerializer.Deserialize<string>(await created.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;
        int[] statuses =
        [
            (await host.AnswerAsync(HttpMethod.Get, "/pending", A)).Status,
            (await host.AnswerAsync(HttpMethod.Get, $"/requests/{request.Value}", A)).Status,
            (await host.AnswerAsync(HttpMethod.Post, $"/requests/{request.Value}/decisions", A, Approve)).Status,
            (await host.AnswerAsync(HttpMethod.Get, "/delegations", A)).Status,
            (int)created.StatusCode,
            (await host.AnswerAsync(HttpMethod.Post, $"/delegations/{id}/end", A)).Status,
        ];

        Assert.Equal([200, 200, 200, 200, 201, 204], statuses);
        Assert.Null(created.Headers.Location);
        Assert.True(Guid.TryParse(id, out _));
        host.AssertNoLogHoldsAValueUsed(Unloggable);
    }

    [Fact]
    public async Task APR_LIB_004_AC7_MalformedInputAnswers400WithNoCodeABodyNotJson415AndAPersonOutsideItsRuleOrASeamThatThrows500()
    {
        await using EndpointsHost host = await EndpointsHost.StartAsync(postgres);
        await host.PublishAsync(Type());
        RequestId request = await host.CreateAsync(Request());
        string decide = $"/requests/{request.Value}/decisions";
        string tooLong = new('p', 129);

        (int, JsonElement?)[] malformed =
        [
            await host.AnswerAsync(HttpMethod.Post, decide, A, "{"),
            await host.AnswerAsync(HttpMethod.Post, decide, A, "{}"),
            await host.AnswerAsync(HttpMethod.Post, decide, A, """{"outcome":"maybe"}"""),
            await host.AnswerAsync(HttpMethod.Post, decide, A, """{"outcome":0}"""),
            await host.AnswerAsync(HttpMethod.Post, decide, A, "null"),
            await host.AnswerAsync(HttpMethod.Post, decide, A, $$"""{"outcome":"approve","actingFor":"{{tooLong}}"}"""),
            await host.AnswerAsync(HttpMethod.Post, "/delegations", A, $$"""{"to":"{{tooLong}}","starts":"{{Start:O}}","ends":"{{Start.AddDays(1):O}}"}"""),
            await host.AnswerAsync(HttpMethod.Post, "/delegations", A, $$"""{"to":"{{Deputy}}","starts":"{{Start:O}}","ends":"{{Start.AddDays(1):O}}","types":null}"""),
            await host.AnswerAsync(HttpMethod.Post, "/delegations", A, $$"""{"to":"{{Deputy}}","ends":"{{Start.AddDays(1):O}}"}"""),
            await host.AnswerAsync(HttpMethod.Post, "/delegations/not-an-id/end", A),
            await host.AnswerAsync(HttpMethod.Get, "/pending?size=0", A),
            await host.AnswerAsync(HttpMethod.Get, "/pending?size=many", A),
        ];
        int notJson = (await host.AnswerAsync(HttpMethod.Post, decide, A, Approve, "text/plain")).Status;
        (int, JsonElement?) outsideItsRule = await host.AnswerAsync(HttpMethod.Get, "/pending", tooLong);
        host.Person.Throw();
        (int, JsonElement?) thrown = await host.AnswerAsync(HttpMethod.Get, "/pending", A);

        Assert.Equal([.. Enumerable.Repeat<(int, string?, string?)>((400, null, null), 12)], malformed.Select(Problem));
        Assert.Equal(415, notJson);
        Assert.Equal([(500, null, null), (500, null, null)], new[] { outsideItsRule, thrown }.Select(Problem));
        Assert.Empty((await host.ReadAsync(request)).Decisions);
        host.AssertNoLogHoldsAValueUsed([.. Unloggable, tooLong]);
    }

    /// <summary>An answer's status, and its problem details' code and details as <c>name=value</c>, none for a success or where it carries none.</summary>
    private static (int Status, string? Code, string? Details) Problem((int Status, JsonElement? Body) answer)
    {
        if (answer.Status is >= 200 and < 300 || answer.Body is not { } body || !body.TryGetProperty("code", out JsonElement code))
        {
            return (answer.Status, null, null);
        }

        string details = body.TryGetProperty("details", out JsonElement found)
            ? string.Join(',', found.EnumerateObject().Select(detail => $"{detail.Name}={detail.Value.GetString()}"))
            : string.Empty;
        return (answer.Status, code.GetString(), details);
    }
}