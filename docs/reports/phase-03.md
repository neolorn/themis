# Phase 3: Requests, decisions and events

## Items built

The tests of `Themis.Tests`, `Themis.Core.Tests` and `Themis.Testing.Tests` are in those projects. Every integration test runs on a database of its own in the `postgres:16.15` container of its class, captures every log entry of its host and asserts that none holds a person reference, role, target, value or reason it used.

**`01` APR-REQ-001**: criteria 1 to 3; `IApprovals.CreateAsync` and its `DbTransaction` overload, the tables `approvals.requests`, `steps`, `assignments`, `decisions`, `events` and `jobs` and the migration `20261031051643_Requests` (`internal sealed`).
- HostTransactionTests: APR_REQ_001_AC1_RequestCreatedInAHostTransactionThatRollsBackLeavesNoRequestAssignmentOrEvent
- CreateTests: APR_REQ_001_AC2_EachRefusalRecordsNothingAndNamesTheFieldWhereItHasOne, APR_REQ_001_AC3_SnapshotValueOfTheWrongKindIsRefusedNamingTheField; Create_ApproversOver200Persons_IsRefusedWithTooManyAndRecordsNothing, Create_RequestedEvent_NamesTheFirstStepsAssigneesAndTheWatchers
- RequestSchemaTests: Steps_StatusOutsideItsClosedSet_IsRefusedByTheDatabase, Jobs_EachJob_HasItsRow; StartTests: APR_OVR_002_AC2_EveryTableTheLibraryUsesLivesInItsOwnSchema, now with the six tables

**`01` APR-REQ-002**: criteria 1 and 2.
- HostTransactionTests: APR_REQ_002_AC1_TwoCreatesOfOneTypeForOneTargetAtOnceRecordOneRequestAndTheOtherIsRefusedOpen, the second create waiting on the first's open transaction
- CreateTests: APR_REQ_002_AC2_AfterTheFirstRequestIsRejectedANewOneForTheTargetIsRecorded
- RequestSchemaTests: Requests_TwoOpenOfOneTypeAndTarget_AreRefusedByTheDatabase, Requests_OneOpenAndOneClosedOfOneTypeAndTarget_AreAccepted

**`01` APR-REQ-003**: criteria 1 to 3.
- CreateTests: APR_REQ_003_AC1_StepWhosePermissionTheDirectoryAnswersForThreePersonsOneTheRequesterHasTwoAssignments, APR_REQ_003_AC2_PersonGivenThePermissionAfterTheStepStartedIsNotAssignedToIt, APR_REQ_003_AC3_StepWhoseConditionIsFalseIsRecordedSkippedAndTheNextOneStarts

**`01` APR-REQ-004**: criteria 1 and 3 to 6; `IApprovals.DecideAsync`, its six checks in their order under the request's lock.
- DecideTests: APR_REQ_004_AC1_DecisionByAPersonWithNoAssignmentOfTheCurrentStepIsRefusedNotAssigned, APR_REQ_004_AC3_PersonTheDirectoryNoLongerAnswersAsAbleToApproveIsRefusedNotPermittedAndNothingIsRecorded, APR_REQ_004_AC4_TwoApprovalsOfAnAnyOneStepAtOnceRecordOneDecisionAndTheOtherIsRefusedNotAssigned, APR_REQ_004_AC5_DecisionOnARejectedRequestIsRefusedClosed, APR_REQ_004_AC6_AllStepOfThreeAssignmentsCompletesAtTheThirdApprovalAndNotBefore; Decide_Requester_IsRefusedSeparation, Decide_ActingForAnotherWithoutADelegation_IsRefusedNotAssigned, Decide_ReasonOver2000Characters_IsRefusedInvalidBeforeTheRequestIsRead

**`01` APR-REQ-005**: criteria 1 to 3.
- DecideTests: APR_REQ_005_AC1_RejectionWithoutAReasonIsRefusedAndRecordsNothing, APR_REQ_005_AC2_RejectionInTheFirstOfThreeStepsRejectsTheRequestAndNoLaterStepStarts, APR_REQ_005_AC3_ResubmissionNamesTheRejectedRequestAndReadingItAnswersThatLink

