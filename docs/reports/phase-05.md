# Phase 5: The release gate, the sample host and release 1.0.0-rc.1

## Items built

The tests of `Themis.Repository.Tests` are in that project, and those of the sample host in `samples/Observatory.Tests`. A release `V` is `X.Y.Z` or `X.Y.Z-rc.N`, `N` from 1, wherever a gate reads a release (T-020).

**`03` APR-LIB-006**: criteria 1 and 2; the release compatibility gate, `build/gates/gate-release-compatibility.ps1`, which runs the tests of `ReleaseGateTests`, and `build/gates/release-baseline.json` (the codes, configuration keys, spellings and metrics of a release), read at the tag of the newest final release, a release candidate's tag never being it, its baseline empty before the first.
- ReleaseRuleTests (unit): APR_LIB_006_AC1_ChangeOrRemovalOfTheSurfaceCodesKeysSpellingsOrMetricsNeedsAMajorRelease, APR_LIB_006_AC1_AdditionOfAMemberCodeKeySpellingOrMetricNeedsAMinorRelease, APR_LIB_006_AC1_MemberAddedToAHostImplementedInterfaceWithoutADefaultImplementationIsABreak, APR_LIB_006_AC1_ReleaseTagBelowWhatChangedNeedsFails, APR_LIB_006_AC1_PullRequestNeedingAMajorReleasePassesOnlyWhereACommitSinceTheNewestFinalReleaseIsMarkedBreaking; Compare_NothingChanged_NeedsAPatchRelease, MarkedBreaking_CommitMessage_ReadsItsMarkOrFooter, ReleaseSet_WrittenAndParsed_IsTheSameSet, ReleaseSet_ReadFromTheBuiltAssemblies_HoldsTheCodesKeysSpellingsAndInstruments
- ReleaseHistoryTests (integration): APR_LIB_006_AC2_WithNoFinalReleaseTaggedAPullRequestAndAReleaseCandidatesTagThatRemoveAPublicMemberPass, APR_LIB_006_AC2_WithAFinalRelease100TaggedTheSameRemovalFailsInAPullRequestCarryingNoBreakingMarkAndInATagV110Rc1; CompatibilityViolations_RemovalMarkedBreakingAfterTheFinalRelease_PassInAPullRequestAndInAMajorReleasesTag, CompatibilityViolations_CodeRemovedSinceTheFinalReleasesBaseline_FailsInAPullRequest, WithoutDefault_MemberOfABuiltSeam_IsTrue
- ReleaseGateTests (integration, the gate itself): ReleaseGate_ThisRepositoryAtHead_IsWithinWhatItsNewestFinalReleaseAllows, ReleaseGate_ThisRepositoryAtHead_HoldsTheOwnSetOfItsLastReleaseCommit
- CommitMessageGateTests (integration): PreviousReleaseTag_ReleasesAndReleaseCandidates_AnswersTheHighestBySemanticVersioningPrecedence, PreviousReleaseTag_Final_AnswersTheHighestFinalReleaseAlone, PreviousReleaseTag_TagOnHeadItself_IsLeftOut, and `TagsOfOtherShapes_AreLeftOut` with the shapes outside the rule (`v1.1.0-rc.0`, `rc.01`, `beta`, a leading zero)

