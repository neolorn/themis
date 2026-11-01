# 05 — Engineering conventions

How the library is built. Every rule here is checked by the build, a test or a gate,
save the parts marked "checked in review", which the lead's review of each phase checks.

---

## 1. Layout and setup

**APR-ENG-001** — The repository SHALL hold one solution, `Themis.sln`, with one project
per package of `03` APR-LIB-001 under `src/` and one test project per source project
under `tests/`, and a sample host with its test project in a solution of its own under
`samples/`, built only from the packed packages.

**Values (T-017, T-025).** A source project is `src/<Package>`; its test project
`tests/<Package>.Tests`; the tests over the whole repository (its projects' references
and grants, its layout and shared settings, the secret scan, the contract tests over the
packages' built assemblies, `03` APR-LIB-001 and APR-LIB-002, `07` APR-THR-003) live in
`tests/Themis.Repository.Tests`, the one test project with no source project. Each
project's namespace root is its name; a file's namespace matches its folder; one public
top-level type per file, a type's cases declared in its file. Projects are flat but for
these folders: in `Themis`, `Types` (`02`), `Requests` (`01`), `Operations` (`04`) and
`Migrations`, into which the migrations are generated; every other project, the test
projects and the sample host's included, keeps its files at its root (which file goes in
which folder is checked in review). A shipped project's types are internal unless `03`
APR-LIB-002 makes them public, and its public types sit at its root, so each is in its
package's namespace; a test project's test classes are public, since xUnit.net runs no
other. `InternalsVisibleTo` is granted only from a source project to its own test
project, and from `Themis` to `Themis.Cli`, which applies the migrations `Themis` holds
(`04` APR-OPS-002), to `Themis.Testing`, which removes the hosted jobs by their types
and applies the migrations for a test (`03` APR-LIB-007), and to `Themis.Endpoints`,
which asks an internal query whether a person is named in the `Notify` of a request's
events, its watcher check (`03` APR-LIB-004); no public member changes for it. The
packages are released together (`03` APR-LIB-001).

The sample host is an ASP.NET Core application with an invented domain naming no real
product or company (checked in review): it publishes two approval types at its start,
one with two steps and a deadline and one with a condition, maps the endpoints of
`Themis.Endpoints` (`03` APR-LIB-004), to which its own approval endpoints give way,
supplying the person from its `X-Person` header through `ICurrentPerson` (in the sample
only), keeps endpoints of its own for what those do not serve (creating, cancelling and
completing requests), answers the directory from a small table of its own, and logs the
events its observer receives. Its tests run through `Themis.Testing` (`03` APR-LIB-007)
against a real database. It runs in the gate (APR-ENG-012) from packages packed from the same commit
into `artifacts/packages/`, which `.gitignore` lists, at a version the gate passes it;
it alone has no lockfile and no locked restore, since its packages change with every
commit. The sample host and its test project are outside `Themis.sln`, in
`samples/Samples.sln`, and `samples/nuget.config` adds `artifacts/packages/` to the
package sources for them alone. Both `nuget.config` files carry `packageSourceMapping`:
the library's own package identifiers from `artifacts/packages/` only (in
`samples/nuget.config`), and every other identifier from nuget.org only.