**`01` APR-REQ-006**: criteria 1 and 2.
- DecideTests: APR_REQ_006_AC1_UnderSkipAPersonWhoApprovedStep1AndIsAssignedToAnAnyOneStep2LeavesItCompleteWithoutASecondDecision, APR_REQ_006_AC2_UnderRequireOtherTheSamePersonIsNotAssignedToStep2AndAnotherAssigneeDecidesIt

**`01` APR-REQ-007**: criteria 1 and 2.
- CreateTests: APR_REQ_007_AC1_StepWhosePermissionResolvesToTheRequesterAloneIsAssignedToTheStepsFallbackPerson; Create_StepsFallbackExcluded_AssignsTheTypesFallback
- DecideTests: APR_REQ_007_AC2_RequestWhoseRequesterIsBothTheStepsAndTheTypesFallbackEndsCancelledWithNoApprover, the occurrence written by the create and raised by the `watch` job

**`01` APR-REQ-010**: criterion 1; `IApprovals.CancelAsync` and its `DbTransaction` overload.
- CancelAndReadTests: APR_REQ_010_AC1_PendingRequestCancelledByItsRequesterEndsCancelledWithTheActorRecorded; Cancel_ApprovedRequest_EndsCancelledWithTheActorAndThePersonActedFor, Cancel_ClosedUnknownOrReasonTooLong_IsRefusedAndChangesNothing
- HostTransactionTests: Cancel_OnAHostTransactionThatRollsBack_LeavesTheRequestPending

**`01` APR-REQ-013**: criterion 2; `IApprovals.ReadAsync` and `PendingAsync` without delegations.
- CancelAndReadTests: APR_REQ_013_AC2_RequestDecidedByThePersonOrPastItsStepLeavesTheirPendingList; Pending_PagesOfOne_AnswerTheRequestsOldestFirstThenNoCursor, Pending_CursorThatDoesNotRead_IsRefusedCursorInvalid, Pending_RequesterOrSubject_IsLeftOut, Read_UnknownRequest_IsRefusedUnknown, Read_DecidedRequest_AnswersItsWholeRecord

**`01` APR-REQ-014**: criteria 1 to 3; the events of every move, and their delivery by the `observer` job.
- JobTests: APR_REQ_014_AC1_EventWrittenInAHostTransactionThatRollsBackIsNeverDelivered, APR_REQ_014_AC2_ObserverThatThrowsIsCalledAgainWithTheSameEventAndARequestsEventsReachItInOrder
- DecideTests: APR_REQ_014_AC3_ApprovalCompletingTheLastStepWritesApprovedNamingTheRequesterAndTheWatchers

**`02` APR-TYPE-001**: criteria 2 and 4, deferred from phase 2.
- CreateTests: APR_TYPE_001_AC2_RequestCreatedUnderVersion1DecidesUnderItsStepsAfterVersion2AndOneCreatedAfterUsesVersion2, APR_TYPE_001_AC4_RetiredTypeRefusesANewRequestAndLetsItsOpenRequestsEnd

**`02` APR-TYPE-003**: criteria 1 to 3, deferred from phase 2.
- CreateTests: APR_TYPE_003_AC1_WithTwoPoliciesWhoseStartConditionsBothHoldTheRequestTakesTheFirst, APR_TYPE_003_AC2_PolicyWhoseStartConditionHoldsAndWhoseEveryStepsConditionIsFalseIsPassedOver, APR_TYPE_003_AC3_WithNoPolicyHoldingATypeThatDoesNotAllowItSendsTheRequestToItsFallbackPersonAndOneThatAllowsItApprovesAtOnce

**`02` APR-TYPE-006**: criterion 1, deferred from phase 2.
- DecideTests: APR_TYPE_006_AC1_WatcherIsNamedOnTheRequestedAndApprovedEventsAndTheirDecisionIsRefusedUnlessAssigned

