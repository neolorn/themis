# Phase 1: Foundations

## Items built

Each test below was written before the code or script it proves. The tests of `Themis.Tests`, `Themis.Core.Tests` and `Themis.Cli.Tests` are in those projects; the rest are in `tests/Themis.Repository.Tests`.

**`04` APR-OPS-001**: criteria 1 and 2.
- ConfigurationTests: APR_OPS_001_AC1_EveryKeyHasItsDefaultWhenAbsent, KeyOutsideItsRuleStopsTheStartNamingIt, ClaimTimeoutNotAboveThreeTimesTimeoutStopsTheStartNamingIt; ConfigurationRules_KeyAtTheEdgeOfItsRule_IsAccepted, ConfigurationRules_FirstKeyOutsideItsRule_IsTheOneAnswered, ApprovalsOptionsValidator_ValueOutsideItsRange_IsRefused
- StartTests: APR_OPS_001_AC2_KeyChangedWhileTheApplicationRunsTakesEffectOnlyAfterARestart

**`04` APR-OPS-002**: criteria 1 to 4; the schema `approvals`, the history `approvals.__migrations_history`, the first migration `20261031031534_Foundations` (`internal sealed`), the start's schema check, and `Themis.Cli` with `themis migrate`.
- StartTests: APR_OPS_002_AC1_DatabaseMissingTheReleasesLastMigrationStopsTheStart; Start_EmptyDatabase_StopsNamingTheFirstMigration, Start_MigrationOfALaterRelease_IsAccepted
- MigrateTests: APR_OPS_002_AC2_MigrateTwiceAppliesNothingTheSecondTimeAndExitsZeroBothTimes, APR_OPS_002_AC3_RoleNamedInApplicationRolesCanReadInsertAndUpdateEveryTableButTheHistoryWhichItCanOnlyReadAndDeleteFromNone, APR_OPS_002_AC4_RoleTheDatabaseLacksExitsOneWithInputInvalidAndAppliesNothing, NameOf64BytesWhoseFirst63AreARolesNameExitsOneWithInputInvalidAndAppliesNothing; Migrate_TableOwnedByAnotherRoleThatGrantsSelectWithoutTheRightToGrantIt_ExitsOneWithGrantFailedAndGrantsNothing, Migrate_GrantThatFails_ExitsOneWithGrantFailedNamingTheRoleAndGrantsNothing, Migrate_SequenceOfTheSchema_IsGrantedUsage, Migrate_PrivilegeARoleAlreadyHolds_IsNotRevoked, Migrate_ConnectionThatCannotConnect_ExitsOneWithConnectionFailed, Migrate_MigrationThatFails_ExitsOneNamingIt
- MigrateInputTests: APR_OPS_002_AC3_InputThatDoesNotReadExitsOneWithInputInvalid; Run_ArgumentsOtherThanMigrate_ExitsOne

**`04` APR-OPS-003** (the connection, `@now`, the retries and the session timeout): criterion 4; the alert table's closed set.
- StatementRuleTests: APR_OPS_003_AC4_NoHandWrittenStatementReadsTheDatabaseClock, StatementReadingTheDatabaseClockIsFound, StatementTakingTheInstantAsAParameterIsAccepted, NoHandWrittenStatementTakesASessionLevelAdvisoryLock, SessionLevelAdvisoryLockIsFound, TransactionScopedAdvisoryLockIsAccepted, StatementInAFileUnderSrcIsFound, CommentQuotingTheDatabaseClockIsNoStatement, HandWrittenStatementOfAMigrationIsFound, SqlFileUnderSrcIsRead; CSharpSource_StringLiteral_IsReadWhole, CSharpSource_Comment_IsBlankedAndHoldsNoLiteral
- TransactionTests: RunAsync_TransactionAbortedWith40001Each_IsRunAgainAtMostRetriesTimes, RunAsync_TransactionAbortedTwiceFor40001And40P01_CommitsOnTheThirdRun, RunAsync_RetriesZero_RunsOnce, RunAsync_ErrorOtherThanADeadlockOrASerializationFailure_IsNotRunAgain, RunAsync_EachTransaction_SetsItsIdleTimeoutToTwiceTheTimeout, Now_ParameterOfAStatement_IsTheLibrarysClocksInstant
- StartTests: Alerts_ConditionOutsideItsClosedSet_IsRefusedByTheDatabase

