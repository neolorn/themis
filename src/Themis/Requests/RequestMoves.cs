using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Themis.Core;
using Themis.Operations;
using Themis.Types;

namespace Themis.Requests;

/// <summary>
/// The moves of one request inside one transaction, all at one instant: starting its steps
/// with their assignments fixed, approving and closing it, each with its event
/// (<c>01</c> APR-REQ-003, APR-REQ-006, APR-REQ-007, APR-REQ-014). The counts of the meter are
/// kept until the transaction's work succeeds, then added by <see cref="Count"/>.
/// </summary>
internal sealed class RequestMoves(ApprovalsContext context, DirectoryCalls directory, ApprovalsMetrics metrics, DateTimeOffset now)
{
    /// <summary>The most persons a step's approvers may resolve to (<c>01</c> APR-REQ-003).</summary>
    internal const int MostPersons = 200;

    private readonly List<Action> counts = [];

    internal DateTimeOffset Now => now;

    internal DirectoryCalls Directory => directory;

    /// <summary>Adds the counts the moves made to the meter, once the work that made them succeeded.</summary>
    internal void Count()
    {
        foreach (Action count in counts)
        {
            count();
        }
    }

    /// <summary>
    /// Starts the first step from <paramref name="from"/> whose condition holds, recording each
    /// step passed over <c>skipped</c>, and each started step that its assignments satisfy at once
    /// <c>done</c>; approves the request where no step is left, and cancels it with
    /// <c>noapprover</c> where a step finds no one. <paramref name="created"/> marks the create's
    /// own start, whose event is <c>requested</c>.
    /// </summary>
    internal async Task StartAsync(RequestRow request, ApprovalTypeDefinition definition, IReadOnlyList<StepDefinition> steps, int from, bool created, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, FieldValue> snapshot = CanonicalForm.ReadSnapshot(request.Snapshot);
        List<StepRow> rows = await context.Steps.Where(row => row.Request == request.Id).OrderBy(row => row.Number).ToListAsync(cancellationToken).ConfigureAwait(false);
        bool requested = !created;
        for (int number = from; number <= steps.Count; number++)
        {
            StepDefinition step = steps[number - 1];
            StepRow row = rows[number - 1];
            if (step.Condition is not null && !ConditionCheck.Holds(step.Condition, snapshot, request.RequesterRoles, request.Subject is null ? null : request.SubjectRoles))
            {
                row.Status = StepStatus.Skipped;
                continue;
            }

            ApproverQuery query = Query(request, snapshot, number);
            List<AssignmentRow>? assignments = await AssignAsync(request, definition, step, query, cancellationToken).ConfigureAwait(false);
            if (assignments is null)
            {
                if (!requested)
                {
                    await WriteAsync(request, definition, snapshot, EventKind.Requested, step: null, actor: null, actingFor: null, reasonCode: null, notify: [], cancellationToken).ConfigureAwait(false);
                }

                await CloseAsync(request, definition, RequestStatus.Cancelled, EventKind.Cancelled, actor: null, actingFor: null, reason: null, ReasonCode.NoApprover, cancellationToken).ConfigureAwait(false);
                await OccurAsync(request, number, cancellationToken).ConfigureAwait(false);
                return;
            }

            row.Status = StepStatus.Current;
            row.StartedAt = now;
            row.DueAt = step.Deadline is null ? null : now + step.Deadline.After;
            request.Step = number;
            await PersonLock.ShareAsync(context, assignments.Select(assignment => assignment.Person), cancellationToken).ConfigureAwait(false);
            context.Assignments.AddRange(assignments);
            if (requested)
            {
                await WriteAsync(request, definition, snapshot, EventKind.StepStarted, number, actor: null, actingFor: null, reasonCode: null, [.. assignments.Select(assignment => assignment.Person)], cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await WriteAsync(request, definition, snapshot, EventKind.Requested, number, actor: null, actingFor: null, reasonCode: null, [.. assignments.Where(assignment => assignment.Decision is null).Select(assignment => assignment.Person)], cancellationToken).ConfigureAwait(false);
                requested = true;
            }

            if (!Satisfied(step.Mode, deadlineRan: false, assignments))
            {
                return;
            }

            row.Status = StepStatus.Done;
        }

        if (!requested)
        {
            await WriteAsync(request, definition, snapshot, EventKind.Requested, step: null, actor: null, actingFor: null, reasonCode: null, notify: [], cancellationToken).ConfigureAwait(false);
        }

        await ApproveAsync(request, definition, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Approves the request: no step is current, and its <c>approved</c> event names the requester and the watchers.</summary>
    internal async Task ApproveAsync(RequestRow request, ApprovalTypeDefinition definition, CancellationToken cancellationToken)
    {
        request.Status = RequestStatus.Approved;
        request.ApprovedAt = now;
        request.Step = null;
        IReadOnlyDictionary<string, FieldValue> snapshot = CanonicalForm.ReadSnapshot(request.Snapshot);
        await WriteAsync(request, definition, snapshot, EventKind.Approved, step: null, actor: null, actingFor: null, reasonCode: null, [request.Requester], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Closes the request with <paramref name="status"/>, its current step <c>closed</c>, and
    /// writes its <paramref name="kind"/> event naming the requester and the watchers.
    /// </summary>
    internal async Task CloseAsync(RequestRow request, ApprovalTypeDefinition definition, RequestStatus status, EventKind kind, string? actor, string? actingFor, string? reason, ReasonCode? reasonCode, CancellationToken cancellationToken)
    {
        if (request.Step is int current)
        {
            StepRow step = await context.Steps.SingleAsync(row => row.Request == request.Id && row.Number == current, cancellationToken).ConfigureAwait(false);
            step.Status = StepStatus.Closed;
        }

        int? closedStep = request.Step;
        request.Status = status;
        request.Step = null;
        request.ClosedAt = now;
        request.ClosedBy = actor;
        request.ClosedFor = actingFor;
        request.Reason = reason;
        request.ReasonCode = reasonCode;
        IReadOnlyDictionary<string, FieldValue> snapshot = CanonicalForm.ReadSnapshot(request.Snapshot);
        await WriteAsync(request, definition, snapshot, kind, closedStep, actor, actingFor, reasonCode, [request.Requester], cancellationToken).ConfigureAwait(false);
        string type = request.Type;
        string spelt = Spelling<RequestStatus>.Write(status);
        counts.Add(() => metrics.RequestsClosed.Add(1, new KeyValuePair<string, object?>("type", type), new KeyValuePair<string, object?>("status", spelt)));
    }

    /// <summary>
    /// Escalates the step <paramref name="number"/> at its deadline (<c>01</c> APR-REQ-009): its
    /// <c>EscalateTo</c> resolved now, less the exclusions of a step's start and the persons it
    /// already holds, each added as an escalation assignment that no earlier decision satisfies,
    /// so a deadline never approves; where no one is left, the step's fallback, then the type's;
    /// where both are excluded too, the step keeps its own assignees and
    /// <c>request-unassignable</c> occurs. The <c>escalated</c> event names the persons added.
    /// </summary>
    internal async Task EscalateAsync(RequestRow request, ApprovalTypeDefinition definition, StepDefinition step, int number, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, FieldValue> snapshot = CanonicalForm.ReadSnapshot(request.Snapshot);
        ApproverReference escalateTo = step.Deadline?.EscalateTo ?? throw new InvalidOperationException("An escalating deadline names no one to escalate to.");
        IReadOnlyList<string> resolved = escalateTo switch
        {
            ApproverReference.Person person => [person.Reference],
            ApproverReference.Permission permission => await directory.ResolveAsync(permission.Name, Query(request, snapshot, number), cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException("An approver reference is of no case of ApproverReference."),
        };
        List<string> persons = [.. resolved.Distinct(StringComparer.Ordinal)];
        if (persons.Count > MostPersons)
        {
            throw Refusal.Of(Codes.DirectoryTooMany);
        }

        Dictionary<string, Guid> approvedEarlier = await ApprovedEarlierAsync(request.Id, number, cancellationToken).ConfigureAwait(false);
        HashSet<string> holding = new(await context.Assignments.Where(row => row.Request == request.Id && row.Step == number).Select(row => row.Person).ToListAsync(cancellationToken).ConfigureAwait(false), StringComparer.Ordinal);
        bool requireOther = definition.Duplicates == DuplicateRule.RequireOther;
        bool Excluded(string person) => person == request.Requester || person == request.Subject || (requireOther && approvedEarlier.ContainsKey(person)) || holding.Contains(person);

        List<AssignmentRow> added = [.. persons.Where(person => !Excluded(person)).Select(person => Assignment(request.Id, number, person, AssignmentOrigin.Escalation, escalateTo))];
        if (added.Count == 0)
        {
            string? fallback = new[] { step.Fallback, definition.Fallback }.FirstOrDefault(person => !Excluded(person));
            if (fallback is null)
            {
                await OccurAsync(request, number, cancellationToken).ConfigureAwait(false);
                return;
            }

            added.Add(Assignment(request.Id, number, fallback, AssignmentOrigin.Fallback, new ApproverReference.Person(fallback)));
        }

        await PersonLock.ShareAsync(context, added.Select(assignment => assignment.Person), cancellationToken).ConfigureAwait(false);
        context.Assignments.AddRange(added);
        await WriteAsync(request, definition, snapshot, EventKind.Escalated, number, actor: null, actingFor: null, reasonCode: null, [.. added.Select(assignment => assignment.Person)], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Counts a decision of <paramref name="type"/> with <paramref name="outcome"/>.</summary>
    internal void CountDecision(string type, DecisionOutcome outcome)
    {
        string spelt = Spelling<DecisionOutcome>.Write(outcome);
        counts.Add(() => metrics.Decisions.Add(1, new KeyValuePair<string, object?>("type", type), new KeyValuePair<string, object?>("outcome", spelt)));
    }

    /// <summary>Counts a request of <paramref name="type"/> created.</summary>
    internal void CountCreated(string type)
    {
        counts.Add(() => metrics.RequestsCreated.Add(1, new KeyValuePair<string, object?>("type", type)));
    }

    /// <summary>
    /// Whether a step of <paramref name="mode"/> is complete with <paramref name="assignments"/>: any satisfied for <c>AnyOne</c>,
    /// every one for <c>All</c>, or an escalation assignment satisfied, or, where the step's deadline has run (<paramref name="deadlineRan"/>)
    /// and it holds no escalation assignment, a fallback assignment satisfied, which the escalation assigned in its place (<c>01</c> APR-REQ-009, T-023).
    /// </summary>
    internal static bool Satisfied(ApprovalMode mode, bool deadlineRan, IReadOnlyCollection<AssignmentRow> assignments)
    {
        bool standsIn = deadlineRan && !assignments.Any(assignment => assignment.Origin == AssignmentOrigin.Escalation);
        return assignments.Any(assignment => assignment.Decision is not null && (assignment.Origin == AssignmentOrigin.Escalation || (standsIn && assignment.Origin == AssignmentOrigin.Fallback)))
            || (mode == ApprovalMode.AnyOne ? assignments.Any(assignment => assignment.Decision is not null) : assignments.All(assignment => assignment.Decision is not null));
    }

    /// <summary>The query the directory is asked with for the request's step <paramref name="step"/>, or none when watchers are resolved.</summary>
    internal static ApproverQuery Query(RequestRow request, IReadOnlyDictionary<string, FieldValue> snapshot, int? step)
    {
        return new ApproverQuery
        {
            Request = new RequestId(request.Id),
            Type = request.Type,
            Version = request.Version,
            Target = new Target { Kind = request.TargetKind, Id = request.TargetId },
            Requester = request.Requester,
            Subject = request.Subject,
            Snapshot = snapshot,
            Step = step,
        };
    }

    /// <summary>
    /// The assignments of a step starting now: its approvers resolved, each person once, less the
    /// requester, the subject and, under <c>RequireOther</c>, everyone who approved an earlier step
    /// or for whom someone did; under <c>Skip</c> such a person's assignment is satisfied by that
    /// decision. Where no one is left, the step's fallback, then the type's; none where both are
    /// excluded too.
    /// </summary>
    private async Task<List<AssignmentRow>?> AssignAsync(RequestRow request, ApprovalTypeDefinition definition, StepDefinition step, ApproverQuery query, CancellationToken cancellationToken)
    {
        int number = query.Step!.Value;
        List<(string Person, ApproverReference Via)> named = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (ApproverReference approver in step.Approvers)
        {
            IReadOnlyList<string> persons = approver switch
            {
                ApproverReference.Person person => [person.Reference],
                ApproverReference.Permission permission => await directory.ResolveAsync(permission.Name, query, cancellationToken).ConfigureAwait(false),
                _ => throw new InvalidOperationException("An approver reference is of no case of ApproverReference."),
            };
            foreach (string person in persons.Where(seen.Add))
            {
                named.Add((person, approver));
            }
        }

        if (named.Count > MostPersons)
        {
            throw Refusal.Of(Codes.DirectoryTooMany);
        }

        Dictionary<string, Guid> approvedEarlier = await ApprovedEarlierAsync(request.Id, number, cancellationToken).ConfigureAwait(false);
        bool requireOther = definition.Duplicates == DuplicateRule.RequireOther;
        bool Excluded(string person) => person == request.Requester || person == request.Subject || (requireOther && approvedEarlier.ContainsKey(person));

        List<AssignmentRow> assignments = [.. named.Where(entry => !Excluded(entry.Person)).Select(entry => Assignment(request.Id, number, entry.Person, AssignmentOrigin.Approver, entry.Via))];
        if (assignments.Count == 0)
        {
            string? fallback = new[] { step.Fallback, definition.Fallback }.FirstOrDefault(person => !Excluded(person));
            if (fallback is null)
            {
                return null;
            }

            assignments.Add(Assignment(request.Id, number, fallback, AssignmentOrigin.Fallback, new ApproverReference.Person(fallback)));
        }

        if (!requireOther)
        {
            foreach (AssignmentRow assignment in assignments)
            {
                if (approvedEarlier.TryGetValue(assignment.Person, out Guid decision))
                {
                    assignment.Decision = decision;
                }
            }
        }

        return assignments;
    }

    /// <summary>Each person who approved a step before <paramref name="step"/>, or for whom someone did, with that approval.</summary>
    private async Task<Dictionary<string, Guid>> ApprovedEarlierAsync(Guid request, int step, CancellationToken cancellationToken)
    {
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        List<DecisionRow> approvals = await context.Decisions
            .Where(row => row.Request == request && row.Step < step && row.Outcome == DecisionOutcome.Approve)
            .OrderBy(row => row.At)
            .ThenBy(row => row.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        Dictionary<string, Guid> approved = new(StringComparer.Ordinal);
        foreach (DecisionRow approval in approvals)
        {
            approved.TryAdd(approval.Decider, approval.Id);
            if (approval.ActingFor is not null)
            {
                approved.TryAdd(approval.ActingFor, approval.Id);
            }
        }

        return approved;
    }

    private static AssignmentRow Assignment(Guid request, int step, string person, AssignmentOrigin origin, ApproverReference via)
    {
        return new AssignmentRow { Request = request, Step = step, Person = person, Origin = origin, Via = CanonicalForm.WriteApprover(via) };
    }

    /// <summary>
    /// Writes the request's next event of <paramref name="kind"/>, naming <paramref name="notify"/>
    /// and, for every kind but <c>stepstarted</c>, the type's watchers resolved now
    /// (<c>01</c> APR-REQ-014; <c>02</c> APR-TYPE-006).
    /// </summary>
    private async Task WriteAsync(RequestRow request, ApprovalTypeDefinition definition, IReadOnlyDictionary<string, FieldValue> snapshot, EventKind kind, int? step, string? actor, string? actingFor, ReasonCode? reasonCode, List<string> notify, CancellationToken cancellationToken)
    {
        if (kind is not (EventKind.StepStarted or EventKind.Escalated))
        {
            ApproverQuery query = Query(request, snapshot, step: null);
            foreach (ApproverReference watcher in definition.Watchers)
            {
                IReadOnlyList<string> persons = watcher switch
                {
                    ApproverReference.Person person => [person.Reference],
                    ApproverReference.Permission permission => await directory.ResolveAsync(permission.Name, query, cancellationToken).ConfigureAwait(false),
                    _ => throw new InvalidOperationException("A watcher is of no case of ApproverReference."),
                };
                notify.AddRange(persons);
            }
        }

        request.Sequence++;
        context.Events.Add(new EventRow
        {
            Id = Guid.CreateVersion7(now),
            Request = request.Id,
            Sequence = request.Sequence,
            Kind = kind,
            At = now,
            Type = request.Type,
            Version = request.Version,
            TargetKind = request.TargetKind,
            TargetId = request.TargetId,
            Step = step,
            Actor = actor,
            ActingFor = actingFor,
            ReasonCode = reasonCode,
            Notify = [.. notify.Distinct(StringComparer.Ordinal)],
        });
    }

    /// <summary>Records an occurrence of <c>request-unassignable</c> for the request's type, in the move's transaction, for the <c>watch</c> job to raise (<c>04</c> APR-OPS-006).</summary>
    private async Task OccurAsync(RequestRow request, int step, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO approvals.alerts (condition, scope, holding, last_occurred_at, request, step)
            VALUES ('request-unassignable', @scope, false, @now, @request, @step)
            ON CONFLICT (condition, scope) DO UPDATE SET last_occurred_at = @now, request = @request, step = @step
            """,
            [new NpgsqlParameter("scope", request.Type), new NpgsqlParameter("now", now), new NpgsqlParameter("request", request.Id), new NpgsqlParameter("step", step)],
            cancellationToken).ConfigureAwait(false);
    }
}