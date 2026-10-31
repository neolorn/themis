# 07 — Threat model

What is worth protecting, who would go after it, and which requirement stands in the
way. An approval is worth having only where no one can give it to themselves, take it by
racing or by changing the rules, or keep it while changing what was approved.

---

## 1. Assets

| Asset | Where it is | Protected by |
|---|---|---|
| The rule of who must agree | The approval types' versions | Immutable versions, kept by hash; a request keeps its version (`02` APR-TYPE-001) |
| The record of who decided what | Requests, steps, assignments, decisions, delegations | Every move under the request's lock; decisions never changed; no `DELETE` granted to the application (`01` APR-REQ-004, APR-REQ-015; `04` APR-OPS-002, APR-OPS-003) |
| What was approved | The snapshot, and the host's own fields it covers | Protected fields locked while pending, voiding the approval when changed before completion (`01` APR-REQ-011) |
| The host's knowledge that an approval happened | The events | Written in the move's transaction, delivered at least once, in order, with a unique identifier (`01` APR-REQ-014) |
| Person references, roles, snapshot values, reasons | The library's tables | Never logged (`04` APR-OPS-005); the database's own access control and the deployment's encryption at rest (`04` APR-OPS-008) |
| The database connection string | The secret source only | `04` APR-OPS-008 |

---

## 2. Threats and controls

**APR-THR-001** — Each threat below SHALL be met by the control it names, and each
control SHALL have the test its requirement gives.

| Threat | Control |
|---|---|
| A requester approves their own request, or the person it is about approves it | The requester and the subject are never assigned, and a decision by either, in their own name or as a delegate, is refused (`01` APR-REQ-003, APR-REQ-004) |
| A requester approves through a delegation: lending to themselves, or deciding as the delegate of an approver | No delegation to oneself; a delegate passes every separation check the lender does, and is refused where they are the requester or the subject (`01` APR-REQ-004, APR-REQ-008) |
| Authority travels along a chain of delegations to someone no approver chose | A delegate never passes on what was lent to them (`01` APR-REQ-008) |
| One person stands for two approvers, deciding twice in one step or approving several steps alone | One decision per person per step; under `RequireOther`, a person who approved an earlier step is not assigned again and cannot decide a later one (`01` APR-REQ-004, APR-REQ-006) |
| A person who lost the authority decides from an assignment made before | The host's directory is asked at every decision, for the decider and for the lender (`01` APR-REQ-004) |
| Two decisions race: a request approved twice, an approval after a rejection, or two approvals counted for one | The request's row locked for every move, the status checked under the lock, one decision per assignment (`01` APR-REQ-004; `04` APR-OPS-003) |
| The rules are made easier while a request is pending, so it passes | A request keeps the version it was created under; versions are never changed (`02` APR-TYPE-001) |
| What was approved is changed after the approval and before the host acts (the approved price raised, the approved role swapped) | The protected fields are reported locked while pending, and a change after approval voids it until the host completes it (`01` APR-REQ-011, APR-REQ-012) |
| A request passes with no one deciding: a deadline lapsing, every step skipped, no rule matching | No deadline action approves; a policy whose every step is skipped does not hold; with no policy holding, the type's fallback person decides unless the type explicitly allows approval at once (`02` APR-TYPE-003, APR-TYPE-004; APR-THR-002) |
| A request is left with no one able to decide it, blocking the host's work | The step's fallback, then the type's; where both are excluded the request is cancelled with `noapprover` and an alert raised (`01` APR-REQ-007; `04` APR-OPS-006) |
| The record is erased or rewritten to hide a decision | No `DELETE` or `TRUNCATE` granted to the application's roles; only the values that record a later move change, each with its event (`01` APR-REQ-015; `04` APR-OPS-002). Someone holding the database's owner role can still write; the events delivered to the host's observer keep a copy outside the library's tables |
| The host never learns that a request was approved or rejected, or learns it twice and acts twice | The event is written in the move's transaction and delivered until taken, the `observer-failing` alert raised while it lags; each event carries a unique identifier by which the host ignores one it has had (`01` APR-REQ-014) |
| A definition or a condition is made to exhaust the engine | A definition bounded in size, policies, steps and approvers; a condition bounded in depth and nodes; a step's approvers bounded to 200 persons (`02` APR-TYPE-002, APR-TYPE-005; `01` APR-REQ-003) |
| A host's slow directory holds a request's lock and stalls every decision on it | Each directory call bounded by `Themis:Timeout`; every transaction of the library's own bounded by the session timeout (`03` APR-LIB-005; `04` APR-OPS-003) |
| A name or reference carries SQL or control characters into the database or the logs | Names and references within `00` section 1.5's rule; every statement's values passed as parameters (`05` APR-ENG-005); logs carry identifiers only (`04` APR-OPS-005) |
| A log leaks who asked, who decided, or what for | Logs carry identifiers, keys, statuses and reason codes only, checked in the gate (`04` APR-OPS-005; `05` APR-ENG-012) |

*Source: T-007, T-008, T-009, T-010, T-011, T-013, T-014, T-016*

**Acceptance criteria**
1. Every control above that the library itself carries has a test carrying the
   identifier of the requirement that names it; the database's access control, its
   encryption at rest and the host's handling of events are the deployment's and the
   host's (`04` APR-OPS-007).

---

**APR-THR-002** — No request SHALL become `approved` but by the approvals its steps
need, or, where no policy of its version holds, by its type allowing approval at once.

*Source: T-003, T-005*

**Acceptance criteria**
1. A test drives each path that can move a request to `approved` (the last step's
   approval, an escalation assignee's approval, the later steps all skipped, the
   type's approval at once) and finds no other: a deadline, a cancel, a void and a
   recorded change never approve.

---

**APR-THR-003** — The repository's whole history SHALL be scanned for secrets before
every push, by the implementer (the first push, which comes before the pipeline exists,
`05` APR-ENG-011, included), and by the pipeline on every pull request, every push to
the default branch and every release tag (`05` APR-ENG-012), and a finding SHALL stop
the push it comes before and block the merge; no finding is silenced but by gitleaks'
default configuration (`05` APR-ENG-001). The repository may be public, so nothing that
reaches it is private.

*Source: T-017*

**Acceptance criteria**
1. A commit carrying a credential-shaped string fails the scan; the test builds that
   string at run time, in a repository it makes under `tmp/`, so no such string is ever
   written in the repository's own history.
2. The secret-scanning gate's script fails on the same string on a line that carries
   gitleaks' inline allow marker, and where a `.gitleaksignore` is tracked or sits at
   the scanned repository's root, whatever it lists.

---

**APR-THR-004** — The library SHALL take every person reference it is given as the
host's statement of who acts, and SHALL decide only whether that person may act under
the rules.

**Values (T-001, T-006).** Who is signed in, and whether the person a host names as the
decider, the actor or the requester is the one making the call, is the host's to check
before it calls the library, as for any of its own endpoints. A host that passes a
person reference taken from a request's body, unchecked, lets anyone decide as anyone.

*Source: T-001, T-006*
