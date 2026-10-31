# Phase 0: Repository and gates

## Items built

Every test below is in `tests/Themis.Repository.Tests`, written before the code or script it proves.

**`05` APR-ENG-001** (all but the sample host and `RELEASE-NOTES.md`): criterion 1.
- RepositoryLayoutTests: APR_ENG_001_AC1_ProjectsFollowTheLayout, ProjectFoldersFollowTheLayout, ProjectOutsideTheLayoutIsFound, ProjectOfNoPlaceIsFound, PackageWithoutItsProjectIsFound, CommandLinePackageIsNotYetRequired, SourceProjectWithoutItsTestProjectIsFound, RepositoryWithoutItsRepositoryTestsIsFound, SolutionOtherThanTheTwoIsFound, SampleSolutionIsAccepted, ProjectTheSolutionDoesNotListIsFound, ProjectTheSolutionListsOutsideTheLayoutIsFound, EnginesFoldersAreAccepted, FolderOutsideTheLayoutIsFound, FolderInsideAnEngineFolderIsFound, FolderOfATestProjectIsFound, FileWithoutANamespaceIsFound, NamespaceThatDoesNotMatchItsFolderIsFound, PublicTypeOutsideTheProjectsRootIsFound, TwoPublicTypesInOneFileAreFound, TwoPublicTypesInABlockScopedNamespaceAreFound, TypeWithItsNestedCasesIsOneTopLevelType, RootOfTheListedFilesIsAccepted, RootFileOutsideTheListIsFound, ListedRootFileMissingIsFound, IgnoreFileListsThePackedPackages, IgnoreFileWithoutThePackedPackagesIsFound, PackageSourcesFollowTheRule, PackageSourcesOutsideTheRuleAreFound, PackageSourceMappingOutsideTheRuleIsFound; RepositoryFixture_AsMade_KeepsEveryRule
- RootFileTests: APR_ENG_001_AC1_RootHoldsTheFilesTheLayoutLists
- GrantTests: APR_ENG_001_AC1_GrantsFollowTheRule
- AssemblyContractProbeTests: APR_ENG_001_AC1_GrantOfEveryFormIsReadFromTheBuiltAssembly
- ScannerConfigurationTests: APR_ENG_001_AC1_ScannerConfigurationAddsNoEntry, DefaultExtendedAloneIsAccepted, EntryAddedToTheConfigurationIsFound, ConfigurationThatDoesNotExtendTheDefaultAloneIsFound
- SecretScanTests: FetchSecretScanner_ChangedScanner_IsReplacedByTheArchives, FetchSecretScanner_ArchiveOtherThanThePublished_Fails

**`05` APR-ENG-002**: criteria 1 and 2.
- SettingRuleTests: APR_ENG_002_AC1_ProjectsAndSharedFilesSetOnlyWhatTheRuleAllows, ProjectSettingWhatOnlyTheSharedFilesSetIsFound, ProjectPropertyThatTurnsAWarningOffIsFound, PropertyThatTurnsAWarningOffInATargetIsFound, ReferenceThatTurnsAWarningOffIsFound, ItemThatTurnsAWarningOffIsFound, SharedBuildSettingsTurningAWarningOffIsFound, SharedBuildSettingsOtherThanTheRulesAreFound, PackageVersionsSettingAnotherPropertyIsFound, SharedBuildFileBesideTheRootsIsFound, AnalyserConfigurationBesideTheRootEditorConfigIsFound, RootEditorConfigTmpAndBuildOutputAreLeftOut, SdkOtherThanTheRulesIsFound, RepositoryWithoutGlobalJsonIsFound; SettingViolations_ProjectWithoutItsLockfile_IsFound
- BuildSettingsTests: APR_ENG_002_AC2_AWarningFailsTheBuild, AnMSBuildWarningFailsTheBuild, ALockedRestoreThatDiffersFromTheLockfileFails

**`05` APR-ENG-004**: criteria 1 and 2.
- AssemblyContractTests: APR_ENG_004_AC1_EveryPublicClassIsSealedOrAbstract, EveryPublicAsynchronousMethodTakesACancellationToken, APR_ENG_004_AC2_NoMemberIsLookedUpByName; AssemblySurface_ShippedAssemblies_AreEachPackagesBuiltAssembly
- AssemblyContractProbeTests: APR_ENG_004_AC1_PublicClassNeitherSealedNorAbstractIsFound, PublicAsynchronousMethodWithoutATokenIsFound, APR_ENG_004_AC2_LookupOfMembersByNameIsFound, LookupsOfGeneratedCodeAreLeftOut
- PublicSurfaceBuildTests: APR_ENG_004_AC1_PublicMemberWithoutDocumentationFailsTheBuild

