# Phase 4: Delegation, deadlines, protection and the record

## Items built

The tests of `Themis.Tests`, `Themis.Core.Tests`, `Themis.Cli.Tests` and `Themis.Repository.Tests` are in those projects. Every integration test of `Themis.Tests` runs on a database of its own in the `postgres:16.15` container of its class, captures every log entry of its host and asserts that none holds a person reference, role, target, value or reason it used.

**`01` APR-REQ-008**: criteria 1 to 4; `IDelegations` (`CreateAsync`, `EndAsync`, `ListAsync`), the table `approvals.delegations`, the foreign key from `approvals.events.delegation` and the migration `20261031063506_Delegations` (`internal sealed`); a decision through a delegation live at that instant and covering the type.
- DelegationTests: APR_REQ_008_AC1_DelegationToOneselfIsRefused, APR_REQ_008_AC2_DelegateDecidesForTheLenderWithinThePeriodAndIsRefusedNotAssignedAfterItEndsOrIsEnded, APR_REQ_008_AC3_DelegateOfADelegateCannotDecideForTheFirstLender, APR_REQ_008_AC4_DecisionThroughADelegationRecordsTheDeciderAndThePersonActedFor; Create_EndsNotAfterStartsOrNotAfterNowOrPeriodOver365Days_IsRefusedNamingEnds, Create_Delegation_WritesItsDelegationCreatedEventNamingTheDelegate, End_LiveDelegation_RecordsItsActorAndInstantAndAnUnknownOrEndedOneIsRefused, List_Person_AnswersTheDelegationsFromAndToThemThatHaveNotEnded, Decide_LenderTheDirectoryNoLongerAnswers_IsRefusedNotPermitted, Decide_ThroughADelegationNotCoveringTheType_IsRefusedNotAssigned, Decide_DelegateWhoDecidedTheStepInTheirOwnName_IsRefusedDuplicateActingForTheLender

**`01` APR-REQ-004**: criterion 2, left by phase 3.
- DelegationTests: APR_REQ_004_AC2_DelegateWhoIsTheRequestsSubjectIsRefusedSeparation

**`01` APR-REQ-009**: criteria 1 to 3; the `deadlines` job's actions: `Reject` and `Cancel` with the reason code `deadline`, `Escalate` with its escalation assignments, the fallbacks after them, and `request-unassignable` where both are excluded.
- DeadlineTests: APR_REQ_009_AC1_StepWithAOneHourDeadlineAndRejectIsRejectedWithDeadlineOnceTheClockPassesTheHourAndNotBefore, APR_REQ_009_AC2_EscalatedAllStepCompletesAtTheFirstApprovalOfItsEscalationAssignee, APR_REQ_009_AC3_StepEscalatedOnceIsNotEscalatedAgain; Deadline_Cancel_CancelsTheRequestWithDeadline, Deadline_EscalateToExcludedOrAlreadyAssigned_AddsOnlyThePersonsLeft, Deadline_EscalationLeavingNoOne_GoesToTheStepsFallback, Deadline_EscalationWithBothFallbacksExcluded_KeepsTheStepsAssigneesAndRaisesRequestUnassignable, Deadline_DirectoryFailing_LeavesTheStepForTheJobsNextRun, Deadline_RequestAHostTransactionHolds_IsLeftForALaterRunRatherThanWaitedFor

**`01` APR-REQ-010**: criterion 2, left by phase 3.
- CompleteTests: APR_REQ_010_AC2_ApprovedRequestNotYetCompletedCanBeCancelledAndACompletedOneCannot

**`01` APR-REQ-011**: criteria 1 to 3; `IApprovals.ProtectionAsync`, `RecordChangeAsync` and its `DbTransaction` overload.
- ProtectionTests: APR_REQ_011_AC1_FieldOfAPendingRequestReadsLockedAndRecordingAChangeToItIsRefused, APR_REQ_011_AC2_ChangeRecordedToAnApprovedRequestsProtectedFieldVoidsItAndAChangeToAFieldItDoesNotProtectLeavesItApproved, APR_REQ_011_AC3_ChangeRecordedAfterTheRequestIsCompletedVoidsNothing; Protection_FieldOnAnotherTarget_IsFree, RecordChange_OnAHostTransaction_VoidsNothingWhenTheHostRollsBackAndVoidsWhenItCommits

**`01` APR-REQ-012**: criterion 1; `IApprovals.CompleteAsync` and its `DbTransaction` overload.
- CompleteTests: APR_REQ_012_AC1_CompletingAPendingRequestIsRefusedAndCompletingAnApprovedOneEndsItCompleted; Complete_ClosedOrUnknownRequest_IsRefusedAndChangesNothing, Complete_OnAHostTransaction_LeavesTheRequestApprovedWhenTheHostRollsBackAndCompletesItWhenItCommits

