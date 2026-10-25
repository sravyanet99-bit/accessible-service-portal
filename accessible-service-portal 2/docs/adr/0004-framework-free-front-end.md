# ADR 0004: Framework-free front end

**Status:** Accepted

## Context

The front end exists to demonstrate HTML, CSS, JavaScript and accessibility fundamentals. Frameworks such as Angular or React provide component models, but they also abstract away the semantics, focus management and live-region behaviour this project is meant to show.

## Decision

- Use native ES modules with no build step: `core/` for cross-cutting utilities, `features/` for UI areas, and `main.js` as the composition root.
- Build DOM with `h()`, which inserts text only as text nodes, so the UI is XSS-safe by construction and compatible with a CSP that forbids inline scripts and styles.
- Prefer platform features to custom widgets: `<dialog>` for modals, `<button>` for every action, native `<select>` and radios, and `fieldset`/`legend`.
- Organise CSS with cascade layers (`reset → tokens → base → layout → components → utilities → preferences`) and design tokens, with dark mode, reduced motion, forced colours and print handled in the `preferences` layer.

## Consequences

- Every accessibility behaviour is explicit and reviewable in a few hundred lines.
- The same patterns map directly onto Angular (signals and services for state, CDK `LiveAnnouncer` and `FocusTrap`) when the app is ported into a larger framework codebase.
- There is no component library, so new widgets must be built with the same care.
