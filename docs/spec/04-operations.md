# 04 — Operations

Configuration, the schema and its migrations, the database's use, the background work,
what the library reports about itself, the deployment's part, and the one secret it
needs. Every value named `Themis:...` is configuration (`06` section 4).

---

**APR-OPS-001** — Every behaviour a deployment may want to change SHALL be
configuration, bound from the host's configuration section `Themis`, with the default
and the rule `06` section 4 gives, validated when the application starts.

**Values (T-017).** The library checks each key of `06` section 4 itself, on the raw
configuration value and before binding, so a value outside its rule stops the start with
`approvals.startup.configurationinvalid`, `details.key` naming the key as `06` spells
it; the section is then bound through .NET's options, by the configuration binder's
source generator, with source-generated validation as a second line. Every key takes
effect at the start: a value changed while the application runs is read at its next
start, so two processes of one deployment never judge a deadline or a lag by values one
of them has not read.

*Source: T-017*

**Acceptance criteria**
1. Every key of `06` section 4 has its default when absent and stops the start when
   outside its rule.
2. A key changed while the application runs takes effect only after a restart.

---

**APR-OPS-002** — The library's tables SHALL live in the schema `approvals`, created and
changed only by the library's migrations, applied by the deployment's pipeline before
the new version starts and never by the application at startup.

**Values (T-013, T-017).** The schema name is fixed, and does not change with the
library's name (`00`); the migration history table is `approvals.__migrations_history`,
so the library's history never meets a host's. The migrations are applied by
`themis migrate`, the command of the `Themis.Cli` tool of the same release, which reads
one JSON document on its standard input and nothing else:
`{ "connection": "<connection string>", "applicationRoles": [ "<role>", ... ] }`, the
roles optional. Once connected, and before it applies any migration, it reads which of
the roles the database has, comparing each name as text with `pg_roles`, so a name
longer than PostgreSQL's 63-byte limit on identifiers matches none; a role it lacks
answers `approvals.migrate.inputinvalid`, `details.member` `applicationRoles`, and
nothing is applied. It applies the pending migrations one at a time through EF Core's
migrator, each in its own transaction, then, in one transaction, grants each role
`USAGE` on the schema, `SELECT`, `INSERT` and `UPDATE` on each of its tables but the
migration history, `SELECT` alone on the migration history, and `USAGE` on each of its
sequences, and grants no role `DELETE` or `TRUNCATE` on any of them, so the record
stays append-only for the application (`01` APR-REQ-015), revoking nothing a role
already holds. In the same transaction it checks that each role holds each privilege
granted (`has_schema_privilege`, `has_table_privilege`, `has_sequence_privilege`); a
grant that fails, or that the check does not find, answers
`approvals.migrate.grantfailed`, `details.role` the role, and no grant of the run stays.
It exits 0 on success; on a failure it exits 1 and writes one JSON line on standard
error, `{ "code": "<code>", "details": { ... } }`, with a code of `06` section 1, the
migrations before the failing one staying applied.

The migrations EF Core's tool generates are made `internal sealed` in the commit that
adds them, so they stay off the public surface (`03` APR-LIB-002), and that commit runs
`dotnet format` on the migration's main file, so the format gate passes. Each migration
leaves the schema usable by the release before it, so the processes of two releases run
side by side during a deployment; a change that cannot is split across two releases. At
startup the library checks that the database holds every migration of its release and
stops the start with `approvals.startup.schemamismatch`, `details.migration` the first
missing, where it does not; later migrations, of a newer release, are accepted. A
migration that drops or rewrites data is named in the release notes.

*Source: T-013, T-017*

**Acceptance criteria**
1. Starting against a database missing the release's last migration stops the start.
2. `themis migrate` twice in a row applies nothing the second time and exits 0 both
   times.
3. A role named in `applicationRoles` can read, insert and update every table of the
   schema but the migration history, which it can read alone, and can delete from none;
   input that does not read exits 1 with `approvals.migrate.inputinvalid`.
4. A role the database lacks, and a name of 64 bytes whose first 63 are an existing
   role's name, each exit 1 with `approvals.migrate.inputinvalid` naming
   `applicationRoles`, and no migration is applied.

---

**APR-OPS-003** — The library SHALL use the host's PostgreSQL database through its own
connection, take its locks in one order, and judge time by its own clock.

**Values (T-016, T-017).** The connection string is the secret `connection`
(APR-OPS-008); the library's own work (an operation made without the host's
transaction, the jobs) runs on it. A host that passes its own transaction (`03`
APR-LIB-008) gives the library the database that transaction runs on. Every move of a
request, and every publish of a type, runs in one transaction that takes its locks in
this order, so no two of the library's own transactions deadlock:
1. a type's lock, for a publish or a retirement alone: the transaction-scoped advisory
   lock `pg_advisory_xact_lock` on the first 8 bytes, read as a signed big-endian
   integer, of the SHA-256 of the text `approvals-type`, a zero byte and the key;
