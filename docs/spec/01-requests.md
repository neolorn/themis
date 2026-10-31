# 01 — Requests

A request's life: how a host asks for an approval, who is asked, how each decision is
checked and counted, how delegation and deadlines work, how a request ends, how the
fields it covers are kept from changing under it, and what the library tells the host.
Every value named `Themis:...` is configuration (`06` section 4). The statuses are in
`06` section 3: a request is **open** while `pending` or `approved`, and **closed** once
`rejected`, `cancelled`, `void` or `completed`.

---

## 1. Asking

**APR-REQ-001** — A host SHALL ask for an approval through one operation,
`IApprovals.CreateAsync`, which records the request under the latest version of a
published, current approval type, chooses its policy, starts its first step, and
returns.

**Values (T-003, T-007).** The `ApprovalRequest` record (`03` APR-LIB-002) carries:
- `Type`: a published type's key;
- `Target`: the kind and the identifier of what the request is about (`00`
  section 1.5);
- `Snapshot`: the values of the version's protected fields, each a `FieldValue` of its
  field's kind (`02` APR-TYPE-002), a text at most 1,024 characters;
- `Requester`: a person reference; `Subject`: a person reference or none;
- `RequesterRoles` and `SubjectRoles`: 0 to 64 role names each, as the host holds them
  at this moment, read by conditions alone (`02` APR-TYPE-005); `SubjectRoles` empty
  where there is no subject;
- `Previous`: the request this one resubmits, or none (APR-REQ-005).

The request is refused, nothing recorded, with a failure (`06` section 1):
- `approvals.type.unknown` or `approvals.type.retired` (`02` APR-TYPE-001);
- `approvals.request.snapshotinvalid`, `details.field`, where the snapshot lacks a
  protected field, holds a field the version does not protect, or holds a value not of
  its field's kind or past its length;
- `approvals.request.open` where an open request of the type already covers the target
  (APR-REQ-002);
- `approvals.request.previousinvalid` where `Previous` names no request, or one of
  another type or target, or one still open;
- `approvals.directory.unavailable` or `approvals.directory.toomany` where starting the
  first step cannot resolve its approvers (APR-REQ-003).

These are faults of the calling code (`05` APR-ENG-003): a person reference, a role name
or a target outside its rule; subject roles without a subject. The request takes its
policy as `02` APR-TYPE-003 gives, records its version, its policy (none for the
fallback step or an approval at once), the instant and the rest of the record, starts
its first step whose condition holds (APR-REQ-003), and writes its `requested` event
(APR-REQ-014). `CreateAsync(ApprovalRequest, DbTransaction, CancellationToken)` records
it on the host's open `NpgsqlTransaction`, so it commits or rolls back with the host's
own write, the library committing nothing; without a transaction, the call records it in
a transaction of its own. Either answers a `Result<CreatedRequest>`: the request's
identifier and its status, `pending`, or `approved` where the type approved it at once.

*Source: T-003, T-007*

**Acceptance criteria**
1. A request created in a host transaction that rolls back leaves no request, no
   assignment and no event.
2. Each refusal above records nothing and names the field where it has one.
3. A snapshot value of the wrong kind for its field is refused with
   `approvals.request.snapshotinvalid` naming the field.

---

**APR-REQ-002** — A target SHALL have at most one open request of each approval type.

**Values (T-016).** A unique index over the type, the target's kind and identifier, for
open requests alone, decides it; a create that meets it inside a host's transaction
waits for the other transaction and is refused once that commits. A request of another
type MAY cover the same target at the same time.

*Source: T-016*

**Acceptance criteria**
1. Two creates of one type for one target at once record one request; the other is
   refused with `approvals.request.open`.
2. After the first request is rejected, a new one for the target is recorded.

---

## 2. Steps, assignments and decisions

**APR-REQ-003** — A step SHALL start with its assignments fixed: the persons its
approvers name at that moment, less those the rules exclude.

