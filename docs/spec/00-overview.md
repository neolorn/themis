# 00 — Overview

Specification for a reusable .NET library that answers one question for any application
that imports it: **who must agree, and did they?** A host asks for an approval of
something of its own (an order, a role grant, a price change), the library finds who
must agree under the rules the host published, records each decision, enforces the
rules that make an approval worth having, and tells the host how it ended. It knows
nothing of orders, roles or flows, and it never calls the host's business code.

`Themis` is the library's name (T-001): its namespaces, its project and package
identifiers, its solution file, its configuration section, its registration method, the
command of `Themis.Cli` and the names `06` section 7 lists carry it. No type is named
after the library (`05` APR-ENG-007), and the names that are stored or shared by two
releases (the schema, the lock texts) are neutral and never change.

Intended experience for a new project: **import → publish who must agree → ask.**

**Status:** specification. Everything needed to build is in documents 00 to 07.
`decision-log.md` records why each choice was made and what was rejected; it is
background and never required to build. Requirements cite their source decision so the
reasoning is one lookup away.

---

## 1. Reading this specification

### 1.1 Document set

| Document | Covers |
|---|---|
| **00-overview** | This document: what the library is, its parts, what is in and out of the first release, glossary |
| **01-requests** | A request's life: creating it, its steps and assignments, decisions, delegation, deadlines, cancelling, protected fields, completion, events and the audit record |
| **02-approval-types** | What a host publishes: approval types, their versions, policies, steps, approvers, conditions and watchers |
| **03-library-contract** | Packages, the public surface and its signatures, registration, the seams a host supplies, versioning, the testing package |
| **04-operations** | Configuration, schema and migrations, the database's use, background work, observability, alerts, secrets |
| **05-engineering-conventions** | Layout, setup, design rules, names, testing, dependencies, version control, gates |
| **06-reference** | Codes, enumerations, statuses, configuration keys, alert conditions, event kinds, secrets and other names |
| **07-threat-model** | Assets, threats and their controls |
| `decision-log.md` | Rationale. **Supplementary; not required to build** |

`06-reference` is authoritative for every name, code, key, default and status; where
another chapter names one, it is looked up there.

### 1.2 Requirement format

Every requirement carries an identifier, a normative statement and its source decision.
**Acceptance criteria accompany every requirement that can be tested.** A requirement
that states a design constraint for a later release, or a fact, carries none.

> **APR-REQ-001** — An approval SHALL be asked for only as a request of a published
> approval type.
>
> *Source: T-003*

Identifiers are stable. A withdrawn requirement's identifier is retired, never reused.
Every identifier starts with `APR-`, so it never collides with an identifier of another
library the host uses.

### 1.3 Normative language

**SHALL** / **SHALL NOT**: mandatory; a build that does otherwise is non-conformant.
**SHOULD** / **SHOULD NOT**: strong default; departure requires a recorded reason.
**MAY**: genuinely optional. Anything not marked is descriptive context.

### 1.4 Acceptance criteria

Each criterion is objectively checkable and written to be turned into a test. A
requirement whose criteria cannot be checked without human judgment is under-specified
and is raised, never guessed at.

### 1.5 Lengths and names