2. the request's row, `SELECT ... FOR UPDATE`, before any other row of the request is
   read for change; where a move touches several requests (a recorded change voiding
   several), their rows in the order of their identifiers;
3. then the request's steps, assignments, decisions and events, which only a holder of
   the request's lock writes.

A create takes no request lock, since its row is new; the unique index of `01`
APR-REQ-002 makes a second create of one type and target wait for the first and then be
refused. A decision on a delegation reads the delegation without a lock: a delegation
ended in the same instant may still be used by a decision whose transaction began
before the ending committed, which the record shows by both instants. A transaction of
the library's own that PostgreSQL aborts for a deadlock (40P01) or a serialization
failure (40001) is run again up to `Themis:Database:Retries` times, after which a job
leaves the item to its next run and an operation lets the database's error through; a
host's transaction is never run again by the library. Every transaction of the
library's own sets `idle_in_transaction_session_timeout` (`SET LOCAL`) to twice
`Themis:Timeout`, so the server ends a session that stalls inside one and frees its
locks.

Every statement takes the current instant as a parameter, `@now`, from the library's
`TimeProvider`, never the database's `now()`, so a test clock governs the database's
view of time as it governs the code's. A column holding a closed set (a status, a kind)
is `text` with a `CHECK` constraint, so the host's data source needs no type mapping of
the library's. Identifiers are UUID version 7. The processes' clocks SHALL agree within
one second, which a host synchronised by NTP keeps, so a deadline one process records is
not judged passed by another before its time by more than that.

*Source: T-016, T-017*

**Acceptance criteria**
1. A request created in a host transaction on a data source the host built, with no
   configuration of the library's, commits with the host's write.
2. Two decisions on one request at once, each in its own transaction, never deadlock,
   and the second is judged on what the first left (`01` APR-REQ-004).
3. A status outside its closed set is refused by the database.
4. No hand-written statement calls `now()`, `current_timestamp` or `clock_timestamp()`,
   nor `pg_advisory_lock`.

---

**APR-OPS-004** — The library's background work SHALL run as hosted services in the
host's process, turned on or off per application by configuration, and SHALL do nothing
twice when several processes run it.

**Values (T-012, T-014, T-019).**

| Job | Does | Every |
|---|---|---|
| `deadlines` | Runs the action of each current step whose deadline has passed (`01` APR-REQ-009) | `Themis:Jobs:Interval` |
| `observer` | Delivers the events not yet taken, in order for each request (`01` APR-REQ-014); where `Themis:Observer:Enabled` is off it completes at once, delivering nothing | `Themis:Jobs:Interval` |
| `watch` | Judges the alert conditions (APR-OPS-006) | `Themis:Jobs:Interval` |

The jobs run where `Themis:Jobs:Enabled` is on (the default); where `Themis:Jobs:Hosted`
is on too (the default), hosted services run them on their timers, and where it is off
only `IApprovalJobs.RunAsync` runs them (`03` APR-LIB-002), which throws
`InvalidOperationException` where `Themis:Jobs:Enabled` is off. Each job holds a claim
on its row of the library's job table while it runs: a holder, an instant and the
instant the claim expires, `@now` plus `Themis:Jobs:ClaimTimeout`, taken by a
conditional update where the row is free or its claim has expired and its last start is
at least the job's interval, less one second, before `@now`, renewed after each item it
handles by an update conditional on the claim still being its own (a run whose renewal
changes nothing stops before its next item), and released with the instant of
completion when the run ends; `RunAsync` takes the claim whatever the interval. The
`deadlines` job takes the steps due in batches of `Themis:Jobs:BatchSize`, oldest due
first, selecting their requests `FOR UPDATE SKIP LOCKED`, so a request a host's open
transaction holds is left for a later run rather than waited for. The `observer` job
takes, for each request in turn, its lowest undelivered event, delivering it only where
every event of that request with a lower sequence number has been taken, and moves on to
the next request where a call fails, so one failing request never holds back another.
A job that fails is logged and runs again at its next time. A job no application runs is
never judged stalled.

*Source: T-012, T-014, T-019*

**Acceptance criteria**
1. With `Themis:Jobs:Hosted` off, no step's deadline runs until `RunAsync` is called for
   `deadlines`, which then runs it.
2. Two processes running the `deadlines` job at once run each step's action once.
3. A process that dies holding a job's claim leaves it to another process after
   `Themis:Jobs:ClaimTimeout`.
4. An observer failing on one request's event still receives the events of other
   requests in the same run.

---

**APR-OPS-005** — The library SHALL log what it does without writing any person
reference, target, role, snapshot value or reason, and SHALL publish metrics and a
health check a host can collect.