The repository's other files: at its root the solution, `Directory.Build.props`,
`Directory.Packages.props`, `global.json`, `.editorconfig`, `.gitignore`,
`nuget.config`, `.config/dotnet-tools.json` (the local .NET tools), `CHANGELOG.md` and
`RELEASE-NOTES.md` (the release notes of `04` APR-OPS-007: one section per release,
headed by its version and written in its release commit, or before it in a `docs:`
commit of the same pull request, where the review reads it (T-024), holding the
deployment's duties as they stand at that release and the migrations of that release
that drop or rewrite data, `04` APR-OPS-002); the pipeline's definition under
`.github/workflows/`; the gates' scripts under `build/gates/`, with the secret scanner's
configuration there, `build/gates/release-baseline.json`, the release gate's baseline
(`03` APR-LIB-006), and `build/gates/built-items.txt`, the acceptance criteria proven so
far by a test, one per line as `APR-REQ-004 AC2`, which the test-name gate reads and
each phase extends with the criteria it proves. The secret scanner is gitleaks 8.30.1,
its download checked against its published SHA-256, which the fetch script itself holds
(checked in review), and left under `tmp/tools/` by one step of `build/gates/` that the
secret-scanning and the test gates both run first. Its configuration extends gitleaks'
default and adds no entry, since no test needs a credential-shaped literal: every secret
a test needs is made at run time. No allow-list but gitleaks' default reaches the scan:
it runs with `--ignore-gitleaks-allow`, so a line's inline `gitleaks:allow` marker
silences nothing, with `--gitleaks-ignore-path` naming a folder that holds no file, and
with no `--baseline-path`; and it fails where a `.gitleaksignore` is tracked or sits at
the scanned repository's root, where gitleaks reads one whatever its flags say. The
gates' scripts are PowerShell 7 scripts, and the pipeline runs on GitHub's
`ubuntu-24.04` runner using no action but `actions/checkout` and `actions/setup-dotnet`
(the SDK of `global.json`), each at its latest release when the workflow is written and
pinned by that release's full commit SHA (all checked in review).

*Source: T-017, T-024, T-025*

**Acceptance criteria**
1. A test fails on a project outside this layout, a grant outside this rule, or an entry
   added to the scanner's configuration.
2. The sample host builds from the packages packed from the same commit, and its tests
   pass.

---

**APR-ENG-002** — Every project SHALL target .NET 10 with the settings of one committed
`Directory.Build.props`, and the SDK SHALL be fixed in `global.json`.

**Values (T-017).** `Nullable` enable, `ImplicitUsings` disable, `TreatWarningsAsErrors`
true, `MSBuildTreatWarningsAsErrors` true, `AnalysisLevel` latest-all,
`EnforceCodeStyleInBuild` true, `Deterministic` true, `GenerateDocumentationFile` true,
`EnableConfigurationBindingGenerator` true, `NuGetAudit` true with `NuGetAuditMode` all;
none overridden by a project. `global.json` names a .NET 10 SDK with `rollForward`
`latestPatch`. Package versions live in `Directory.Packages.props` (Central Package
Management) with a committed lockfile per project, restored in locked mode in the
pipeline (the sample host aside, APR-ENG-001). Of the analysers' rules, `.editorconfig`
turns off only these, each with a comment line above it giving the reason: CA1707 under
`tests/` and in the sample host's test project (a test's name carries its criterion's
identifier, APR-ENG-008); CA1515 in the same places (xUnit.net runs only public test
classes); CA1812 under `src/`, under `tests/` and in the sample host (an internal class
the dependency injection container makes is made, though no code calls `new` on it);
CA1030 for `IAlertSink` (an alert is an awaited call with a token, not an event); CA1034
under `src/Themis.Core/` (a case of a type with cases is a nested record reached through
it, `new FieldValue.Flag(true)`, `03` APR-LIB-002); CA1303 everywhere (exception
messages are for developers and never shown to people); CA1032 for
`ApprovalsStartupException` (it always carries a code and details, which the standard
constructors would leave out); CA1861 under `src/Themis/Migrations/` (EF Core's
generator passes literal column arrays to the migration operations); and CS1591 and
CA2007 under `tests/` and in the sample host's test project (a test states no contract
of its own, and xUnit.net's own analyser refuses the `ConfigureAwait(false)` CA2007 asks
for). Every project carries `[assembly: CLSCompliant(false)]`, written once in
`Directory.Build.props`, for CA1014. Formatting is defined once in `.editorconfig`,
which is the output of `dotnet new editorconfig` for the SDK of `global.json`,
unchanged, with the turn-offs above added (checked in review), and checked by
`dotnet format --verify-no-changes`.