**Values (T-005, T-008).** The first step starts when the request is created, and the
next when a step completes; a step whose condition is false is recorded `skipped` and
the one after it is considered, and where none is left the request is `approved`. To
start a step, each approver reference is resolved: a `Person` to itself, a `Permission`
by the directory's `ResolveAsync` (`03` APR-LIB-005), answering the persons holding it;
the union, each person once, is at most 200 persons, more answering the operation
`approvals.directory.toomany` with nothing changed. From it are removed the requester,
the subject, and, where the version's `Duplicates` is `RequireOther`, every person who
approved, or for whom someone approved, an earlier step of the request (APR-REQ-006).
Each person left is one **assignment** of the step; a step left with none goes to its
fallback (APR-REQ-007). The assignments are never resolved again: a person who gains the
permission later is not added, and one who loses it is refused at the decision
(APR-REQ-004). The step's start and its assignments are written with the move that
started it, its `stepstarted` event naming the assignees (for the first step, the
`requested` event does).

*Source: T-005, T-008*

**Acceptance criteria**
1. A step whose permission the directory answers for three persons, one of them the
   requester, has two assignments.
2. A person given the permission after the step started is not assigned to it.
3. A step whose condition is false is recorded skipped, and the next one starts.

---

**APR-REQ-004** — A decision SHALL be accepted only from a person who holds an undecided
assignment of the request's current step, in their own name or through a delegation,
who is neither the requester nor the subject, and whom the host's directory answers as
able to approve at that moment; and it SHALL be recorded atomically and never changed.

**Values (T-007, T-008, T-016).** `IApprovals.DecideAsync(DecisionRequest)` takes the
request, the deciding person, `ActingFor` (none, or the person whose assignment the
decider holds through a delegation, APR-REQ-008), the outcome (`Approve` or `Reject`)
and a reason (optional for an approval; for a rejection required, APR-REQ-005; at most
2,000 characters, a longer one answering `approvals.reason.invalid` before anything is
read). It runs in one transaction that first locks the request's row, then checks, in
this order, answering the first failure and changing nothing:
1. the request exists: `approvals.request.unknown`;
2. it is `pending`: `approvals.request.closed` (a request already decided, or approved,
   included);
3. the assignment it acts on (the decider's own, or `ActingFor`'s through a delegation
   live at this instant and covering the type) is of the current step and undecided:
   `approvals.decision.notassigned`;
4. neither the decider nor `ActingFor` is the requester or the subject:
   `approvals.decision.separation`;
5. the decider has made no decision on this step, and, where `Duplicates` is
   `RequireOther`, neither the decider nor `ActingFor` approved an earlier step:
   `approvals.decision.duplicate`;
6. the directory's `CanApproveAsync` answers true for the decider and, through a
   delegation, for `ActingFor` too: `approvals.decision.notpermitted`.

It then writes the decision (its identifier, the step, the assignment, the decider,
`ActingFor`, the outcome, the reason and the instant), marks the assignment decided, and
moves the request: an approval completes the step where its mode is met (`AnyOne`: this
approval; `All`: every assignment approved) or where an escalation assignee approved
(APR-REQ-009), and the next step starts or the request is `approved`; a rejection
rejects the request (APR-REQ-005). The directory's calls are made inside the
transaction, each bounded by `Themis:Timeout`; one that throws or does not answer within
it answers `approvals.directory.unavailable` and changes nothing. Of two decisions on
one request at once, the second is checked after the first's transaction ends, against
what it left.

*Source: T-007, T-008, T-016*

**Acceptance criteria**
1. A decision by a person with no assignment of the current step is refused with
   `approvals.decision.notassigned`.
2. A delegate who is the request's subject is refused with
   `approvals.decision.separation`.
3. A person the directory no longer answers as able to approve is refused with
   `approvals.decision.notpermitted`, and nothing is recorded.
4. Two approvals of an `AnyOne` step at once record one decision; the other is refused
   with `approvals.decision.notassigned`.
5. A decision on a rejected request is refused with `approvals.request.closed`.
6. An `All` step of three assignments completes at the third approval and not before.

---

**APR-REQ-005** — Any rejection SHALL reject the whole request, with its reason; a
resubmission SHALL be a new request linked to the rejected one.

