# Decision log

Why each choice of the specification was made and what was rejected. Supplementary: the
chapters are complete without it. An entry that changes an earlier one carries
`> **Amended.**` under the earlier entry's heading.

The source of intent is the owner's draft of an approval engine (2026-10-30): its
purpose, its data, its nine rules, its interface and its boundaries. Every rule of it
stands; where this log goes beyond it, the entry says so and why.

---

## T-001 — A product-agnostic approval library

**Date:** 2026-10-31 · **Status:** accepted

**TL;DR.** One library, `Themis`, that answers "who must agree, and did they?" for any
application that imports it. It knows nothing of orders, roles or flows, never calls the
host's business code, and runs in the host's process on the host's database.

- **Product-agnostic.** The owner's rule for every library of his: standalone and
  reusable, never shaped by one host. The owner's draft says the same: no business logic
  inside, nothing hardcoded to one project (`00` APR-OVR-001).
- **A library, not a service.** An approval is asked for in the middle of a host's own
  write (an order confirmed, a role granted); a service would put a network hop and a
  second deployment between them, and lose the host's transaction (`00` APR-OVR-002).
- **The name.** Themis, the Titan of divine law and order: the rules that say who must
  agree. The stored names (the schema `approvals`, the codes `approvals.*`, the lock
  text) are neutral, so a rename would never touch a database.
- **Names and references.** A name (a key, a permission, a role) has one strict
  spelling, so it never needs escaping in a column, a log or a metric tag; a reference
  (a person, a target's identifier) is the host's opaque string, compared exactly.

*Rejected:* a service with its own API (a hop and a deployment more, no shared
transaction); a module inside one application (not reusable, which the owner's draft
asks for).

---

## T-002 — Approval types are published data, in immutable versions

**Date:** 2026-10-31 · **Status:** accepted

**TL;DR.** A host publishes each approval type through `IApprovalTypes.PublishAsync`.
Publishing an equal definition changes nothing; a different one makes the next version.
A request keeps its version for its whole life.

- **The owner's draft:** "Editing a policy creates a new version. Running requests keep
  their version." A version is therefore a stored, numbered, never-changed thing, and a
  request points at one.
- **Data, not configuration.** A host may keep its rules in code, publish them at its
  start, or let an administrator edit them on a screen of its own; all three call one
  operation. Reading the rules from `IConfiguration` would make each process's files the
  source of truth, so two processes could disagree on a version, and an edit would need
  a restart.
- **Idempotent by a canonical hash.** A host that publishes its rules at every start
  must not make a new version each time. Equality is the SHA-256 of one exact
  serialization (`05` APR-ENG-006), in which a member at its default is left out, so a
  later release that adds a member with a default leaves every published definition's
  hash as it was. The form is stored as text, so the bytes hashed are the bytes kept.
- **Retirement** stops new requests and lets open ones end; publishing again revives
  the type. Nothing is deleted.

*Rejected:* rules in configuration files (above); editable rules with a "last edited"
stamp (a request's rules would change under it, the very thing the draft forbids);
comparing definitions member by member in code (one more thing to keep in step with
every new member; the canonical form does it once).

---

## T-003 — What a version holds

> **Amended.** T-021: a definition past 64 KiB names the empty path, the definition
> itself, and is checked once every member is within its rule.

**Date:** 2026-10-31 · **Status:** accepted

**TL;DR.** A key; protected fields with their kinds; ordered policies, each a start
condition and steps; the duplicate setting; watchers; the type's fallback person; and
whether approving at once is allowed where no policy holds.

- **Policies in order.** The draft: "Start condition and priority. The first matching
  policy wins." Order is the priority, so there is no number to tie.
- **A policy whose every step is skipped does not hold.** Otherwise a policy could
  approve a request by its conditions alone, with no person deciding, which the draft's
  "auto-approve only if explicitly allowed" rules out. Such a request moves on to the
  next policy, then to the type's fallback.
- **No policy holding.** The draft: "a named fallback approver. Auto-approve only if
  explicitly allowed for that type." The fallback is one step, named `fallback`, of the
  type's fallback person; `AllowAutoApprove` is false unless the host sets it.
- **Protected fields have kinds** (text, whole number, decimal, flag), so the snapshot
  is checked at create and a condition never meets a value it cannot compare.
- **Limits** on every list and on the definition's size, so a definition, which a host
  may take from an administrator's screen, cannot exhaust the engine.

*Rejected:* a numeric priority (ties need a rule; order has none); untyped snapshots (a
condition comparing text with a number would fail at decision time, not at publish).

---

## T-004 — Conditions are a small data tree

**Date:** 2026-10-31 · **Status:** accepted

**TL;DR.** `AllOf`, `AnyOf`, `Negate`, `Compare`, `OneOf`, `RequesterHas` and
`SubjectHas`, over the snapshot and the roles the host passes at create, checked at
publish.

- **Data, not code.** A condition must be stored in a version and hashed with it, and
  must be checkable at publish, which rules out a lambda or a script.
- **The draft's inputs alone:** "Conditions can use the snapshot data and the
  requester's and subject's roles." The roles are passed by the host at create and kept
  on the request, so a later change of someone's roles never changes which policy a
  request took.
- **Never fails at decision time.** Every field a condition names is a protected field
  of its kind; ordering comparisons only on numbers; a whole number and a decimal
  compare as decimals.
- **Case names** keep clear of the keywords the analysers check (CA1716): `Not` and `In`
  would fail the build.

*Rejected:* an expression language (a parser, an attack surface and a version problem
for a handful of operators); reading the host's current data at decision time (the
library cannot see it, and the answer would change under a request).

---

## T-005 — Steps, assignments and deadlines

**Date:** 2026-10-31 · **Status:** accepted

**TL;DR.** A step names its approvers (a permission or a person), its mode (any one or
all), an optional condition, a fallback person and an optional deadline whose action
escalates, rejects or cancels, never approves. Its assignments are fixed when it starts.

- **The draft's step, as written:** approvers by permission or person; any one or all;
  optional condition; fallback approver; deadline with escalate, reject or cancel;
  "Never auto-approve."
- **Fixed at start.** The draft: "The approver list is fixed at step start." Someone
  given the permission later is not added; someone who loses it is refused at the
  decision (T-008).
- **A cap of 200 persons** per step, so a permission held by a whole organization cannot
  make thousands of assignments.
- **Deadlines in whole seconds**, from 1 minute to 365 days, run by a job (T-019), at
  most once per step.
- **A number out of a list** (two of five) is designed for, since assignments and
  decisions are kept one by one already.

*Rejected:* resolving approvers again at each decision (the list would move under the
approvers, and the record would not show who was asked); an automatic approval at a
deadline (the draft forbids it).

---

## T-006 — The seams a host supplies

**Date:** 2026-10-31 · **Status:** accepted

**TL;DR.** `IApproverDirectory` (who holds a permission for this request; may this
person decide now), `IApprovalObserver` (the events), `ISecretSource` (the connection
string), and optionally `IAlertSink` and `TimeProvider`.

- **The draft's two functions:** "resolveApprovers(step)" and "canApprove(user,
  request)", "the only part to rewrite when copying the engine to another system". They
  are `ResolveAsync` and `CanApproveAsync`, given the request's type, target, persons,
  snapshot and step, and for a check the reference that assigned the person and the
  lender where a delegate decides, so the host can answer each case.