**`05` APR-ENG-007**: criterion 1, the public type whose name holds the library's name.
- AssemblyContractTests: APR_ENG_007_AC1_NoPublicTypeNameHoldsTheLibrarysName
- AssemblyContractProbeTests: APR_ENG_007_AC1_PublicTypeNamedAfterTheLibraryIsFound

**`05` APR-ENG-008**: criteria 1, 2 and 3.
- PackageAllowListTests: APR_ENG_008_AC1_NoMockingPackageIsReferenced, MockingPackageReferencedIsFound, MockingPackageTakenThroughAnotherIsFound
- TestNameGateTests: APR_ENG_008_AC2_EveryListedCriterionWithItsTestPasses, ListedCriterionWithoutItsTestFails; TestNameGate_NoCriterionListed_Fails
- TestsGateTests: APR_ENG_008_AC3_TestsOfOneKindEachRunAndPass, TestWithoutExactlyOneKindFailsTheTestsGate; TestsGate_NoTestProject_Fails

**`05` APR-ENG-009**: criterion 1.
- PackageAllowListTests: APR_ENG_009_AC1_PackagesAndToolsAreWithinTheTable, PackageOutsideTheTableIsFound, PackageVersionOutsideTheTableIsFound, PackageTakenByAReferenceOutsideTheProjectFileIsFound, ToolOtherThanDotnetEfIsFound, ToolManifestWithoutDotnetEfIsFound, DotnetEfAtAVersionOtherThanEntityFrameworkCoresIsFound

**`05` APR-ENG-010**: criterion 1.
- BuildSettingsTests: APR_ENG_010_AC1_ARestoreResolvingAPackageWithAPublishedAdvisoryFailsTheBuild

**`05` APR-ENG-011** (all but the release step): criterion 1, and criterion 2 without the release commit's exemption; the packages' description, authors, address and exact-version dependency.
- CommitMessageGateTests: APR_ENG_011_AC1_CommitsWithinTheRulePass, NameInItsOwnSpellingPasses, CommitMessageOutsideTheRuleFails, BehaviourChangingCommitWithoutAChangelogLineFails, ChangelogLineOutsideUnreleasedFails, ChangelogLineUnderChangedPassesOnceAReleaseIsTagged, ChangelogLineUnderChangedFailsAfterATagThatIsNoRelease, MergeCommitsThePlatformWritesAreNotChecked, LocalMergeCommitOutsideTheRuleFails, APR_ENG_011_AC2_CommitChangingAReleasedFileFails, CommitDeletingAReleasedFileFails, CommitMovingAReleasedFileDeletesItAndFails, NewProjectsPublicApiFileAndAnUnshippedChangePass; PreviousReleaseTag_TagsOfOtherShapes_AreLeftOut, PreviousReleaseTag_FirstRelease_IsNone
- PackTests: Pack_EachPackage_CarriesItsDescriptionAuthorsAndAddressAlone
- PackageManifestRuleTests: ManifestViolations_PackageAsTheRuleGivesIt_HasNone, ManifestViolations_AnotherPackageProperty_IsFound, ManifestViolations_PropertyNotAsTheRuleGivesIt_IsFound, PackageTable_Descriptions_AreTheCellsWithoutMarkupOrReferences

**`05` APR-ENG-012** (all but the migrations, release compatibility and sample host gates): the gates as scripts under `build/gates/`, `run-gates.ps1`, and the `gates` job of `.github/workflows/gates.yml`.
- RunGatesTests: RunGates_WorkingTreeOfHead_RunsEachGate, RunGates_FailingGate_FailsTheRun, RunGates_HeadOtherThanTheCheckedOutCommit_Refuses, RunGates_WorkingTreeOtherThanHeads_Refuses, RunGates_UntrackedFileOfAnExcludedPath_Runs, RunGates_NoGateScript_Fails

