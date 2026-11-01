using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

using Themis.Core;
using Themis.Endpoints;

namespace Observatory;

/// <summary>
/// The observatory's own endpoints, for what those of <c>Themis.Endpoints</c> do not serve:
/// they create, cancel and complete requests. The person acting is read through
/// <see cref="ICurrentPerson"/>, as the library's endpoints read it; a refusal answers 409 with
/// the library's code.
/// </summary>
internal static class Endpoints
{
    internal static WebApplication MapObservatory(this WebApplication app)
    {
        app.MapPost("/telescope-time", CreateTelescopeTimeAsync);
        app.MapPost("/instrument-loans", CreateInstrumentLoanAsync);
        app.MapPost("/requests/{request:guid}/cancel", CancelAsync);
        app.MapPost("/requests/{request:guid}/complete", CompleteAsync);
        return app;
    }

    private static Task<IResult> CreateTelescopeTimeAsync(HttpContext http, TelescopeTimeBody body, IApprovals approvals, ICurrentPerson current, CancellationToken cancellationToken)
    {
        return CreateAsync(http, approvals, current, ObservatoryTypes.TelescopeTime, new Target { Kind = "proposal", Id = body.Proposal }, "hours", body.Hours, cancellationToken);
    }

    private static Task<IResult> CreateInstrumentLoanAsync(HttpContext http, InstrumentLoanBody body, IApprovals approvals, ICurrentPerson current, CancellationToken cancellationToken)
    {
        return CreateAsync(http, approvals, current, ObservatoryTypes.InstrumentLoan, new Target { Kind = "instrument", Id = body.Instrument }, "value", body.Value, cancellationToken);
    }

    private static async Task<IResult> CreateAsync(HttpContext http, IApprovals approvals, ICurrentPerson current, string type, Target target, string field, decimal value, CancellationToken cancellationToken)
    {
        if (await current.ReadAsync(http, cancellationToken).ConfigureAwait(false) is not { } person)
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
            ? Results.Created(new Uri($"/approvals/requests/{recorded.Request.Value}", UriKind.Relative), new CreatedBody(recorded.Request.Value, recorded.Status))
            : Refused(created.Failure);
    }

    private static async Task<IResult> CancelAsync(HttpContext http, Guid request, CancelBody body, IApprovals approvals, ICurrentPerson current, CancellationToken cancellationToken)
    {
        if (await current.ReadAsync(http, cancellationToken).ConfigureAwait(false) is not { } person)
        {
            return Results.Unauthorized();
        }

        Result cancelled = await approvals.CancelAsync(new CancelRequest { Request = new RequestId(request), Actor = person, Reason = body.Reason }, cancellationToken).ConfigureAwait(false);
        return cancelled.IsSuccess ? Results.NoContent() : Refused(cancelled.Failure);
    }

    private static async Task<IResult> CompleteAsync(HttpContext http, Guid request, IApprovals approvals, ICurrentPerson current, CancellationToken cancellationToken)
    {
        if (await current.ReadAsync(http, cancellationToken).ConfigureAwait(false) is not { } person)
        {
            return Results.Unauthorized();
        }

        Result completed = await approvals.CompleteAsync(new RequestId(request), person, cancellationToken).ConfigureAwait(false);
        return completed.IsSuccess ? Results.NoContent() : Refused(completed.Failure);
    }

    private static IResult Refused(Failure? failure)
    {
        return Results.Conflict(new RefusalBody(failure?.Code ?? string.Empty));
    }

    internal sealed record TelescopeTimeBody(string Proposal, decimal Hours);

    internal sealed record InstrumentLoanBody(string Instrument, decimal Value);

    internal sealed record CancelBody(string? Reason);

    internal sealed record CreatedBody(Guid Request, RequestStatus Status);

    internal sealed record RefusalBody(string Code);
}