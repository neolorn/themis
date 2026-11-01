# 06 — Reference

Authoritative for every name, code, key, default and status. Where another chapter names
one, this chapter's spelling and value govern.

---

## 1. Codes

A failure carries one code and its details (`03` APR-LIB-002). A "(startup)" code is
thrown in an `ApprovalsStartupException` while the host starts; a "(migrate)" code is
written by `themis migrate` (`04` APR-OPS-002); every other code is answered as a
failure by an operation.

| Code | Details | Answered where |
|---|---|---|
| `approvals.startup.configurationinvalid` (startup) | `key` | A configuration value outside its rule (`04` APR-OPS-001) |
| `approvals.startup.seammissing` (startup) | `key` | A required seam is not registered (`03` APR-LIB-005) |
| `approvals.startup.secretunavailable` (startup) | `key` | The secret source does not answer a secret (`04` APR-OPS-008) |
| `approvals.startup.secretinvalid` (startup) | `key` | A secret outside its form (`04` APR-OPS-008) |
| `approvals.startup.schemamismatch` (startup) | `migration` | The database lacks a migration of the release (`04` APR-OPS-002) |
| `approvals.migrate.inputinvalid` (migrate) | `member` | The tool's input does not read, or names a role the database lacks |
| `approvals.migrate.connectionfailed` (migrate) | | The tool cannot connect |
| `approvals.migrate.failed` (migrate) | `migration` | A migration fails |
| `approvals.migrate.grantfailed` (migrate) | `role` | A grant fails or is not found (`04` APR-OPS-002) |
| `approvals.type.invalid` | `member` | A definition outside its rule, `member` the empty path where its canonical form is too large (`02` APR-TYPE-002) |
| `approvals.type.unknown` | | No such type, or no such version (`02` APR-TYPE-001) |
| `approvals.type.retired` | | A request of a retired type (`02` APR-TYPE-001) |
| `approvals.request.snapshotinvalid` | `field` | A snapshot that does not match its version's protected fields (`01` APR-REQ-001) |
| `approvals.request.open` | | An open request of the type already covers the target (`01` APR-REQ-002) |
| `approvals.request.previousinvalid` | | `Previous` names no closed request of the type and target (`01` APR-REQ-001) |
| `approvals.request.unknown` | | No such request |
| `approvals.request.closed` | | A move of a request that is closed, or a decision on one no longer pending (`01` APR-REQ-004, APR-REQ-010, APR-REQ-012) |
| `approvals.request.notapproved` | | A completion of a pending request (`01` APR-REQ-012) |
| `approvals.request.cursorinvalid` | | A pending list's cursor that does not read (`01` APR-REQ-013) |
| `approvals.decision.notassigned` | | No undecided assignment of the current step for the decider or the person acted for (`01` APR-REQ-004) |
| `approvals.decision.separation` | | The decider or the person acted for is the requester or the subject (`01` APR-REQ-004) |
| `approvals.decision.duplicate` | | The decider already decided the step, or under `RequireOther` approved an earlier step (`01` APR-REQ-004, APR-REQ-006) |
| `approvals.decision.notpermitted` | | The directory does not answer the decider, or the person acted for, as able to approve (`01` APR-REQ-004) |
| `approvals.decision.reasonrequired` | | A rejection without a reason (`01` APR-REQ-005) |
| `approvals.reason.invalid` | | A reason over 2,000 characters, or not text (`00` section 1.5; `01` APR-REQ-004, APR-REQ-010) |
| `approvals.field.locked` | | A change recorded to a field a pending request covers (`01` APR-REQ-011) |
| `approvals.delegation.invalid` | `member` | A delegation outside its rule (`01` APR-REQ-008) |
| `approvals.delegation.unknown` | | No such delegation |
| `approvals.delegation.ended` | | Ending a delegation that has ended (`01` APR-REQ-008) |
| `approvals.directory.unavailable` | | The directory throws or does not answer within `Themis:Timeout` (`03` APR-LIB-005) |
| `approvals.directory.toomany` | | A step's approvers resolve to more than 200 persons (`01` APR-REQ-003) |
| `approvals.erasure.pending` | | An erasure of a person who is the requester, the subject or an assignee of a `pending` request, or a delegate of one of its open assignments, or the subject of an `approved` request (`01` APR-REQ-015) |
| `approvals.erasure.typeversion` | `type` | An erasure of a person whom the newest version of an approval type names (`01` APR-REQ-015) |
| `approvals.retention.notgranted` | | An erasure whose session lacks the privileges it needs (`01` APR-REQ-015; `04` APR-OPS-002); a `retention` run that lacks them throws, naming it (`04` APR-OPS-004) |

---

## 2. Enumerations

