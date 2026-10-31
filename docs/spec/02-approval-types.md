# 02 — Approval types

What a host publishes: for each kind of thing it asks approval for, who must agree. An
approval type is data, published through `IApprovalTypes` (`03` APR-LIB-002), never read
from configuration, so a host feeds it from its own code at startup or from its own
administration screen. Every value named `Themis:...` is configuration (`06` section 4).

---

## 1. Types and versions

**APR-TYPE-001** — An approval type SHALL be published as numbered, immutable versions:
publishing a definition equal to the type's latest version SHALL change nothing, and
publishing a different one SHALL make the next version, which new requests use while
every request keeps the version it was created under.

**Values (T-002).** `IApprovalTypes.PublishAsync(ApprovalTypeDefinition)` answers a
`Result<PublishedVersion>`: the key, the version number and whether this call made it.
Two definitions are equal where their canonical forms are: the definition written by the
library's serializer with its members in the order of `03` APR-LIB-002 and every list in
the order given, its SHA-256 kept with the version. Publishing runs in one transaction
under the type's lock (`04` APR-OPS-003): it reads the latest version, answers it where
its hash is the definition's, and otherwise writes version `latest + 1` (1 for the
first) with the definition, its hash and the instant. A version is never changed or
deleted. `ReadAsync(key, version)` answers a version's definition, the latest where no
version is named, or `approvals.type.unknown`. `RetireAsync(key)` marks the type
retired: a new request of it is refused with `approvals.type.retired`, its open requests
go on under their versions, and a later `PublishAsync` of the type makes it current
again. Each publish and retirement is logged with the key and the version.

*Source: T-002*

**Acceptance criteria**
1. Publishing a definition twice makes one version; changing one step's mode and
   publishing again makes version 2.
2. A request created under version 1 decides under version 1's steps after version 2 is
   published, and a request created after it uses version 2.
3. Two publishes of two different definitions of one key at once make versions 1 and 2,
   never two versions numbered alike.
4. A retired type refuses a new request and lets its open requests end.

---

**APR-TYPE-002** — A definition SHALL be refused at publish, with nothing written,
unless every member is within its rule.

**Values (T-003).** `approvals.type.invalid`, `details.member` the path of the first
member outside its rule (`policies[1].steps[0].fallback`), where:
- `Key` follows the name rule (`00` section 1.5);
- `ProtectedFields` holds 0 to 32 fields, each a name and a `FieldKind` (`Text`,
  `WholeNumber`, `DecimalNumber`, `Flag`), no two named alike;
- `Policies` holds 1 to 16 policies, no two named alike, each with a name and 1 to 10
  steps, no two of one policy named alike (APR-TYPE-003, APR-TYPE-004);
- `Duplicates` is `Skip` or `RequireOther` (`01` APR-REQ-006);
- `Watchers` holds 0 to 16 approver references (APR-TYPE-006);
- `Fallback`, the type's fallback approver, is a person reference, and
  `AllowAutoApprove` is a boolean, false by default (APR-TYPE-003);
- every condition is within APR-TYPE-005's rule;
- the canonical form is at most 64 KiB, checked once every member above is within its
  rule; a larger one names the empty path, the definition itself (T-021).

*Source: T-003, T-021*

**Acceptance criteria**
1. Each condition listed above refuses the publish, naming the member, and writes no
   version.

---

## 2. Policies and steps

**APR-TYPE-003** — A request SHALL take the first of its version's policies, in order,
whose start condition holds and which has a step whose condition holds; where none does,
the request SHALL go to the type's fallback approver, or be approved at once only where
the type allows it.

**Values (T-003).** A policy is a name, a `StartCondition` (none: it always holds) and
its steps. The policies are tried in the order given, which is their priority, against
the request's snapshot and roles (`01` APR-REQ-001). A policy whose start condition
holds but every one of whose steps' conditions is false counts as not holding, so a
policy never approves by its conditions alone. Where no policy holds:
- with `AllowAutoApprove` true, the request is made `approved` at once, with no step and
  no decision, its events saying so (`01` APR-REQ-014);