A length in characters counts Unicode scalar values (code points other than surrogates).
A string holding a surrogate that is not part of a pair is not text, and fails the check
of its length. A **name** (an approval type's key, a policy's, a step's, a field's, a
permission's, a role's, a target's kind) is 1 to 64 lower-case ASCII letters and
digits separated by single `.`, `-` or `_`, starting with a letter. A **reference** (a
person reference, a target's identifier) is 1 to 128 characters, none of them a control
character, compared exactly.

*Source: T-001*

---

## 2. What this is

**APR-OVR-001** — The library SHALL be **product-agnostic**: it SHALL NOT name, assume
or depend on any host's applications, business, data or layout. What is approved, who
holds which permission, and what happens once something is approved are the host's.

*Source: T-001*

**Acceptance criteria**
1. No identifier, text, default or test fixture of the library names a host product,
   company or application (checked in review, since a test would have to name them).

---

**APR-OVR-002** — The library SHALL be **a library, not a service**: imported by the
host and running in-process, with its own database schema and migrations and its own
background work run by the host's process. It SHALL NOT read or write the host's tables,
and SHALL NOT call the host's code but through the seams of `03` APR-LIB-005.

*Source: T-001, T-006*

**Acceptance criteria**
1. Nothing of the library needs a process, container or network service of its own; its
   only outside dependency is the host's PostgreSQL database.
2. Every table the library uses lives in its own schema (`04` APR-OPS-002).

### 2.1 Tech stack

| Layer | Choice |
|---|---|
| Runtime | .NET 10 |
| Database | PostgreSQL, through EF Core and the Npgsql provider |

Versions are pinned in `05` APR-ENG-009.

*Source: T-001*

### 2.2 The parts

| Package | What it holds | Who uses it |
|---|---|---|
| `Themis.Core` | The public types and contracts every other package shares | Every other package, and a host's code that names the types alone |
| `Themis` | The approval engine: approval types, requests, decisions, delegation, deadlines, the event outbox, the schema and the background work | An application that asks for approvals or decides them |
| `Themis.Testing` | An in-memory approver directory, a recording observer, fixed secrets, an on-demand run of the background work and a test database's migration | A host's tests, and the sample host (`05` APR-ENG-001) |
| `Themis.Cli` | A .NET tool, command `themis`, that applies the migrations (`04` APR-OPS-002) | A deployment's pipeline |

No package knows another library: a host that uses this library beside others (an
identity library, a messaging library) connects them in its own code, through the seams
of `03` APR-LIB-005 and the events of `01` APR-REQ-014 (T-001).

*Source: T-001, T-006*

---

## 3. The first release, and what it is designed for

**APR-OVR-003** — The first release SHALL build every requirement of `01` to `07`, and
SHALL be designed so that each item below is added later without a breaking change to
the public surface (`03` APR-LIB-006):

| Designed for, not built | Where its design lives |
|---|---|
| HTTP endpoints for an approver's queue, decisions and delegations | `03` APR-LIB-004 |
| Deleting closed requests after a retention period, and erasing a person | `01` APR-REQ-015 |
| A step whose approvers are a number out of a list (two of five) | `02` APR-TYPE-004 |
| Reminders before a deadline | `01` APR-REQ-009 |

*Source: T-013, T-015*

---

## 4. Glossary

| Term | Meaning |
|---|---|
| **Host** | The application that imports the library |
| **Approval type** | A kind of thing the host asks approval for (`order.approve`), with the rules of who must agree, published as numbered versions (`02`) |
| **Version** | One immutable set of an approval type's rules; a request keeps the version it was created under |
| **Policy** | One of a version's alternative rule sets, chosen by its start condition, in order |
| **Step** | One stage of a policy: who may approve it, how many must, and what happens at its deadline |
| **Request** | One ask for approval of one target under one version |
| **Target** | What a request is about in the host's terms: a kind and an identifier (`orders`, `4711`) |
| **Snapshot** | The values of the protected fields when the request was made |
| **Protected field** | A field of the target that the approval covers: locked while the request is pending, and voiding the approval if it changes before the host has acted |
| **Requester** | The person who asked; **subject**: the person the request is about, where it is about one |
| **Assignment** | One person asked to decide one step, fixed when the step starts |
| **Decision** | One person's approval or rejection of one step, recorded and never changed |
| **Delegation** | One person's authority to decide, lent to another for a period and for some or all types |
| **Watcher** | A person told of a request and its outcome who never decides |
| **Person reference** | An opaque string the host gives for a person, the same everywhere for that person; the library never interprets it |
| **Event** | A record of something that happened to a request or a delegation, delivered to the host's observer at least once |