**`04` APR-OPS-005** (the logging, the meter and the health check's registration): no criterion.
- ObservabilityTests: AddThemis_Meter_PublishesTheReferencesCountersUnderTheMeterThemis, AddThemis_MeterFactoryTheHostRegisters_MakesTheMeter, AddThemis_JobsEnabledByDefault_RegistersTheHealthCheckThemisWhichReportsHealthy, AddThemis_JobsDisabled_RegistersNoHealthCheck

**`04` APR-OPS-006** (the alert table and the sink): no criterion.
- AlertSinkTests: AddThemis_NoAlertSinkRegistered_TheLibrarysOwnLogsARaiseAtTheErrorLevel, AddThemis_NoAlertSinkRegistered_TheLibrarysOwnLogsAClearAtTheErrorLevel, AddThemis_AlertSinkRegisteredAfterTheCall_ReplacesTheLibrarysOwn, AddThemis_AlertSinkRegisteredBeforeTheCall_IsKept

**`04` APR-OPS-008**: criteria 1 and 2.
- SecretTests: APR_OPS_008_AC1_SourceThatAnswersNoConnectionStopsTheStartNamingIt, SourceThatAnswersConnectionWithNoVersionStopsTheStartNamingIt, ValueNpgsqlDoesNotReadStopsTheStartAsInvalid, ValueThatIsNotUtf8StopsTheStartAsInvalid, APR_OPS_008_AC2_NoLogEntryOfAFailedStartHoldsTheConnectionString; Start_ConnectionOfSeveralVersions_OpensTheHighest, Start_SourceThatDoesNotAnswerWithinTheTimeout_StopsAsUnavailable, Start_SecretSource_IsAskedForTheConnectionAlone
- StartTests: APR_OPS_008_AC2_NoLogEntryOfAStartOrOfItsSchemaFailureHoldsTheConnectionString

**`03` APR-LIB-003**: criteria 1 and 2.
- StartTests: APR_LIB_003_AC1_HostWithValidConfigurationAndItsSeamsStartsWithTheOneCall
- ConfigurationTests: APR_LIB_003_AC2_ConfigurationValueOutsideItsRuleStopsTheStartNamingTheKey
- ReadyTests: ApprovalsReady_BeforeTheStartupChecks_ThrowsInvalidOperationException, ApprovalsReady_AfterAFailedStart_StillThrows

**`03` APR-LIB-005** (the seams' registration and the start's refusal): criterion 1.
- SeamTests: APR_LIB_005_AC1_SecretSourceMissingStopsTheStartNamingIt, ApproverDirectoryMissingStopsTheStartNamingIt, ObserverMissingWhereTheJobsRunAndTheObserverIsOnStopsTheStartNamingIt; Start_ObserverMissingWhereItIsNotRequired_GoesPastTheSeams, Start_SeamRegisteredAfterAddThemis_IsTaken

**`03` APR-LIB-002**, the types phase 1 uses: `ApprovalsStartupException`, the seams `ISecretSource`, `IAlertSink`, `IApproverDirectory` and `IApprovalObserver`, and the values they carry; `Themis.Cli`'s empty surface files.
- PrintTests: Target_ToString_WritesNeitherItsKindNorItsIdentifier, FieldValue_ToString_WritesNoValue, ApproverReference_PersonsToString_WritesNoReference, ApproverQuery_ToString_WritesItsRequestTypeVersionAndStepAlone, ApproverCheck_ToString_WritesNoPersonTargetOrValue, ApprovalEvent_ToString_WritesNoPersonOrTarget, SecretVersion_ToString_WritesItsNumberAndNeverItsValue, ApprovalsStartupException_Made_CarriesItsCodeAndDetails
- The contract tests of phase 0 (AssemblyContractTests) now run on `Themis.Cli.dll` too.

**`05` APR-ENG-007**: criterion 1, now for codes, configuration keys, spellings, alert conditions and instruments.
- ReferenceTests: APR_ENG_007_AC1_EveryCodeTheLibraryAnswersIsInSection1AndSpeltAsTheRuleGives, EveryConfigurationKeyTheLibraryBindsIsInSection4, EveryEnumerationIsSpeltAsSection2SpellsIt, EveryAlertConditionIsSpeltAsSection5SpellsIt, EveryInstrumentATestHostsMeterPublishesIsInSection7

**`05` APR-ENG-012**, the migrations gate: `build/gates/gate-migrations.ps1` applies the migrations twice to an empty database in a PostgreSQL 16.15 container and fails where the second run changes the history or the schema. Run by `run-gates.ps1` as every script present is.

**`05` APR-ENG-001** and **`05` APR-ENG-011**, `Themis.Cli`'s project: the layout now requires it, and its package may carry the tool package type the SDK writes.
- RepositoryLayoutTests: APR_ENG_001_AC1_PackageWithoutItsProjectIsFound (now with `Themis.Cli`); the phase 0 test CommandLinePackageIsNotYetRequired removed
- PackageManifestRuleTests: ManifestViolations_ToolPackageType_IsAcceptedOnTheToolAlone

**`00` APR-OVR-002**: criterion 2; criterion 1 checked in review (below).
- StartTests: APR_OVR_002_AC2_EveryTableTheLibraryUsesLivesInItsOwnSchema

## Items not built

- `03` APR-LIB-003 criterion 3, a host's hosted service creating a request in its `StartAsync`: needs the request's create, phase 3.
- `03` APR-LIB-005 criteria 2 and 3, the directory's timeout and its empty answer: phase 3, as the plan gives.
- `04` APR-OPS-003 criteria 1 and 2, a create in a host's transaction and two decisions at once: phase 3, with the requests. Criterion 3, a status outside its closed set: the statuses come in phase 3; the closed set built now, the alert table's condition, is tested under a Method_Scenario name. Each lock comes with the item that takes it.
- `04` APR-OPS-005 criteria 1 and 2: the request's life and the outbox's lag come in phase 3. The gauge `approvals.events.pending` comes with the outbox it observes; the health check reports healthy until then, since neither of its conditions can hold.
- `04` APR-OPS-006 criteria 1 to 3: the `watch` job and the `noapprover` cancellation, phases 3 and 4.
- `05` APR-ENG-005 criterion 1: the retries are built and tested now under Method_Scenario names; the criterion is listed with the phase that proves it, phase 2.

## Criteria checked without a test

- `00` APR-OVR-002 criterion 1: the only outside dependency of `Themis`, `Themis.Core` and `Themis.Cli` is the host's PostgreSQL database; read in review of the project references and the code, and the dependency allow-list gate holds them.
- `05` APR-ENG-008, tests written first: each item's tests were written before its code; most were run failing first, the rest (the transactions', the meter's and the health check's) first run once the code beside them was written.
- `05` APR-ENG-011, the commit messages of `phase-01` read.