- otherwise the request takes the **fallback step**: one step named `fallback`, whose
  sole approver is the type's `Fallback` person, `AnyOne`, with no condition, no
  deadline and that person as its fallback too.

*Source: T-003*

**Acceptance criteria**
1. With two policies whose start conditions both hold, the request takes the first.
2. A policy whose start condition holds and whose every step's condition is false is
   passed over.
3. With no policy holding, a type that does not allow it sends the request to its
   fallback person, and one that allows it approves the request at once.

---

**APR-TYPE-004** — Each step SHALL name who may approve it, how many of them must, a
fallback approver, and MAY name a condition and a deadline.

**Values (T-005, T-012).** A step holds:
- `Approvers`: 1 to 16 approver references, each `Permission(name)` (every person the
  host's directory answers for that permission, `03` APR-LIB-005) or
  `Person(reference)`;
- `Mode`: `AnyOne` (one approval completes the step) or `All` (every assignment must be
  approved);
- `Condition`: optional; a step whose condition is false when the request reaches it is
  skipped (`01` APR-REQ-003);
- `Fallback`: a person reference, the approver where the step's exclusions leave no one
  (`01` APR-REQ-007);
- `Deadline`: optional; `After`, 1 minute to 365 days from the step's start in whole
  seconds, and `Action`, one of `Escalate` (with `EscalateTo`, one approver reference,
  required for it alone), `Reject` and `Cancel` (`01` APR-REQ-009). No action approves.

*Designed for, not built:* a mode needing a number of approvals out of the assignments
(`AtLeast(n)`), a new case of the mode that changes no table, since a step's
assignments and decisions are kept one by one already.

*Source: T-005, T-012*

**Acceptance criteria**
1. A step with no approver, more than 16, a deadline outside its range, an `Escalate`
   without a target or a target without `Escalate`, or a fallback that is not a person
   reference, refuses the publish.

---

## 3. Conditions and watchers

**APR-TYPE-005** — A condition SHALL be data, checked at publish against the type's
protected fields, and SHALL decide on the request's snapshot and roles alone, never
failing when a request is decided.

**Values (T-004).** A `Condition` has the cases:
- `AllOf(conditions)` and `AnyOf(conditions)`, of 1 to 16 conditions each, and
  `Negate(condition)`;
- `Compare(field, op, value)`: `field` a protected field, `op` one of `Equal`,
  `NotEqual`, `Greater`, `GreaterOrEqual`, `Less`, `LessOrEqual`, the last four for a
  number field alone, `value` of the field's kind (a whole number MAY be compared with a
  decimal field and the reverse, both read as decimals); text compares ordinally, a flag
  by equality alone;
- `OneOf(field, values)`: 1 to 32 values of the field's kind;
- `RequesterHas(role)` and `SubjectHas(role)`: whether the role names, which the request
  carries, hold `role` (false for `SubjectHas` where there is no subject).

A tree is at most 8 levels deep and 64 nodes in all, and names only fields the type
protects. Since `01` APR-REQ-001 refuses a snapshot whose values are not of the fields'
kinds, a condition never meets a value it cannot compare.

*Source: T-004*

**Acceptance criteria**
1. `Compare(amount, Greater, 1000)` holds for an `amount` of 1000.01 and not for 1000.
2. A condition naming a field the type does not protect, comparing a text field with
   `Greater`, or nested nine levels deep, refuses the publish.
3. `SubjectHas(role)` is false on a request with no subject.

---

**APR-TYPE-006** — A type MAY name watchers, who SHALL be told of each of its requests
and its outcome and SHALL never be assigned or decide.

**Values (T-014).** Watchers are approver references, resolved as approvers are, when
each event that names them is written (`01` APR-REQ-014): the `requested` event and the
outcome events. A watcher who is also an assignee is told as an assignee as well; being
a watcher never makes a person eligible to decide.

*Source: T-014*

**Acceptance criteria**
1. A watcher is named on a request's `requested` and `approved` events, and a decision
   of theirs is refused unless they are assigned.
