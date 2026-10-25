# ADR 0001: Layered architecture with a rich domain model

**Status:** Accepted

## Context

Service requests follow a workflow (open, in progress, resolved, reopened, cancelled) whose rules must hold no matter which client calls the API. If those rules sit in controllers or the UI, they get duplicated and drift apart.

## Decision

Split the solution into Domain, Application, Infrastructure and Api projects, with dependencies pointing inward:

- `ServiceRequest` is an aggregate with no public setters. State changes only through `Submit` and `ChangeStatus`, which enforce the workflow and keep the history and version up to date.
- The application layer orchestrates each use case and defines the `IServiceRequestRepository` port.
- Infrastructure implements the port; the API layer only translates HTTP to use cases and back.

## Consequences

- Workflow rules are tested in milliseconds with no host or database (`ServicePortal.Domain.Tests`).
- Use cases are tested against a small in-memory fake repository (`ServicePortal.Application.Tests`).
- There are more projects than a single-project API would need. That's acceptable for a codebase meant to grow, and the boundaries are enforced by the compiler rather than by convention.
