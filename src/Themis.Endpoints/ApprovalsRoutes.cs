using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

using Themis.Core;

namespace Themis.Endpoints;

/// <summary>
/// The endpoints' handlers (<c>03</c> APR-LIB-004). Each reads its person through
/// <see cref="ICurrentPerson"/>, bounded by the HTTP request's own cancellation alone, and
/// answers 401 where there is none, 500 with no details where the seam throws or answers a
/// person reference outside its rule, 415 for a body that is not JSON, 400 with no code for
/// the caller's malformed input, a failure's status with its code and details, and the
/// library's types in <see cref="ApprovalsJson"/> on a success.
/// </summary>
internal static partial class ApprovalsRoutes
{
    /// <summary>The category of the endpoints' log entries.</summary>
    private const string Category = "Themis.Endpoints";

    private delegate Task<IResult> Handler(HttpContext context, string person, CancellationToken cancellationToken);

    internal static Task PendingAsync(HttpContext context)
    {
        return AnswerAsync(context, json: false, static async (context, person, cancellationToken) =>
        {
            string? cursor = context.Request.Query["cursor"].ToString() is { Length: > 0 } given ? given : null;
            int? size = null;
            if (context.Request.Query.TryGetValue("size", out StringValues sizes))
            {
                if (!int.TryParse(sizes.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out int parsed))
                {
                    return Problem(StatusCodes.Status400BadRequest);
                }

                size = parsed;
            }

            Result<PendingPage> page = await ApprovalsOf(context).PendingAsync(person, cursor, size, cancellationToken).ConfigureAwait(false);
            return page.Value is { } found ? Json(found, StatusCodes.Status200OK) : Refused(page.Failure);
        });
    }

    internal static Task RequestAsync(HttpContext context)
    {
        return AnswerAsync(context, json: false, static async (context, person, cancellationToken) =>
        {
            if (Id(context) is not { } id)
            {
                return Problem(StatusCodes.Status400BadRequest);
            }

            Result<RequestRecord> read = await ApprovalsOf(context).ReadAsync(new RequestId(id), cancellationToken).ConfigureAwait(false);
            if (read.Value is not { } record)
            {
                return Refused(read.Failure);
            }

            return await Parties.IsPartyAsync(record, person, context.RequestServices, cancellationToken).ConfigureAwait(false)
                ? Json(record, StatusCodes.Status200OK)
                : Refused(new Failure { Code = Codes.RequestUnknown });
        });
    }

    internal static Task DecideAsync(HttpContext context)
    {
        return AnswerAsync(context, json: true, static async (context, person, cancellationToken) =>
        {
            if (Id(context) is not { } id)
            {
                return Problem(StatusCodes.Status400BadRequest);
            }

            DecisionBody body = await BodyAsync<DecisionBody>(context, cancellationToken).ConfigureAwait(false);
            DecisionRequest decision = new() { Request = new RequestId(id), Decider = person, Outcome = body.Outcome, Reason = body.Reason, ActingFor = body.ActingFor };
            Result<DecisionReceipt> decided = await ApprovalsOf(context).DecideAsync(decision, cancellationToken).ConfigureAwait(false);
            return decided.Value is { } receipt ? Json(receipt, StatusCodes.Status200OK) : Refused(decided.Failure);
        });
    }

    internal static Task DelegationsAsync(HttpContext context)
    {
        return AnswerAsync(context, json: false, static async (context, person, cancellationToken) =>
        {
            IReadOnlyList<DelegationRecord> delegations = await DelegationsOf(context).ListAsync(person, cancellationToken).ConfigureAwait(false);
            return Json(delegations, StatusCodes.Status200OK);
        });
    }

    internal static Task DelegateAsync(HttpContext context)
    {
        return AnswerAsync(context, json: true, static async (context, person, cancellationToken) =>
        {
            DelegationBody body = await BodyAsync<DelegationBody>(context, cancellationToken).ConfigureAwait(false);
            DelegationDefinition delegation = new() { From = person, To = body.To, Starts = body.Starts, Ends = body.Ends, Types = body.Types, CreatedBy = person };
            Result<DelegationId> created = await DelegationsOf(context).CreateAsync(delegation, cancellationToken).ConfigureAwait(false);
            return created.IsSuccess ? Json(created.Value, StatusCodes.Status201Created) : Refused(created.Failure);
        });
    }