- **The observer** carries the draft's events to the host, which owns what happens
  after an approval. The secret source and the alert sink are the same seams the owner's
  other libraries use, so a host answers them alike.
- **The host vouches for who acts.** Every person reference is the host's statement;
  checking that the caller is that person is the host's (`07` APR-THR-004).

*Rejected:* the library reading the host's users and permissions itself (it would know
the host's tables, which T-001 rules out).

---

## T-007 — Requests and their operations

> **Amended.** T-022: a create whose first step leaves no one to decide it answers the
> status `cancelled`.

**Date:** 2026-10-31 · **Status:** accepted

**TL;DR.** Create (optionally in the host's transaction), decide, cancel, complete,
record a protected field's change, read, a person's pending list, a target's requests,
whether a field is protected, and a person's export. Statuses: `pending`, `approved`,
`rejected`, `cancelled`, `void` and `completed`.

- **The draft's interface:** create, decide, cancel, status and isProtected, with
  events. Each is here; the rest are what a host needs around them: an approver's queue,
  a target's history, and the export a person may ask for.
- **`completed` is added.** The draft's rule 7: "if a protected field changes before the
  action is done, the approval becomes void." The engine can only tell "before the
  action is done" if the host says when it is done, so completion is a status, and after
  it no change voids anything.
- **Locking and voiding.** While pending, the host refuses a change to a protected field
  ("the owning module enforces the lock"); the library answers `Locked` and refuses to
  record the change. After approval it answers `Approved`, and a recorded change voids.
- **Cancel** is always allowed while open, with the actor and the person acted for both
  recorded (the draft: "by the requester, or by the owning module on someone's behalf.
  The real actor is recorded").
- **One open request per type and target**, so two approvals of the same thing never
  run side by side and a protected field has one answer.
- **The host's transaction.** A request recorded with the host's own write commits or
  rolls back with it, so an order never exists without its approval request, or the
  reverse.

*Rejected:* voiding on any change of the target (the draft names protected fields); a
status read without the steps (an approver's screen needs who was asked and who
decided).

---

## T-008 — Separation of duties

**Date:** 2026-10-31 · **Status:** accepted

**TL;DR.** Neither the decider nor the person they act for is ever the requester or the
subject; one person's decision counts once per request; the directory is asked at every
decision; the duplicate setting says whether an earlier approver stands for a later step
or is replaced.

- **The draft's rules 1 and 2**, as written, enforced twice: at a step's start, by not
  assigning the requester and the subject, and at the decision, by refusing them.
- **Counting once.** A person decides each step once; under `RequireOther` they are not
  assigned to a later step they approved before, and cannot decide one.
- **`Skip`** (the draft: "skip the repeat step") marks such an assignment satisfied by
  the earlier decision: an `AnyOne` step completes, an `All` step waits for its other
  assignees. The decision is not recorded twice.

*Rejected:* checking separation only at the decision (an approver's queue would show
requests they can never decide).

---

## T-009 — No dead ends

**Date:** 2026-10-31 · **Status:** accepted

**TL;DR.** Where a step's exclusions leave no one, its fallback person decides, then the
type's; where both are excluded, the request is cancelled with `noapprover` and an alert
raised.

- **The draft's rule 5** names the step's fallback. A fallback can be excluded too (a
  manager asking for their own approval), so the type's fallback comes next, and after
  it a cancel that tells the host, rather than a request no one can ever decide.

*Rejected:* assigning an excluded fallback anyway (separation of duties would break);
leaving the request pending (a dead end the draft forbids).

---

## T-010 — Delegation

**Date:** 2026-10-31 · **Status:** accepted

**TL;DR.** A person lends their authority to decide to another for a period and some or
all types; never to themselves; a delegate never passes it on; both pass every check at
the decision and both are recorded.

- **The draft's rule 3**, as written, with the delegate told through the
  `delegationcreated` event.
- **Lent authority is the lender's own assignments**, so a delegate decides only where
  the lender could, and a chain of lenders is impossible by construction.
- **Ending early** is recorded, never deleted. A period is at most 365 days.

*Rejected:* delegating a permission rather than assignments (it would reach requests the
lender was never asked to decide).

---

## T-011 — Rejection and resubmission

**Date:** 2026-10-31 · **Status:** accepted

**TL;DR.** Any rejection rejects the whole request, with a reason of 1 to 2,000
characters; a resubmission is a new request naming the previous one.

- **The draft's rule 4**, as written. An approval's reason is optional. A resubmission
  starts from its own first step under the type's latest version, since the earlier
  request may have been rejected for the rules themselves.

---

## T-012 — Escalation

> **Amended.** T-023: the fallback an escalation assigns completes the step at its first
> approval, as an escalation assignee does.

**Date:** 2026-10-31 · **Status:** accepted

**TL;DR.** A deadline's escalation adds the target's persons to the step; the first
approval by one of them completes the step whatever its mode; a step escalates once.

- **Why the first escalation approval completes the step:** escalation exists because
  the step's own approvers did not decide; requiring them still, under `All`, would
  leave the escalation powerless.

*Rejected:* replacing the step's assignees (their decisions already made would be lost).

---

## T-013 — An append-only record

**Date:** 2026-10-31 · **Status:** accepted

**TL;DR.** Nothing about a request, a decision, a delegation, a version or an event is
deleted; only the values that record a later move change, each with its event. The
application's roles hold no `DELETE`. An export of what names a person is built;
retention and erasure are designed for.

- **The draft's rule 9:** "nothing is edited or deleted." A request's status and current
  step are kept on its row for reading, and every move that changes them writes its
  event, so the history is the events.
- **Retention and erasure** come later, as a job and a grant; the tables name a person
  only by reference, so erasure is one replacement.

*Rejected:* a status history table besides the events (two records of one fact).

---

## T-014 — Events through an outbox

**Date:** 2026-10-31 · **Status:** accepted

**TL;DR.** Each move writes its event in its own transaction; the `observer` job
delivers events at least once, in order per request, each with a UUIDv7 identifier.

- **The draft's boundaries:** "Events are sent through an outbox. Consumers must ignore
  duplicate event IDs."
- **The draft's six kinds**, plus `stepstarted` (a later step's approvers need to be
  told), `completed` and `delegationcreated` (rule 3's notice).
- **Who to tell** is on each event, so the host's messages need no read of the record;
  no snapshot value or reason travels in an event.
- **One failing request never holds back another**, while each request's events stay in
  order.

*Rejected:* calling the observer inside the move's transaction (a slow or failing host
handler would block or undo the approval).

---

## T-015 — No HTTP endpoints in the first release

**Date:** 2026-10-31 · **Status:** accepted

**TL;DR.** The host's own API calls the library; endpoints are designed for, as a
package of their own.

- An approver's screen shows the host's data (the order, the person), which the library
  does not hold, so the host's endpoint is needed anyway. Endpoints come later on the
  same operations, through a new seam for the signed-in person.

---

## T-016 — Concurrency

**Date:** 2026-10-31 · **Status:** accepted

**TL;DR.** Every move locks the request's row first; a decision on a request no longer
pending is refused; one decision per person per assignment; one lock order everywhere.

- **The draft's rule 8**, as written. The lock order (a type's lock, the request's row,
  then its children) means two of the library's own transactions never deadlock.