Members in this order; the spelling is the one written wherever the library writes the
member as text (`03` APR-LIB-002). A column holding a member is `text` with a `CHECK`
constraint listing these spellings (`04` APR-OPS-003), so a member added comes with the
migration that adds its spelling there: `ck_steps_mode` for an `ApprovalMode`,
`ck_events_kind` for an `EventKind`, and `ck_jobs_name` for an `ApprovalJob`, whose list
is the job names of section 7.

| Enumeration | Members (spelling) |
|---|---|
| `FieldKind` | `Text` (`text`), `WholeNumber` (`wholenumber`), `DecimalNumber` (`decimalnumber`), `Flag` (`flag`) |
| `CompareOperator` | `Equal` (`equal`), `NotEqual` (`notequal`), `Greater` (`greater`), `GreaterOrEqual` (`greaterorequal`), `Less` (`less`), `LessOrEqual` (`lessorequal`) |
| `ApprovalMode` | `AnyOne` (`anyone`), `All` (`all`), `AtLeast` (`atleast`) |
| `DeadlineAction` | `Escalate` (`escalate`), `Reject` (`reject`), `Cancel` (`cancel`) |
| `DuplicateRule` | `Skip` (`skip`), `RequireOther` (`requireother`) |
| `DecisionOutcome` | `Approve` (`approve`), `Reject` (`reject`) |
| `AssignmentOrigin` | `Approver` (`approver`), `Fallback` (`fallback`), `Escalation` (`escalation`) |
| `ReasonCode` | `NoApprover` (`noapprover`, `01` APR-REQ-007), `Deadline` (`deadline`, `01` APR-REQ-009), `FieldChanged` (`fieldchanged`, `01` APR-REQ-011) |
| `FieldProtection` | `Free` (`free`), `Locked` (`locked`), `Approved` (`approved`) |
| `EventKind` | As section 6, in its order |
| `ApprovalJob` | `Deadlines` (`deadlines`), `Observer` (`observer`), `Watch` (`watch`), `Retention` (`retention`) |

---

## 3. Statuses

**A request's status** (`RequestStatus`), members in this order. A request is **open**
while `pending` or `approved` and **closed** otherwise; a closed request never moves.

| Status (spelling) | Meaning |
|---|---|
| `Pending` (`pending`) | A step is current; decisions are taken |
| `Approved` (`approved`) | Every step that held is complete, or the type approved it at once; the host has not yet acted |
| `Rejected` (`rejected`) | A person rejected it, or a deadline did |
| `Cancelled` (`cancelled`) | Someone cancelled it, a deadline did, or no one could be assigned |
| `Void` (`void`) | A protected field changed after it was approved and before it was completed |
| `Completed` (`completed`) | The host acted on the approval |

| From | To | By |
|---|---|---|
| (new) | `pending` | A create whose policy has a step that holds (`01` APR-REQ-001) |
| (new) | `approved` | A create of a type that allows it, with no policy holding (`02` APR-TYPE-003) |
| `pending` | `approved` | The last step that holds completes (`01` APR-REQ-003, APR-REQ-004) |
| `pending` | `rejected` | A rejection (`01` APR-REQ-005); a deadline's `Reject` (`01` APR-REQ-009) |
| `pending` | `cancelled` | A cancel (`01` APR-REQ-010); a deadline's `Cancel`; no one to assign (`01` APR-REQ-007) |
| `approved` | `cancelled` | A cancel (`01` APR-REQ-010) |
| `approved` | `void` | A change recorded to a protected field (`01` APR-REQ-011) |
| `approved` | `completed` | A completion (`01` APR-REQ-012) |

**A step's status** (`StepStatus`), members in this order:

| Status (spelling) | Meaning |
|---|---|
| `Waiting` (`waiting`) | Not yet reached, or never reached because the request closed or approved before it |
| `Current` (`current`) | Started; its assignments are being decided |
| `Done` (`done`) | Completed by its approvals |
| `Skipped` (`skipped`) | Its condition was false when the request reached it |
| `Closed` (`closed`) | It was current when the request was rejected or cancelled |

---

## 4. Configuration

Under the section `Themis`. A duration is written in .NET's `TimeSpan` form
(`00:00:05`). Every key takes effect at the start (`04` APR-OPS-001).

