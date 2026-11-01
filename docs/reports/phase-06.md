# Phase 6: Retention and erasure

## Items built

The tests are in `tests/Themis.Tests` unless named otherwise. Each integration test runs on a database of its own in the `postgres:16.15` container of its class and asserts no log entry holds a value it used.

**`01` APR-REQ-015**: criteria 3 to 10; `Themis:Retention:ClosedRequests`, unset by default, from 30 to 10,950 whole days; the job `retention`, its row added by the migration `20261101000902_Retention`; `IRecordRetention` with `EraseAsync`, scoped, answering `ErasureReceipt` (`Changed`, `KeptVersions` of `TypeVersionReference`); the codes `approvals.retention.notgranted`, `approvals.erasure.pending` and `approvals.erasure.typeversion`; the counters `approvals.retention.deleted`, tagged `kind` (`request`, `delegation`), and `approvals.erasures`.
- RetentionTests (integration): APR_REQ_015_AC3_RequestClosedADayLessThanThePeriodStaysAndOneClosedADayMoreGoesWithEveryRowOfItAndNothingElseDoes, APR_REQ_015_AC4_UndeliveredEventHoldsARequestPastThePeriodUntilTheObserverTakesIt, APR_REQ_015_AC4_LaterRequestNamingItAsPreviousThatIsNotDueHoldsItAndTheChainGoesTogether, APR_REQ_015_AC6_RunWithoutTheRetentionGrantFailsNamingRetentionNotGrantedAndRunsOnceTheLoginHoldsApprovalsRetention, APR_REQ_015_AC8_WithThePeriodUnsetNothingIsDeletedAndTheRunNeedsNoGrant, APR_REQ_015_AC9_DelegationEndedADayMoreThanThePeriodAgoGoesWithItsEventAndOneEndedADayLessStays, APR_REQ_015_AC9_UndeliveredEventHoldsADelegationPastThePeriodUntilTheObserverTakesIt; RunAsync_Retention_CountsEachRequestAndDelegationDeletedByKind
- ErasureTests (integration): APR_REQ_015_AC5_ErasureReplacesEveryReferenceWithOnePseudonymClearsThePersonsReasonsAndASecondCallAnswersZero, APR_REQ_015_AC6_ErasureOfTheRequesterSubjectAssigneeDelegateOrAnyoneElseAPendingRequestNamesIsRefusedWithErasurePending, APR_REQ_015_AC6_ErasureOfAPersonTheNewestVersionOfATypeNamesIsRefusedWithErasureTypeVersionAndTheOlderVersionsAreAnsweredOnceItNoLongerDoes, APR_REQ_015_AC6_ErasureWithoutTheRetentionGrantIsRefusedWithRetentionNotGranted, APR_REQ_015_AC7_ExportOfAnErasedReferenceOrOfThePseudonymThatReplacedItIsEmpty, APR_REQ_015_AC10_ErasureRacingACreateNamingThePersonEndsWithOneAfterTheOtherNeverADeadlock; EraseAsync_ChangingRows_CountsOneErasureAndNoneForAnAnswerOfZero
- MigrateTests (`tests/Themis.Cli.Tests`, integration): APR_REQ_015_AC6_LoginNamedInRetentionRolesHoldsApprovalsRetentionWhichDeletesFromTheRequestAndDelegationTablesAndUpdatesTheirPersonAndReasonColumnsOnly
- ConfigurationTests (unit): the key's rows in the configuration report and its refusals (29 days, 10,951 days, a part of a day, text); ApprovalsOptionsValidator_RetentionPeriodOutsideItsRange_IsRefused, ApprovalsOptionsValidator_RetentionPeriodUnset_IsAccepted

**`04` APR-OPS-002**: criterion 4 for `retentionRoles`; `themis migrate` creates the role `approvals_retention` (NOLOGIN) and grants it to each login `retentionRoles` names, only where it names one.
- MigrateTests (`tests/Themis.Cli.Tests`, integration): APR_OPS_002_AC4_LoginInRetentionRolesTheDatabaseLacksExitsOneWithInputInvalidNamingRetentionRolesAndAppliesNothing
- MigrateInputTests (`tests/Themis.Cli.Tests`, unit): `retentionRoles` as text, as a number and as an empty reference, each refused naming `retentionRoles`

**`04` APR-OPS-003**: the person's lock, `approvals-person`; an erasure takes it exclusive first; a create, a step's start, an escalation, a fallback's assignment and a delegation's creation take each person they name shared, after the request's row; criterion 10 of APR-REQ-015 above is its test.

`build/gates/built-items.txt` gains `APR-REQ-015 AC3` to `APR-REQ-015 AC10`.

## Columns an erasure touches