**`03` APR-LIB-002**: criterion 3, deferred from phase 2; the request and event types, `IApprovals` but for what phase 4 builds, and `IApprovalJobs`, registered as a singleton.
- PrintTests: APR_LIB_002_AC3_FormattingAnApprovalRequestOrADecisionRequestWritesNoPersonRoleTargetValueOrReason; RequestRecordsAndCancelRequest_ToString_WriteNoPersonRoleTargetValueOrReason

**`03` APR-LIB-005**: criteria 2 and 3; every directory call bounded by `Themis:Timeout`.
- DecideTests: APR_LIB_005_AC2_DirectoryThatDoesNotAnswerWithinTheTimeoutLeavesADecisionRefusedUnavailableAndTheRequestAsItWas; Decide_DirectoryThatThrows_IsRefusedUnavailable
- CreateTests: APR_LIB_005_AC3_DirectoryThatAnswersAnEmptyPersonReferenceMakesTheCreateThrowAndNothingIsRecorded

**`03` APR-LIB-007**: criterion 1; `AddThemisTesting` and `TestJobs`, beside the directory, observer, secrets and migration of `4e0f214`.
- JobTests: APR_LIB_007_AC1_HostTestPublishesCreatesDecidesAndReadsTheApprovedEventFromTheRecordingObserverAfterRunUntilIdleAsync
- TestingRegistrationTests: AddThemisTesting_AfterTheHostsSeams_ReplacesThemEachResolvableByItsOwnTypeAndBySeam, AddThemisTesting_SeamRegisteredAfterIt_ReplacesTheTestingOne, RunUntilIdleAsync_Rounds_RunDeadlinesThenObserverUntilARoundHandlesNothing, TestJobs_NoJobs_ThrowsArgumentNullException
- InMemoryApproverDirectoryTests and RecordingObserverTests, six tests

**`03` APR-LIB-008**: criteria 1 to 3.
- HostTransactionTests: APR_LIB_008_AC1_CreateOnAHostTransactionLeavesNothingWhenTheHostRollsBackAndBothWhenItCommits, APR_LIB_008_AC2_RefusedCreateOnAHostTransactionLeavesTheHostsEarlierWritesAndTheHostsCommitSucceeds, APR_LIB_008_AC3_TransactionOfAnotherProviderOrACommittedOneMakesTheCallThrowArgumentException

**`04` APR-OPS-003**: criteria 1 to 3; the request's lock, `SELECT ... FOR UPDATE`, before any other row of it is read for change.
- HostTransactionTests: APR_OPS_003_AC1_RequestCreatedInAHostTransactionOnADataSourceTheHostBuiltCommitsWithTheHostsWrite
- DecideTests: APR_OPS_003_AC2_TwoDecisionsOnOneRequestAtOnceNeverDeadlockAndTheSecondIsJudgedOnWhatTheFirstLeft, both decisions waiting on a transaction that holds the request's row
- RequestSchemaTests: APR_OPS_003_AC3_StatusOutsideItsClosedSetIsRefusedByTheDatabase

**`04` APR-OPS-004**: criteria 3 and 4; the `deadlines`, `observer` and `watch` jobs on their rows of the job table, the hosted timer and `IApprovalJobs.RunAsync`.
- JobTests: APR_OPS_004_AC3_ProcessThatDiesHoldingAJobsClaimLeavesItToAnotherProcessAfterTheClaimTimeout, APR_OPS_004_AC4_ObserverFailingOnOneRequestsEventStillReceivesTheEventsOfOtherRequestsInTheSameRun; RunAsync_Deadlines_TakesAndReleasesItsClaimRecordingItsCompletionAndHandlesNothing, RunAsync_ClaimHeldByAnother_AnswersNothingAndLeavesTheClaim, RunAsync_JobsDisabled_ThrowsInvalidOperationException, RunAsync_ObserverDisabled_CompletesAtOnceDeliveringNothing, HostedJobs_JobsEnabledAndHosted_RunEachJobOnTheTimerAndDeliverTheEvents, HostedJobs_AddThemisTestingOrHostedOff_RunNoJob