**`05` APR-ENG-011**: criteria 2 to 4; the release commit's checks in `gate-commit-messages.ps1`, `chore: release V` with its changelog heading `## [V] - YYYY-MM-DD` and a first line naming any other version refused; `previous-release-tag.ps1` by Semantic Versioning precedence, with `-Final`; the workflow run for `vX.Y.Z` and `vX.Y.Z-rc.N` tags, and its `release` job, which packs every package at the tag's version through `build/gates/pack-packages.ps1` and creates the tag's release with them and `CHANGELOG.md` attached, its notes `RELEASE-NOTES.md`, a release candidate's alone marked as a pre-release.
- ReleaseCommitGateTests (integration): APR_ENG_011_AC2_ReleaseCommitChangingTheReleasedFilesAsItsValueGivesPasses, for `1.0.0-rc.1`, `1.0.0-rc.10`, `1.0.0` and `2.10.0`; APR_ENG_011_AC3_ReleaseCommitChangingAFileOutsideItsValueFails, APR_ENG_011_AC3_ReleaseCommitWritingOneOfItsFilesOtherwiseThanItsValueGivesFails, APR_ENG_011_AC3_ReleaseCommitFollowedByACommitOtherThanADocsCommitChangingOnlyDocsFails, APR_ENG_011_AC3_ReleaseCommitNamingAVersionOutsideTheRuleFails; Gate_ChoreThatNamesNoVersion_IsNoReleaseCommit
- ReleaseHistoryTests (integration): APR_ENG_011_AC3_ReleaseCommitWhoseBaselineIsNotTheReleasesOwnSetFails; BaselineViolations_ReleaseCommitWritingTheReleasesOwnSet_AreNone, BaselineViolations_ChangeOutsideDocsSinceTheReleaseCommit_AreNotLookedFor
- CommitMessageGateTests (integration): APR_ENG_011_AC1_ChangelogLineUnderChangedPassesOnceAReleaseIsTagged, now for `v1.0.0` and `v1.0.0-rc.1`
- WorkflowTests (unit): APR_ENG_011_AC4_PipelineRunsForTheTagsOfFinalReleasesAndReleaseCandidates, APR_ENG_011_AC4_ReleaseJobRunsOnlyForATagOnceTheGatesJobHasPassed, APR_ENG_011_AC4_OnlyTheReleaseJobCanWriteToTheRepositorysContents, APR_ENG_011_AC4_ReleaseJobPacksEveryPackageAtTheTagsVersionAndCreatesTheTagsReleaseMarkingAReleaseCandidatesAPreRelease; GatesJob_EveryEvent_RunsEveryGateAndCannotWriteToTheContents

**`05` APR-ENG-001**: criterion 1 for the sample host's solution, and criterion 2; the sample host `samples/Observatory` and its test project `samples/Observatory.Tests` in `samples/Samples.sln`, built from the packages packed from the same commit, with `samples/nuget.config`; and its gate, `build/gates/gate-sample-host.ps1` (APR-ENG-012), which packs the packages at `0.0.0-sample.g<commit>`, builds the solution at that version, checks it with `dotnet format --verify-no-changes` and runs its tests.
- The sample host's domain is an invented observatory: `telescope.time`, a scheduler's step then the director's, the schedule escalating to the deputy director after two days; `instrument.loan`, the desk's step, then the custodian's where the loan's value is above 10,000. Its directory is a table of its own, its observer logs each event's kind, request and step, and its secrets come from its connection string `Approvals`.
- ObservatoryTests (`samples/Observatory.Tests`): TelescopeTime_ScheduledThenDirected_IsApprovedAndTheObserverTakesTheApprovedEvent, TelescopeTime_ScheduleDeadlinePassed_IsEscalatedToTheDeputyDirector, InstrumentLoan_ValueWithinTheDesksLimit_IsApprovedByTheDeskAlone, InstrumentLoan_ValueAboveTheDesksLimit_WaitsForTheCustodian, InstrumentLoan_RejectedByTheDesk_IsRejected, Request_CancelledByItsRequester_IsCancelledAndLeavesThePendingList, Request_ApprovedThenCompleted_IsCompleted, Decision_ByAPersonTheTableDoesNotName_IsRefusedWithTheLibrarysCode, Endpoints_NoPerson_AreRefused, EventLog_EventsOfARequest_AreLoggedByKindAndRequestNamingNoPerson; each on a database of its own in the `postgres:16.15` container of its class, capturing every log entry of the host and asserting none holds a person, proposal, instrument or reason it used
- RepositoryLayoutTests (unit): APR_ENG_001_AC1_SampleHostsPackageSourcesFollowTheRule, APR_ENG_001_AC1_SampleHostsPackageSourcesWithinTheRuleAreAccepted, APR_ENG_001_AC1_SampleHostsPackageSourcesOutsideTheRuleAreFound, APR_ENG_001_AC1_SampleHostsPackageSourceMappingOutsideTheRuleIsFound, APR_ENG_001_AC1_SampleHostsEntryPointOfTopLevelStatementsDeclaringNoNamespaceIsAccepted, APR_ENG_001_AC1_SampleFileOtherThanTheHostsEntryPointDeclaringNoNamespaceIsFound; SamplePackageSourceViolations_NoFile_IsFound
- GrantTests (contract): APR_ENG_001_AC1_SampleProjectGrantingItsInternalsIsFound, for an item, an attribute item and a source attribute; GrantViolations_SampleProjectGrantingNothing_NeedsNoBuiltAssembly
- SampleHostGateTests (integration): Gate_RepositoryWithoutTheSampleSolution_ChecksNothingAndFails

