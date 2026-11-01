# Phase 9: Endpoints for an approver

## Items built

The tests of `Themis.Endpoints` are in `tests/Themis.Endpoints.Tests`. Each integration test runs an ASP.NET Core host on a test server, on a database of its own in the `postgres:16.15` container of its class, and asserts no log entry holds a value it used. The sample host's tests are in `samples/Observatory.Tests`.

**`03` APR-LIB-004**: criteria 1 to 8. The package `Themis.Endpoints` holds:
- `ICurrentPerson` and `ApprovalsEndpointRouteBuilderExtensions.MapThemisApprovals`;
- the six routes;
- the person read through the seam, bounded by the HTTP request's cancellation alone;
- the statuses of `06` section 1's column "Over HTTP";
- the endpoints' own JSON: camel case, identifiers as strings, enumerations by their spellings, cases under `case`;
- the party check;
- the check of `ICurrentPerson` at mapping.

`Themis` adds the internal query `RequestWatchers`, registered by `AddThemis`, and grants `Themis.Endpoints` its internals.
- MappingTests (unit): APR_LIB_004_AC8_MappingWithoutCurrentPersonRegisteredThrowsSeamMissingNamingCurrentPerson; MapThemisApprovals_CurrentPersonRegistered_AnswersTheGroupUnderThePrefix
- StatusTests (unit): APR_LIB_004_AC4_EachCodeAnswersTheStatusSection1GivesIt, every code of the column, a startup code and an unknown one; APR_LIB_004_AC4_OnlyAStatusUnder500CarriesTheCodeAndDetails
- EndpointsTests (integration):
  - APR_LIB_004_AC1_CallWithNoPersonAnswers401OnEveryRoute
  - APR_LIB_004_AC2_PendingListsThePersonsOwnRequestsAndThoseTheyHoldThroughADelegation
  - APR_LIB_004_AC2_DecisionIsThePersonsOwnOrForTheLenderTheyHoldADelegationFrom
  - APR_LIB_004_AC2_RequestReadAnswersTheRecordToEachPartyInTheLibrarysJson
  - APR_LIB_004_AC2_DelegationIsCreatedFromThePersonListedAndEndedWithThePersonItsActor
  - APR_LIB_004_AC3_RequestOfWhichThePersonIsNoPartyAnswers404AsAnUnknownOneDoes
  - APR_LIB_004_AC4_FailuresAnswerTheirStatusWithCodeAndDetailsAndTheDirectorysFailures503And500WithNone
  - APR_LIB_004_AC6_EachRouteAnswersItsSuccessStatusAndACreatedDelegationNoLocation
  - APR_LIB_004_AC7_MalformedInputAnswers400WithNoCodeABodyNotJson415AndAPersonOutsideItsRuleOrASeamThatThrows500
- RequestWatchersTests (integration, `tests/Themis.Tests`): NamesAsync_PersonAnEventNamesInNotify_AnswersTrueAndAnyOtherFalse
- ObservatoryTests (sample): APR_LIB_004_AC5_SampleHostServesTheLibrarysEndpointsForThePersonOfItsHeader
- SampleHostEndpointsTests (contract, `tests/Themis.Repository.Tests`): APR_LIB_004_AC5_SampleHostMapsTheEndpointsWithItsPersonSeamAndItsGateTestsCallThem, since the test-name gate lists the tests of `tests/` alone

The sample host now:
- maps the endpoints under `/approvals`, with `HeaderPerson`, its `ICurrentPerson` reading `X-Person`;
- keeps its own create, cancel and complete endpoints, which read the person through the same seam;
- no longer has its own decision and pending endpoints.

Its tests decide and read the pending list through the library's endpoints. A decision by a person not assigned now answers 403 with `approvals.decision.notassigned`, where the sample's own endpoint answered 409.

The repository's tests:
- `Packages` gains `Themis.Endpoints`.
- `ReferenceRules` allows it `Themis` and ASP.NET Core's shared framework, and lets `Themis` grant it its internals.
- `PublicTypeTable` gives its two public types.
- `Themis.Repository.Tests` references it.
- The sample host's package mapping holds it.
- The cases added: a project reference, a package and a framework outside its row; ASP.NET Core's framework in a test project; and the grant rule (GrantTests APR_ENG_001_AC1_OnlyThemisGrantsThemisEndpointsItsInternals).

Other changes:
- `Directory.Packages.props` gives `Themis.Endpoints` at `$(ThemisVersion)`.
- `samples/nuget.config` maps it to `artifacts/packages/`.
- `pack-packages.ps1` packs it with no change, since it packs every project of `src/`.
- `PackageRules` accepts the element `frameworkReferences`, which the SDK writes for the framework reference, on `Themis.Endpoints` alone, as it accepts `packageTypes` on `Themis.Cli` alone (PackageManifestRuleTests ManifestViolations_FrameworkReference_IsAcceptedOnTheEndpointsAlone).
- The commit-message gate allows the scope `endpoints`, with a case in CommitMessageGateTests.
- `build/gates/built-items.txt` gains `APR-LIB-004 AC1` to `APR-LIB-004 AC8`.
- PackageManifestRuleTests now passes.

## Items not built

None of phase 9.

## Criteria checked without a test