**Values (T-011).** A person's rejection needs a reason of 1 to 2,000 characters, or is
refused with `approvals.decision.reasonrequired`. The request moves to `rejected`, every
undecided assignment stays undecided, no later step starts, and its `rejected` event is
written. The engine itself rejects only at a deadline whose action says so
(APR-REQ-009), with the reason code `deadline` and no person. A host resubmits by
creating a request whose `Previous` names the closed one (APR-REQ-001); the new request
starts from its own first step under the type's latest version.

*Source: T-011*

**Acceptance criteria**
1. A rejection without a reason is refused and records nothing.
2. A rejection in the first of three steps rejects the request, and no later step
   starts.
3. A resubmission names the rejected request, and reading it answers that link.

---

**APR-REQ-006** — One person's decision SHALL count once per request, and a type's
`Duplicates` SHALL say whether a person who approved one step stands for a later step
they are assigned to or is replaced by someone else.

**Values (T-008).** Counting once: a decider decides each step once, whether in their
own name or for another (APR-REQ-004 check 5). Where a later step starts:
- `Skip`: an assignee who approved an earlier step of the request, or for whom someone
  approved it, has that assignment marked satisfied by the earlier decision, with no new
  decision; an `AnyOne` step with such an assignee completes at once, and an `All` step
  waits for its other assignees alone;
- `RequireOther`: such persons are not assigned (APR-REQ-003), and a step left with no
  one goes to its fallback (APR-REQ-007).

*Source: T-008*

**Acceptance criteria**
1. Under `Skip`, a person who approved step 1 and is assigned to an `AnyOne` step 2
   leaves step 2 complete without a second decision.
2. Under `RequireOther`, the same person is not assigned to step 2, and another assignee
   decides it.

---

**APR-REQ-007** — A step SHALL never be left with no one to decide it: where its
exclusions leave no assignment, it SHALL go to its fallback approver, then to the
type's; where both are excluded, the request SHALL be cancelled with the reason code
`noapprover`.

**Values (T-009).** The step's `Fallback` person is assigned where the rules leave no
one, unless the same rules exclude them (the requester, the subject, or, under
`RequireOther`, a person who approved an earlier step); then the type's `Fallback`; then
the request moves to `cancelled` with no actor and the reason code `noapprover`, its
`cancelled` event is written, and the `request-unassignable` alert is raised for the
type (`04` APR-OPS-006), so the host can mend the type's rules. A fallback assignment is
marked so on the record.

*Source: T-009*

**Acceptance criteria**
1. A step whose permission resolves to the requester alone is assigned to the step's
   fallback person.
2. A request whose requester is both the step's and the type's fallback ends `cancelled`
   with `noapprover`, and the alert is raised.

---

## 3. Delegation and deadlines

**APR-REQ-008** — A person SHALL be able to lend their authority to decide to another
person for a period and for some or all approval types, never to themselves, and a
delegate SHALL never pass on what was lent to them.