*Source: T-017*

**Acceptance criteria**
1. No project file sets a target framework, a listed property or a package version, and
   `Directory.Packages.props` sets no property but `ManagePackageVersionsCentrally`; no
   project file, `Directory.Build.props` or `Directory.Packages.props` turns a warning
   off or keeps it from failing the build, by setting, whatever its value, a property
   among `NoWarn`, `WarningsNotAsErrors`, `WarningLevel`, `MSBuildWarningsAsMessages`,
   `MSBuildWarningsNotAsErrors`, `RunAnalyzers`, `RunAnalyzersDuringBuild`,
   `EnableNETAnalyzers`, `CodeAnalysisRuleSet`, `NuGetAuditLevel`, `AnalysisMode`, and
   the `AnalysisLevel` or `AnalysisMode` of one category of rules, by an item's metadata
   (`NoWarn` on a reference) or by an item (`NuGetAuditSuppress`, an analyser
   configuration file); and no `.editorconfig` or `.globalconfig` but the root
   `.editorconfig` is tracked or sits in the working tree outside `tmp/` and the
   projects' `bin/` and `obj/`.
2. A warning fails the build, the compiler's, an analyser's, NuGet's, MSBuild's or the
   SDK's alike; a locked restore that differs from the lockfile fails.

---

## 2. Design

**APR-ENG-003** — An expected outcome SHALL be returned as a result, never thrown; a
fault SHALL be thrown.

**Values (T-017).** `Result` and `Result<T>` (`Themis.Core`) carry success or a failure
with its code and details (`06` section 1). A refusal that a person, a host's data or a
moment can cause is a failure: a decision by someone not assigned, a request already
closed, a definition outside its rule, a directory that does not answer. A mistake only
the calling code can make is a fault: a null or empty argument, a person reference, role
name or target outside `00` section 1.5's rule, subject roles without a subject, a
host's transaction that is not open (`03` APR-LIB-008), a directory answer outside its
rule (`03` APR-LIB-005); it throws `ArgumentException` or `InvalidOperationException`,
and a broken invariant throws. A deployment fault a job's run meets, such as a missing
grant, throws `InvalidOperationException` naming its code (a `retention` run without
its privileges, `04` APR-OPS-004), while an operation that meets the same fault answers
it as a failure (`EraseAsync`, `01` APR-REQ-015). A `catch` never turns a fault into a
success, and an exception is never swallowed: it is handled, rethrown, or logged and
the operation answers a failure.

*Source: T-017, T-025*

**Acceptance criteria**
1. No public operation throws for a code of `06` section 1, save a job's run that meets
   a deployment fault, such as a missing grant, which throws `InvalidOperationException`
   naming the code; a "(startup)" code is thrown only as an `ApprovalsStartupException`
   while the host starts (`03` APR-LIB-003).

---

**APR-ENG-004** — Every type SHALL be sealed unless designed for inheritance, every
asynchronous method SHALL take a `CancellationToken`, data crossing a boundary SHALL be
immutable, and there SHALL be no static mutable state and no reflection.

**Values (T-017).** Records for data, `init` members, read-only collections; JSON
through source-generated contexts only (APR-ENG-006); configuration through the binder's
source generator and source-generated options validation (`04` APR-OPS-001); logging
through source-generated `LoggerMessage` methods (`04` APR-OPS-005); the clock through
`TimeProvider`; the built-in dependency injection container only. Every public type and
member of a shipped project carries XML documentation stating its contract, the failures
it answers and the faults it throws.

*Source: T-017*

**Acceptance criteria**
1. A contract test reads the built assemblies' metadata (`System.Reflection.Metadata`,
   in tests only) and fails on a public class neither sealed nor abstract, and on a
   public asynchronous method without a `CancellationToken` (save
   `IAsyncDisposable.DisposeAsync`); the build fails on a shipped project's public
   member without documentation.
