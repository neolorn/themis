using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

using Themis.Core;
using Themis.Operations;
using Themis.Types;

namespace Themis.Requests;

/// <summary>
/// Creates, decides, cancels and reads requests (<c>01</c>), each move in one transaction, the
/// library's own or a host's, that first locks the request's row (<c>04</c> APR-OPS-003). The
/// directory is resolved when a move needs it, so a host without one is stopped by the start's
/// seam check (<c>03</c> APR-LIB-005) rather than by building its services.
/// </summary>
internal sealed partial class ApprovalService(
    ApprovalsReady ready,
    ApprovalsDatabase database,
    IOptions<ApprovalsOptions> options,
    IServiceProvider services,
    ApprovalsMetrics metrics,
    TimeProvider clock,
    ILogger<ApprovalService> logger) : IApprovals
{
    /// <summary>The most roles of a requester or a subject (<c>01</c> APR-REQ-001).</summary>
    private const int MostRoles = 64;

    /// <summary>The most characters of a snapshot's text (<c>01</c> APR-REQ-001).</summary>
    private const int LongestSnapshotText = 1024;

    /// <summary>The most characters of a reason (<c>01</c> APR-REQ-004, APR-REQ-010).</summary>
    private const int LongestReason = 2000;

    private const int PageSize = 20;

    private const int LargestPage = 100;

    public ValueTask<Result<CreatedRequest>> CreateAsync(ApprovalRequest request, CancellationToken cancellationToken)
    {
        CheckRequest(request);
        ready.ThrowIfNotReady();
        return CreateOnAsync(request, host: null, cancellationToken);
    }

    public ValueTask<Result<CreatedRequest>> CreateAsync(ApprovalRequest request, DbTransaction transaction, CancellationToken cancellationToken)
    {
        CheckRequest(request);
        NpgsqlTransaction host = HostTransactions.Check(transaction);
        ready.ThrowIfNotReady();
        return CreateOnAsync(request, host, cancellationToken);
    }

    public async ValueTask<Result<DecisionReceipt>> DecideAsync(DecisionRequest decision, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decision);
        CheckReference(decision.Decider, nameof(decision));
        CheckOptionalReference(decision.ActingFor, nameof(decision));
        if (decision.Outcome is not (DecisionOutcome.Approve or DecisionOutcome.Reject))
        {
            throw new ArgumentException("The outcome is no member of DecisionOutcome.", nameof(decision));
        }

        ready.ThrowIfNotReady();
        if (!ReasonWithinRule(decision.Reason))
        {
            return Result.Fail<DecisionReceipt>(new Failure { Code = Codes.ReasonInvalid });
        }

        if (decision.Outcome == DecisionOutcome.Reject && string.IsNullOrEmpty(decision.Reason))
        {
            return Result.Fail<DecisionReceipt>(new Failure { Code = Codes.ReasonRequired });
        }

        Result<(DecisionReceipt Receipt, RequestMoves Moves)> decided = await RunAsync(null, (context, cancel) => DecideOnAsync(context, decision, cancel), cancellationToken).ConfigureAwait(false);
        return Answer(decided);
    }

    public ValueTask<Result> CancelAsync(CancelRequest cancel, CancellationToken cancellationToken)
    {
        CheckCancel(cancel);
        ready.ThrowIfNotReady();
        return CancelOnAsync(cancel, host: null, cancellationToken);
    }

    public ValueTask<Result> CancelAsync(CancelRequest cancel, DbTransaction transaction, CancellationToken cancellationToken)
    {
        CheckCancel(cancel);
        NpgsqlTransaction host = HostTransactions.Check(transaction);
        ready.ThrowIfNotReady();
        return CancelOnAsync(cancel, host, cancellationToken);
    }

    public async ValueTask<Result<RequestRecord>> ReadAsync(RequestId request, CancellationToken cancellationToken)
    {
        ready.ThrowIfNotReady();
        ApprovalsContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            RequestRow? row = await context.Requests.AsNoTracking().SingleOrDefaultAsync(found => found.Id == request.Value, cancellationToken).ConfigureAwait(false);
            if (row is null)
            {
                return Result.Fail<RequestRecord>(new Failure { Code = Codes.RequestUnknown });
            }

            List<StepRow> steps = await context.Steps.AsNoTracking().Where(step => step.Request == row.Id).OrderBy(step => step.Number).ToListAsync(cancellationToken).ConfigureAwait(false);
            List<AssignmentRow> assignments = await context.Assignments.AsNoTracking().Where(assignment => assignment.Request == row.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
            List<DecisionRow> decisions = await context.Decisions.AsNoTracking().Where(decision => decision.Request == row.Id).OrderBy(decision => decision.At).ThenBy(decision => decision.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(Record(row, steps, assignments, decisions));
        }
    }

    public async ValueTask<Result<PendingPage>> PendingAsync(string person, string? cursor, int? size, CancellationToken cancellationToken)
    {
        CheckReference(person, nameof(person));
        if (size is < 1 or > LargestPage)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "A page holds 1 to 100 items.");
        }

        ready.ThrowIfNotReady();
        (DateTimeOffset At, Guid Id)? after = null;
        if (cursor is not null)
        {
            after = PendingCursor.Read(cursor);
            if (after is null)
            {
                return Result.Fail<PendingPage>(new Failure { Code = Codes.CursorInvalid });
            }
        }

        int take = size ?? PageSize;
        ApprovalsContext context = database.Context();
        await using (context.ConfigureAwait(false))
        {
            IQueryable<PendingRow> pending =
                from assignment in context.Assignments
                join step in context.Steps on new { assignment.Request, Number = assignment.Step } equals new { step.Request, step.Number }
                join request in context.Requests on assignment.Request equals request.Id
                where assignment.Person == person && assignment.Decision == null
                    && step.Status == StepStatus.Current && request.Status == RequestStatus.Pending
                    && request.Requester != person && (request.Subject == null || request.Subject != person)
                select new PendingRow { Request = request.Id, Type = request.Type, TargetKind = request.TargetKind, TargetId = request.TargetId, Step = step.Number, StepName = step.Name, CreatedAt = request.CreatedAt };
            if (after is { } from)
            {
                pending = pending.Where(row => EF.Functions.GreaterThan(ValueTuple.Create(row.CreatedAt, row.Request), ValueTuple.Create(from.At, from.Id)));
            }

            List<PendingRow> rows = await pending.OrderBy(row => row.CreatedAt).ThenBy(row => row.Request).Take(take + 1).AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
            List<PendingItem> items = [.. rows.Take(take).Select(row => new PendingItem
            {
                Request = new RequestId(row.Request),
                Type = row.Type,
                Target = new Target { Kind = row.TargetKind, Id = row.TargetId },
                Step = row.Step,
                StepName = row.StepName,
                CreatedAt = row.CreatedAt,
            })];
            string? next = rows.Count > take ? PendingCursor.Write(rows[take - 1].CreatedAt, rows[take - 1].Request) : null;
            return Result.Success(new PendingPage { Items = items, Next = next });
        }
    }

    private static Result<T> Answer<T>(Result<(T Value, RequestMoves Moves)> outcome)
    {
        if (outcome.Failure is { } failure)
        {
            return Result.Fail<T>(failure);
        }

        outcome.Value.Moves.Count();
        return Result.Success(outcome.Value.Value);
    }

    private static void CheckRequest(ApprovalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Type is null)
        {
            throw new ArgumentException("The request names no type.", nameof(request));
        }

        if (request.Target is null || !TextRules.IsName(request.Target.Kind) || !TextRules.IsReference(request.Target.Id))
        {
            throw new ArgumentException("The target is outside its rule.", nameof(request));
        }

        if (request.Snapshot is null || request.Snapshot.Any(field => field.Key is null || field.Value is null))
        {
            throw new ArgumentException("The snapshot is none, or holds a field that is none.", nameof(request));
        }

        CheckReference(request.Requester, nameof(request));
        CheckOptionalReference(request.Subject, nameof(request));
        CheckRoles(request.RequesterRoles, nameof(request));
        CheckRoles(request.SubjectRoles, nameof(request));
        if (request.Subject is null && request.SubjectRoles.Count > 0)
        {
            throw new ArgumentException("Subject roles are given without a subject.", nameof(request));
        }
    }

    private static void CheckCancel(CancelRequest cancel)
    {
        ArgumentNullException.ThrowIfNull(cancel);
        CheckReference(cancel.Actor, nameof(cancel));
        CheckOptionalReference(cancel.OnBehalfOf, nameof(cancel));
    }

    private static void CheckReference(string? person, string argument)
    {
        if (!TextRules.IsReference(person))
        {
            throw new ArgumentException("A person reference is outside its rule.", argument);
        }
    }

    private static void CheckOptionalReference(string? person, string argument)
    {
        if (person is not null)
        {
            CheckReference(person, argument);
        }
    }

    private static void CheckRoles(IReadOnlyList<string>? roles, string argument)
    {
        if (roles is null || roles.Count > MostRoles || !roles.All(TextRules.IsName))
        {
            throw new ArgumentException("The roles are none, more than 64, or hold a role that is not a name.", argument);
        }
    }

    private static bool ReasonWithinRule(string? reason)
    {
        return reason is null || TextRules.Length(reason) is <= LongestReason;
    }

    /// <summary>The first field of <paramref name="snapshot"/> outside the version's protected fields: missing, of another kind or too long, in the version's order, then one the version does not protect, in ordinal order.</summary>
    private static string? InvalidField(ApprovalTypeDefinition definition, IReadOnlyDictionary<string, FieldValue> snapshot)
    {
        foreach (ProtectedField field in definition.ProtectedFields)
        {
            if (!snapshot.TryGetValue(field.Name, out FieldValue? value) || !OfKind(value, field.Kind))
            {
                return field.Name;
            }
        }

        HashSet<string> protectedNames = new(definition.ProtectedFields.Select(field => field.Name), StringComparer.Ordinal);
        return snapshot.Keys.Where(name => !protectedNames.Contains(name)).Order(StringComparer.Ordinal).FirstOrDefault();
    }

    private static bool OfKind(FieldValue value, FieldKind kind)
    {
        return (value, kind) switch
        {
            (FieldValue.PlainText text, FieldKind.Text) => text.Value is not null && TextRules.Length(text.Value) is <= LongestSnapshotText,
            (FieldValue.WholeNumber, FieldKind.WholeNumber) => true,
            (FieldValue.DecimalNumber, FieldKind.DecimalNumber) => true,
            (FieldValue.Flag, FieldKind.Flag) => true,
            _ => false,
        };
    }

    private static RequestRecord Record(RequestRow row, List<StepRow> steps, List<AssignmentRow> assignments, List<DecisionRow> decisions)
    {
        return new RequestRecord
        {
            Request = new RequestId(row.Id),
            Type = row.Type,
            Version = row.Version,
            Policy = row.Policy,
            Target = new Target { Kind = row.TargetKind, Id = row.TargetId },
            Snapshot = CanonicalForm.ReadSnapshot(row.Snapshot),
            Requester = row.Requester,
            Subject = row.Subject,
            RequesterRoles = row.RequesterRoles,
            SubjectRoles = row.SubjectRoles,
            Previous = row.Previous is Guid previous ? new RequestId(previous) : null,
            Status = row.Status,
            CreatedAt = row.CreatedAt,
            ApprovedAt = row.ApprovedAt,
            ClosedAt = row.ClosedAt,
            ClosedBy = row.ClosedBy,
            ClosedFor = row.ClosedFor,
            Reason = row.Reason,
            ReasonCode = row.ReasonCode,
            ChangedField = row.ChangedField,
            Steps = [.. steps.Select(step => new StepRecord
            {
                Number = step.Number,
                Name = step.Name,
                Mode = step.Mode,
                Status = step.Status,
                StartedAt = step.StartedAt,
                DueAt = step.DueAt,
                DeadlineRanAt = step.DeadlineRanAt,
                Assignments = [.. assignments.Where(assignment => assignment.Step == step.Number).OrderBy(assignment => assignment.Origin).ThenBy(assignment => assignment.Person, StringComparer.Ordinal).Select(assignment => new AssignmentRecord
                {
                    Person = assignment.Person,
                    Origin = assignment.Origin,
                    Decision = assignment.Decision is Guid decision ? new DecisionId(decision) : null,
                })],
            })],
            Decisions = [.. decisions.Select(decision => new DecisionRecord
            {
                Decision = new DecisionId(decision.Id),
                Step = decision.Step,
                Decider = decision.Decider,
                ActingFor = decision.ActingFor,
                Outcome = decision.Outcome,
                Reason = decision.Reason,
                At = decision.At,
            })],
        };
    }

    private static bool IsOpenIndex(Exception failure)
    {
        return failure.GetBaseException() is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_requests_open" };
    }

    private async ValueTask<Result<CreatedRequest>> CreateOnAsync(ApprovalRequest request, NpgsqlTransaction? host, CancellationToken cancellationToken)
    {
        Result<(CreatedRequest Created, RequestMoves Moves)> created = await RunAsync(host, (context, cancel) => CreateInAsync(context, request, cancel), cancellationToken).ConfigureAwait(false);
        Result<CreatedRequest> answer = Answer(created);
        if (answer.Value is { } made)
        {
            string status = Spelling<RequestStatus>.Write(made.Status);
            Recorded(request.Type, made.Request.Value, status);
        }

        return answer;
    }

    private async ValueTask<Result> CancelOnAsync(CancelRequest cancel, NpgsqlTransaction? host, CancellationToken cancellationToken)
    {
        if (!ReasonWithinRule(cancel.Reason))
        {
            return Result.Fail(new Failure { Code = Codes.ReasonInvalid });
        }

        Result<(bool Cancelled, RequestMoves Moves)> cancelled = await RunAsync(host, (context, cancelToken) => CancelInAsync(context, cancel, cancelToken), cancellationToken).ConfigureAwait(false);
        Result<bool> answer = Answer(cancelled);
        return answer.Failure is { } failure ? Result.Fail(failure) : Result.Success();
    }

    /// <summary>Runs <paramref name="work"/> on the host's transaction where one is given, and otherwise in one of the library's own; a refusal answers its failure, nothing of the work written.</summary>
    private async Task<Result<T>> RunAsync<T>(NpgsqlTransaction? host, Func<ApprovalsContext, CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        try
        {
            T value = host is null
                ? await LibraryTransactions.RunAsync(database, options.Value, work, cancellationToken).ConfigureAwait(false)
                : await HostTransactions.RunAsync(host, work, cancellationToken).ConfigureAwait(false);
            return Result.Success(value);
        }
        catch (Refusal refusal)
        {
            return Result.Fail<T>(refusal.Failure);
        }
    }

    private RequestMoves Moves(ApprovalsContext context)
    {
        return new RequestMoves(context, new DirectoryCalls(services.GetRequiredService<IApproverDirectory>(), options.Value.Timeout, clock), metrics, clock.GetUtcNow());
    }

    private async Task<(CreatedRequest, RequestMoves)> CreateInAsync(ApprovalsContext context, ApprovalRequest request, CancellationToken cancellationToken)
    {
        RequestMoves moves = Moves(context);
        if (!TextRules.IsName(request.Type))
        {
            throw Refusal.Of(Codes.TypeUnknown);
        }

        var latest = await (
            from version in context.Versions
            join type in context.Types on version.Key equals type.Key
            where version.Key == request.Type
            orderby version.Version descending
            select new { version.Version, version.Definition, type.Retired })
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false) ?? throw Refusal.Of(Codes.TypeUnknown);
        if (latest.Retired)
        {
            throw Refusal.Of(Codes.TypeRetired);
        }

        ApprovalTypeDefinition definition = CanonicalForm.Read(System.Text.Encoding.UTF8.GetBytes(latest.Definition));
        if (InvalidField(definition, request.Snapshot) is { } field)
        {
            throw Refusal.Of(Codes.SnapshotInvalid, "field", field);
        }

        if (request.Previous is { } previous)
        {
            RequestRow? earlier = await context.Requests.AsNoTracking().SingleOrDefaultAsync(row => row.Id == previous.Value, cancellationToken).ConfigureAwait(false);
            if (earlier is null || earlier.Type != request.Type || earlier.TargetKind != request.Target.Kind || earlier.TargetId != request.Target.Id || earlier.Status is RequestStatus.Pending or RequestStatus.Approved)
            {
                throw Refusal.Of(Codes.PreviousInvalid);
            }
        }

        bool open = await context.Requests.AnyAsync(row => row.Type == request.Type && row.TargetKind == request.Target.Kind && row.TargetId == request.Target.Id && (row.Status == RequestStatus.Pending || row.Status == RequestStatus.Approved), cancellationToken).ConfigureAwait(false);
        if (open)
        {
            throw Refusal.Of(Codes.RequestOpen);
        }

        IReadOnlyList<string>? subjectRoles = request.Subject is null ? null : request.SubjectRoles;
        PolicyDefinition? policy = PolicyChoice.Choose(definition, request.Snapshot, request.RequesterRoles, subjectRoles);
        bool atOnce = policy is null && definition.AllowAutoApprove;
        IReadOnlyList<StepDefinition> steps = policy?.Steps ?? (atOnce ? [] : [PolicyChoice.FallbackStep(definition)]);
        RequestRow row = new()
        {
            Id = Guid.CreateVersion7(moves.Now),
            Type = request.Type,
            Version = latest.Version,
            Policy = policy?.Name,
            TargetKind = request.Target.Kind,
            TargetId = request.Target.Id,
            Snapshot = CanonicalForm.WriteSnapshot(request.Snapshot),
            Requester = request.Requester,
            Subject = request.Subject,
            RequesterRoles = [.. request.RequesterRoles],
            SubjectRoles = [.. request.SubjectRoles],
            Previous = request.Previous?.Value,
            Status = RequestStatus.Pending,
            CreatedAt = moves.Now,
        };
        context.Requests.Add(row);
        context.Steps.AddRange(steps.Select((step, index) => new StepRow { Request = row.Id, Number = index + 1, Name = step.Name, Mode = step.Mode, Status = StepStatus.Waiting }));
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException failure) when (IsOpenIndex(failure))
        {
            throw Refusal.Of(Codes.RequestOpen);
        }

        await moves.StartAsync(row, definition, steps, from: 1, created: true, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        moves.CountCreated(row.Type);
        return (new CreatedRequest { Request = new RequestId(row.Id), Status = row.Status }, moves);
    }

    private async Task<(DecisionReceipt, RequestMoves)> DecideOnAsync(ApprovalsContext context, DecisionRequest decision, CancellationToken cancellationToken)
    {
        RequestMoves moves = Moves(context);
        RequestRow request = await LockAsync(context, decision.Request.Value, cancellationToken).ConfigureAwait(false);
        if (request.Status != RequestStatus.Pending || request.Step is not int current)
        {
            throw Refusal.Of(Codes.RequestClosed);
        }

        // A delegation lends an assignment (01 APR-REQ-008); until delegations are recorded none
        // is live, so a decision acting for another holds no assignment.
        AssignmentRow? assignment = decision.ActingFor is null
            ? await context.Assignments.SingleOrDefaultAsync(row => row.Request == request.Id && row.Step == current && row.Person == decision.Decider && row.Decision == null, cancellationToken).ConfigureAwait(false)
            : null;
        if (assignment is null)
        {
            throw Refusal.Of(Codes.NotAssigned);
        }

        string[] persons = decision.ActingFor is null ? [decision.Decider] : [decision.Decider, decision.ActingFor];
        if (persons.Any(person => person == request.Requester || person == request.Subject))
        {
            throw Refusal.Of(Codes.Separation);
        }

        ApprovalTypeDefinition definition = await DefinitionAsync(context, request, cancellationToken).ConfigureAwait(false);
        bool decided = await context.Decisions.AnyAsync(row => row.Request == request.Id && row.Step == current && row.Decider == decision.Decider, cancellationToken).ConfigureAwait(false);
        bool approvedEarlier = definition.Duplicates == DuplicateRule.RequireOther
            && await context.Decisions.AnyAsync(row => row.Request == request.Id && row.Step < current && row.Outcome == DecisionOutcome.Approve && (persons.Contains(row.Decider) || (row.ActingFor != null && persons.Contains(row.ActingFor))), cancellationToken).ConfigureAwait(false);
        if (decided || approvedEarlier)
        {
            throw Refusal.Of(Codes.Duplicate);
        }

        IReadOnlyDictionary<string, FieldValue> snapshot = CanonicalForm.ReadSnapshot(request.Snapshot);
        ApproverCheck check = new()
        {
            Person = decision.Decider,
            Via = CanonicalForm.ReadApprover(assignment.Via),
            ActingFor = decision.ActingFor,
            Query = RequestMoves.Query(request, snapshot, current),
        };
        if (!await moves.Directory.CanApproveAsync(check, cancellationToken).ConfigureAwait(false))
        {
            throw Refusal.Of(Codes.NotPermitted);
        }

        DecisionRow row = new()
        {
            Id = Guid.CreateVersion7(moves.Now),
            Request = request.Id,
            Step = current,
            Decider = decision.Decider,
            ActingFor = decision.ActingFor,
            Outcome = decision.Outcome,
            Reason = decision.Reason,
            At = moves.Now,
        };
        context.Decisions.Add(row);
        assignment.Decision = row.Id;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        moves.CountDecision(request.Type, decision.Outcome);
        if (decision.Outcome == DecisionOutcome.Reject)
        {
            await moves.CloseAsync(request, definition, RequestStatus.Rejected, EventKind.Rejected, decision.Decider, decision.ActingFor, decision.Reason, reasonCode: null, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            StepRow step = await context.Steps.SingleAsync(found => found.Request == request.Id && found.Number == current, cancellationToken).ConfigureAwait(false);
            List<AssignmentRow> assignments = await context.Assignments.Where(found => found.Request == request.Id && found.Step == current).ToListAsync(cancellationToken).ConfigureAwait(false);
            if (RequestMoves.Satisfied(step.Mode, assignments))
            {
                step.Status = StepStatus.Done;
                await moves.StartAsync(request, definition, StepsOf(definition, request), current + 1, created: false, cancellationToken).ConfigureAwait(false);
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (new DecisionReceipt { Decision = new DecisionId(row.Id), Status = request.Status }, moves);
    }

    private async Task<(bool, RequestMoves)> CancelInAsync(ApprovalsContext context, CancelRequest cancel, CancellationToken cancellationToken)
    {
        RequestMoves moves = Moves(context);
        RequestRow request = await LockAsync(context, cancel.Request.Value, cancellationToken).ConfigureAwait(false);
        if (request.Status is not (RequestStatus.Pending or RequestStatus.Approved))
        {
            throw Refusal.Of(Codes.RequestClosed);
        }

        ApprovalTypeDefinition definition = await DefinitionAsync(context, request, cancellationToken).ConfigureAwait(false);
        await moves.CloseAsync(request, definition, RequestStatus.Cancelled, EventKind.Cancelled, cancel.Actor, cancel.OnBehalfOf, cancel.Reason, reasonCode: null, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (true, moves);
    }

    /// <summary>Locks the request's row, <c>SELECT ... FOR UPDATE</c>, before any other row of it is read for change, and answers it; or refuses with <c>approvals.request.unknown</c>.</summary>
    private static async Task<RequestRow> LockAsync(ApprovalsContext context, Guid id, CancellationToken cancellationToken)
    {
        RequestRow? request = await context.Requests
            .FromSqlRaw("SELECT * FROM approvals.requests WHERE id = @id FOR UPDATE", new NpgsqlParameter("id", id))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return request ?? throw Refusal.Of(Codes.RequestUnknown);
    }

    private static async Task<ApprovalTypeDefinition> DefinitionAsync(ApprovalsContext context, RequestRow request, CancellationToken cancellationToken)
    {
        string definition = await context.Versions.Where(version => version.Key == request.Type && version.Version == request.Version).Select(version => version.Definition).SingleAsync(cancellationToken).ConfigureAwait(false);
        return CanonicalForm.Read(System.Text.Encoding.UTF8.GetBytes(definition));
    }

    /// <summary>The steps the request took: its policy's, or the type's fallback step where it took none.</summary>
    private static IReadOnlyList<StepDefinition> StepsOf(ApprovalTypeDefinition definition, RequestRow request)
    {
        return request.Policy is null ? [PolicyChoice.FallbackStep(definition)] : definition.Policies.Single(policy => policy.Name == request.Policy).Steps;
    }

    [LoggerMessage(EventId = 1, EventName = "RequestRecorded", Level = LogLevel.Information, Message = "Request {Request} of type {Type} recorded, {Status}.")]
    private partial void Recorded(string type, Guid request, string status);

    /// <summary>A row of a pending list's query, its members set by initialisers so the query can order and filter on them.</summary>
    private sealed class PendingRow
    {
        internal Guid Request { get; init; }

        internal required string Type { get; init; }

        internal required string TargetKind { get; init; }

        internal required string TargetId { get; init; }

        internal int Step { get; init; }

        internal required string StepName { get; init; }

        internal DateTimeOffset CreatedAt { get; init; }
    }

    /// <summary>A pending list's cursor: the creation instant and identifier of a page's last request, as base64url.</summary>
    private static class PendingCursor
    {
        internal static string Write(DateTimeOffset at, Guid id)
        {
            byte[] bytes = new byte[24];
            BinaryPrimitives.WriteInt64BigEndian(bytes, at.UtcTicks);
            id.TryWriteBytes(bytes.AsSpan(8), bigEndian: true, out _);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        internal static (DateTimeOffset At, Guid Id)? Read(string cursor)
        {
            string padded = cursor.Replace('-', '+').Replace('_', '/') + new string('=', (4 - (cursor.Length % 4)) % 4);
            Span<byte> bytes = stackalloc byte[25];
            if (cursor.Length != 32 || !Convert.TryFromBase64String(padded, bytes, out int written) || written != 24)
            {
                return null;
            }

            long ticks = BinaryPrimitives.ReadInt64BigEndian(bytes);
            if (ticks < DateTimeOffset.MinValue.UtcTicks || ticks > DateTimeOffset.MaxValue.UtcTicks)
            {
                return null;
            }

            return (new DateTimeOffset(ticks, TimeSpan.Zero), new Guid(bytes[8..24], bigEndian: true));
        }
    }
}