- `05` APR-ENG-008, tests written first: met, with one exception. The tests were written before the code, but they could not compile until `Themis.Endpoints`, its types and `RequestWatchers` existed, so none of them was run and seen to fail first. Their first run found four faults, all in the tests' own expectations, not in the code:
  - a step's status is `current`;
  - an assignment's origin is `approver`;
  - two decisions at one instant came back in an order the test did not fix;
  - a person deciding twice is refused with `notassigned`, not `duplicate`.
- The repository's tests were changed before `src/Themis.Endpoints` existed, and failed until it did.

## Analyser suppressions

None added.

## Resolved by the lead

- `Themis` grants `Themis.Endpoints` its internals for one internal query, whether a person is named in the `Notify` of a request's events, its watcher check. No public member changes (the lead's answer on phase 9; `phase-06-docs` at `8c1b5cc`). Governs: `03` APR-LIB-004, `05` APR-ENG-001.
- A delegate is a party only through a delegation that covers the request's type and is live at the call. Governs: `03` APR-LIB-004.
- The caller's malformed input answers 400 with no code and no details: a body that does not parse, a member missing, or a value the library refuses with `ArgumentException`. A person reference from `ICurrentPerson` outside its rule answers 500. Governs: `03` APR-LIB-004.
- Success statuses:
  - 200 for each `GET` and for a decision;
  - 201 for a delegation created, with its identifier as a string and no `Location`;
  - 204 for an ending.
  Governs: `03` APR-LIB-004.
- `MapThemisApprovals` checks `ICurrentPerson` as it maps the endpoints. Governs: `03` APR-LIB-004, APR-LIB-005.
- Statuses: `approvals.directory.unavailable` answers 503 with no details, and `approvals.directory.toomany`, the erasure and retention codes and any other 500. Governs: `03` APR-LIB-004, `06` section 1.

## Resolved by rule

- A request of which the person is no party answers 404 with `approvals.request.unknown`, as an unknown request does, so the answer does not tell the two apart. Governs: `03` APR-LIB-004.
- The checks run in this order: the person (401, or 500 where the seam throws or answers outside its rule); then a JSON body's type (415, on the two routes that take a body); then the call.
- A malformed identifier in a route, or a `size` that is not a whole number, is malformed input and answers 400. The routes take `{id}` with no constraint, so it is not answered 404 by routing. Governs: `03` APR-LIB-004.
- A body's members beyond its own, such as `from` or `createdBy`, are ignored. The endpoint sets both to the person. Governs: `03` APR-LIB-004.
- The JSON is the endpoints' own options, so the host's JSON settings neither change it nor are changed by it:
  - JSON's web defaults;
  - enumerations by their `JsonStringEnumMemberName` spellings, numbers refused;
  - the four identifiers as their UUIDs' strings;
  - `Condition`, `ApproverReference` and `FieldValue` written with their case under `case` in camel case, as the canonical form writes them;
  - members that are none are written as `null`.
  Governs: `03` APR-LIB-004, `06` section 2.
- Problem details are written through ASP.NET Core's problem results, so a host's `IProblemDetailsService` applies. A refusal under 500 carries `code` and `details`. The endpoints' own 401, 415 and 400 for malformed input carry neither, nor does a 500 or 503. Governs: `03` APR-LIB-004.
- A throw of `ICurrentPerson`, or a person reference outside its rule, is logged at the error level, under the category `Themis.Endpoints`, naming the exception's type alone and no message, so no value of the host's reaches the log. Governs: `03` APR-LIB-004, `04` APR-OPS-005.
- A delegate party is found through `IDelegations.ListAsync` for the person, and the instant is the host's `TimeProvider`. Governs: `03` APR-LIB-004.

## Open questions

None.

## Defects found in the documents

- `05` APR-ENG-011 gives the properties a package carries, and leaves every other unset; it does not name the `frameworkReferences` element the SDK writes for `Themis.Endpoints`' framework reference, which APR-LIB-001 allows it. The repository's test accepts it on that package alone. The item parks.

## Checks

- The commits: the merge of `phase-06-docs` at `8c1b5cc` (`134c01d`), `15d910a`, and this report's own, on `phase-06` after `6660606`.
- Fast checks, all green:
  - the build of `Themis.sln` in Release with warnings as errors;
  - the analysers;
  - `dotnet format Themis.sln --verify-no-changes`;
  - the unit tests and the contract tests.
- The whole of each test project, in the gates' run:
  - `Themis.Endpoints.Tests`, 44 of 44;
  - `Themis.Tests`, 469 of 469;
  - `Themis.Repository.Tests`, 470 of 470;
  - `Themis.Cli.Tests`, 34 of 34;
  - `Themis.Core.Tests`, 23 of 23;
  - `Themis.Testing.Tests`, 10 of 10;
  - the sample host's tests, 13 of 13.
- Before the push: `run-gates.ps1 -Base origin/main -Head HEAD`: every gate passed. Its first run failed three gates:
  - `gate-commit-messages`, on one body line of the feature's commit over 72 characters;
  - `gate-test-names`, on `APR-LIB-004 AC5`, whose sample test the gate does not list;
  - `gate-tests`, on the package's `frameworkReferences` element.
  The commit, not pushed, was amended with the contract test of AC5, the package rule and the shorter line, and every gate then passed.