- **One open request per type and target** is a unique index, so two creates at once
  record one.

---

## T-017 — How the library is built

> **Amended.** T-020: release candidates of 1.0.0 until the owner calls a release final;
> the compatibility rule binds from the first final release.

**Date:** 2026-10-31 · **Status:** accepted

**TL;DR.** The owner's other libraries' way: .NET 10, EF Core with Npgsql on
PostgreSQL, four packages released together at one version, a public surface checked by
its files and a release gate, tests first, gates as scripts, Conventional Commits, and a
testing package.

- **Packages.** `Themis.Core` (the types and seams, the base class library only),
  `Themis` (the engine), `Themis.Testing` and `Themis.Cli` (`themis migrate`).
- **The same conventions as the owner's messaging library** (layout, settings,
  analysers, gates, release commit, secret scanning), so an implementer and a reviewer
  move between the two without learning a new way.
- **No package feed.** Releases attach the packages to the tag's release; a consumer
  copies them into a local package folder.

---

## T-018 — Calls into the host's directory

**Date:** 2026-10-31 · **Status:** accepted

**TL;DR.** The directory is called inside the transaction of the move that needs it,
each call bounded by `Themis:Timeout`; a call that fails changes nothing.

- **Inside the transaction**, so the persons assigned and the check passed are those of
  the moment the move is written, not of a moment before it.