**Values (T-019).** Log entries go through source-generated `LoggerMessage` methods and
name requests, decisions, delegations and events by identifier, type key, version,
status, step number and reason code, never by a person reference, a target, a role, a
field value or a reason's text. Metrics are published on the .NET meter of `06` section
7, with its instruments. The health check of `06` section 7, registered by `AddThemis`
in an application where the jobs run, reports unhealthy while the oldest undelivered
event is older than `Themis:Observer:LagThreshold` or a job is stalled (APR-OPS-006).

*Source: T-019*

**Acceptance criteria**
1. A test capturing every log entry of a request's life, from create to completion, its
   refusals included, finds no person reference, target, role, field value or reason.
2. The meter counts requests created and closed by status, and the health check turns
   unhealthy past the lag threshold.

---

**APR-OPS-006** — The library SHALL raise each alert condition of `06` section 5
through the alert sink when it starts to hold, raise it again at a fixed interval while
it holds, and clear it once when it stops.

**Values (T-009, T-019).** `IAlertSink.RaiseAsync(condition, scope, details)` and
`ClearAsync(condition, scope)` (`03` APR-LIB-002), each bounded by `Themis:Timeout`; the
library's own sink logs each at the error level. A condition is raised when it starts to
hold and again each time `Themis:Alerts:Repeat` has passed since it was last raised
while it still holds, and cleared once when it stops holding, as `06` section 5 defines
for each. A condition's state per scope (holding or not, when it was last raised, and
for `request-unassignable` the instant of its last occurrence) is kept in the library's
alert table, so the deployment raises it once however many processes see it; an
occurrence is written by the move that meets it, in its transaction, and the `watch`
job raises, raises again and clears.

*Source: T-009, T-019*

**Acceptance criteria**
1. With `Themis:Alerts:Repeat` 1 hour, a condition holding from 00:00 to 02:30 is raised
   at 00:00, 01:00 and 02:00 and cleared once.
2. Two processes seeing the same condition raise it once.
3. A request cancelled with `noapprover` raises `request-unassignable` for its type,
   cleared once `Themis:Alerts:Repeat` passes with no new occurrence.

---

**APR-OPS-007** — The deployment SHALL meet what the library cannot do alone, and the
release notes SHALL state it.

**Values (T-006, T-017).**
- **Migrations.** `themis migrate` of the new release runs before its processes start,
  with the application roles named (APR-OPS-002).
- **Jobs.** At least one application runs the jobs (`Themis:Jobs:Enabled`), or no
  deadline runs and no event is delivered.
- **Observer.** Every application that runs the jobs registers the host's observer, or
  turns `Themis:Observer:Enabled` off where the deployment wants no events delivered,
  which then pile up undelivered (APR-OPS-004).
- **Directory.** The host's `IApproverDirectory` answers within `Themis:Timeout` and
  from the host's current data, since the library judges by what it answers (`03`
  APR-LIB-005).
- **Who may cancel and complete.** The host decides who may cancel a request and when it
  has acted on one; the library records who it says did (`01` APR-REQ-010,
  APR-REQ-012).
- **Protected fields.** The host refuses a change to a field the library reports
  `Locked`, and records every change to a protected field through `RecordChangeAsync`,
  since the library cannot see the host's tables (`01` APR-REQ-011).
- **Clocks.** The processes' clocks agree as APR-OPS-003 says.
- **PostgreSQL.** Version 16 or later; the integration tests run on the floor,
  `postgres:16.15` (`05` APR-ENG-008).
- **Stopping.** The host's shutdown timeout and the orchestrator's grace period are
  above `Themis:Timeout`, so a job item under way ends before the process does.

*Source: T-006, T-017*

**Acceptance criteria**
1. The release notes of each release (`RELEASE-NOTES.md`, `05` APR-ENG-001) state every
   duty above (checked in review).

---

**APR-OPS-008** — The library SHALL read the secret it needs through the secret source
when the application starts, and SHALL never hold it in configuration, logs or its
tables.

**Values (T-006).**

| Name | What it is | Used by |
|---|---|---|
| `connection` | The database connection string, UTF-8 | APR-OPS-003 |

The secret is read at the start and used at its highest version; a password changed in
place in it is an ordinary change, read at the next start. Where the source does not
answer it, the start stops with `approvals.startup.secretunavailable`; where it answers
a value that is not UTF-8 or not a connection string Npgsql reads, with
`approvals.startup.secretinvalid`; both name it in `details.key`. It is held as bytes
until the data source is built from it, and never logged. The library keeps no other
secret: what it stores is protected by the database's own access control and the
deployment's encryption at rest (`07`).

*Source: T-006*

**Acceptance criteria**
1. A source that answers no `connection` stops the start naming it; one that answers a
   value Npgsql does not read stops it as invalid.
2. No log entry of the start, its failures included, holds the connection string.