**`01` APR-REQ-013**: criterion 1, left by phase 3; the pending list with delegations, and `ByTargetAsync`.
- DelegationTests: APR_REQ_013_AC1_DelegatesPendingListHoldsTheLendersOpenAssignmentsOfTheCoveredTypesMarkedAsActedForTheLenderAndNoneOfAnotherType; Pending_OwnAndLentAssignmentsOfOneRequest_ListItOnceAsTheirOwn, Pending_DelegateWhoDecidedTheStepInTheirOwnName_NoLongerListsItForTheLender
- ExportTests: ByTarget_TargetWithRequests_AnswersThemNewestFirstAndNoneOfAnotherTarget

**`01` APR-REQ-015**: criteria 1 and 2; `IApprovals.ExportAsync`.
- MigrateTests (`Themis.Cli.Tests`): APR_REQ_015_AC1_ApplicationRoleCannotDeleteOrTruncateTheRowsOfAnyOfTheLibrarysTables, every table of the schema tried with `DELETE` and `TRUNCATE` as a role `themis migrate` granted
- ExportTests: APR_REQ_015_AC2_ExportHoldsEveryRequestAssignmentDecisionAndDelegationThatNamesThePersonAndNoneThatDoesNot

**`03` APR-LIB-002**: `IDelegations` and the rest of `IApprovals`, with `DelegationDefinition`, `DelegationRecord`, `RequestSummary`, `PersonExport`, `AssignmentExport` and `DecisionExport`, none of which prints a person or a reason.
- PrintTests: DelegationsSummariesAndExports_ToString_WriteNoPersonTargetOrReason

**`03` APR-LIB-003**: criterion 3, left by phase 1.
- StartTests: APR_LIB_003_AC3_HostedServiceRegisteredBeforeAddThemisThatCreatesARequestInItsStartAsyncRecordsItOnlyAfterTheStartupChecksRan, the startup checks' log line before the request's

**`03` APR-LIB-007**: criterion 2.
- DeadlineTests: APR_LIB_007_AC2_TestMovesTheClockPastAStepsDeadlineAndFindsTheStepEscalatedAfterRunUntilIdleAsync

**`04` APR-OPS-004**: criteria 1 and 2; the steps due taken in batches of `Themis:Jobs:BatchSize`, oldest due first, each request `FOR UPDATE SKIP LOCKED` in a transaction of its own, the claim renewed after each.
- DeadlineTests: APR_OPS_004_AC1_WithHostedOffNoStepsDeadlineRunsUntilRunAsyncIsCalledForDeadlinesWhichThenRunsIt, APR_OPS_004_AC2_TwoProcessesRunningTheDeadlinesJobAtOnceRunEachStepsActionOnce

**`04` APR-OPS-005**: criterion 1, left by phase 3; the health check's judgement of a stalled job, whatever `Themis:Observer:Enabled` says, the lag alone judged where it is on (T-022).
- CompleteTests: APR_OPS_005_AC1_LogsOfARequestsLifeFromCreateToCompletionItsRefusalsIncludedHoldNoPersonTargetRoleValueOrReason
- JobTests: Health_StalledJob_ReadsUnhealthyWhateverThemisObserverEnabledSays, for the observer on and off

**`04` APR-OPS-006**: `job-stalled`.
- JobTests: Watch_StartedJobWhoseCompletionAndRenewalPassTheStallThreshold_RaisesJobStalledWithItsLastCompletionAndClearsItOnceItCompletes, Watch_StartedJobThatNeverCompleted_RaisesJobStalledWithNoLastCompletionAndNeverJudgesAJobNoApplicationRuns

**`07` APR-THR-001**: criterion 1.
- ThreatModelTests (`Themis.Repository.Tests`, contract): APR_THR_001_AC1_EveryControlTheLibraryCarriesHasATestCarryingTheIdentifierOfTheRequirementThatNamesIt

**`07` APR-THR-002**: criterion 1.
- ThreatTests: APR_THR_002_AC1_OnlyTheStepsApprovalsOrTheTypesApprovalAtOnceApproveARequestAndNoDeadlineCancelVoidOrRecordedChangeDoes

`04` APR-OPS-004 criterion 4 stays in `build/gates/built-items.txt`.

## Items not built

None of row 4.

## Criteria checked without a test

- `07` APR-THR-004 has no criterion. The library takes every person reference it is given as the host's statement of who acts: no call reads a signed-in identity, and each decides only whether that person may act under the rules.
- `05` APR-ENG-008, tests written first: met for each item, with these exceptions.
  - The print test of `DelegationDefinition` and `DelegationRecord` came with the export item, after the delegation item had built the two records; it was written before the export item's own records.
  - The `ArgumentNullException` that `CompleteAsync`, `IDelegations.EndAsync` and `ListAsync` now throw for a null person, as their documentation says, has no test of its own.
  - The tests of `07` APR-THR-002, `04` APR-OPS-005 criterion 1 and `03` APR-LIB-003 criterion 3 prove code built before them, and no code came with them.
- `05` APR-ENG-011, the commit messages of `phase-04` read and checked by `gate-commit-messages.ps1`.
- `04` APR-OPS-005: every integration test of this phase captures every log entry and asserts none holds a person reference, role, target, value or reason it used.

## Awaiting the pipeline's run

