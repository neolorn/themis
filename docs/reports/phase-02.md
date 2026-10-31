# Phase 2: Approval types

## Items built

Each test below was written before the code it proves. The tests of `Themis.Tests` and `Themis.Core.Tests` are in those projects. `a1f347a`, the result types and the enumerations of approval types made before this run, was checked against `03` APR-LIB-002 and `06` sections 1 and 2: no defect found.

**`02` APR-TYPE-001**: criteria 1 and 3; `IApprovalTypes.PublishAsync`, `ReadAsync` and `RetireAsync`, the tables `approvals.types` and `approvals.versions` and the migration `20261031043253_ApprovalTypes` (`internal sealed`).
- PublishTests: APR_TYPE_001_AC1_PublishingADefinitionTwiceMakesOneVersionAndChangingOneStepsModeMakesVersion2, APR_TYPE_001_AC3_TwoPublishesOfTwoDifferentDefinitionsOfOneKeyAtOnceMakeVersions1And2; ReadAsync_NoVersionNamed_AnswersTheLatestAndAVersionNamedAnswersIt, ReadAsync_NoSuchTypeOrVersion_AnswersTypeUnknown, PublishAsync_EachPublish_IsLoggedWithTheKeyAndTheVersion
- RetireTests: RetireAsync_PublishedType_IsRetiredUntilAPublishMakesItCurrentAgain, RetireAsync_ThenAChangedDefinitionPublished_MakesTheNextVersionCurrent, RetireAsync_RetiredType_StaysRetired, RetireAsync_NoSuchType_AnswersTypeUnknownAndWritesNothing, RetireAsync_TypesLockHeld_WaitsForIt, RetireAsync_Retirement_IsLoggedWithTheKeyAndTheVersion
- TypeSchemaTests: Versions_NumberBelowOne_IsRefusedByTheDatabase, Versions_HashOfAnotherLength_IsRefusedByTheDatabase, Versions_OfATypeNotRecorded_IsRefusedByTheDatabase, Versions_TwoOfOneNumber_AreRefusedByTheDatabase
- StartTests: APR_OVR_002_AC2_EveryTableTheLibraryUsesLivesInItsOwnSchema, now with the two tables

**`02` APR-TYPE-002**: every rule but the canonical form's size (below); `approvals.type.invalid` with `details.member`, the path of the first member outside its rule in the canonical order.
- PublishTests: APR_TYPE_002_AC1_EachMemberOutsideTheTypesRulesRefusesThePublishNamingItAndWritesNoVersion
- DefinitionRulesTests: Invalid_DefinitionWithinEveryRule_AnswersNone, Invalid_DefinitionOutsideARule_AnswersThePathOfTheFirstMemberOutsideIt, Invalid_DefinitionAtTheEdgeOfARule_AnswersNone, Invalid_TwoMembersOutsideTheirRules_AnswersTheFirstInTheCanonicalOrder
- TextRulesTests: IsName_Name_IsTrue, IsName_OutsideTheRule_IsFalse, IsName_Length_IsAtMost64, IsReference_Length_Is1To128ScalarValues, IsReference_ControlCharacterOrLoneSurrogate_IsFalse, IsText_LoneSurrogate_IsFalse, Length_Text_CountsScalarValues

**`02` APR-TYPE-003** (the choice of a policy and the fallback step): no criterion.
- PolicyChoiceTests: Choose_TwoPoliciesWhoseStartConditionsHold_AnswersTheFirst, Choose_PolicyWhoseEveryStepsConditionIsFalse_IsPassedOver, Choose_PolicyWhoseStartConditionIsFalse_IsPassedOver, Choose_StepWithoutACondition_Holds, Choose_NoPolicyHolds_AnswersNone, FallbackStep_Made_IsOneAnyOneStepWhoseSoleApproverAndFallbackIsTheTypesFallback

**`02` APR-TYPE-004**: criterion 1.
- PublishTests: APR_TYPE_004_AC1_EachMemberOutsideAStepsRulesRefusesThePublishNamingIt