**`04` APR-OPS-005**: criterion 2; the gauge `approvals.events.pending` and the health check's judgement of the observer's lag.
- JobTests: APR_OPS_005_AC2_MeterCountsRequestsCreatedAndClosedByStatusAndTheHealthCheckTurnsUnhealthyPastTheLagThreshold
- CancelAndReadTests: Logs_RequestsLifeToItsCancelAndItsRefusals_HoldNoPersonTargetRoleValueOrReason
- ObservabilityTests: AddThemis_Meter_PublishesTheReferencesInstrumentsUnderTheMeterThemis, now with the gauge

**`04` APR-OPS-006**: criteria 1 to 3; `observer-failing` and `request-unassignable`.
- JobTests: APR_OPS_006_AC1_WithRepeatOneHourAConditionHoldingFromMidnightTo0230IsRaisedAt00000100And0200AndClearedOnce, APR_OPS_006_AC2_TwoProcessesSeeingTheSameConditionRaiseItOnce, APR_OPS_006_AC3_RequestCancelledWithNoApproverRaisesRequestUnassignableForItsTypeClearedOnceTheRepeatPassesWithNoNewOccurrence

## Items not built

- `01` APR-REQ-004 criterion 2, a delegate who is the subject: delegations, phase 4. Until then a decision acting for another holds no assignment and is refused `approvals.decision.notassigned`.
- `01` APR-REQ-010 criterion 2: its first half is built and tested under a Method_Scenario name; a completed request needs `CompleteAsync`, phase 4.
- `01` APR-REQ-013 criterion 1, a delegate's pending list: phase 4. `ByTargetAsync` is not in phase 3's row.
- `03` APR-LIB-007 criterion 2 and `04` APR-OPS-004 criteria 1 and 2: the deadline actions, phase 4 (see Resolved by the lead).
- `04` APR-OPS-005 criterion 1 names a life to completion: the life to approval, rejection and cancellation, with its refusals, is tested under a Method_Scenario name, and the criterion is added once `CompleteAsync` exists, phase 4.
- `job-stalled` and the health check's judgement of a stalled job: not in phase 3's row of `04` APR-OPS-006.
- `03` APR-LIB-003 criterion 3, a hosted service creating a request in its `StartAsync`: not in phase 3's row.

## Criteria checked without a test

- `05` APR-ENG-008, tests written first: not met for the request engine and the jobs. Their code was written before their tests, and the tests found three defects, fixed before the commits: the pending list's query did not translate, a committed host transaction was not refused with `ArgumentException`, and the directory resolved when the services were built stopped a host without one before the seam check could name it.
- `05` APR-ENG-011, the commit messages of `phase-03` read and checked by `gate-commit-messages.ps1`.
- `04` APR-OPS-005: every integration test of this phase captures every log entry and asserts none holds a person reference, role, target, value or reason it used.

## Awaiting the pipeline's run

- `phase-03`'s pull request into `main`: the pipeline's `gates` run on the report's own commit is the gate of record.

## Analyser suppressions

- CA1032 and CA1064 on `Themis.Requests.Refusal`: an internal exception that carries a refusal from inside a transaction's work to the operation's answer, never thrown out of the library, so it takes no public constructors.
- CA1031 on `ObserverDelivery.DeliverAsync`, `AlertWatch.CallAsync` and `HostedJob.RunAsync`: the observer and the alert sink are the host's code, and a job that fails is logged and runs again, so whatever is thrown is caught (`01` APR-REQ-014; `04` APR-OPS-004, APR-OPS-006).

## Resolved by the lead

