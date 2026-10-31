# Implementation plan

The order in which the specification is built. What each item means is in `docs/spec/`;
this plan only orders it.

## 1. How to read the specification

Read `00-overview`, `05-engineering-conventions` and `03-library-contract` in full
before the first phase, then `01-requests` and `02-approval-types` before phase 2.
`06-reference` is authoritative for every name, code, key, default and status.
`decision-log.md` is rationale.

## 2. Working rules

- Each phase is built with tests first, one logical change per commit, the fast checks
  on every commit, the commit-message gate and the secret scan before each push, and the
  full gate (`05` APR-ENG-012) in the pipeline on every push, its run on the last commit
  pushed being the gate of record, then a report in the shape of section 5. A phase
  begins only when the one before it has a clean report and its pull request is merged
  into `main` after the lead's review.
- A phase builds the part of each item it names; where an item is split across phases,
  the table says which part each builds. A criterion that needs an item of a later phase
  is proven in that phase, and the report of the earlier one names it as waiting there.
- Each phase implements, in code, the rows of `06` that its items use: the codes, keys,
  enumerations, statuses, alert conditions, event kinds and names.
- Items designed for and not built (the rows of `00` APR-OVR-003, `03` APR-LIB-004, and
  every paragraph marked "designed for, not built") are placed in no phase; what the
  first release must do for them is stated in the items that are.
- A question about the specification parks its item and what depends on it; the rest of
  the phase goes on.

## 3. Release 1

