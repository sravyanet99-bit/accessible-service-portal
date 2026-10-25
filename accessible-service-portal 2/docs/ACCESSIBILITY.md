# Accessibility conformance report

**Product:** Client Service Desk (web)
**Standard:** WCAG 2.2 Level A and AA. Section 508 (Revised 2018) incorporates WCAG 2.0 A/AA by reference, so this report also covers the 508 web requirements (E205.4).
**Format:** modelled on the VPAT® 2.5 WCAG edition.

## Evaluation methods

| Method | Tooling | Where |
|---|---|---|
| Automated scan | axe-core 4.x, WCAG 2.0–2.2 A/AA rules, light and dark themes, error and dialog states | `tests/e2e/accessibility.spec.js` |
| Keyboard journeys | Playwright (Chromium, Firefox): skip link, error summary, dialog focus return, sorting, submission | `tests/e2e/accessibility.spec.js` |
| Markup regression | xUnit: landmarks, labels, live regions, table semantics, no inline code (CSP) | `tests/ServicePortal.Api.IntegrationTests/AccessibilityMarkupTests.cs` |
| Contrast | Every foreground/background pair calculated for both themes | table below |
| Manual | NVDA + Firefox, JAWS + Chrome, VoiceOver + Safari (macOS/iOS), 400% zoom, Windows High Contrast | checklist below |

## Conformance levels

**Supports**: meets the criterion. **N/A**: no content of that type.

## Table 1: Level A

| Criterion | Level | Remarks and implementation |
|---|---|---|
| 1.1.1 Non-text Content | Supports | Decorative graphics (logo mark, status shapes, priority bars, sort arrows) are `aria-hidden` or CSS-generated; the meaning is always also in text. |
| 1.2.x Time-based Media | N/A | No audio or video. |
| 1.3.1 Info and Relationships | Supports | Landmarks; logical heading outline; `<label for>` on every control; `fieldset`/`legend` for grouped fields and radios; table `caption`, `th scope="col"`, row headers; `dl` for record details; `ol` for history. |
| 1.3.2 Meaningful Sequence | Supports | DOM order matches visual order at all breakpoints. |
| 1.3.3 Sensory Characteristics | Supports | Instructions never rely on shape, position or colour. |
| 1.4.1 Use of Color | Supports | Status uses shape + colour + word; priority uses bars + word; links are underlined; errors carry an icon, a bold label and a border. |
| 1.4.2 Audio Control | N/A | No audio. |
| 2.1.1 Keyboard | Supports | Only native interactive elements are used; every function is keyboard-operable; no pointer-only handlers. |
| 2.1.2 No Keyboard Trap | Supports | Native modal `<dialog>`: Escape closes it and focus returns. |
| 2.1.4 Character Key Shortcuts | Supports | No single-key shortcuts. |
| 2.2.1 Timing Adjustable | Supports | No time limits; forms never expire. |
| 2.2.2 Pause, Stop, Hide | Supports | Nothing moves, blinks or auto-updates. |
| 2.3.1 Three Flashes | Supports | No flashing content. |
| 2.4.1 Bypass Blocks | Supports | A skip link is the first focusable element; `main` has `tabindex="-1"` so focus actually moves. |
| 2.4.2 Page Titled | Supports | Descriptive title, prefixed with "Error:" while the form has errors. |
| 2.4.3 Focus Order | Supports | Focus rules are documented below and covered by tests; no positive `tabindex`. |
| 2.4.4 Link Purpose (In Context) | Supports | Link and button text is descriptive; repeated actions include hidden context ("Delete request SR-001009"). |
| 2.5.1 Pointer Gestures | Supports | Single-pointer activation only. |
| 2.5.2 Pointer Cancellation | Supports | Native buttons activate on release. |
| 2.5.3 Label in Name | Supports | Accessible names begin with the visible label. |
| 2.5.4 Motion Actuation | N/A | No motion input. |
| 3.1.1 Language of Page | Supports | `<html lang="en">`. |
| 3.2.1 On Focus | Supports | Focus never triggers a change of context. |
| 3.2.2 On Input | Supports | Filters update results in place without moving focus; nothing navigates on input. |
| 3.2.6 Consistent Help | Supports | Help and the feedback route sit in the same place on every view. |
| 3.3.1 Error Identification | Supports | Error summary (focused, linked) plus an inline message per field; `aria-invalid="true"`. |
| 3.3.2 Labels or Instructions | Supports | Visible labels; hints linked with `aria-describedby`; required/optional stated up front. |
| 3.3.7 Redundant Entry | Supports | Autocomplete tokens let browsers fill known details; nothing is asked twice. |
| 4.1.2 Name, Role, Value | Supports | Native semantics plus `aria-expanded`/`aria-controls` (disclosure), `aria-sort` (columns), `aria-pressed` (status filters), `aria-current` (pagination), `aria-busy` (loading). |