- The `deadlines` job in phase 3 (reading a): a real job on its row of the job table, taking, renewing and releasing its claim as `04` APR-OPS-004 gives, handling no step and answering 0 items; it throws only where `Themis:Jobs:Enabled` is off. `TestJobs` is built in full. Moved to phase 4: `03` APR-LIB-007 criterion 2, `04` APR-OPS-004 criteria 1 and 2.

## Resolved by rule

- Where several snapshot fields are wrong, the first protected field of the version, in the version's order, that is missing, of another kind or too long is named, and then the first field the version does not protect, in ordinal order. Governs: `01` APR-REQ-001, which names one field.
- A reason over 2,000 characters, and then a rejection without one, are refused before anything is read. Governs: `01` APR-REQ-004 ("before anything is read") and APR-REQ-005.
- A create whose first step finds no one is recorded, cancelled with `noapprover`, and answered as a success whose status is `cancelled`. Governs: `01` APR-REQ-007, under which the request is recorded with its event and its alert, so the create cannot answer a failure, whose rule is that nothing is recorded; `01` APR-REQ-001 lists `pending` and `approved` alone.
- A step that finds no one keeps `waiting`, never having started; the request has no current step once cancelled. Governs: `06` section 3.
- `stepstarted` names every assignee of the step, a satisfied one under `Skip` included; `requested` names the unsatisfied alone, as `01` APR-REQ-014 says. Governs: `01` APR-REQ-014, "the step's new assignees".
- The 200 persons are counted over the union of the approvers before the exclusions. Governs: `01` APR-REQ-003.
- An escalation assignee's approval completes its step in the move's check, though no escalation exists until phase 4. Governs: `01` APR-REQ-004.
- A committed or rolled back host transaction is found when the savepoint is taken, before any statement of the operation, and answered with `ArgumentException`. Governs: `03` APR-LIB-008.
- A job run that throws releases its claim without recording a completion, so `job-stalled` sees it. Governs: `04` APR-OPS-004, "released with the instant of completion when the run ends".
- A run's items: the events the `observer` job takes, and the conditions the `watch` job raises or clears; the `observer` job takes at most `Themis:Jobs:BatchSize` requests a round. Governs: `03` APR-LIB-002, `04` APR-OPS-004.
- `observer-failing` holds only where the application running the `watch` job has `Themis:Observer:Enabled` on. Governs: `06` section 5, "in a deployment whose applications run the observer".
- The gauge answers the number of events not yet taken as the last `watch` run counted them, and nothing before the first. Governs: `06` section 7.
- The hosted timer runs each job at the start and then every `Themis:Jobs:Interval`, the claim's condition on the last start keeping several processes to one run an interval. Governs: `04` APR-OPS-004.
- The pending list is ordered by the request's creation and then its identifier; its cursor is the last item's two, 24 bytes in base64url. Governs: `01` APR-REQ-013, "oldest request first, an opaque cursor".
- The test of the separation check writes the requester's assignment into the table, since only a delegation reaches that check through the engine. Governs: `05` APR-ENG-008, every criterion a test can decide.

## Open questions

- `04` APR-OPS-005 says the health check turns unhealthy while the oldest undelivered event is older than `Themis:Observer:LagThreshold`, with no exception for a deployment that turns `Themis:Observer:Enabled` off, whose events pile up undelivered (`04` APR-OPS-007). `observer-failing` holds only where the observer runs. We built the health check as the text reads: (a) it stays so, and such a deployment reads unhealthy once its first event is older than the threshold; (b) the health check judges the lag only where `Themis:Observer:Enabled` is on, as the alert does.

## Checks

- The commits: `0b35169` to the report's own, the head of `phase-03`, cut from `main` at `73fd97b`.
- Fast checks on every commit: the build of `Themis.sln` in Release with warnings as errors, the analysers, `dotnet format Themis.sln --verify-no-changes`, the unit tests and the contract tests: green.
- Integration tests: every class of `Themis.Tests` of the category: green.
- Before the push: `run-gates.ps1 -Base origin/main -Head HEAD`, every gate passed, and the full-history secret scan: green.