**`03` APR-LIB-001**: the sample host's row in the references test.
- ProjectReferenceTests (unit): APR_LIB_001_AC1_SampleHostAndItsTestProjectWithinTheirRowAreAccepted, APR_LIB_001_AC1_SampleProjectReferenceOutsideItsRowIsFound, APR_LIB_001_AC1_TestProjectTakingAPackedPackageOfTheLibraryIsFound
- PackageAllowListTests (unit): APR_ENG_009_AC1_LibrarysOwnPackagesTheSampleHostTakesAreNoThirdPartyPackages

**`04` APR-OPS-007**: `RELEASE-NOTES.md`, one section `## 1.0.0-rc.1`: the opening paragraph, the packages and what each holds, "Migrations that drop or rewrite data" (none: every drop of the four migrations is in a `Down`), and "What the deployment does", each duty once.

`build/gates/built-items.txt` gains `APR-ENG-011 AC3`, `APR-ENG-011 AC4`, `APR-LIB-006 AC1` and `APR-LIB-006 AC2`.

## Items not built

- The release commit `chore: release 1.0.0-rc.1` and the tag `v1.0.0-rc.1`, which wait for the lead's message after the review. The release commit writes `build/gates/release-baseline.json` as the release gate's test writes it to `tmp/release-baseline.json`.

## Criteria checked without a test

- `04` APR-OPS-007 criterion 1 is checked in review: `RELEASE-NOTES.md` names every duty.
- `05` APR-ENG-001 criterion 2 is proven by the sample host's gate, whose run is its proof; the test-name gate lists the tests under `tests/` alone, so the criterion is not in `build/gates/built-items.txt`.
- `05` APR-ENG-008, tests written first: met for each item, with these exceptions.
  - `build/gates/pack-packages.ps1` has no test of its own; the sample host's gate and the release job run it.
  - `ReleaseSet_WrittenAndParsed_IsTheSameSet` and `ReleaseSet_ReadFromTheBuiltAssemblies_HoldsTheCodesKeysSpellingsAndInstruments` passed under the stub that let the release rules' tests compile, since they test the set's reading and writing, which came with it.
  - `APR_LIB_001_AC1_TestProjectTakingAPackedPackageOfTheLibraryIsFound`, `GatesJob_EveryEvent_RunsEveryGateAndCannotWriteToTheContents` and `PreviousReleaseTag_TagOnHeadItself_IsLeftOut` hold behaviour the rules had before this phase, and passed before its changes.
  - The sample host's tests failed before the host, whose entry point was missing, and passed at their first run with it.
- `05` APR-ENG-011, the commit messages of `phase-05` read and checked by `gate-commit-messages.ps1`.

## Awaiting the pipeline's run

- `phase-05`'s pull request into `main`: the pipeline's `gates` run on the report's own commit is the gate of record, the sample host's gate among its gates.
- `05` APR-ENG-011 criterion 4: the `release` job's run on the tag `v1.0.0-rc.1`, once the owner pushes it, creating its release as a pre-release with the four packages and `CHANGELOG.md`.

## Analyser suppressions

None added.

## Resolved by the lead

- `RELEASE-NOTES.md` is written in this phase's `docs:` commit, at the lead's message, ahead of the release commit `05` APR-ENG-001 names; the release commit keeps its one section. Governs: `05` APR-ENG-001, APR-ENG-011.

## Resolved by rule

