# Release notes

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
