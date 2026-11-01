# Phase 8: Reminders before a deadline

## Items built

The tests are in `tests/Themis.Tests`. Each integration test runs on a database of its own in the `postgres:16.15` container of its class and asserts no log entry holds a value it used.

**`01` APR-REQ-009**: criteria 5 to 8; `Deadline.RemindBefore`, empty by default and not `required`, last in the deadline's canonical order and left out where empty; its publish rule, refused at `policies[i].steps[j].deadline.remindBefore`, or at the reminder by its index; `EventKind.Reminded` (`reminded`); the reminders the `deadlines` job sends before its actions; the counter `approvals.reminders`, tagged `type`, in the unit `{reminder}`; the migration `20261101011642_Reminders`, which adds `steps.reminders_sent` and `steps.remind_at`, the index `ix_steps_remind`, and the spelling `reminded` to `ck_events_kind`.
- PublishTests (integration): APR_REQ_009_AC5_PublishRefusesAReminderUnderAnHourOneNotShorterThanTheDeadlineAFourthAndTwoAlike
- DefinitionRulesTests (unit): the same rules, six cases (under an hour, as long as the deadline, no whole seconds, a fourth, two alike, none), and two at the edge (three, the shortest an hour; one a second shorter than the deadline)
- ReminderTests (integration): APR_REQ_009_AC6_EachReminderFiresOnceAtItsTimeNamingTheStepsOpenAssignmentsEachWithItsDelegate, APR_REQ_009_AC7_NoReminderFiresAfterTheStepCloses, APR_REQ_009_AC8_RestartSendsNoReminderTwiceAndAfterAGapTheLatestDueOnceWhileTheDeadlineIsAhead, APR_REQ_009_AC8_LateRunPastTheDeadlineDropsTheRemindersAndRunsTheDeadlinesActionAlone; RunAsync_Deadlines_CountsEachReminderSentTaggedWithItsType
- CanonicalFormTests (unit): APR_TYPE_004_AC6_VersionPublishedBeforeAtLeastExistedKeepsItsHash now holds for `RemindBefore` too; Read_CanonicalForm_GivesTheDefinitionWhoseFormItIs reads an absent `RemindBefore` as empty

`build/gates/built-items.txt` gains `APR-REQ-009 AC5` to `APR-REQ-009 AC8`. ReferenceTests APR_ENG_007_AC1 and the meter's test in ObservabilityTests now pass.

## Items not built

None of phase 8.

## Criteria checked without a test

- `05` APR-ENG-008, tests written first: met, with one exception. The tests were written before the code, but they could not compile until `RemindBefore`, `Reminded` and the counter existed, so none of them was run and seen to fail first. Their first run found two faults, both fixed in this phase: an empty `RemindBefore` written into the canonical form, which the AC6 hash test caught, and a reading of a form without policies.

## Awaiting phase 9

- PackageManifestRuleTests (`tests/Themis.Repository.Tests`): `Themis.Endpoints`.

## Analyser suppressions

None added.

## Resolved by the lead

- Where several of a step's reminders are due at once, one `reminded` event is written, for the latest due, counted once, and the earlier ones are passed; a reminder is sent only while the deadline is ahead, and once it has passed the reminders left are passed and only the action runs (the lead's answer on phase 8; `phase-06-docs` at `d61b81a`). Governs: `01` APR-REQ-009.
- `RemindBefore` defaults to empty and is not `required`; `approvals.reminders` is tagged `type`, in the unit `{reminder}` (T-025). Governs: `02` APR-TYPE-004, `04` APR-OPS-005.

## Resolved by rule

- A step records the reminders sent or passed in `reminders_sent`, earliest first, and the next reminder's instant in `remind_at`, none once no reminder is left; both are set at the step's start and moved in the transaction that writes the `reminded` event, so none is sent twice across a restart. The deadline's action clears `remind_at`. Governs: `01` APR-REQ-009.
- The job takes each step whose `remind_at` has passed under its request's lock, `FOR UPDATE SKIP LOCKED`, as it takes an action, and sends nothing where the request is no longer pending, the step no longer current or its action has run. Governs: `01` APR-REQ-009, `04` APR-OPS-004.
- The `reminded` event names the step's open assignments, by origin then person, each followed by the delegates who hold it through a delegation live at that instant and covering the type; no watcher and not the requester. Governs: `01` APR-REQ-014, APR-REQ-009.
- The job's count of items handled counts each reminder sent with each action run. Governs: `04` APR-OPS-004.
- `RemindBefore` is checked last in the deadline, after `escalateTo`; a reminder at least as long as `After`, under an hour, of no whole seconds or like one before it is refused by its index, and a list over three or none by the list. Governs: `02` APR-TYPE-004.

## Open questions

None.

## Checks

- The commits: `4f7b12e`, the merge of `phase-06-docs` at `d61b81a` (`c1e368b`), and this report's own, on `phase-06` after `d2b1155`.
- Fast checks: the build of `Themis.sln` in Release with warnings as errors, the analysers, `dotnet format Themis.sln --verify-no-changes`, the unit tests and the contract tests: green but for the package table's test.
- Integration tests: the whole of `Themis.Tests`, 468 of 468 green; `Themis.Cli.Tests`, 34 of 34; `Themis.Core.Tests`, 23 of 23.
- Before the push: `run-gates.ps1 -Base origin/main -Head HEAD`: every gate passed but `gate-tests`, on the package table's test awaiting phase 9 alone.
