# ADR 0002: Result pattern and Problem Details for errors

**Status:** Accepted

## Context

Most failures in this system are expected: invalid input, a missing record, a disallowed transition, a stale edit. Using exceptions for these hides them from method signatures and leaves the mapping to HTTP scattered across the codebase.

## Decision

- Use cases return `Result` or `Result<T>`. An `Error` carries a stable `Code`, a user-safe `Message`, an `ErrorType` and optional per-field messages.
- A single mapper (`ProblemResults.ToProblem`) converts `ErrorType` to the HTTP status and an RFC 9457 Problem Details body.
- Exceptions are reserved for the unexpected. `GlobalExceptionHandler` logs the full detail and returns a generic 500 without leaking internals.
- Request contracts use strings for enum-like fields, so bad values reach the validator and produce friendly field messages instead of serializer errors.
- Validation returns one message per field, keyed to the UI's input ids.

## Consequences

- Each failure mode is visible in the code and has one HTTP mapping.
- Clients can branch on `code` instead of parsing English text.
- The UI can attach server messages to the correct fields, which makes server-side validation as accessible as client-side validation.