**Values (T-010).** `IDelegations.CreateAsync(DelegationDefinition)` takes the person
lending (`From`), the delegate (`To`), the instants it `Starts` and `Ends`, the types it
covers (0 to 64 keys, none meaning every type) and the person recording it (`CreatedBy`,
the host's actor), and answers a `Result<DelegationId>`; it is refused with
`approvals.delegation.invalid`, `details.member`, where `To` is `From`, `Ends` is not
after `Starts` or not after now, or the period passes 365 days. A delegation is **live**
from `Starts` until `Ends` or until `EndAsync(id, actor)` ends it earlier
(`approvals.delegation.unknown` where there is no such delegation,
`approvals.delegation.ended` where it has ended), an ending being recorded with its
actor and instant, never deleted. A live delegation lets `To` decide on `From`'s own
assignments of the types it covers, as APR-REQ-004 checks; it lends nothing that was
lent to `From`, so authority never travels along a chain. Both persons pass the same
checks at the decision, and the decision records both names. Creating one writes its
`delegationcreated` event naming `To` (APR-REQ-014). `ListAsync(person)` answers the
delegations from and to a person that have not ended. Several delegations from one
person MAY be live at once.

*Source: T-010*

**Acceptance criteria**
1. A delegation to oneself is refused.
2. A delegate decides for the lender within the period and is refused with
   `approvals.decision.notassigned` after it ends or is ended.
3. A delegate of a delegate cannot decide for the first lender.
4. A decision through a delegation records the decider and the person acted for.

---

**APR-REQ-009** — A step with a deadline SHALL, once the deadline passes undecided,
escalate, reject or cancel as its definition says, once, and SHALL never approve.

**Values (T-005, T-012).** The `deadlines` job (`04` APR-OPS-004) takes each current
step whose start plus its `After` has passed and on which its action has not run, under
its request's lock, one at a time:
- `Reject`: the request moves to `rejected` with the reason code `deadline`;
- `Cancel`: it moves to `cancelled` with the reason code `deadline`;
- `Escalate`: `EscalateTo` is resolved and the exclusions applied as at a step's start
  (APR-REQ-003), the persons left are added to the step as **escalation** assignments,
  and its `escalated` event names them; the first approval by an escalation assignee
  completes the step whatever its mode, and the step's own assignees may still decide.
  Where no one is left, the step's fallback and then the type's are tried as APR-REQ-007
  gives; where both are excluded, the step keeps its own assignees and the
  `request-unassignable` alert is raised.

The action runs once: the step records the instant it ran, and no later deadline applies
to it. A directory call that fails leaves the step for the job's next run.

*Designed for, not built:* reminders before a deadline, as events a later release writes
from the same step record.

*Source: T-005, T-012*

**Acceptance criteria**
1. A step with a one-hour deadline and the action `Reject` is rejected with `deadline`
   once the clock passes the hour, and not before.
2. An escalated `All` step completes at the first approval of its escalation assignee.
3. A step escalated once is not escalated again.

---

## 4. Ending

**APR-REQ-010** — An open request SHALL always be cancellable, the person who cancelled
it and the one they acted for being recorded.

**Values (T-007).** `IApprovals.CancelAsync(CancelRequest)` takes the request, the actor
(the person the host says cancelled it), `OnBehalfOf` (none, or the person the actor
acted for, a host's module cancelling for its user, say) and a reason (0 to 2,000
characters, a longer one answering `approvals.reason.invalid`). A `pending` or
`approved` request moves to `cancelled` and its `cancelled` event is written; a closed
one answers `approvals.request.closed`; one that does not exist
`approvals.request.unknown`. Who may cancel is the host's to decide.

*Source: T-007*

**Acceptance criteria**
1. A pending request cancelled by its requester ends `cancelled` with the actor
   recorded.
2. An approved request not yet completed can be cancelled; a completed one cannot.

---

**APR-REQ-011** — While a request is pending, the fields it protects SHALL be reported
locked, and once it is approved, a change to one of them before the host completes it
SHALL void the approval.

**Values (T-007).** `IApprovals.ProtectionAsync(target, field)` answers a
`FieldProtection`: `Locked` where a pending request covers the field on the target (the
host refuses the change; the library cannot), `Approved` where an approved request does
(a change voids it), `Free` otherwise; a request covers a field where its version
protects it. `RecordChangeAsync(target, field, actor)`, which the host calls when it
changes a protected field, moves every approved request covering the field on the
target to `void`, writing each one's `voided` event with the actor, and answers their
identifiers; where a pending request covers it, it is refused with
`approvals.field.locked` and changes nothing; otherwise it answers none. Its overload
with a `DbTransaction` records the voiding in the host's transaction, so the host's
change and the void commit together.

*Source: T-007*

**Acceptance criteria**
1. A field of a pending request reads `Locked`, and recording a change to it is refused.
2. A change recorded to an approved request's protected field voids it; a change to a
   field it does not protect leaves it approved.
3. A change recorded after the request is completed voids nothing.

---

**APR-REQ-012** — A host SHALL mark an approved request completed once it has acted on
it, after which no change voids it.

**Values (T-007).** `IApprovals.CompleteAsync(id, actor)`, and its overload with a
`DbTransaction` so the host's action and the completion commit together, moves an
`approved` request to `completed` and writes its `completed` event; a `pending` one
answers `approvals.request.notapproved`, a closed one `approvals.request.closed`.

*Source: T-007*

**Acceptance criteria**
1. Completing a pending request is refused; completing an approved one ends it
   `completed`.

---

## 5. Reading, events and the record

**APR-REQ-013** — A host SHALL be able to read a request's whole record, the requests a
person may decide now, and the requests on a target.

**Values (T-007).** `IApprovals.ReadAsync(id)` answers a `RequestRecord` (`03`
APR-LIB-002): the request as created, its version and policy, its status and the instant
it closed with its actor, the person acted for and its reason or reason code, each step
with its status (`06` section 3), start, deadline and
escalation, each assignment with its person, whether it is a fallback or an escalation,
and the decision that satisfied it, and every decision; or `approvals.request.unknown`.
`PendingAsync(person, cursor, size)` answers, a page at a time (`size` 1 to 100, 20
where none is given, oldest request first, an opaque cursor), the requests on which the
person may decide now: those where they hold an undecided assignment of the current
step, and those where a person who lent them a live delegation covering the type does,
each item naming the request, its type, its target, its step, its creation and the
person acted for, if any; a request whose requester or subject the person is, is left
out. `ByTargetAsync(target)` answers the target's requests, newest first, at most 100. A
cursor that does not read answers `approvals.request.cursorinvalid`.

*Source: T-007*

**Acceptance criteria**
1. A delegate's pending list holds the lender's open assignments of the covered types,
   marked as acted for the lender, and none of another type.
2. A request decided by the person, or past its step, leaves their pending list.

---

**APR-REQ-014** — Every move of a request, and every delegation created, SHALL write an
event in the transaction that makes it, and every event SHALL reach the host's observer
at least once, in order for each request.

**Values (T-014).** The event kinds are in `06` section 6. An event carries its
identifier (a UUIDv7, unique, by which a consumer ignores one it has had), its kind, its
instant, its sequence number within its request, the request, type, version and target
(none for a delegation's), the step where it concerns one, the actor and the person
acted for where a person caused it, the reason code where one applies, and the persons
to tell:
- `requested`: the first step's unsatisfied assignees and the watchers; a request
  approved at once writes `requested` then `approved`;
- `stepstarted` and `escalated`: the step's new assignees;
- `approved`, `rejected`, `cancelled`, `voided` and `completed`: the requester and the
  watchers;
- `delegationcreated`: the delegate.

Watchers are resolved when the event is written (`02` APR-TYPE-006). An event carries no
snapshot value and no reason text, which the host reads from the record. The `observer`
job (`04` APR-OPS-004) delivers events to `IApprovalObserver.ObserveAsync` (`03`
APR-LIB-005), each call bounded by `Themis:Timeout`, an event being delivered only once
every event of its request with a lower sequence number has been, and taken when the
call returns without throwing; a delivered event is marked with the instant it was
taken and kept. The `observer-failing` alert is raised while the oldest undelivered
event is older than `Themis:Observer:LagThreshold`.

*Source: T-014*

**Acceptance criteria**
1. An event written in a host transaction that rolls back is never delivered.
2. An observer that throws is called again with the same event, and a request's events
   reach it in their order.
3. An approval completing the last step writes `approved` naming the requester and the
   watchers.

---

**APR-REQ-015** — The record SHALL be append-only: nothing the library writes about a
request, a decision, a delegation, a version or an event SHALL be deleted, and nothing
SHALL be changed but the values that record a later move.

**Values (T-013).** The values that change, each by a move of this chapter recorded by
its event: a request's status, current step, closing instant, actor, person acted for
and reason; a step's status, start and deadline action; an assignment's decision; an
event's delivery instant; a delegation's ending. The application roles are granted no
`DELETE` on the library's tables (`04` APR-OPS-002). `IApprovals.ExportAsync(person)`
answers what names a person: the requests they made or are the subject of, their
assignments, the decisions they made or that were made for them with the reasons, and
the delegations from and to them.

*Designed for, not built:* deleting closed requests after a retention period, and
erasing a person by replacing their reference throughout, as a job and a grant a later
release adds; the tables already name a person only by reference.

*Source: T-013*

**Acceptance criteria**
1. The application role cannot delete a row of any of the library's tables.
2. An export holds every request, assignment, decision and delegation that names the
   person, and none that does not.