| Key | Default | Rule | Used by |
|---|---|---|---|
| `Themis:Timeout` | `00:00:05` | 1 second to 1 minute | Each call into a seam (`03` APR-LIB-005); the session timeout (`04` APR-OPS-003) |
| `Themis:Database:Retries` | `3` | 0 to 10 | `04` APR-OPS-003 |
| `Themis:Jobs:Enabled` | `true` | A boolean | `04` APR-OPS-004 |
| `Themis:Jobs:Hosted` | `true` | A boolean | `04` APR-OPS-004 |
| `Themis:Jobs:Interval` | `00:00:30` | 5 seconds to 1 hour | `04` APR-OPS-004 |
| `Themis:Jobs:ClaimTimeout` | `00:05:00` | 1 minute to 1 hour, and above three times `Themis:Timeout` | `04` APR-OPS-004 |
| `Themis:Jobs:BatchSize` | `100` | 1 to 1,000 | `04` APR-OPS-004 |
| `Themis:Jobs:StallFactor` | `3` | 2 to 100 | `job-stalled` (section 5) |
| `Themis:Observer:Enabled` | `true` | A boolean | `01` APR-REQ-014; `03` APR-LIB-005 |
| `Themis:Observer:LagThreshold` | `00:05:00` | 1 minute to 1 day | `observer-failing` (section 5); the health check (`04` APR-OPS-005) |
| `Themis:Alerts:Repeat` | `01:00:00` | 1 minute to 1 day | `04` APR-OPS-006 |
| `Themis:Retention:ClosedRequests` | None | 30 to 10,950 days (30 years of 365 days), in whole days: a value holding a part of a day is refused; unset, nothing is deleted | `01` APR-REQ-015; `04` APR-OPS-004 |

---

## 5. Alert conditions

Raised, raised again and cleared as `04` APR-OPS-006 gives, by the `watch` job.

| Condition | Scope | Holds while | Details |
|---|---|---|---|
| `observer-failing` | None | The oldest event not yet taken was written longer ago than `Themis:Observer:LagThreshold`, in a deployment whose applications run the observer (`01` APR-REQ-014) | `oldest`, the instant it was written |
| `request-unassignable` | The type's key | Less than `Themis:Alerts:Repeat` has passed since a request of the type was cancelled with `noapprover`, or since an escalation of one of its steps found no one (`01` APR-REQ-007, APR-REQ-009) | `request`, the last such request's identifier; `step`, its step number |
| `job-stalled` | The job's name | The job's last completion and its last renewal are both older than `Themis:Jobs:StallFactor` of `Themis:Jobs:Interval` plus `Themis:Timeout`, once a process has started it (`04` APR-OPS-004) | `lastCompleted`, the instant, or none |

---

## 6. Event kinds

`EventKind`, members in this order, and who each event names to be told (`01`
APR-REQ-014).

| Kind (spelling) | Written when | Notify |
|---|---|---|
| `Requested` (`requested`) | A request is created | The first step's unsatisfied assignees and the type's watchers |
| `StepStarted` (`stepstarted`) | A step after the first starts | The step's new assignees |
| `Escalated` (`escalated`) | A deadline adds escalation assignments | The new assignees |
| `Approved` (`approved`) | The request is approved | The requester and the watchers |
| `Rejected` (`rejected`) | The request is rejected | The requester and the watchers |
| `Cancelled` (`cancelled`) | The request is cancelled | The requester and the watchers |
| `Voided` (`voided`) | The request is voided | The requester and the watchers |
| `Completed` (`completed`) | The request is completed | The requester and the watchers |
| `DelegationCreated` (`delegationcreated`) | A delegation is created | The delegate |
| `Reminded` (`reminded`) | A reminder before a step's deadline falls due (`01` APR-REQ-009) | The step's open assignees, each with the delegates who hold their assignment |

---

## 7. Names

| Name | Value | Changes with the library's name |
|---|---|---|
| Schema | `approvals` | No |
| Migration history | `approvals.__migrations_history` | No |
| Type lock text | `approvals-type` (`04` APR-OPS-003) | No |
| Person lock text | `approvals-person` (`04` APR-OPS-003) | No |
| Job names | `deadlines`, `observer`, `watch`, `retention` (the list of `ck_jobs_name`, section 2) | No |
| Retention role | `approvals_retention` (`04` APR-OPS-002) | No |
| Command of `Themis.Cli` | `themis` | Yes |
| Meter | `Themis` | Yes |
| Health check | `themis` | Yes |

The meter's instruments:

| Instrument | Kind | Tags |
|---|---|---|
| `approvals.requests.created` | Counter | `type` |
| `approvals.requests.closed` | Counter | `type`, `status` |
| `approvals.decisions` | Counter | `type`, `outcome` |
| `approvals.events.delivered` | Counter | `kind` |
| `approvals.events.pending` | Observable gauge | None |
| `approvals.retention.deleted` | Counter | `kind` (`request` or `delegation`) |
| `approvals.erasures` | Counter | None |
| `approvals.reminders` | Counter | `type` |

---

## 8. Secrets

| Name | Form | Required |
|---|---|---|
| `connection` | A connection string Npgsql reads, UTF-8 | Always (`04` APR-OPS-008) |