- A commit's first line claims a release where it reads `<type>: release` and a version, with or without `v`, scope or `!`; only `chore: release V` makes a release commit, and a `chore` that names no version is none. Governs: `05` APR-ENG-011.
- The date of the release commit's changelog heading is the commit's author date in UTC. Governs: `05` APR-ENG-011.
- The release commit's `RELEASE-NOTES.md` holds exactly one line `## V`. Governs: `05` APR-ENG-011, `04` APR-OPS-007.
- The content of `build/gates/release-baseline.json` is checked by the release gate, while only `docs/` changes follow the last release commit; the commit gate checks the rest of the release commit. Governs: `03` APR-LIB-006, `05` APR-ENG-011.
- The release's set is read by reflection from the built assemblies, as the contract tests read them: the constants of the codes, the paths of the options under `Themis`, the spellings of the public enumerations, and the instruments of the library's meter, each metric's kind as `06` section 7 writes it ("Counter", "Observable gauge"). Governs: `03` APR-LIB-006.
- A member added to a seam that the built assembly does not hold counts as one without a default implementation, so a break. Governs: `03` APR-LIB-006.
- Every commit since the newest final release is read for `!` or `BREAKING CHANGE:`, and the pull-request reading applies wherever no release tag points at HEAD. Governs: `03` APR-LIB-006.
- The release job attaches `CHANGELOG.md` beside the packages, as the plan's proof names the release "with its changelog", and the release's notes are the whole of `RELEASE-NOTES.md`. Governs: `05` APR-ENG-011.
- `pack-packages.ps1` sets the version through MinVer's `MinVerVersionOverride`, restoring in locked mode. Governs: `05` APR-ENG-011.
- The workflow's tag filter lets through some tags outside the rule, such as `v1.0.0-rc.0`, since a filter pattern cannot exclude a leading zero; the release step refuses any tag outside the rule before it packs. Governs: `05` APR-ENG-011.
- The sample host's test project references the sample host, the project it tests, beside the packages its row allows. Governs: `03` APR-LIB-001.
- No project of the sample host's solution grants its internals, which is read from its project files and sources, since `Themis.sln` does not build it. Governs: `05` APR-ENG-001.
- The library's own packages are no third-party packages for the allow-list; which project may take them is `03` APR-LIB-001's table, and no project under `tests/` takes one. Governs: `05` APR-ENG-009.
- `Directory.Packages.props` gives `Themis` and `Themis.Testing` the version `$(ThemisVersion)`, which the sample host's gate passes; `Themis.sln` takes neither as a package. Governs: `05` APR-ENG-002.
- `samples/nuget.config` maps the four package identifiers of the library to `../artifacts/packages/` alone and `*` to nuget.org alone. Governs: `05` APR-ENG-001.
- The sample host's `Program.cs` holds top-level statements, to which C# gives no namespace; its tests start the host through its generated `Program` class. Governs: `05` APR-ENG-001.
- The sample host's tests carry the trait `Category` `sample`, the fourth kind, and run by its gate alone. Governs: `05` APR-ENG-008.
- The sample host's gate restores from a package folder of its own under `tmp/`, from which the library's packages of earlier runs are removed first. Governs: `05` APR-ENG-001.
- The sample host's endpoints read the acting person from the header `X-Person` and answer a refusal with 409 and the library's code. Governs: `05` APR-ENG-001.
- The release notes give the clocks' agreement as `04` APR-OPS-003 states it, within one second, and the PostgreSQL floor as version 16; the version the integration tests run is not a deployment's duty. Governs: `04` APR-OPS-007.

## Open questions

None.

## Checks

- The commits: `main` at `843fc63` when `phase-05` was cut, then the commits of this phase to the report's own.
- Fast checks on every commit: the build of `Themis.sln` in Release with warnings as errors, the analysers, `dotnet format Themis.sln --verify-no-changes`, the unit tests and the contract tests: green.
- Integration tests: the classes this phase wrote or changed: green.
- The sample host's gate: the packages packed, `samples/Samples.sln` built from them, `dotnet format --verify-no-changes` and its 11 tests: green.
- Before the push: `run-gates.ps1 -Base origin/main -Head HEAD`. Every gate but the commit-message gate passed, the full-history secret scan and the sample host's gate among them; the commit-message gate found one body line of the release job's commit over 72 characters. That commit, not yet pushed, was reworded and the commits after it replayed onto it, their trees unchanged; then `run-gates.ps1` again on the report's own commit: every gate passed.