- `requests`: `requester`, `subject`, `closed_by`, `closed_for` replaced; `reason` cleared where `closed_by` is the person.
- `assignments`: `person` replaced; `via` replaced where it is the person's approver reference.
- `decisions`: `decider`, `acting_for` replaced; `reason` cleared where `decider` is the person.
- `delegations`: `from_person`, `to_person`, `created_by`, `ended_by` replaced.
- `events`: `actor`, `acting_for` replaced, and each entry of `notify` that is the person.
- `steps` hold no person. No other column holds free text a person wrote. The snapshot of a request's type version and the published versions are not changed.

## Items not built

None of phase 6.

## Criteria checked without a test

- `05` APR-ENG-008, tests written first: met for each item, with these exceptions.
  - The job `retention`, `RecordRetention` and the grant's check were written before their tests.
  - The person's lock and criterion 10's test were written together, in `57653b9`.
  - The broadened pending refusal of `b0dbb80` was written before its test's case of a watcher.

## Awaiting phases 7 to 9

Three tests read the chapters merged from `phase-06-docs` at `379ed8e` and fail until the items they name are built, on this same branch and pull request:
- ReferenceTests APR_ENG_007_AC1_EveryEnumerationIsSpeltAsSection2SpellsIt: `AtLeast` (phase 7) and `Reminded` (phase 8).
- ObservabilityTests AddThemis_Meter_PublishesTheReferencesInstrumentsUnderTheMeterThemis: `approvals.reminders` (phase 8).
- PackageManifestRuleTests (`tests/Themis.Repository.Tests`): the package table's row `Themis.Endpoints` (phase 9).

## Analyser suppressions

None added.

## Resolved by the lead

- The role is `approvals_retention`; an unset period completes the job at once; the run without the grant throws `InvalidOperationException` naming `approvals.retention.notgranted`; `EraseAsync` answers it as a `Result` (T-025 and the lead's messages on phase 6). Governs: `01` APR-REQ-015.
- A chain of requests linked by `Previous` goes in one transaction, at most 1,000 requests a run, a chain alone over 1,000 going alone. Governs: `01` APR-REQ-015.
- The refusals' order is `notgranted`, `pending`, `typeversion`, the last naming the type in `details.type`. Governs: `01` APR-REQ-015.
- An export of a pseudonym is empty. Governs: `01` APR-REQ-015.
- An erasure locks no pending request's row; criterion 10 tests the order of locks (the lead's message on `379ed8e`). Governs: `04` APR-OPS-003.

## Resolved by rule

- An erasure is refused while any pending request names the person in any column, or the person is the delegate, by a delegation that has not ended, of an open assignee of its current step; so it never locks a pending request's row (`04` APR-OPS-003). Governs: `01` APR-REQ-015.
- The grant counts as held by the session's privileges however obtained: the job needs DELETE on the six tables, an erasure that and UPDATE on the columns it rewrites. Governs: `01` APR-REQ-015.
- The delegations a run deletes count toward the same 1,000, each in a transaction of its own with its events. Governs: `01` APR-REQ-015.
- `approvals.erasures` counts a call that changed at least one row; `approvals.retention.deleted` counts each request and each delegation, not their rows. Governs: `06` section 7.
- A delegation ending exactly at the cutoff stays: due means ended strictly before it. Governs: `01` APR-REQ-015.

## Open questions

- With `Themis:Observer:Enabled` off in every application, no event is ever taken, so criterion 4 holds every closed request and criterion 9 every delegation with an event, and the job deletes nothing. We built it as the chapter reads. Is that intended, or should events count as delivered where no application delivers them? This parks nothing.

## Checks

- The commits: `main` at `ad4f606`, then the plan's and the lead's `docs:` commits `89bace8` to `cc3bac5`, `7e90ee5`, the merge of `phase-06-docs` at `379ed8e` (`a420a47`), `57653b9`, `b0dbb80`, and the report's own.
- Fast checks on the last commit: the build of `Themis.sln` in Release with warnings as errors, the analysers, `dotnet format Themis.sln --verify-no-changes`, the unit tests and the contract tests: green but for the two `Themis.Tests` tests above, and the package table's test in `Themis.Repository.Tests`.
- Integration tests: RetentionTests and ErasureTests, 15 of 15 green; `Themis.Cli.Tests`, 34 of 34 green; the whole of `Themis.Tests`, every test green but the two above.
- Before the push: `run-gates.ps1 -Base origin/main -Head HEAD`. Every gate passed but two: `gate-tests`, on the three tests awaiting phases 7 to 9 alone; and `gate-commit-messages`, on the merge commit's first line, which git wrote, and on the first line and body of the pending refusal's commit. Neither was pushed: both were reworded and the commits after them replayed onto them, their trees unchanged.
