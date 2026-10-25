# ADR 0003: Snapshot persistence behind a repository port

**Status:** Accepted (sample scope)

## Context

The portfolio app must run with a single command and no database to install, yet data should survive restarts and concurrent edits must not overwrite each other.

## Decision

- The repository stores immutable snapshots, not live aggregates. Reads return fresh aggregates, so a caller can't mutate stored state by accident.
- An async gate serialises writes. Updates and deletes succeed only when the stored version equals the caller's expected version. The API exposes that version as an ETag and requires `If-Match` on `PATCH` and `DELETE`.
- An `ISnapshotStore` persists state. The JSON file store writes to a temporary file and atomically replaces the original, and it refuses to start on a corrupt file rather than overwrite it.

## Consequences

- Zero setup, durable data, and correct optimistic concurrency.
- Single-instance only. To scale out, implement `IServiceRequestRepository` with EF Core or Dapper over SQL Server or PostgreSQL, mapping `Version` to a rowversion or `xmin` column. The application and API layers don't change.