## Awaiting the pipeline's run

- `phase-01`'s own pull request into `main`, opened once this commit is pushed: the pipeline's `gates` run on this commit, the report's own, is the gate of record, and the migrations gate's first run on `ubuntu-24.04`. `run-gates.ps1` passed every gate locally on Windows, the migrations gate included.

## Analyser suppressions

None added to `.editorconfig`. Two in code, each with its justification, where the specification fixes the name or the shape:
- CA1711 on `ApproverReference.Permission` (`src/Themis.Core/ApproverReference.cs`): `03` APR-LIB-002 names the case `Permission`.
- CA1822 on `Target`'s `PrintMembers` (`src/Themis.Core/Target.cs`): the compiler makes `ToString` call this instance member, which prints nothing of the target.

## Resolved by rule

- `Themis:Jobs:StallFactor` is read as a whole number from 2 to 100. Governs: `06` section 4, `04` APR-OPS-001. The rule "2 to 100" and the default `3` give no fraction, and a factor counts intervals.
- The read of the secret `connection` is bounded by `Themis:Timeout`, a source that does not answer within it stopping the start with `approvals.startup.secretunavailable`. Governs: `04` APR-OPS-008, `03` APR-LIB-005. `06` section 4 gives `Themis:Timeout` as the bound of each call into a seam.
- The health check's registration reads `Themis:Jobs:Enabled` raw at `AddThemis`, registering it unless the value reads as `false`. Governs: `04` APR-OPS-005. The registration comes before any check, and a value outside its rule stops the start whatever is registered.
- The meter is made by the host's `IMeterFactory` where one is registered, and otherwise by the library itself. Governs: `04` APR-OPS-005, `05` APR-ENG-009. The package that registers a meter factory, `Microsoft.Extensions.Diagnostics`, is not in APR-ENG-009's table.
- `Themis.Cli`'s package carries `packageTypes`, which the SDK writes for a .NET tool. Governs: `05` APR-ENG-011. `00` section 2.2 makes the package a .NET tool, so the rule's "no other package property" covers the properties a project sets; the exemption holds for `Themis.Cli` alone.

## Open questions

None. Phase 0's take-up, as the coordinator's message asks:
- Settings of the guide's section 5: the default branch `main`, dependency vulnerability alerting, secret scanning and push protection on, squash merges off. All set; none refused by the account.
- `main` protected: the `gates` check required, force pushes and deletion refused.
- Before the push: the commit-message gate over `origin/main..phase-00` (14 commits) and the full-history secret scan: green.
- `phase-00` pushed, its pull request https://github.com/neolorn/themis/pull/1 opened; `gates` run 41163339612 on its head `b23f783`: success. The pull request was merged as `a244440`, the head of `main`.

## Checks

- The commit: the report's own commit, the head of `phase-01`, on `c691143`, the branch cut from `phase-00` at `b23f783`, which `main` holds through its merge `a244440`.
- Fast checks on every commit: the build of `Themis.sln` in Release with warnings as errors, the analysers, `dotnet format Themis.sln --verify-no-changes`, and the unit tests: green.
- Every unit, integration and contract test and the migrations gate: green. `run-gates.ps1 -Base main -Head HEAD`: every gate passed (build, commit messages, dependency allow-list, format, migrations, project references, public surface, restore, secret scan, test names, tests).
- Before the push: the commit-message gate over `origin/main..phase-01` (14 commits) and the full-history secret scan: green.
