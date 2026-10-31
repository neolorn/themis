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
