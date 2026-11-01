# Phase 10: Release 1.0.0-rc.2

## Items built

**`05` APR-ENG-011**: the release commit `chore: release 1.0.0-rc.2`, made as phase 5 made rc.1's (T-020).
- Each `PublicAPI.Shipped.txt` now holds its parent's entries and those of the `PublicAPI.Unshipped.txt` beside it, `#nullable enable` first and the rest in ordinal order. No entry was marked `*REMOVED*`. The 37 entries of `Themis.Core` and the 4 of `Themis.Endpoints` moved; the other three packages had none.
- Each `PublicAPI.Unshipped.txt` holds `#nullable enable` alone.
- `build/gates/release-baseline.json` is the release's own set, as the release gate wrote it at that commit. It adds:
  - the codes `approvals.erasure.pending`, `approvals.erasure.typeversion` and `approvals.retention.notgranted`;
  - the key `Themis:Retention:ClosedRequests`;
  - the spellings `retention`, `atleast` and `reminded`;
  - the counters `approvals.erasures`, `approvals.reminders` and `approvals.retention.deleted`.
- `CHANGELOG.md`: the `Unreleased` entries are under `## [1.0.0-rc.2] - 2026-11-01` (the commit's date in UTC), below an empty `## [Unreleased]`.
- `RELEASE-NOTES.md` is unchanged by the release commit. Its section was written before it, in the `docs:` commit of the same pull request (T-024).

**`04` APR-OPS-007**: `RELEASE-NOTES.md` gains the section `## 1.0.0-rc.2`, above rc.1's. It holds:
- the opening paragraph;
- "Changes since 1.0.0-rc.1":
  - every change, each an addition, by retention and erasure, `AtLeast`, reminders and the HTTP endpoints;
  - nothing removed or changed;
  - what a host adapts, the new members of `ApprovalJob`, `ApprovalMode` and `EventKind`;
- the five packages and what each holds, as `00` section 2.2 gives them;
- "Migrations that drop or rewrite data": none. The three migrations of the release add to the schema alone, and every drop is in a `Down`;
- "What the deployment does": each duty of APR-OPS-007 once, retention and erasure among them.

`docs/spec/05-engineering-conventions.md` at `ac33e2f` names `frameworkReferences` on `Themis.Endpoints`. It is merged from `phase-10-docs`, and the repository's test of phase 9 already holds that rule.

## Items not built

None of phase 10. The tag `v1.0.0-rc.2` is the lead's, on the release commit once the pull request's merge brings it into `main`.

## Criteria checked without a test

- `04` APR-OPS-007 criterion 1, every duty stated: checked against the item's list; checked in review.

## Analyser suppressions

None added.

## Resolved by the lead

- Release `1.0.0-rc.2`, made as phase 5 made rc.1's, the release gate against the newest final release, none yet, so nothing binds (the lead's message on phase 10). Governs: `03` APR-LIB-006, `05` APR-ENG-011, T-020.

## Resolved by rule

- The release notes come before the release commit, in a `docs:` commit of the same pull request, as rc.1's did (T-024). The release commit leaves `RELEASE-NOTES.md` unchanged. Governs: `05` APR-ENG-011.

## Open questions

None.

## Checks

- The commits on `phase-10`, cut from `origin/main` at `3299315`:
  - the merge of `phase-10-docs` at `ac33e2f` (`2a19336`);
  - `cffd445`, the release notes;
  - `2636a3d`, the release commit;
  - this report's own, a `docs:` commit changing only `docs/`.
- Before the push: `run-gates.ps1 -Base origin/main -Head HEAD` on the release commit: every gate passed, the release gate reading the release commit's baseline and the sample host's gate among them. Before that run, the commit-message gate alone found one body line of the release notes' commit over 72 characters. That commit, not pushed, was reworded and the release commit replayed onto it, its tree unchanged.