2. A test fails on a lookup or call of members by name (`System.Reflection` member
   lookups, `Activator`, `Type.GetType`) outside generated code.

---

**APR-ENG-005** — Persistence SHALL go through EF Core with the Npgsql provider over the
library's own `DbContext`, and every multi-row change SHALL be one transaction.

**Values (T-016, T-017).** Hand-written SQL, where EF Core cannot express a statement
(a claim, `FOR UPDATE` and `FOR UPDATE SKIP LOCKED`, the type's advisory lock), lives
beside the code that needs it and takes its values as parameters. The lock order, the
retries, the `@now` parameter and the session timeout are `04` APR-OPS-003's. Every
advisory lock is transaction-scoped (`pg_advisory_xact_lock`), taken inside the
transaction it serialises, so a pooler never strands one. A definition's canonical form
is stored as `text`, never `jsonb`, which would reorder its members, so the bytes hashed
are the bytes kept; a snapshot is stored as `jsonb`. Identifiers are UUID version 7,
made from the library's `TimeProvider` (`Guid.CreateVersion7`).

*Source: T-016, T-017*

**Acceptance criteria**
1. A transaction of the library's own that PostgreSQL aborts with 40001 is run again, at
   most `Themis:Database:Retries` times.
2. A published definition read back holds the exact bytes whose hash its version keeps.

---

**APR-ENG-006** — A definition's canonical form SHALL be one exact sequence of bytes for
each definition, so two equal definitions always hash alike and a later release that
adds a member leaves every earlier definition's form unchanged.

**Values (T-002, T-025).** The canonical form is UTF-8 JSON written by a source-generated
`System.Text.Json` context with the serializer's default encoder and no indentation or
whitespace: each record's members in the order of `03` APR-LIB-002, under their names in
camel case; a member that is none, an empty list or `false` left out, so a member a
later release adds with such a default changes no earlier form; a case written as an
object whose first member is `case`, the case's name in camel case
(`{"case":"permission", "name":"orders.approve"}`), then its own members; an enumeration
member by its wire spelling (`06` section 2); a whole number as a JSON integer; a
decimal without trailing zeros or exponent (`1000.5`, `0`); a duration
(`Deadline.After`, each of `Deadline.RemindBefore`) as its whole number of seconds,
each being in whole seconds (`02` APR-TYPE-004); lists in the order given. The SHA-256
of these bytes is the version's hash (`02` APR-TYPE-001), and a definition read back is
the one these bytes give.

*Source: T-002, T-025*

**Acceptance criteria**
1. Two definitions equal but for the scale of a decimal value (`1000.50` and `1000.5`)
   have one canonical form.
2. A test holds the canonical form of a fixed definition as a literal and fails where
   the serializer writes it otherwise.

---

## 3. Names

**APR-ENG-007** — Codes SHALL be lower case, dot-separated, starting with `approvals.`
(`approvals.request.closed`); configuration keys SHALL be the configuration paths of
`06` section 4 under `Themis`; enumerations SHALL be spelled as `06` spells them; and no
type SHALL be named after the library.

**Values (T-001, T-017, T-025).** The library's name appears only in namespaces,
project and package identifiers, the solution file (`Themis.sln`), the configuration
section, the registration and mapping methods (`AddThemis`, `AddThemisTesting`,
`MapThemisApprovals`), the command of `Themis.Cli`, and the names of `06` section 7
that `06` lists as changing with it.

*Source: T-001, T-017, T-025*

**Acceptance criteria**
1. A test fails on a code the library answers that `06` section 1 does not list, a
   configuration key it binds that section 4 does not list, a spelling that differs
   from `06`, a metric instrument a test host's meter publishes that section 7 does not
   list, and a public type whose name holds the library's name.

---

## 4. Testing

**APR-ENG-008** — Tests SHALL be written first (checked in review), use xunit.v3 on
Microsoft.Testing.Platform and hand-written fakes, and name the acceptance criterion
they prove.

**Values (T-017).** Each test carries the trait `Category` with one of `unit` (nothing
external), `integration` (something outside the process: real PostgreSQL in a container
through Testcontainers, the image `postgres:16.15` (the floor, `04` APR-OPS-007), one
container per test class that needs one; `git` and the secret scanner; or the SDK's
build and pack, writing under `tmp/`) and `contract` (the public surface and the
reference tables); the sample host's tests are a fourth kind in their own project. The
pipeline runs each kind on its own, by that trait; a run that finds no test of its kind
passes (Microsoft.Testing.Platform's exit code 8 for no test is accepted), and a test of
`tests/` that carries none of the three kinds, or more than one, fails the gate. An
acceptance-criterion test is named `ITEM_ACn_Outcome`
(`APR_REQ_004_AC1_UnassignedDeciderIsRefused`); every other test
`Method_Scenario_Outcome`. No mocking package: the seams are faked by hand, and
`Themis.Testing`'s own types serve the library's tests too. Concurrency criteria are
tested with real transactions on the container's database, the order of two
transactions forced by holding one open while the other starts. Every criterion a test
can decide has one; the rest are named in the phase report with how they were checked.

*Source: T-017*

**Acceptance criteria**
1. No mocking package is referenced.
2. Every testable criterion of a built item has a test carrying its identifier.
3. A test of `tests/` that carries no `Category` trait, more than one, or a value other
   than the three, fails the tests gate.

---

## 5. Dependencies

**APR-ENG-009** — These SHALL be the only third-party packages, each referenced only by
the projects `03` APR-LIB-001 allows.

| Purpose | Package | Licence |
|---|---|---|
| Data access | `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Relational`, `Npgsql.EntityFrameworkCore.PostgreSQL`; `Microsoft.EntityFrameworkCore.Design` (making migrations, build-only); `dotnet-ef`, a local .NET tool at the EF Core version, in `.config/dotnet-tools.json` (making migrations) | MIT, PostgreSQL |
| Hosting abstractions | `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Configuration.Abstractions`, `Microsoft.Extensions.Configuration.Binder`, `Microsoft.Extensions.Options`, `Microsoft.Extensions.Options.ConfigurationExtensions`, `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Hosting.Abstractions`, `Microsoft.Extensions.Diagnostics.HealthChecks` | MIT |
| Public surface | `Microsoft.CodeAnalysis.PublicApiAnalyzers` (build-only) | MIT |
| Versioning | `MinVer` (build-only) | Apache 2.0 |
| Tests | `xunit.v3`, `Testcontainers.PostgreSql`, `Microsoft.AspNetCore.Mvc.Testing` (test projects only, the sample host's and `tests/Themis.Endpoints.Tests` among them, at the version of ASP.NET Core's shared framework) | Apache 2.0, MIT, MIT |

Every package is taken at its latest stable release when added, the Microsoft packages
at the release that matches the SDK's .NET 10 runtime, and its listing checked then
(checked in review). Adding a package is a decision-log entry.

*Source: T-017, T-025*

**Acceptance criteria**
1. The set of direct package references is within this table, and the tool manifest
   names `dotnet-ef` alone; a pull request adding another fails a check.

---

**APR-ENG-010** — A package with a known vulnerability SHALL fail the build,
vulnerability alerting SHALL be on for the repository, and a security update SHALL be
taken as soon as it lands (checked in review).

**Values (T-017).** NuGet's audit of every package, transitive ones included, runs in
each restore (APR-ENG-002), and its warnings fail the build.

*Source: T-017*

**Acceptance criteria**
1. A restore resolving a package version with a published advisory fails the build.

---

## 6. Version control and gates

**APR-ENG-011** — Work SHALL be trunk-based with short-lived branches reaching the
default branch by pull request (save the first, `docs:` commit, which the implementer
pushes to the empty default branch before setting its protection), commit messages SHALL
follow Conventional Commits 1.0.0, a `CHANGELOG.md` SHALL follow Keep a Changelog 1.1.0,
and MinVer SHALL derive the version from the tag.

**Values (T-017, T-020, T-025).**
- **Commit message:** a type of `feat`, `fix`, `refactor`, `perf`, `test`, `docs`,
  `build`, `ci`, `chore`; an optional scope naming the package's area (`core`, `engine`,
  `testing`, `cli`, `endpoints`, `sample`); a description in the imperative (checked in
  review), beginning with a lower-case letter (a name it mentions keeps its own
  spelling), with no full stop, the first line at most 72 characters; a body only where
  the diff cannot explain itself (checked in review), as dash fragments of at most 72
  characters, each a reason or a non-obvious consequence; a footer carrying the items the
  commit implements (`Implements: APR-REQ-004`) and nothing else; `!` or
  `BREAKING CHANGE:` for a break of `03` APR-LIB-006.
- **Changelog:** every commit that changes behaviour, one of type `feat`, `fix` or
  `perf` or marked `!`, adds its line under `Unreleased` in the same commit (`Added`
  while nothing is released).
- **History:** no force push to the default branch, and no merge of a red check. A
  commit that is not pushed and that no other branch has merged MAY be amended or
  rebased to meet a gate; history that is pushed or merged is never rewritten.
  `PublicAPI.Shipped.txt` changes only in a release commit, as
  `build/gates/release-baseline.json` does (`03` APR-LIB-006).
- **Release commit:** the commit that makes release `V`, a final release `X.Y.Z` or a
  release candidate `X.Y.Z-rc.N` (`03` APR-LIB-006, T-020), has the message
  `chore: release V` and changes no file but these: each `PublicAPI.Shipped.txt`, to its
  parent's entries and those of the `PublicAPI.Unshipped.txt` beside it, less each entry
  that file marks `*REMOVED*`, `#nullable enable` first and the rest in ordinal order;
  each `PublicAPI.Unshipped.txt`, to nothing but `#nullable enable`;
  `build/gates/release-baseline.json`, to the release's own set as the release gate
  reads it at that commit; `CHANGELOG.md`, whose `Unreleased` entries move under a new
  heading `## [V] - YYYY-MM-DD`, the commit's date in UTC, below an empty
  `## [Unreleased]`; and `RELEASE-NOTES.md`. In its pull request only `docs:` commits
  that change nothing outside `docs/` follow it, so the tag on it carries everything the
  pull request built.
- **Releases:** a release is a tag `v<V>` on the release commit, pushed once the pull
  request's merge has brought that commit into the default branch, by the implementer
  or, where the machine's permissions refuse it the push, by the owner; a release
  candidate on the lead's message, a final release on the owner's (T-020). The
  pipeline's release job runs only for such a tag, after the gates job has passed on it,
  and is the one job that can write to the repository's contents: it packs every package
  at the tag, each at the tag's version, and creates the tag's release with the runner's
  GitHub CLI, a release candidate's marked as a pre-release, its notes the tag's
  `RELEASE-NOTES.md`, the packages attached; it publishes them to no
  package feed. Each package carries its identifier, its description (the text of its
  "What it holds" cell in `00` section 2.2, without markup or references), as its
  authors the name the lead's first message of phase 0 gives, and the repository's
  address, and `Themis.Cli` also `PackAsTool` and `ToolCommandName` (`themis`); every
  other package property is left unset. A package's dependency on another of the
  library's packages is the exact version packed (`[v]`, `03` APR-LIB-001): `dotnet
  pack` writes a project reference as that version or later, and the .NET 10 SDK has no
  setting for an exact one, so `Directory.Build.props` holds one target, run after
  NuGet's `_GetProjectReferenceVersions`, that sets the `ProjectVersion` of each of
  NuGet's `_ProjectReferencesWithVersions` items to `[v]`; both are names inside NuGet's
  pack targets, not a documented setting, so `03` APR-LIB-001's criterion 2 is what
  catches an SDK that changes them.

*Source: T-017, T-020, T-025*

**Acceptance criteria**
1. A commit message outside the rule, and a behaviour-changing commit without a
   changelog line, fail a check.
2. A commit that changes the content of an existing `PublicAPI.Shipped.txt` or
   `build/gates/release-baseline.json`, or deletes one, fails a check, save the release
   commit, which changes them as the release commit's value gives.
3. A commit `chore: release V`, `V` a final release or a release candidate, that changes
   a file outside the release commit's value, or one of its files otherwise than the
   value gives, or that a commit other than a `docs:` commit changing only `docs/`
   follows in its pull request, fails a check. A version outside `X.Y.Z` and
   `X.Y.Z-rc.N`, `N` from 1, fails it.
4. A release tag's run creates the tag's release, holding every package at the tag's
   version, a release candidate's marked as a pre-release, only once its gates have
   passed, and no other job of the pipeline can write to the repository's contents.

---

**APR-ENG-012** — The pipeline SHALL block on every gate below, and each gate SHALL be a
script the implementer runs locally with the same result, save the log gate, which the
integration tests' run checks.

| Gate | Source |
|---|---|
| Build and analysers, warnings as errors | APR-ENG-002, APR-ENG-004 |
| `dotnet format --verify-no-changes` | APR-ENG-002 |
| Locked restore and the vulnerability audit | APR-ENG-002, APR-ENG-010 |
| Unit, integration and contract tests | APR-ENG-008 |
| Public surface files up to date | `03` APR-LIB-002 |
| Release compatibility of the public surface, codes, configuration keys, enumerations, statuses, event kinds and metric names | `03` APR-LIB-006 |
| Dependency allow-list | APR-ENG-009 |
| Project references and grants | APR-ENG-001; `03` APR-LIB-001 |
| Migrations applied twice in a row to an empty database, the second applying nothing | `04` APR-OPS-002 |
| No person reference, target, role, field value or reason in any log line of the integration tests: each integration test captures its log lines with an in-memory logger provider and asserts at its end that none holds a value the test used; it is checked by the integration tests' run and has no script of its own | `04` APR-OPS-005 |
| Secret scanning of the whole history (gitleaks) | `07` APR-THR-003 |
| Commit message format, changelog line, and `PublicAPI.Shipped.txt` and the release baseline unchanged outside a release commit | APR-ENG-011 |
| Acceptance-criterion test names present for each criterion of `build/gates/built-items.txt` | APR-ENG-008 |
| The sample host builds from the packed packages, its code passes `dotnet format --verify-no-changes` on `samples/Samples.sln`, and its tests pass | APR-ENG-001, APR-ENG-002 |

The gates live as scripts under `build/gates/`, which the pipeline runs in one job,
`gates` (the one status check branch protection requires), on every pull request, every
push to the default branch and every release tag, the release job of APR-ENG-011 waiting
for the last; the commit-message gate checks the commits a pull request or a push
brings, and for a release tag the commits since the previous release tag (the whole
history for the first), save the merge commits the platform writes, while the secret
scan reads the whole history each time (`07` APR-THR-003); a gate whose part is built in
a later phase (the migrations gate, plan phase 1; the release compatibility gate and the
sample host's, phase 5) has its script added by that phase, and the job runs every
script present. Before it pushes, the implementer runs the fast checks, every unit test,
the commit-message gate and the secret scan, and the integration tests it writes or its
change touches, never the whole integration set; the pipeline's `gates` run on the last
commit pushed, the report's own commit, is the gate of record, which the implementer
waits for in the same run and names with its result (the first `docs:` commit, pushed
before the gates exist, is checked by the secret scan alone).

*Source: T-017*

**Acceptance criteria**
1. Each gate runs on pull request, and any failing blocks the merge.
2. Each gate's script, run locally on a commit, gives the result the pipeline gives on
   it, whether or not the implementer runs it before a push.
