# 03 — Library contract

What a host references, what it calls, what it supplies, and what the library promises
not to break. Every value named `Themis:...` is configuration (`06` section 4).

---

**APR-LIB-001** — The library SHALL ship as the packages of `00` section 2.2, and each
project SHALL reference only what this item allows it.

**Values (T-001, T-017, T-024).**

| Project | May reference |
|---|---|
| `Themis.Core` | The base class library only |
| `Themis` | `Themis.Core`; EF Core, its relational layer and the Npgsql provider; the Microsoft.Extensions abstractions of `05` APR-ENG-009 |
| `Themis.Testing` | `Themis.Core`, `Themis` |
| `Themis.Cli` (a .NET tool, command `themis`) | `Themis` |
| `tests/Themis.Repository.Tests` | Every shipped project, whose built assemblies its contract tests read (`05` APR-ENG-004), and the test packages of `05` APR-ENG-009 |
| Any other test project | Its source project, `Themis.Testing`, and the test packages of `05` APR-ENG-009 |
| The sample host and its test project | The packed packages above, ASP.NET Core's shared framework, and for its tests the sample host itself and the test packages of `05` APR-ENG-009 (T-024) |

Every shipped project references `Microsoft.CodeAnalysis.PublicApiAnalyzers` and every
packed project `MinVer`, both as build-only references; `Themis` references
`Microsoft.EntityFrameworkCore.Design` as a build-only reference for making migrations.
No shipped project references ASP.NET Core: the first release has no endpoints
(APR-LIB-004). No package names any other library of a host (T-001). The library's
packages depend on one another at exactly the version packed (`[v]`, written as `05`
APR-ENG-011 gives), since they are released together and grant one another their
internals (`05` APR-ENG-001), so NuGet reports a host's mix of versions at its restore
rather than letting it fail at run time.

*Source: T-001, T-017, T-024*

**Acceptance criteria**
1. A test reads each project's references and fails on one this table does not allow.
2. A test packs the packages from one commit and fails on a dependency of one of them on
   another of the library's packages that is not the exact version packed (`[v]`).

---

**APR-LIB-002** — The public surface SHALL be exactly what the packages' public API
files list, each public type SHALL have the shape below, and a change to it SHALL be
visible in review.