    internal static Task EndAsync(HttpContext context)
    {
        return AnswerAsync(context, json: false, static async (context, person, cancellationToken) =>
        {
            if (Id(context) is not { } id)
            {
                return Problem(StatusCodes.Status400BadRequest);
            }

            Result ended = await DelegationsOf(context).EndAsync(new DelegationId(id), person, cancellationToken).ConfigureAwait(false);
            return ended.IsSuccess ? TypedResults.NoContent() : Refused(ended.Failure);
        });
    }

    /// <summary>Reads the person, checks the body's type where the route takes one, runs <paramref name="handler"/> and writes its answer.</summary>
    private static async Task AnswerAsync(HttpContext context, bool json, Handler handler)
    {
        ArgumentNullException.ThrowIfNull(context);
        IResult answer = await AnswerOfAsync(context, json, handler, context.RequestAborted).ConfigureAwait(false);
        await answer.ExecuteAsync(context).ConfigureAwait(false);
    }

    private static async Task<IResult> AnswerOfAsync(HttpContext context, bool json, Handler handler, CancellationToken cancellationToken)
    {
        string? person;
        try
        {
            person = await context.RequestServices.GetRequiredService<ICurrentPerson>().ReadAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            CurrentPersonThrew(Logger(context), exception.GetType().Name);
            return Problem(StatusCodes.Status500InternalServerError);
        }

        if (person is null)
        {
            return Problem(StatusCodes.Status401Unauthorized);
        }

        if (!TextRules.IsReference(person))
        {
            CurrentPersonOutsideItsRule(Logger(context));
            return Problem(StatusCodes.Status500InternalServerError);
        }

        if (json && !context.Request.HasJsonContentType())
        {
            return Problem(StatusCodes.Status415UnsupportedMediaType);
        }

        try
        {
            return await handler(context, person, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException)
        {
            return Problem(StatusCodes.Status400BadRequest);
        }
    }

    private static IApprovals ApprovalsOf(HttpContext context)
    {
        return context.RequestServices.GetRequiredService<IApprovals>();
    }

    private static IDelegations DelegationsOf(HttpContext context)
    {
        return context.RequestServices.GetRequiredService<IDelegations>();
    }

    private static ILogger Logger(HttpContext context)
    {
        return context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(Category);
    }

    /// <summary>The route's identifier, or none where it is not a UUID.</summary>
    private static Guid? Id(HttpContext context)
    {
        return Guid.TryParse(context.Request.RouteValues["id"] as string, out Guid id) ? id : null;
    }

    /// <summary>Reads the body as <typeparamref name="T"/>; a body that does not parse, lacks a member or is <c>null</c> throws <see cref="JsonException"/>.</summary>
    private static async Task<T> BodyAsync<T>(HttpContext context, CancellationToken cancellationToken)
    {
        return await JsonSerializer.DeserializeAsync<T>(context.Request.Body, ApprovalsJson.Options, cancellationToken).ConfigureAwait(false)
            ?? throw new JsonException("The body is null.");
    }

    private static JsonHttpResult<T> Json<T>(T value, int status)
    {
        return TypedResults.Json(value, ApprovalsJson.Options, contentType: null, statusCode: status);
    }

    /// <summary>Problem details of <paramref name="status"/> with no code and no details.</summary>
    private static ProblemHttpResult Problem(int status)
    {
        return TypedResults.Problem(statusCode: status);
    }

    /// <summary>Problem details of the failure's status, with its code and details where that status carries them.</summary>
    private static ProblemHttpResult Refused(Failure? failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        int status = Statuses.For(failure.Code);
        return Statuses.CarriesCode(status)
            ? TypedResults.Problem(statusCode: status, extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = failure.Code, ["details"] = failure.Details })
            : Problem(status);
    }

    [LoggerMessage(EventId = 1, EventName = "CurrentPersonThrew", Level = LogLevel.Error, Message = "The host's ICurrentPerson threw {ExceptionType}, a fault of the host's code; the call answered 500.")]
    private static partial void CurrentPersonThrew(ILogger logger, string exceptionType);

    [LoggerMessage(EventId = 2, EventName = "CurrentPersonOutsideItsRule", Level = LogLevel.Error, Message = "The host's ICurrentPerson answered a person reference outside its rule, a fault of the host's code; the call answered 500.")]
    private static partial void CurrentPersonOutsideItsRule(ILogger logger);
}