using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

using Themis.Core;

namespace Observatory;

/// <summary>
/// The observatory's own endpoints, which call the library: they create, decide, cancel and
/// complete requests and read a person's pending list. The person acting is read from the
/// header <c>X-Person</c>, which stands in for the signed-in person a real host would read;
/// a refusal answers 409 with the library's code.
/// </summary>
internal static class Endpoints
{
    private const string PersonHeader = "X-Person";

    internal static WebApplication MapObservatory(this WebApplication app)
    {
        app.MapPost("/telescope-time", CreateTelescopeTimeAsync);
        app.MapPost("/instrument-loans", CreateInstrumentLoanAsync);
        app.MapPost("/requests/{request:guid}/decisions", DecideAsync);
        app.MapPost("/requests/{request:guid}/cancel", CancelAsync);
        app.MapPost("/requests/{request:guid}/complete", CompleteAsync);
        app.MapGet("/pending", PendingAsync);
        return app;
    }

    private static Task<IResult> CreateTelescopeTimeAsync(HttpRequest http, TelescopeTimeBody body, IApprovals approvals, CancellationToken cancellationToken)
    {
        return CreateAsync(http, approvals, ObservatoryTypes.TelescopeTime, new Target { Kind = "proposal", Id = body.Proposal }, "hours", body.Hours, cancellationToken);
    }

    private static Task<IResult> CreateInstrumentLoanAsync(HttpRequest http, InstrumentLoanBody body, IApprovals approvals, CancellationToken cancellationToken)
    {
        return CreateAsync(http, approvals, ObservatoryTypes.InstrumentLoan, new Target { Kind = "instrument", Id = body.Instrument }, "value", body.Value, cancellationToken);
    }

    private static async Task<IResult> CreateAsync(HttpRequest http, IApprovals approvals, string type, Target target, string field, decimal value, CancellationToken cancellationToken)
    {
        if (Person(http) is not { } person)
        {
            return Results.Unauthorized();
        }

        ApprovalRequest request = new()
        {
            Type = type,
            Target = target,
            Snapshot = new Dictionary<string, FieldValue> { [field] = new FieldValue.DecimalNumber(value) },
            Requester = person,
        };
        Result<CreatedRequest> created = await approvals.CreateAsync(request, cancellationToken).ConfigureAwait(false);
        return created.Value is { } recorded
            ? Results.Created(new Uri($"/requests/{recorded.Request.Value}", UriKind.Relative), new CreatedBody(recorded.Request.Value, recorded.Status))
            : Refused(created.Failure);
    }

    private static async Task<IResult> DecideAsync(HttpRequest http, Guid request, DecisionBody body, IApprovals approvals, CancellationToken cancellationToken)
    {
        if (Person(http) is not { } person)
        {
            return Results.Unauthorized();
        }

        DecisionRequest decision = new() { Request = new RequestId(request), Decider = person, Outcome = body.Outcome, Reason = body.Reason };
        Result<DecisionReceipt> decided = await approvals.DecideAsync(decision, cancellationToken).ConfigureAwait(false);
        return decided.Value is { } receipt ? Results.Ok(new DecidedBody(receipt.Status)) : Refused(decided.Failure);
    }

    private static async Task<IResult> CancelAsync(HttpRequest http, Guid request, CancelBody body, IApprovals approvals, CancellationToken cancellationToken)
    {
        if (Person(http) is not { } person)
        {
            return Results.Unauthorized();
        }

        Result cancelled = await approvals.CancelAsync(new CancelRequest { Request = new RequestId(request), Actor = person, Reason = body.Reason }, cancellationToken).ConfigureAwait(false);
        return cancelled.IsSuccess ? Results.NoContent() : Refused(cancelled.Failure);
    }

    private static async Task<IResult> CompleteAsync(HttpRequest http, Guid request, IApprovals approvals, CancellationToken cancellationToken)
    {
        if (Person(http) is not { } person)
        {
            return Results.Unauthorized();
        }

        Result completed = await approvals.CompleteAsync(new RequestId(request), person, cancellationToken).ConfigureAwait(false);
        return completed.IsSuccess ? Results.NoContent() : Refused(completed.Failure);
    }

    private static async Task<IResult> PendingAsync(HttpRequest http, string? cursor, IApprovals approvals, CancellationToken cancellationToken)
    {
        if (Person(http) is not { } person)
        {
            return Results.Unauthorized();
        }

        Result<PendingPage> pending = await approvals.PendingAsync(person, cursor, size: null, cancellationToken).ConfigureAwait(false);
        return pending.Value is { } page
            ? Results.Ok(new PendingBody([.. page.Items.Select(item => new PendingEntry(item.Request.Value, item.Type, item.Step, item.StepName))], page.Next))
            : Refused(pending.Failure);
    }

    private static string? Person(HttpRequest http)
    {
        string person = http.Headers[PersonHeader].ToString();
        return person.Length > 0 ? person : null;
    }

    private static IResult Refused(Failure? failure)
    {
        return Results.Conflict(new RefusalBody(failure?.Code ?? string.Empty));
    }

    internal sealed record TelescopeTimeBody(string Proposal, decimal Hours);

    internal sealed record InstrumentLoanBody(string Instrument, decimal Value);

    internal sealed record DecisionBody(DecisionOutcome Outcome, string? Reason);

    internal sealed record CancelBody(string? Reason);

    internal sealed record CreatedBody(Guid Request, RequestStatus Status);

    internal sealed record DecidedBody(RequestStatus Status);

    internal sealed record PendingEntry(Guid Request, string Type, int Step, string StepName);

    internal sealed record PendingBody(IReadOnlyList<PendingEntry> Items, string? Next);

    internal sealed record RefusalBody(string Code);
}