**Values (T-002, T-003, T-007, T-017).** Data types are records with `init` members, so
that a later release adds members without a break, a non-nullable member being
`required`, a list member never null; every asynchronous member takes a
`CancellationToken` as its last parameter and answers a `ValueTask`. A type described as
having cases is an abstract record whose cases are nested sealed records of those names,
made with `new` (`new ApproverReference.Permission("orders.approve")`), read by pattern
matching; a value of any other type derived from it is a fault of the code that made it.
An enumeration is a C# `enum` whose members are named, and declared in the order, as
`06` sections 2 and 3 give; each member carries its wire spelling by
`JsonStringEnumMemberName`, and wherever the library writes a member as text (a column,
the canonical form, an alert's details, a log entry, a metric tag) it writes that
spelling. A record that carries a person reference, a target, a role, a snapshot or
field value or a reason overrides `PrintMembers` to print none of them, so a log line
that formats one writes none (`04` APR-OPS-005).

**Values and identifiers** (`Themis.Core`):

| Type | Shape |
|---|---|
| `RequestId`, `DecisionId`, `DelegationId`, `ApprovalEventId` | `readonly record struct RequestId(Guid Value)` and alike, UUID version 7 |
| `Target` | `string Kind` (a name), `string Id` (a reference) (`00` section 1.5) |
| `FieldValue` | Cases `PlainText(string Value)` (named so, since `Text` would match the namespace `System.Text`, CA1724), `WholeNumber(long Value)`, `DecimalNumber(decimal Value)`, `Flag(bool Value)`, one for each `FieldKind` |
| `Result`, `Result<T>`, `Failure` | `Result`: `bool IsSuccess`, `Failure? Failure`, and the factories `static Result Success()`, `static Result Fail(Failure failure)`, `static Result<T> Success<T>(T value)` and `static Result<T> Fail<T>(Failure failure)`. `Result<T>`: `bool IsSuccess`, `T? Value`, `Failure? Failure`. `Failure`: `string Code` of `06` section 1 and `IReadOnlyDictionary<string, string> Details` |
| `ApprovalsStartupException` | The exception a "(startup)" code of `06` section 1 is thrown in when the host starts: `string Code`, `IReadOnlyDictionary<string, string> Details` |
| Enumerations | `FieldKind`, `CompareOperator`, `ApprovalMode`, `DeadlineAction`, `DuplicateRule`, `DecisionOutcome`, `RequestStatus`, `StepStatus`, `AssignmentOrigin`, `ReasonCode`, `FieldProtection`, `EventKind`, `ApprovalJob`, with the members of `06` sections 2 and 3 |

**Approval types** (`Themis.Core`; `02`), members in this order, which is the order of
the canonical form (`05` APR-ENG-006):

| Type | Shape |
|---|---|
| `ApprovalTypeDefinition` | `string Key`, `IReadOnlyList<ProtectedField> ProtectedFields`, `IReadOnlyList<PolicyDefinition> Policies`, `DuplicateRule Duplicates`, `IReadOnlyList<ApproverReference> Watchers`, `string Fallback`, `bool AllowAutoApprove` |
| `ProtectedField` | `string Name`, `FieldKind Kind` |
| `PolicyDefinition` | `string Name`, `Condition? StartCondition`, `IReadOnlyList<StepDefinition> Steps` |
| `StepDefinition` | `string Name`, `IReadOnlyList<ApproverReference> Approvers`, `ApprovalMode Mode`, `Condition? Condition`, `string Fallback`, `Deadline? Deadline` |
| `Deadline` | `TimeSpan After`, `DeadlineAction Action`, `ApproverReference? EscalateTo` |
| `ApproverReference` | Cases `Permission(string Name)` and `Person(string Reference)` |
| `Condition` | Cases `AllOf(IReadOnlyList<Condition> Conditions)`, `AnyOf(IReadOnlyList<Condition> Conditions)`, `Negate(Condition Operand)`, `Compare(string Field, CompareOperator Operator, FieldValue Value)`, `OneOf(string Field, IReadOnlyList<FieldValue> Values)`, `RequesterHas(string Role)`, `SubjectHas(string Role)` (`02` APR-TYPE-005; the names keep clear of the keywords CA1716 checks) |
| `PublishedVersion` | `string Key`, `int Version`, `bool Created` (whether this call made it) |
| `ApprovalTypeVersion` | `string Key`, `int Version`, `ApprovalTypeDefinition Definition`, `DateTimeOffset PublishedAt`, `bool Retired` (the type's state now) |

**Requests** (`Themis.Core`; `01`):

| Type | Shape |
|---|---|
| `ApprovalRequest` | `string Type`, `Target Target`, `IReadOnlyDictionary<string, FieldValue> Snapshot`, `string Requester`, `string? Subject`, `IReadOnlyList<string> RequesterRoles`, `IReadOnlyList<string> SubjectRoles`, `RequestId? Previous` (`01` APR-REQ-001) |
| `CreatedRequest` | `RequestId Request`, `RequestStatus Status` |
| `DecisionRequest` | `RequestId Request`, `string Decider`, `string? ActingFor`, `DecisionOutcome Outcome`, `string? Reason` (`01` APR-REQ-004) |
| `DecisionReceipt` | `DecisionId Decision`, `RequestStatus Status` (the request's status once the decision is recorded) |
| `CancelRequest` | `RequestId Request`, `string Actor`, `string? OnBehalfOf`, `string? Reason` (`01` APR-REQ-010) |
| `RequestRecord` | `RequestId Request`, `string Type`, `int Version`, `string? Policy`, `Target Target`, `IReadOnlyDictionary<string, FieldValue> Snapshot`, `string Requester`, `string? Subject`, `IReadOnlyList<string> RequesterRoles`, `IReadOnlyList<string> SubjectRoles`, `RequestId? Previous`, `RequestStatus Status`, `DateTimeOffset CreatedAt`, `DateTimeOffset? ApprovedAt`, `DateTimeOffset? ClosedAt`, `string? ClosedBy`, `string? ClosedFor`, `string? Reason`, `ReasonCode? ReasonCode`, `string? ChangedField` (the field whose change voided it), `IReadOnlyList<StepRecord> Steps`, `IReadOnlyList<DecisionRecord> Decisions` (`01` APR-REQ-013) |
| `StepRecord` | `int Number` (from 1, in the policy's order), `string Name`, `ApprovalMode Mode`, `StepStatus Status`, `DateTimeOffset? StartedAt`, `DateTimeOffset? DueAt`, `DateTimeOffset? DeadlineRanAt`, `IReadOnlyList<AssignmentRecord> Assignments` |
| `AssignmentRecord` | `string Person`, `AssignmentOrigin Origin`, `DecisionId? Decision` (the decision that satisfied it: its own, or under `Skip` an earlier step's) |
| `DecisionRecord` | `DecisionId Decision`, `int Step`, `string Decider`, `string? ActingFor`, `DecisionOutcome Outcome`, `string? Reason`, `DateTimeOffset At` |
| `PendingPage`, `PendingItem` | `IReadOnlyList<PendingItem> Items`, `string? Next` (the next page's cursor, none on the last); `RequestId Request`, `string Type`, `Target Target`, `int Step`, `string StepName`, `DateTimeOffset CreatedAt`, `string? ActingFor` (`01` APR-REQ-013) |
| `RequestSummary` | `RequestId Request`, `string Type`, `int Version`, `RequestStatus Status`, `DateTimeOffset CreatedAt`, `DateTimeOffset? ClosedAt` |
| `PersonExport` | `IReadOnlyList<RequestRecord> Requests` (those the person made or is the subject of), `IReadOnlyList<AssignmentExport> Assignments` (each `RequestId Request`, `int Step`, `AssignmentOrigin Origin`, `DecisionId? Decision`), `IReadOnlyList<DecisionExport> Decisions` (each `RequestId Request` and the members of `DecisionRecord`: those the person made or that were made for them), `IReadOnlyList<DelegationRecord> Delegations` (`01` APR-REQ-015) |

**Delegations and events** (`Themis.Core`; `01`):

| Type | Shape |
|---|---|
| `DelegationDefinition` | `string From`, `string To`, `DateTimeOffset Starts`, `DateTimeOffset Ends`, `IReadOnlyList<string> Types` (empty: every type), `string CreatedBy` (`01` APR-REQ-008) |
| `DelegationRecord` | `DelegationId Delegation`, `string From`, `string To`, `DateTimeOffset Starts`, `DateTimeOffset Ends`, `IReadOnlyList<string> Types`, `string CreatedBy`, `DateTimeOffset CreatedAt`, `DateTimeOffset? EndedAt`, `string? EndedBy` |
| `ApprovalEvent` | `ApprovalEventId Event`, `EventKind Kind`, `DateTimeOffset At`, `long Sequence` (from 1 within its request; 1 for a delegation's), `RequestId? Request`, `string? Type`, `int? Version`, `Target? Target`, `int? Step`, `string? Actor`, `string? ActingFor`, `ReasonCode? ReasonCode`, `DelegationId? Delegation`, `IReadOnlyList<string> Notify` (`01` APR-REQ-014) |

**Operations** (`Themis.Core`, implemented by `Themis`):

| Type | Members |
|---|---|
| `IApprovalTypes` | `PublishAsync(ApprovalTypeDefinition, ...)` answers `Result<PublishedVersion>`; `ReadAsync(string key, int? version, ...)` answers `Result<ApprovalTypeVersion>`; `RetireAsync(string key, ...)` answers `Result` (`02` APR-TYPE-001) |
| `IApprovals` | `CreateAsync(ApprovalRequest, ...)` and `CreateAsync(ApprovalRequest, DbTransaction, ...)` answer `Result<CreatedRequest>`; `DecideAsync(DecisionRequest, ...)` answers `Result<DecisionReceipt>`; `CancelAsync(CancelRequest, ...)` and `CancelAsync(CancelRequest, DbTransaction, ...)` answer `Result`; `CompleteAsync(RequestId, string actor, ...)` and `CompleteAsync(RequestId, string actor, DbTransaction, ...)` answer `Result`; `ProtectionAsync(Target, string field, ...)` answers a `FieldProtection`; `RecordChangeAsync(Target, string field, string actor, ...)` and `RecordChangeAsync(Target, string field, string actor, DbTransaction, ...)` answer `Result<IReadOnlyList<RequestId>>`; `ReadAsync(RequestId, ...)` answers `Result<RequestRecord>`; `PendingAsync(string person, string? cursor, int? size, ...)` answers `Result<PendingPage>`; `ByTargetAsync(Target, ...)` answers `IReadOnlyList<RequestSummary>`; `ExportAsync(string person, ...)` answers a `PersonExport` (`01`) |
| `IDelegations` | `CreateAsync(DelegationDefinition, ...)` answers `Result<DelegationId>`; `EndAsync(DelegationId, string actor, ...)` answers `Result`; `ListAsync(string person, ...)` answers `IReadOnlyList<DelegationRecord>` (`01` APR-REQ-008) |
| `IApprovalJobs` | `RunAsync(ApprovalJob job, ...)` runs one run of the job and answers the number of items it handled (`04` APR-OPS-004) |

**Seams a host supplies** (APR-LIB-005; `Themis.Core`):

| Seam | Members |
|---|---|
| `IApproverDirectory` | `ResolveAsync(string permission, ApproverQuery query, ...)` answers `IReadOnlyList<string>`, the persons holding the permission for that request; `CanApproveAsync(ApproverCheck check, ...)` answers `bool`. `ApproverQuery`: `RequestId Request`, `string Type`, `int Version`, `Target Target`, `string Requester`, `string? Subject`, `IReadOnlyDictionary<string, FieldValue> Snapshot`, `int? Step` (none when watchers are resolved). `ApproverCheck`: `string Person`, `ApproverReference Via` (the first of the step's approver references, in order, that named the assignment's person; the step's or the type's fallback `Person` for a fallback assignment; `EscalateTo` for an escalation one), `string? ActingFor` (set where `Person` decides through a delegation: the lender), `ApproverQuery Query` (`01` APR-REQ-003, APR-REQ-004) |
| `IApprovalObserver` | `ObserveAsync(ApprovalEvent, ...)` answers `ValueTask`; an event is taken when it returns, and not taken when it throws or runs past `Themis:Timeout` (`01` APR-REQ-014) |
| `ISecretSource` | `ReadAsync(string name, ...)` answers `SecretVersions?`: `IReadOnlyList<SecretVersion> Versions`, each `int Version` (from 1) and `ReadOnlyMemory<byte> Value` (`04` APR-OPS-008) |
| `IAlertSink` | `RaiseAsync(string condition, string? scope, IReadOnlyDictionary<string, string> details, ...)` and `ClearAsync(string condition, string? scope, ...)` answer `ValueTask`, as `06` section 5 gives them (`04` APR-OPS-006) |

**Registration and testing:**

| Package | Type | Members |
|---|---|---|
| `Themis` | `ApprovalsServiceCollectionExtensions` | `AddThemis(IServiceCollection, IConfiguration)` answers the `IServiceCollection` (APR-LIB-003) |
| `Themis.Testing` | `InMemoryApproverDirectory`, `RecordingObserver`, `FixedSecretSource`, `TestJobs`, `ApprovalsTestDatabase`, `ApprovalsTestingServiceCollectionExtensions` | The members of APR-LIB-007 |

Everything else is internal: the engine, its jobs, its options and the tables.

*Source: T-002, T-003, T-005, T-006, T-007, T-010, T-014, T-017*

**Acceptance criteria**
1. The build fails when a public member is added or removed without its public API file
   changing.
2. The public API files list each type and member of the tables above and of
   APR-LIB-007, with the types given, and no other public type.
3. Formatting an `ApprovalRequest` or a `DecisionRequest` whose members hold known
   values writes none of the person references, roles, target, snapshot values or
   reason.

---

**APR-LIB-003** — A host SHALL register the library with one call, and the
configuration, the seams and the schema SHALL be checked when the application starts.

**Values (T-006, T-017).** `services.AddThemis(configuration)` binds the configuration
section `Themis` (through the configuration binder's source generator), registers the
engine, `IApprovalTypes`, `IApprovals` and `IDelegations` as scoped services,
`IApprovalJobs` as a singleton and the hosted jobs (`04` APR-OPS-004), and runs every
startup check (the configuration's, `04` APR-OPS-001; the seams', APR-LIB-005; the
secrets', `04` APR-OPS-008; the schema's, `04` APR-OPS-002) in the `StartingAsync` of an
`IHostedLifecycleService`, which the host calls on every such service before any hosted
service's `StartAsync`, so they complete before a host's own hosted service starts or
the application serves a request. A failure stops the start with an
`ApprovalsStartupException` carrying its code of `06` section 1; a call to one of the
library's services before the checks complete throws `InvalidOperationException`, a
fault of the calling code. The library registers no approval type: a host publishes its
own through `IApprovalTypes` (`02`), at its start or later. A host replaces a seam by
registering its own before or after the call.

*Source: T-002, T-006, T-017*

**Acceptance criteria**
1. A host with valid configuration and its seams starts with the one call.
2. A configuration value outside its rule (`06` section 4) stops the start, naming the
   key.
3. With a host's hosted service registered before `AddThemis` that creates a request in
   its `StartAsync`, the request is recorded only after the startup checks ran.

---

**APR-LIB-004** — The first release SHALL have no HTTP endpoints, and SHALL be designed
so that a later release adds them as a package of their own without changing the
engine's surface.

**Values (T-015).** A host's own API calls the library, since an approver's screen shows
the host's data (the order, the person), which the library does not hold.

*Designed for, not built:* a package `Themis.Endpoints` with mapping methods
`MapThemis...` for an approver's pending list, their decisions and their delegations,
each taking the prefix the host chooses and answering the group's `RouteGroupBuilder`,
reading JSON bodies only as `application/json`, acting only for the signed-in person
through a new seam that reads them from the host's request, and calling the operations
of APR-LIB-002 alone, so no engine member changes.

*Source: T-015*

---

**APR-LIB-005** — A host SHALL supply what only it knows through the seams of
APR-LIB-002, the library SHALL refuse to start without a required one, and every call
into a seam SHALL be bounded in time.

**Values (T-006, T-018).**

| Seam | Required | `details.key` |
|---|---|---|
| `ISecretSource` | Always | `secretSource` |
| `IApproverDirectory` | Always | `approverDirectory` |
| `IApprovalObserver` | Where `Themis:Observer:Enabled` is on (the default) and the application runs the jobs (`Themis:Jobs:Enabled`) | `approvalObserver` |
| `IAlertSink` | No: the library's own logs each condition at the error level | |
| `TimeProvider` | No: the system clock | |

A required seam missing stops the start with `approvals.startup.seammissing`, its
`details.key` as above. The directory is called inside the transaction of the move that
needs it (a create, a decision, a step's start, an escalation, an event naming
watchers), so what it answers and what is written agree; each call is bounded by
`Themis:Timeout`, and one that throws or does not answer within it ends the operation
with `approvals.directory.unavailable`, nothing written, the job that made it (`04`
APR-OPS-004) leaving its item to its next run. A directory answer outside its rule (a
person reference outside `00` section 1.5) is a fault of the host's code: the operation
throws `InvalidOperationException` naming the seam and writes nothing. The observer is
called by the `observer` job alone, outside any transaction (`01` APR-REQ-014).

*Source: T-006, T-018*

**Acceptance criteria**
1. Each required seam, missing where it is required, stops the start naming it.
2. A directory that does not answer within `Themis:Timeout` leaves a decision refused
   with `approvals.directory.unavailable` and the request as it was.
3. A directory that answers an empty person reference makes the create throw, and
   nothing is recorded.

---

**APR-LIB-006** — The library SHALL follow Semantic Versioning 2.0.0, and SHALL release
candidates of 1.0.0 until its first final release: from that release on, a release that
changes or removes a public member, a code, a configuration key, a value of an
enumeration or status, an event kind, or a metric's name, or that adds to an interface a
host implements a member without a default implementation, is a major release.

**Values (T-017).** The interfaces a host implements are the seams of APR-LIB-002. A
designed-for feature that needs more of a host comes as a new interface, never as a
member added to one of these. The library's schema is its own and private, so a
migration is not part of this rule; a migration that drops or rewrites data is named in
the release notes (`04` APR-OPS-002). A stored definition's canonical form is part of
the rule: a release that would write a published definition's form differently is
major, since it would make a new version of every type a host publishes again (`05`
APR-ENG-006). The release gate's baseline is `build/gates/release-baseline.json`,
written in each release commit beside `PublicAPI.Shipped.txt`: the codes, the
configuration keys, the spellings of the enumerations, statuses and event kinds, and the
metric names and kinds. The gate takes the current set as the contract tests read it
(`05` APR-ENG-007) and compares it with the baseline as the tag of the newest final
release holds it, on a pull request as on a release tag, a release candidate's tag never
being one; before the first final release no such tag holds one, so the baseline is
empty. An addition is minor; a change or a removal is major.

**Release candidates (T-020).** A version is final only on the owner's word. Until then
each release of it is a release candidate, `X.Y.Z-rc.N`, `N` counting from 1 for each
version (SemVer 2.0.0 compares it numerically, so `rc.10` follows `rc.9`). The first
final release is 1.0.0, so every release before it is `1.0.0-rc.N`, and nothing in this
rule binds those: a release candidate of 1.0.0 may change or remove anything, a stored
definition's canonical form included, as SemVer leaves a pre-release free to fall short
of what its version will promise. The release notes of each release candidate name every
change and removal since the release before it, so a host knows what to adapt. From the
first final release on, a release candidate `X.Y.Z-rc.N` is held to what `X.Y.Z` needs
against the newest final release.

*Source: T-017, T-020*

**Acceptance criteria**
1. The release gate compares the public surface, the codes, the configuration keys, the
   enumerations, statuses and event kinds and the metric names with the newest final
   release's, and fails a release whose version is below what changed needs, a member
   added to a host-implemented interface without a default implementation counted as a
   break (`05` APR-ENG-012). That rule is applied to the version of a release tag, a
   release candidate's included; on a pull request, whose version is a pre-release, the
   gate fails a change that needs a major release where no commit since the newest final
   release carries `!` or `BREAKING CHANGE:` (`05` APR-ENG-011); before the first final
   release its baseline is empty, so nothing is a break.
2. With no final release tagged, a pull request and a release candidate's tag that
   remove a public member pass the release gate; with a final release `1.0.0` tagged,
   the same removal fails it in a pull request carrying neither `!` nor
   `BREAKING CHANGE:`, and in a tag `v1.1.0-rc.1`.

---

**APR-LIB-007** — `Themis.Testing` SHALL give a host's tests, and the sample host, an
in-memory approver directory, a recording observer, fixed secrets, the jobs run on
demand and a test database's migration, so a test runs real requests through the
engine.

**Values (T-017).**

| Type | Members |
|---|---|
| `InMemoryApproverDirectory` | An `IApproverDirectory`; `Grant(string permission, string person)` and `Revoke(string permission, string person)`; `Block(string person)` and `Unblock(string person)`; `FailWith(Exception? failure)`, every call throwing it while it is set. `ResolveAsync` answers the persons granted the permission, in the order granted; `CanApproveAsync` answers false for a blocked person, and otherwise true where `Via` is a `Person` naming them, or a `Permission` they hold, or where `ActingFor` is set (a delegate needs no grant of their own); `IReadOnlyList<ApproverCheck> Checks`, every check asked |
| `RecordingObserver` | An `IApprovalObserver`; `IReadOnlyList<ApprovalEvent> Received`, in the order taken; `FailWith(Func<ApprovalEvent, bool> fails)`, an event it matches being thrown on and not taken |
| `FixedSecretSource` | An `ISecretSource` answering nothing until set; `SetSecret(string name, SecretVersions versions)` and `RemoveSecret(string name)` |
| `TestJobs` | `TestJobs(IApprovalJobs jobs)`; `RunUntilIdleAsync(CancellationToken cancellationToken)` runs the `deadlines` job then the `observer` job until a round of both handles nothing, and answers the number of items handled; a test moves the clock, the `TimeProvider` the library is registered with, to pass a deadline |
| `ApprovalsTestDatabase` | A static class; `MigrateAsync(string connection, CancellationToken cancellationToken)` answers `ValueTask` and applies the release's migrations to that database as `themis migrate` does (`04` APR-OPS-002) |
| `ApprovalsTestingServiceCollectionExtensions` | `AddThemisTesting(IServiceCollection services, Action<FixedSecretSource>? configure = null)` answers the `IServiceCollection`; it registers one `InMemoryApproverDirectory`, one `RecordingObserver` and one `FixedSecretSource` as singletons, each resolvable by its own type and by its seam, in place of the host's, `configure` running on the `FixedSecretSource` at once so a test sets `connection` before the host starts, and removes the hosted jobs, so only a test's runs happen, in either order of the two calls; a seam the host registers after it replaces the testing one, as any later registration does |

Nothing in it skips a check of `01` or `02`.

*Source: T-017*

**Acceptance criteria**
1. A host test publishes a type, creates a request, decides it and reads the `approved`
   event from the recording observer after `RunUntilIdleAsync`.
2. A test moves the clock past a step's deadline and finds the step escalated after
   `RunUntilIdleAsync`.

---

**APR-LIB-008** — An operation given a host's transaction SHALL write on it alone,
commit nothing, and leave the outcome to the host's commit or rollback.

**Values (T-007, T-016).** The `DbTransaction` overloads of APR-LIB-002 take an open
`NpgsqlTransaction` of the host's; any other, or one that has ended, is a fault of the
calling code (`ArgumentException`). The library runs the operation's statements on that
transaction's connection, in the same order and under the same locks as on its own
(`04` APR-OPS-003), and never commits, rolls back or runs it again; the locks it takes
are held until the host's transaction ends, so a host keeps such a transaction short.
A failure the operation answers leaves nothing of the operation's own on the
transaction, by a savepoint taken before its first statement and rolled back to, so the
host may go on with its own work or roll back. The events the operation writes are
delivered only once the host's transaction commits (`01` APR-REQ-014). A host's
transaction is on the database the library uses (`04` APR-OPS-003).

*Source: T-007, T-016*

**Acceptance criteria**
1. A create on a host transaction that the host rolls back leaves no request and no
   event; one it commits leaves both, with the host's own row.
2. A refused create on a host transaction leaves the host's earlier writes on it intact,
   and the host's commit succeeds.
3. A transaction of another provider, or a committed one, makes the call throw
   `ArgumentException`.