## Table 2: Level AA

| Criterion | Level | Remarks and implementation |
|---|---|---|
| 1.3.4 Orientation | Supports | Works in portrait and landscape. |
| 1.3.5 Identify Input Purpose | Supports | `autocomplete="name"`, `"email"`, `"tel"`. |
| 1.4.3 Contrast (Minimum) | Supports | All text ≥ 4.5:1 in both themes; the lowest pair is 5.6:1. |
| 1.4.4 Resize Text | Supports | `rem`-based sizing; zoom never disabled. |
| 1.4.5 Images of Text | Supports | None used. |
| 1.4.10 Reflow | Supports | Single column at 320 CSS px with no page-level horizontal scroll (tested); the data table scrolls inside a labelled, focusable region. |
| 1.4.11 Non-text Contrast | Supports | Control borders ≥ 3.7:1; focus indicator is a dark ring plus a yellow halo. |
| 1.4.12 Text Spacing | Supports | No fixed-height text containers; overrides don't clip content. |
| 1.4.13 Content on Hover or Focus | N/A | No hover- or focus-triggered popups. |
| 2.4.5 Multiple Ways | Supports | Primary navigation plus search and filters. |
| 2.4.6 Headings and Labels | Supports | Descriptive headings and labels throughout. |
| 2.4.7 Focus Visible | Supports | `:focus-visible` ring on every interactive element. |
| 2.4.11 Focus Not Obscured (Minimum) | Supports | No sticky overlays; `scroll-padding-top` keeps focused items in view. |
| 2.5.7 Dragging Movements | N/A | No dragging. |
| 2.5.8 Target Size (Minimum) | Supports | All targets ≥ 44 × 44 CSS px (the minimum is 24). |
| 3.1.2 Language of Parts | Supports | Single language. |
| 3.2.3 Consistent Navigation | Supports | Single view; navigation order is fixed. |
| 3.2.4 Consistent Identification | Supports | An action keeps the same name throughout ("Delete request" in the button, dialog and announcement). |
| 3.3.3 Error Suggestion | Supports | Messages say how to fix the problem ("…like name@example.com"). |
| 3.3.4 Error Prevention (Legal, Financial, Data) | Supports | Deletion requires confirmation, and the safe choice has initial focus; stale edits are rejected (412) and explained. |
| 3.3.8 Accessible Authentication (Minimum) | N/A | No authentication in this sample. |
| 4.1.3 Status Messages | Supports | Polite and assertive live regions, present from page load, announce results, saves, deletions and failures. |

## Focus management rules

| Action | Focus goes to | Reason |
|---|---|---|
| Submit with errors | Error summary | The problem is announced immediately, and the links lead to each field. |
| Activate an error-summary link | The field (first radio for the group) | A plain anchor only scrolls; it doesn't move focus. |
| Submit succeeds | Confirmation banner | The outcome is read, and its "View request" button is next. |
| Change page | "Requests" heading | Reading starts at the new results. |
| Search, filter, sort | Stays put; result announced | The user is still working in that control. |
| Open details | Dialog title | Context first, then the controls. |
| Close details | The row's link, even if the list re-rendered | The user returns to where they were. |
| Update status | The status control | The user can continue; the result is announced. |
| Delete | "Requests" heading | The row no longer exists; focus never falls to `<body>`. |

## Contrast verification

| Pair | Light | Dark |
|---|---|---|
| Body text on page | 12.5:1 | 15.0:1 |
| Secondary text on page | 6.7:1 | 8.8:1 |
| Action colour on surface | 6.9:1 | 8.6:1 |
| Primary button text | 6.9:1 | 9.2:1 |
| Error text on error tint | 6.4:1 | 9.4:1 |
| Status: Open / In progress / Resolved / Cancelled on page | 7.5 / 6.9 / 6.8 / 5.6:1 | 9.8 / 10.3 / 10.4 / 9.9:1 |
| Control borders (non-text, 3:1) | 3.7:1 | 5.6:1 |

## Manual test checklist

- [ ] Complete every task with the keyboard only; focus is always visible and in a logical order.
- [ ] NVDA + Firefox: submit with errors, fix them, submit; sort, filter and page through; open, update and delete a request.
- [ ] JAWS + Chrome: as above, and confirm table navigation (Ctrl+Alt+arrows) announces headers.
- [ ] VoiceOver + Safari (macOS and iOS): rotor lists landmarks, headings and form controls correctly.
- [ ] 400% zoom / 320 px width: no loss of content or function, and no page-level sideways scrolling.
- [ ] Windows High Contrast: boundaries, focus, selected states and status shapes remain visible.
- [ ] Reduced motion: dialog and indicator animations are suppressed.