- **Bounded**, since the request's lock is held meanwhile; with every transaction's
  session timeout, a stalled host never holds a request for long.

*Rejected:* resolving before the transaction (a decision could be checked against a
permission revoked a moment later, before the write).

---

## T-019 — Background work, observability and alerts

> **Amended.** T-022: the health check judges the event lag only in an application whose
> `Themis:Observer:Enabled` is on.

**Date:** 2026-10-31 · **Status:** accepted

**TL;DR.** Three jobs (`deadlines`, `observer`, `watch`), each claimed so it runs once
across processes; logs without persons or values; a meter and a health check; alerts
through `IAlertSink` for a lagging observer, an unassignable request and a stalled job.

- **The same job claims as the owner's messaging library**, so a deployment runs the
  jobs in as many processes as it likes.
- **`request-unassignable`** tells the host its rules have a dead end that T-009 had to
  close by cancelling.

---

## T-020 — Release candidates until the final 1.0.0

**Date:** 2026-10-31 · **Status:** accepted · **Amends:** T-017

**TL;DR.** The owner, 2026-10-31: none of his libraries is final or stable enough yet
for the compatibility rule to bind, so each ships release candidates, `1.0.0-rc.N`,
until he calls a release final.

- **A release candidate is `X.Y.Z-rc.N`,** a SemVer 2.0.0 pre-release numbered from 1
  for each version, tagged `vX.Y.Z-rc.N`, its release marked as a pre-release. MinVer
  reads such a tag as it is. Phase 5's release is `1.0.0-rc.1`. A release candidate is
  made on the lead's message; a final release on the owner's.
- **Before the first final release, nothing binds.** A release candidate of 1.0.0 may
  change or remove anything, the canonical form included: SemVer says a pre-release may
  not meet what its version will promise. Each release candidate's release notes name
  every change and removal since the release before it, so a host knows what to adapt.
  From the first final release on, `03` APR-LIB-006 binds against the newest final
  release; a release candidate is never that baseline, and `X.Y.Z-rc.N` is held to what
  `X.Y.Z` needs.
- **Why the rule itself stays.** A host takes a final release's minor and patch versions
  without reading the library's code, and the rule, enforced by the gate, is what makes
  that safe. A break is not forbidden: it is a new major version.

*Rejected:* versions `0.y.z` (the owner asked for release candidates of 1.0.0, which
carry the version the first final release will have).