| # | Phase | Items | Proves |
|---|---|---|---|
| 0 | Repository and gates | The docs committed first and pushed to the empty default branch, then its protection set; `05` all but the sample host and `RELEASE-NOTES.md` (phase 5) of APR-ENG-001, the migrations gate of APR-ENG-012 (phase 1, with `themis migrate`), the release compatibility gate of APR-ENG-012 and the release step of APR-ENG-011 (phase 5), the gates as scripts; `03` APR-LIB-001 (all but the sample host's row), APR-LIB-002 (the projects and their empty surface files, `Themis.Cli`'s project coming in phase 1 with `themis migrate`, since a tool cannot build without its entry point; each type of APR-LIB-002 then comes with the first item that uses it); `00` APR-OVR-001 criterion 1; `07` APR-THR-003 | An empty solution passes every gate of `05` APR-ENG-012 that applies to it, locally, and in the pipeline on its pull request |
| 1 | Foundations | `05` APR-ENG-012's migrations gate; `04` APR-OPS-001, APR-OPS-002, APR-OPS-003 (the connection, `@now`, the retries, the session timeout; each lock comes with the item that takes it), APR-OPS-005 (the logging, the meter and the health check's registration), APR-OPS-006 (the alert table and the sink), APR-OPS-008; `03` APR-LIB-003, APR-LIB-005 (the seams' registration and the start's refusal; the directory's timeout in phase 3); `00` APR-OVR-002 | A host registers with one call; a configuration value, seam or secret outside its rule stops the start naming it; `themis migrate` applies twice, the second time nothing; the application's role can delete nothing |
| 2 | Approval types | `02` all; `05` APR-ENG-005, APR-ENG-006; `04` APR-OPS-003 (the type's lock); `03` APR-LIB-002 (the types of approval types and `IApprovalTypes`) | A definition publishes once however often it is published; a change makes the next version and two at once never share a number; every rule of a definition refuses at publish; the canonical form matches its literal |
| 3 | Requests, decisions and events | `01` APR-REQ-001 to APR-REQ-007, APR-REQ-010, APR-REQ-013 (the read and the pending list without delegations), APR-REQ-014; `03` APR-LIB-002 (the request and event types, `IApprovals` but what phase 4 builds, `IApprovalJobs`), APR-LIB-005 (the directory's timeout), APR-LIB-007 (criterion 1), APR-LIB-008; `04` APR-OPS-003 (the request's lock), APR-OPS-004 (the `observer` and `watch` jobs; the `deadlines` job's claim and run, handling no step; criteria 3 and 4), APR-OPS-005, APR-OPS-006 (`observer-failing`, `request-unassignable`) | A request recorded in a host transaction goes through its steps under separation of duties, is rejected or approved, never deadlocks with a decision racing it, and its events reach the observer in order, at least once |
| 4 | Delegation, deadlines, protection and the record | `01` APR-REQ-008, APR-REQ-009, APR-REQ-011, APR-REQ-012, APR-REQ-013 (the pending list with delegations, `ByTargetAsync`), APR-REQ-015; `03` APR-LIB-002 (`IDelegations` and the rest of `IApprovals`), APR-LIB-007 (criterion 2); `04` APR-OPS-004 (the `deadlines` job's actions; criteria 1 and 2), APR-OPS-006 (`job-stalled`); `07` APR-THR-001, APR-THR-002, APR-THR-004 | A delegate decides for the lender within the period alone; a deadline escalates, rejects or cancels once and never approves; a protected field is locked while pending and voids after approval until completion; an export holds all that names a person; every control of the threat model has its test |
| 5 | Release gate, sample host and release 1.0.0-rc.1 | `03` APR-LIB-006 (the compatibility gate, against the newest final release, its baseline empty before the first; criteria 1 and 2); `05` APR-ENG-001 (the sample host, and its gate of APR-ENG-012), APR-ENG-011 (the release commit's checks, criteria 2 and 3, a release `V` being `X.Y.Z` or `X.Y.Z-rc.N` in the commit gate and wherever a gate reads a release tag; the release job, criterion 4); `03` APR-LIB-001 for the sample host's row in the references test; `04` APR-OPS-007 and `RELEASE-NOTES.md`. The release commit `chore: release 1.0.0-rc.1` comes in the same pull request once the review has passed, on the lead's message, and the tag `v1.0.0-rc.1` after its merge | The sample host builds from the packages packed from the same commit and its tests pass in the gate; a release commit outside its rule fails the gate; version 1.0.0-rc.1 of every package on the tag's release, marked a pre-release, with its changelog and release notes |

**Waiting on inputs.**
- Phase 0: the name the packages carry as their authors, which the lead's first message
  gives (`05` APR-ENG-011).
- Phase 5's release commit and tag: the lead's message to release a release candidate,
  after the review; a final release, the owner's (T-020). The release attaches the
  packages to the tag's release and publishes them to no feed.

## 4. Release 2

The lead names every row of `00` APR-OVR-003 (2026-11-01): what release 1 was designed for and
did not build. Each phase builds one row from the design its requirement holds, keeps
APR-LIB-006 (nothing breaks on the public surface), and gives every criterion it builds a test
carrying its identifier. Where a design leaves a choice or names no criterion for what the
phase builds, the phase asks the lead before it builds that part.

| # | Phase | Items | Proves |
|---|---|---|---|
| 6 | Retention and erasure | `01` APR-REQ-015: deleting closed requests after a retention period, and erasing a person | A closed request past its period goes with its record, and nothing else does; an erased person's references leave every request, decision, delegation and event that held them, the record staying append-only otherwise; both run as jobs and are idempotent |
| 7 | Some approvers out of a list | `02` APR-TYPE-004: a step approved by a number out of a list (two of five) | A version publishes such a step and refuses an impossible one; the step completes at the number, rejects as its design says, and assignments, delegation and deadlines work for it as for any step |
| 8 | Reminders | `01` APR-REQ-009: reminders before a deadline | A reminder fires once at its time before the deadline, to whom its design names, never after the step closes, and survives a restart |
| 9 | HTTP endpoints | `03` APR-LIB-004: an approver's queue, decisions and delegations over HTTP | The endpoints answer only for the signed-in person through the host's seams, refuse what the library refuses with its errors, and the sample host serves them in the gate |
| 10 | Release 1.0.0-rc.2 | As phase 5's release step | The release commit's checks, the compatibility gate and the release, on the lead's message |

## 5. Phase report

`docs/reports/phase-NN.md`, holding only:
- the items built, each with its tests' names;
- the items not built, each with its reason, and the criteria proven and the behaviour
  built in a later phase;
- every criterion checked without a test, with how it was checked;
- every criterion that only the pipeline's run can check (the pipeline half of phase 0's
  proof, `05` APR-ENG-012's criteria), named as awaiting that run, whose result the
  message naming the pull request gives;
- every analyser suppression, with its justification;
- **Resolved by rule**;
- **Open questions**: every question the phase asked, answered or not, with the items it
  parked and the entry that answered it;
- the commit the report names, the fast checks and the checks before the push, green,
  the gate of record being the pipeline's `gates` run on the report's own commit, whose
  identifier and result the run's last message gives (`05` APR-ENG-012).