- `phase-04`'s pull request into `main`: the pipeline's `gates` run on the report's own commit is the gate of record.

## Analyser suppressions

None added.

## Resolved by the lead

- The plan's row 4 (`fea16c9`) names the `observer` job's criterion phase 3 proved.
- The health check (T-022): restructured so that a stalled job turns it unhealthy whatever `Themis:Observer:Enabled` says, and only the lag is gated by it.
- `04` APR-OPS-004 criterion 4 stays in `build/gates/built-items.txt`, proven in phase 3.

## Resolved by rule

- A delegation's refusal names the member `to` where `To` is `From`, and `ends` where `Ends` is not after `Starts`, not after now, or more than 365 days after `Starts`; a period of exactly 365 days and a start in the past are accepted. Governs: `01` APR-REQ-008.
- A delegation has ended where it was ended or its `Ends` has passed: `EndAsync` refuses either with `approvals.delegation.ended`, and `ListAsync` leaves both out; one not yet started is listed and can be ended. The list is ordered by the start and then the identifier. Governs: `01` APR-REQ-008.
- A decision through a delegation reads the delegation in the decision's transaction, without locking it; the directory is asked first for the delegate, with `ActingFor` the lender, then for the lender, with no `ActingFor`, both through the lender's assignment's `Via`. Governs: `01` APR-REQ-004, APR-REQ-008.
- The pending list holds one item for each request: the person's own assignment where they hold one, and otherwise the first lender's, in ordinal order; a request whose current step the person has decided in their own name is left out, as `01` APR-REQ-013 criterion 2 gives. Governs: `01` APR-REQ-013.
- An escalation adds no one who already holds an assignment of the step, and no earlier approval satisfies an escalation assignment under `Skip`, so a deadline never approves. Governs: `01` APR-REQ-009, `07` APR-THR-002.
- A fallback an escalation assigns is a fallback assignment, as `01` APR-REQ-007 gives, not an escalation one, so it completes the step by the step's mode; its `escalated` event names it. Governs: `01` APR-REQ-009, "tried as APR-REQ-007 gives".
- An escalation whose `EscalateTo` resolves to more than 200 persons changes nothing and leaves the step for the next run, as a directory call that fails does. Governs: `01` APR-REQ-003, APR-REQ-009.
- Where both fallbacks are excluded at an escalation, the action has still run: the step records the instant, and no later run tries it again. Governs: `01` APR-REQ-009, "the action runs once".
- A run of the `deadlines` job tries each request once, a request a host's transaction holds being left for the next run and not counted; it takes batches until none is left. Its items are the actions run. Governs: `04` APR-OPS-004.
- A request covers a field where its snapshot holds it, the snapshot holding its version's protected fields and those alone (`01` APR-REQ-001). `RecordChangeAsync` locks the open requests on the target in identifier order. A void records the actor as `ClosedBy`, the field as `ChangedField` and the reason code `fieldchanged`, on the request and on its `voided` event. Governs: `01` APR-REQ-011.
- A completion records its actor as `ClosedBy`; its `completed` event names the requester and the watchers (`06` section 6), and it counts in `approvals.requests.closed` with the status `completed`. Governs: `01` APR-REQ-012.
- An export holds the requests the person made or is the subject of, each as its whole record; their own assignments; the decisions they made or that were made for them; and every delegation from or to them, ended ones included. A request they only cancelled or completed is not among the requests. Governs: `01` APR-REQ-015.
- `ByTargetAsync` orders by creation and then identifier, newest first. Governs: `01` APR-REQ-013.
- `job-stalled` holds where the last completion and the last renewal are both strictly older than `Themis:Jobs:StallFactor` times `Themis:Jobs:Interval` plus `Themis:Timeout`, one never recorded counting as older; its details omit `lastCompleted` where there is none. The health check's description names the stalled jobs. Governs: `06` section 5, `04` APR-OPS-005.
- `07` APR-THR-001 criterion 1 reads every requirement the table names and needs a test named after one of its criteria for each, but for `05` APR-ENG-012, the pipeline's gates, which its run proves. Governs: `07` APR-THR-001.
- `01` APR-REQ-015 criterion 1 is tested in `Themis.Cli.Tests`, as a role `themis migrate` granted, since the grants are the tool's. Governs: `05` APR-ENG-008.
- The test of `04` APR-OPS-006 criterion 1 now counts the calls of `observer-failing` alone, since `job-stalled` is raised for the `observer` job within its three hours. Governs: `04` APR-OPS-006.

## Open questions

None.

## Checks

- The commits: the coordinator's `fea16c9`, at the head of `main` (`05cd915`) when `phase-04` was cut, then the commits of this phase to the report's own.
- Fast checks on every commit: the build of `Themis.sln` in Release with warnings as errors, the analysers, `dotnet format Themis.sln --verify-no-changes`, the unit tests and the contract tests: green.
- Integration tests: every class of every test project of the category: green.
- Before the push: `run-gates.ps1 -Base origin/main -Head HEAD`, every gate passed, and the full-history secret scan: green.