**Propagated to:** `03` APR-LIB-006; `05` APR-ENG-011; `implementation-plan.md` (phase
5 and the inputs waited on).

---

## T-021 — Phase 2's question: the member a definition too large names

**Date:** 2026-10-31 · **Status:** accepted · **Amends:** T-003

**TL;DR.** A definition whose canonical form passes 64 KiB is refused with
`approvals.type.invalid` and `details.member` the empty path: the size belongs to the
definition itself, not to one member. The size is checked once every member is within
its rule, since a definition outside its rules has no canonical form to measure.

- **The empty path is the definition itself,** as the empty JSON Pointer (RFC 6901) is
  the whole document, so a host reads every refusal of the code the same way and
  `details.member` is always present.

*Rejected:* leaving `member` out for this one refusal (a detail a host reads present on
every other refusal of the code); a fixed name such as `definition` (a name that could
one day be a member's).

**Propagated to:** `02` APR-TYPE-002; `06` section 1.

---

## T-022 — Phase 3's questions: the lag where no observer runs, and a create with no one

**Date:** 2026-10-31 · **Status:** accepted · **Amends:** T-007, T-019

**TL;DR.** The health check judges the event lag only in an application whose
`Themis:Observer:Enabled` is on, as the `observer-failing` condition does. A create
whose first step leaves no one to decide it is recorded and cancelled with `noapprover`,
and answers the status `cancelled`.

- **The lag where no observer runs.** A deployment that turns the observer off lets its
  events pile up by design (`04` APR-OPS-007), so judging their age would hold its
  health check unhealthy for good with no fault to mend. An application judges the lag
  only where its own `Themis:Observer:Enabled` is on, the reading `06` section 5's
  `observer-failing` already takes; a stalled job is judged as before.
- **A create with no one to decide it.** `01` APR-REQ-007 cancels a request whose step
  leaves no one, writes its `cancelled` event and raises `request-unassignable`, and its
  criterion 2 finds the request `cancelled`, so the request is recorded: the create
  answers a success whose status is `cancelled`, beside `pending` and `approved`.

*Rejected:* judging the lag wherever the jobs run (unhealthy for good where the
deployment chose no observer); refusing such a create with a failure (it would record
nothing, leaving no request for the alert to name and the host no trace to mend its
type's rules by).

**Propagated to:** `01` APR-REQ-001; `04` APR-OPS-005.

---

## T-023 — The fallback an escalation assigns

**Date:** 2026-10-31 · **Status:** accepted · **Amends:** T-012

**TL;DR.** Where an escalation leaves no one and a fallback is assigned in its place,
that fallback stands in for the escalation: its first approval completes the step
whatever its mode. It is still recorded as a fallback assignment (`01` APR-REQ-007).

- **Why.** An escalation exists because the step's own assignees let its deadline pass.
  Read as an ordinary assignment, the fallback of an escalated `All` step would complete
  nothing while those assignees stay silent, and the escalation would change nothing.
  Phase 4 built that reading by rule from APR-REQ-007's "fallback assignment"; the
  lead's review reversed it.
- **No new origin.** A fallback assigned at a step's start is the step's only
  assignment, so its approval completes the step under either mode already; only the
  fallback an escalation adds beside other assignments needs the rule.

*Rejected:* the fallback completing the step by its mode (the escalation would be void
for an `All` step); a fourth `AssignmentOrigin` for it (the record already says it is a
fallback, and the escalation's run instant on the step says when it came).

**Propagated to:** `01` APR-REQ-004, APR-REQ-009 (criterion 4).

---

# Index

| Title | Entry |
|---|---|
| A product-agnostic approval library | T-001 |
| Approval types are published data, in immutable versions | T-002 |
| What a version holds | T-003 |
| Conditions are a small data tree | T-004 |
| Steps, assignments and deadlines | T-005 |
| The seams a host supplies | T-006 |
| Requests and their operations | T-007 |
| Separation of duties | T-008 |
| No dead ends | T-009 |
| Delegation | T-010 |
| Rejection and resubmission | T-011 |
| Escalation | T-012 |
| An append-only record | T-013 |
| Events through an outbox | T-014 |
| No HTTP endpoints in the first release | T-015 |
| Concurrency | T-016 |
| How the library is built | T-017 |
| Calls into the host's directory | T-018 |
| Background work, observability and alerts | T-019 |
| Release candidates until the final 1.0.0 | T-020 |
| Phase 2's question: the member a definition too large names | T-021 |
| Phase 3's questions: the lag where no observer runs, and a create with no one | T-022 |
| The fallback an escalation assigns | T-023 |
