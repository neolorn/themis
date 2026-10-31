# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- The startup exception, the seams a host supplies and the values they carry
- `AddThemis`, which registers the library and checks its configuration when the host starts
- The start's refusal of a host missing a required seam
- The start's read of the secret `connection`, on which the library opens its own data source
- The schema `approvals` with its alert table, and the start's check that the database holds every migration of the release
- `themis migrate`, the tool that applies the migrations and grants the application roles
- The library's own transactions, run again after a deadlock or a serialization failure and bounded by a session timeout
- The library's own alert sink, which logs each condition at the error level where the host registers none
- The meter `Themis` with its counters, and the health check `themis` where the jobs run
- `Result`, `Result<T>` and `Failure`, and the enumerations of approval types
- The definitions of approval types with their policies, steps and conditions, their versions, and `IApprovalTypes`
- The canonical form of a definition, one exact sequence of bytes whose SHA-256 is its version's hash
- The rules a definition is checked by at publish, each naming the path of the first member outside it
- A condition on a request's snapshot and roles, a whole number and a decimal compared as decimals
- The choice of a request's policy, and the fallback step of a type where no policy holds
- The tables of approval types and their versions, a version's definition kept as text beside its hash
- `IApprovalTypes`, which publishes numbered versions under the type's lock, reads them and retires a type
- The rule that a definition's canonical form is at most 64 KiB, a larger one naming the empty path
- The request, decision and step types, their statuses, `IApprovals` and `IApprovalJobs`
- The tables of requests, steps, assignments, decisions, events and jobs, each status a closed set the database holds
- `Themis.Testing`: an in-memory approver directory, a recording observer, fixed secrets and a test database's migration
- `IApprovals`: a request created, decided, cancelled and read, on the library's own transaction or the host's, its steps' assignments fixed at their start and each move writing its event
- The jobs `deadlines`, `observer` and `watch`, each run under its claim on the hosted timer or through `IApprovalJobs`, the observer told each event in order for each request
- The alerts `observer-failing` and `request-unassignable`, the gauge `approvals.events.pending` and the health check's judgement of the observer's lag where the application runs the observer
- `Themis.Testing`: `AddThemisTesting`, which removes the hosted jobs, and `TestJobs`, which runs them until idle
- `IDelegations`: a delegation created, ended and listed, a delegate deciding for the lender while it is live and seeing the lender's assignments in their pending list
- `IApprovals.CompleteAsync`, on the library's own transaction or the host's, which ends an approved request `completed`
- `IApprovals.ProtectionAsync` and `RecordChangeAsync`: a pending request's protected field reads locked, and a change recorded to an approved one's voids it
- `IApprovals.ByTargetAsync`, a target's requests newest first, and `ExportAsync`, what the record holds that names a person
- The `deadlines` job's actions: a step past its deadline escalated, rejected or cancelled once, its request taken `FOR UPDATE SKIP LOCKED`
- The alert `job-stalled`, and the health check turning unhealthy while a job is stalled whatever `Themis:Observer:Enabled` says
