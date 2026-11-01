# Release notes

## 1.0.0-rc.2

This is the second release candidate of 1.0.0. Until a final release, a release candidate
may change or remove anything, and its notes name each change.

### Changes since 1.0.0-rc.1

Nothing is removed or changed; everything below is added.

- **Retention and erasure.**
  - `Themis:Retention:ClosedRequests` sets how long a closed request is kept. It is unset
    by default, so every request stays.
  - The job `retention` (`ApprovalJob.Retention`, spelt `retention`) deletes the closed
    requests and the ended delegations past that period. An event the observer has not
    taken holds them one period more at most.
  - `IRecordRetention.EraseAsync` answers an `ErasureReceipt` and, inside it,
    `TypeVersionReference`. It erases a person no pending request names, nor an approved
    one as its subject. It replaces every reference to them with one pseudonym, and
    clears the snapshots of the closed requests whose subject they are.
  - The codes `approvals.erasure.pending`, `approvals.erasure.typeversion` and
    `approvals.retention.notgranted`.
  - The counters `approvals.retention.deleted` and `approvals.erasures`.
  - The role `approvals_retention`, which `themis migrate` creates and grants to the
    logins its input names in `retentionRoles`.
  - A person's lock, which an erasure takes exclusive and a create, an assignment or a
    delegation shared.
- **A number of approvals out of a list.**
  - The approval mode `AtLeast` (spelt `atleast`) and `Required` on `StepDefinition` and
    `StepRecord`. Such a step completes at `Required` approvals, counted per distinct
    person.
  - A version published before this release keeps its hash.
- **Reminders.**
  - `Deadline.RemindBefore` holds up to three reminders before a step's deadline. Each
    is sent once, as an event of the new kind `EventKind.Reminded` (spelt `reminded`),
    while the deadline is ahead.
  - The counter `approvals.reminders`.
  - A version published before this release keeps its hash.
- **HTTP endpoints.**
  - The package `Themis.Endpoints`. Its `MapThemisApprovals` maps endpoints for an
    approver's pending list, a request's record, decisions and delegations.
  - The seam `ICurrentPerson`, through which the host names the person each HTTP
    request acts for. Mapping the endpoints without it registered throws
    `approvals.startup.seammissing`, its key `currentPerson`.
  - A host adds its own authorization and antiforgery to the group the mapping answers.

**What a host adapts.** A host's code that switches over `ApprovalJob`, `ApprovalMode` or
`EventKind` meets the new members `Retention`, `AtLeast` and `Reminded`. An observer
receives `reminded` events where a type's deadline names reminders.

### Packages

The packages are released together, each depending on the others at exactly the version
released.

| Package | What it holds |
|---|---|
| `Themis.Core` | The public types and contracts every other package shares |
| `Themis` | The approval engine: approval types, requests, decisions, delegation, deadlines, the event outbox, the schema and the background work |
| `Themis.Testing` | An in-memory approver directory, a recording observer, fixed secrets, an on-demand run of the background work and a test database's migration |
| `Themis.Cli` | A .NET tool, command `themis`, that applies the migrations |
| `Themis.Endpoints` | HTTP endpoints for an approver's queue, decisions and delegations, mapped by the host |

### Migrations that drop or rewrite data

None. The release's migrations add to the schema alone:
- `20261101000902_Retention` adds the job `retention`;
- `20261101010448_AtLeast` adds the step's `required`;
- `20261101011642_Reminders` adds the step's reminders and the event kind `reminded`.

### What the deployment does

- **Migrations.** `themis migrate` of the new release runs before its processes start,
  with the application roles named.
- **Retention and erasure.** Where `Themis:Retention:ClosedRequests` is set or
  `EraseAsync` is called, `themis migrate` runs with `retentionRoles` naming the logins
  of the applications that do so. It runs as a login that may create roles
  (`CREATEROLE`), or those logins hold the privileges otherwise. Else the `retention`
  job fails and an erasure is refused with `approvals.retention.notgranted`.
- **Jobs.** At least one application runs the jobs (`Themis:Jobs:Enabled`), or no
  deadline runs and no event is delivered.
- **Observer.** Every application that runs the jobs registers the host's observer, or
  turns `Themis:Observer:Enabled` off where the deployment wants no events delivered,
  which then pile up undelivered.
- **Directory.** The host's `IApproverDirectory` answers within `Themis:Timeout` and from
  the host's current data, since the library judges by what it answers.
- **Who may cancel and complete.** The host decides who may cancel a request and when it
  has acted on one; the library records who it says did.
- **Protected fields.** The host refuses a change to a field the library reports
  `Locked`, and records every change to a protected field through `RecordChangeAsync`,
  since the library cannot see the host's tables.
- **Clocks.** The processes' clocks agree within one second, which a host synchronised by
  NTP keeps.
- **PostgreSQL.** Version 16 or later.
- **Stopping.** The host's shutdown timeout and the orchestrator's grace period are above
  `Themis:Timeout`, so a job item under way ends before the process does.

## 1.0.0-rc.1

This is the first release candidate of 1.0.0. Until a final release, a release candidate
may change or remove anything, and its notes name each change.

### Packages

The packages are released together, each depending on the others at exactly the version
released.

| Package | What it holds |
|---|---|
| `Themis.Core` | The public types and contracts every other package shares |
| `Themis` | The approval engine: approval types, requests, decisions, delegation, deadlines, the event outbox, the schema and the background work |
| `Themis.Testing` | An in-memory approver directory, a recording observer, fixed secrets, an on-demand run of the background work and a test database's migration |
| `Themis.Cli` | A .NET tool, command `themis`, that applies the migrations |

### Migrations that drop or rewrite data

None.

### What the deployment does

- **Migrations.** `themis migrate` of the new release runs before its processes start,
  with the application roles named.
- **Jobs.** At least one application runs the jobs (`Themis:Jobs:Enabled`), or no
  deadline runs and no event is delivered.
- **Observer.** Every application that runs the jobs registers the host's observer, or
  turns `Themis:Observer:Enabled` off where the deployment wants no events delivered,
  which then pile up undelivered.
- **Directory.** The host's `IApproverDirectory` answers within `Themis:Timeout` and from
  the host's current data, since the library judges by what it answers.
- **Who may cancel and complete.** The host decides who may cancel a request and when it
  has acted on one; the library records who it says did.
- **Protected fields.** The host refuses a change to a field the library reports
  `Locked`, and records every change to a protected field through `RecordChangeAsync`,
  since the library cannot see the host's tables.
- **Clocks.** The processes' clocks agree within one second, which a host synchronised by
  NTP keeps.
- **PostgreSQL.** Version 16 or later.
- **Stopping.** The host's shutdown timeout and the orchestrator's grace period are above
  `Themis:Timeout`, so a job item under way ends before the process does.