**`02` APR-TYPE-005**: criteria 1 to 3.
- ConditionCheckTests: APR_TYPE_005_AC1_CompareAmountGreaterThan1000HoldsFor1000Point01AndNotFor1000, APR_TYPE_005_AC3_SubjectHasIsFalseOnARequestWithNoSubject; Holds_WholeNumbersCompared_AnswersTheComparison, Holds_WholeNumberFieldComparedWithADecimal_ComparesThemAsDecimals, Holds_TextCompared_ComparesOrdinally, Holds_FlagCompared_ComparesByEquality, Holds_OneOf_HoldsWhereTheValueIsListed, Holds_RequesterHas_HoldsWhereTheRequesterRolesHoldTheRole, Holds_AllOfAnyOfAndNegate_CombineTheirConditions, Holds_SnapshotWithoutTheField_ThrowsInvalidOperationException
- PublishTests: APR_TYPE_005_AC2_EachConditionOutsideItsRuleRefusesThePublishNamingIt

**`02` APR-TYPE-006** (the watchers' rule at publish): no criterion; held by the rule tests of APR-TYPE-002.

**`05` APR-ENG-005**: criteria 1 and 2.
- PublishTests: APR_ENG_005_AC1_TransactionAbortedWith40001IsRunAgainAtMostRetriesTimes, APR_ENG_005_AC2_PublishedDefinitionReadBackHoldsTheExactBytesWhoseHashItsVersionKeeps

**`05` APR-ENG-006**: criteria 1 and 2.
- CanonicalFormTests: APR_ENG_006_AC1_DefinitionsEqualButForTheScaleOfADecimalHaveOneCanonicalForm, APR_ENG_006_AC2_FixedDefinitionIsWrittenAsItsLiteral; Write_Decimal_IsWrittenWithoutTrailingZerosOrExponent, Write_MemberThatIsNoneAnEmptyListOrFalse_IsLeftOut, Read_CanonicalForm_GivesTheDefinitionWhoseFormItIs, Hash_CanonicalForm_IsItsSha256

**`04` APR-OPS-003** (the type's lock): no criterion; each publish and retirement takes it first in its transaction.
- TypeLockTests: Text_IsTheReferencesTypeLockText, Key_OfAKey_IsTheFirst8BytesOfTheHashReadAsASignedBigEndianInteger, Key_OfTwoKeys_Differs
- PublishTests: APR_TYPE_001_AC3_TwoPublishesOfTwoDifferentDefinitionsOfOneKeyAtOnceMakeVersions1And2; RetireTests: RetireAsync_TypesLockHeld_WaitsForIt, each against a lock the test takes on its own key

**`03` APR-LIB-002**, the types of approval types: `Result`, `Result<T>`, `Failure`, the enumerations of approval types, `ApprovalTypeDefinition`, `ProtectedField`, `PolicyDefinition`, `StepDefinition`, `Deadline`, `Condition` with its cases, `PublishedVersion`, `ApprovalTypeVersion` and `IApprovalTypes`, registered scoped by `AddThemis`.
- ResultTests: Success_Made_IsASuccessWithNoFailure, Fail_Made_IsNoSuccessAndCarriesItsFailure, SuccessOfAValue_Made_CarriesTheValue, FailOfAValue_Made_CarriesTheFailureAndNoValue, Fail_NoFailure_ThrowsArgumentNullException, Failure_MadeWithoutDetails_HasNone, Failure_MadeWithDetails_CarriesThem
- PrintTests: ApprovalTypeDefinition_ToString_WritesItsKeyAndNoPersonRoleOrValue, PolicyDefinition_ToString_WritesNoPersonRoleOrValue, StepDefinition_ToString_WritesItsNameAndNoPersonRoleOrValue, Condition_ToString_WritesNoRoleOrValue, ApprovalTypeVersion_ToString_WritesNoPersonRoleOrValue
- ApprovalTypeServiceTests: AddThemis_ApprovalTypes_IsAScopedService, ApprovalTypes_BeforeTheStartupChecks_ThrowsInvalidOperationException, ApprovalTypes_NoDefinitionOrNoOrEmptyKey_ThrowsArgumentException

## Items not built

- `02` APR-TYPE-002, the rule that the canonical form is at most 64 KiB: parked on the open question below. Criterion 1 is therefore not listed in `built-items.txt`; its test proves every other rule.
- `02` APR-TYPE-001 criteria 2 and 4, a request decided under its version and a retired type refusing a new request: the requests come in phase 3, with `approvals.type.retired`.
- `02` APR-TYPE-003 criteria 1 to 3: the choice is built and tested now under Method_Scenario names; the criteria name a request, phase 3.
- `02` APR-TYPE-006 criterion 1: the events and the decisions, phase 3.
- `03` APR-LIB-002 criteria 1 to 3: the public surface grows with each phase; criterion 3 names `ApprovalRequest` and `DecisionRequest`, phase 3.

## Criteria checked without a test

- `05` APR-ENG-008, tests written first: each item's tests were written before its code and run failing first.
- `05` APR-ENG-011, the commit messages of `phase-02` read.
- `04` APR-OPS-005: every integration test of this phase captures every log entry and asserts none holds a person reference, role or value it used.

## Awaiting the pipeline's run

- `phase-02`'s pull request into `main`: the pipeline's `gates` run on this commit, the report's own, is the gate of record.

## Analyser suppressions

None added.

## Resolved by rule

- A text value holding a lone surrogate is not of the kind text, and is refused where text is required. Governs: `02` APR-TYPE-005, `00` section 1.5. The rule counts Unicode scalar values, which a lone surrogate is not.
- A member that is none where the rule requires one is outside its rule. Governs: `02` APR-TYPE-002, `05` APR-ENG-003. A definition outside its rule is a failure, not a fault.
- A path names members as the canonical form spells them (camelCase, lists from `[0]`), and a condition too deep or of too many nodes is named by its offending node. Governs: `02` APR-TYPE-002, whose example is `policies[1].steps[0].fallback`.
- `Compare` mixes a whole number and a decimal, compared as decimals; `OneOf` takes values of the field's kind alone. Governs: `02` APR-TYPE-005, `05` APR-ENG-006.
- `ReadAsync` and `RetireAsync` of a key that is not a name answer `approvals.type.unknown`, a null or empty key being a fault. Governs: `02` APR-TYPE-001, `05` APR-ENG-003. No such type can exist.
- Retiring a retired type succeeds and leaves it retired. Governs: `02` APR-TYPE-001, `06` section 1, which has no code for it.
- A publish of a retired type's latest definition makes no version and makes the type current again. Governs: `02` APR-TYPE-001, "a later `PublishAsync` of the type makes it current again".

## Open questions

- `02` APR-TYPE-002, a canonical form over 64 KiB: `approvals.type.invalid` carries `details.member`, the path of the first member outside its rule, and the size is no member's. Readings: (a) `member` is the empty path, the definition itself; (b) the failure carries no `member`; (c) `member` is a fixed name, such as `definition`. The smallest fix for each: (a) a check after the member rules answering `""`; (b) the same check answering no details, with `06` section 1 noting it; (c) the same check answering the name, which `06` section 1 would give. Tier 2; the rule parks until it is settled.

## Checks

- The commit: the report's own commit, the head of `phase-02`, on `c88543f`, the branch cut from `main` at `379aa8b`.
- Fast checks on every commit: the build of `Themis.sln` in Release with warnings as errors, the analysers, `dotnet format Themis.sln --verify-no-changes`, the unit tests and the contract tests: green.
- Integration tests written or touched: PublishTests, RetireTests, TypeSchemaTests and StartTests, and `Themis.Cli.Tests` with `gate-migrations.ps1` for the migration: green.
- Before the push: every unit test, the commit-message gate over `origin/main..phase-02`, the test-names gate and the full-history secret scan: green.