**`03` APR-LIB-001** (all but the sample host's row): criteria 1 and 2.
- ProjectReferenceTests: APR_LIB_001_AC1_EachProjectReferencesOnlyWhatItsRowAllows, ProjectReferenceOutsideTheTableIsFound, PackageReferenceOutsideTheTableIsFound, EnginesPackageWithinItsRowIsAccepted, TestProjectReferenceOutsideItsRowIsFound, TestProjectTakingTheTestingPackageIsAccepted, TestProjectPackageOutsideTheTestPackagesIsFound, ShippedProjectWithoutItsBuildOnlyReferencesIsFound, EngineWithoutTheDesignPackageIsFound, DesignPackageOutsideTheEngineIsFound, FrameworkReferenceIsFound, PackageReferenceInTheSharedBuildSettingsIsFound, ItemInATargetOtherThanTheExactVersionUpdateIsFound, GlobalPackageReferenceIsFound
- PackTests: APR_LIB_001_AC2_EachPackageDependsOnTheLibrarysOtherPackagesAtTheExactVersionPacked
- PackageManifestRuleTests: APR_LIB_001_AC2_DependencyOnALibraryPackageNotAtTheExactVersionPackedIsFound, DependencyAtTheExactVersionPackedIsAccepted, DependencyOnALibraryPackageNotPackedWithItIsFound

**`03` APR-LIB-002** (the projects `Themis.Core`, `Themis` and `Themis.Testing` and their empty surface files): criteria 1 and 2.
- PublicSurfaceBuildTests: APR_LIB_002_AC1_AddingAPublicTypeWithoutItsPublicApiFileFailsTheBuild, RemovingAPublicTypeWithoutItsPublicApiFileFailsTheBuild, PublicSurfaceGateFailsOnAPublicTypeNoFileLists; PublicSurfaceGate_NoProjectUnderSrc_Fails
- AssemblyContractTests: APR_LIB_002_AC2_EveryPublicTypeIsOneTheTablesGive, PublicApiFilesListExactlyThePublicTypes
- AssemblyContractProbeTests: APR_LIB_002_AC2_PublicTypeTheFilesDoNotListIsFound, PublicTypeNoTableGivesIsFound

**`07` APR-THR-003**: criteria 1 and 2.
- SecretScanTests: APR_THR_003_AC1_ACommitCarryingACredentialShapedStringFailsTheScan, ACredentialShapedStringRemovedLaterStillFailsTheScan, APR_THR_003_AC2_ALineCarryingTheInlineAllowMarkerFailsTheScan, ATrackedGitleaksIgnoreFailsTheScanWhateverItLists, AGitleaksIgnoreAtTheScannedRootFailsTheScanWhateverItLists, AFindingAGitleaksIgnoreOfTheWorkingDirectoryListsFailsTheScan; SecretScan_HistoryWithoutSecrets_Passes

**`00` APR-OVR-001**: criterion 1, checked in review (below).

## Items not built

- `05` APR-ENG-001 criterion 2, the sample host, its gate and `RELEASE-NOTES.md`: phase 5.
- `05` APR-ENG-012's migrations gate: phase 1, with `themis migrate`. The release compatibility and sample host gates: phase 5.
- `05` APR-ENG-011 criterion 2's release commit exemption, criteria 3 and 4, and the release job: phase 5.
- `05` APR-ENG-007 criterion 1 for codes, configuration keys, spellings and metric instruments: the phases that add them; phase 0 has none.
- `03` APR-LIB-001's sample host row: phase 5.
- `03` APR-LIB-002: `Themis.Cli`'s project comes in phase 1 with `themis migrate`; each type comes with the first item that uses it; criterion 3 with `ApprovalRequest` and `DecisionRequest`, phase 3. The type table the contract test holds already lists every type of APR-LIB-002 and APR-LIB-007.

## Criteria checked without a test

- `00` APR-OVR-001 criterion 1: every tracked file outside `docs/` read for a host product, company or application; none is named. The one address, `https://github.com/neolorn/themis`, is the repository's own.
- `05` APR-ENG-001, the fetch script's SHA-256 values: each of the six (`linux`, `windows` and `darwin`, `x64` and `arm64`) compared with `gitleaks_8.30.1_checksums.txt` of the gitleaks 8.30.1 release.
- `05` APR-ENG-001, the actions: `actions/checkout` v7.0.1 (`3d3c42e5aac5ba805825da76410c181273ba90b1`) and `actions/setup-dotnet` v6.0.0 (`a98b56852c35b8e3190ac28c8c2271da59106c68`), each the latest release on its releases page when the workflow was written, pinned by that release's commit.
- `05` APR-ENG-008, tests written first: each rule's tests were written with its rule, before the gate script that runs them; the exact-version target's test, `APR_LIB_001_AC2_EachPackageDependsOnTheLibrarysOtherPackagesAtTheExactVersionPacked`, was run failing on the three dependencies before the target was added.
- `05` APR-ENG-011, the imperative description and the body only where the diff cannot explain itself: each commit message of `phase-00` read.

## Awaiting the pipeline's run

- `05` APR-ENG-012 criterion 1, each gate running on the pull request and a failing one blocking the merge: needs `main` pushed, its protection requiring `gates`, and the pull request. Not run (Open question 1).
- `05` APR-ENG-012 criterion 2, each script giving locally the result the pipeline gives: `run-gates.ps1` passed every gate locally on Windows; the pipeline's run on `ubuntu-24.04` has not happened.

## Analyser suppressions

All in the root `.editorconfig`, each the one `05` APR-ENG-002 lists, its reason on the line above it:
- CA1303 for `*.cs`: exception messages are for developers and never shown to people.
- CA1812 under `src/`, `tests/` and `samples/`: an internal class the dependency injection container makes is made, though no code calls `new` on it.
- CA1034 under `src/Themis.Core/`: a case of a type with cases is a nested record reached through it (`03` APR-LIB-002).
- CA1030 for `src/Themis.Core/IAlertSink.cs`: an alert is an awaited call with a token, not an event.
- CA1032 for `src/Themis.Core/ApprovalsStartupException.cs`: it always carries a code and details, which the standard constructors would leave out.
- CA1861 under `src/Themis/Migrations/`: EF Core's generator passes literal column arrays to the migration operations.
- CA1707 and CA1515 under `tests/` and `samples/*.Tests/`: a test's name carries its criterion's identifier (APR-ENG-008); xUnit.net runs only public test classes.
- CS1591 and CA2007 under `tests/` and `samples/*.Tests/`: a test states no contract of its own; xUnit.net's own analyser refuses the `ConfigureAwait(false)` CA2007 asks for.

No suppression in code.

## Resolved by rule

- `build/gates/gate-commit-messages.ps1`, a move of `PublicAPI.Shipped.txt` or `build/gates/release-baseline.json`: the gate reads changes with renames off, so a move is the deletion of the file at its path. Governs: `05` APR-ENG-011 criterion 2. A commit that leaves no file at an existing released file's path deletes it.
- `build/gates/gate-commit-messages.ps1`, the footer: `BREAKING CHANGE:` lines are allowed beside `Implements:` lines. Governs: `05` APR-ENG-011. The value names `BREAKING CHANGE:` as the mark of a break, and in Conventional Commits 1.0.0 it is a footer.
- `tests/Themis.Repository.Tests/ReflectionRules.cs`, `Delegate.CreateDelegate`: counted as a lookup by name. Governs: `05` APR-ENG-004 criterion 2. Making a delegate from a method's name is a lookup of a member by name.

## Open questions

1. **The push of `main` waits on the owner** (not a question of the specification). `git push origin main` is refused by the machine's permission settings, a deny rule matching `git push *main`, which is the owner's; it was not worked around, and the owner pushes `main` himself. Waiting on it: the default branch taking effect, the protection of `main` (the `gates` check required, force pushes and deletion refused), the push of `phase-00`, the pull request into `main`, and the pipeline's `gates` run. Settings set: dependency vulnerability alerting on, secret scanning on, push protection on, squash merges off; the API reports the default branch as `main`.
2. **Tier 2, answered: the docs commit's message was outside `05` APR-ENG-011.** Item: `05` APR-ENG-011 criterion 1, `05` APR-ENG-012. The docs commit `054d80c` had a body line of 83 characters, over the rule's 72; the first release tag's run, which checks the whole history, would have failed on it. Answered by the lead's message: reading (a), the rule holds for every commit, the first included. The lead reworded the unpushed docs commit as `74b2548` "docs: specify the approval library", with no body and the same tree, and `phase-00` was rebased onto it. The commit-message gate now passes over the whole history. Nothing was parked.

## Checks

- The commit: the report's own commit, the head of `phase-00`, on `87e9dcb`, itself on `main` at `74b2548`.
- Fast checks on every commit: the build of `Themis.sln` in Release with warnings as errors, the analysers, `dotnet format Themis.sln --verify-no-changes`, and the unit tests: green.
- Before the push: every unit test, the commit-message gate over `main..HEAD` and over the whole history, the full-history secret scan, and every integration and contract test: green. `run-gates.ps1 -Base main -Head HEAD` on the report's own commit: every gate passed (build, commit messages, dependency allow-list, format, project references, public surface, restore, secret scan, test names, tests).
- The gate of record, the pipeline's `gates` run on the report's commit: not run (Open question 1).
