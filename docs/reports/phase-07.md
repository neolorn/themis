# Phase 7: A number of approvals out of a list

## Items built

The tests are in `tests/Themis.Tests`. Each integration test runs on a database of its own in the `postgres:16.15` container of its class and asserts no log entry holds a value it used.

**`02` APR-TYPE-004**: criteria 2 to 6; `ApprovalMode.AtLeast` (`atleast`); `int? Required` on `StepDefinition`, last in its canonical order, and on `StepRecord`, after `Mode`; the publish rule of `Required`, refused at the path `policies[i].steps[j].required`; the count of an `AtLeast` step per distinct person; a step starting with fewer assignments than its `Required` treated as one with none; the migration `20261101010448_AtLeast`, which adds `steps.required`, the spelling `atleast` to `ck_steps_mode`, and `ck_steps_required`.
- PublishTests (integration): APR_TYPE_004_AC2_PublishRefusesAtLeastWithoutRequiredRequiredWithAnotherModeRequiredOutside2To16AndOneAboveTheDistinctPersonsNamed
- DefinitionRulesTests (unit): the same rules, seven cases, and four at the edge (`Required` 2, 16 of a permission, the distinct persons named, and above the persons named beside a permission)
- AtLeastTests (integration): APR_TYPE_004_AC3_StepNeedingTwoOfFiveCompletesAtTheSecondApprovalByTwoPersons, APR_TYPE_004_AC3_OnePersonApprovingTwiceThroughAnAssignmentSkipSatisfiedAndADelegationCountsOnce, APR_TYPE_004_AC4_RejectionInAnAtLeastStepRejectsTheRequest, APR_TYPE_004_AC5_AtLeastStepStartingWithFewerAssignmentsThanItsRequiredGoesToItsFallbackWhoseApprovalCompletesIt, APR_TYPE_004_AC5_AtLeastStepStartingWithFewerAssignmentsThanItsRequiredAndNoFallbackLeftRaisesRequestUnassignable
- CanonicalFormTests (unit): APR_TYPE_004_AC6_VersionPublishedBeforeAtLeastExistedKeepsItsHash, the fixed definition's form read and written again, its SHA-256 the first release's

`build/gates/built-items.txt` gains `APR-TYPE-004 AC2` to `APR-TYPE-004 AC6`.

## Items not built

None of phase 7. `RemindBefore` and its rules are phase 8.

## Criteria checked without a test

- `05` APR-ENG-008, tests written first: met, with one exception. The tests were written before the code, but they could not compile until `AtLeast` and `Required` existed, so none of them was run and seen to fail first. The first release's hash in the AC6 test was computed from the literal form before any change.

## Awaiting phases 8 and 9

- ReferenceTests APR_ENG_007_AC1_EveryEnumerationIsSpeltAsSection2SpellsIt: `Reminded` alone now (phase 8).
- ObservabilityTests AddThemis_Meter_PublishesTheReferencesInstrumentsUnderTheMeterThemis: `approvals.reminders` (phase 8).
- PackageManifestRuleTests (`tests/Themis.Repository.Tests`): `Themis.Endpoints` (phase 9).

## Analyser suppressions

None added.

## Resolved by the lead

- An assignment `Skip` satisfied counts as one approval of its person (T-025). Governs: `02` APR-TYPE-004, `01` APR-REQ-006.

## Resolved by rule

- `Required` is checked after the step's deadline, the last member of the step in the canonical order, and every refusal of it names `required`, whether the mode or the number is wrong. Governs: `02` APR-TYPE-004.
- An approval is counted as its decider where a decision of the step itself satisfied the assignment, and as the assignment's person where an earlier step's decision did (`Skip`). A person deciding for another, through a delegation, counts once with their own assignment. Governs: `02` APR-TYPE-004.
- A fallback assignment of an `AtLeast` step completes it at its first approval, whether the step started with too few assignments or an escalation assigned it. Governs: `02` APR-TYPE-004, `01` APR-REQ-007, APR-REQ-009.
- `steps.required` is a nullable integer, and `ck_steps_required` holds it to 2 to 16 for `atleast` and to none for any other mode. Governs: `02` APR-TYPE-004.

## Open questions

None.

## Checks

- The commits: `08f1cec`, and this report's own, on `phase-06` after `96966e1`.
- Fast checks: the build of `Themis.sln` in Release with warnings as errors, the analysers, `dotnet format Themis.sln --verify-no-changes`, the unit tests and the contract tests: green but for the three tests above.
- Integration tests: the whole of `Themis.Tests`, 454 tests, every one green but the two above; `Themis.Cli.Tests`, 34 of 34; `Themis.Core.Tests`, 23 of 23.
- Before the push: `run-gates.ps1 -Base origin/main -Head HEAD`: every gate passed but `gate-tests`, on the three tests awaiting phases 8 and 9, and `gate-commit-messages`, on one body line of the feature's commit over 72 characters; that commit, not pushed, was reworded and the report replayed onto it, its tree unchanged, and the commit-message gate then passed.
