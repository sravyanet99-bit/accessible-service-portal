import { h, srOnly } from './dom.js';

const dateTime = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' });
const dateOnly = new Intl.DateTimeFormat(undefined, { day: 'numeric', month: 'short', year: 'numeric' });

/** A <time> element: human text for people, ISO value for machines. */
export function timeElement(iso, { withTime = true } = {}) {
  const date = new Date(iso);
  return h('time', { dateTime: iso }, (withTime ? dateTime : dateOnly).format(date));
}

export const formatDateTime = (iso) => dateTime.format(new Date(iso));

/**
 * Lookups built from the server's metadata, so labels like "In progress" are defined once, on the server.
 * @param {{ statuses: {value:string,label:string}[], priorities: {value:string,label:string}[] }} metadata
 */
export function createLabels(metadata) {
  const statuses = new Map(metadata.statuses.map((s) => [s.value, s.label]));
  const priorities = new Map(metadata.priorities.map((p) => [p.value, p.label]));
  return {
    status: (value) => statuses.get(value) ?? value,
    priority: (value) => priorities.get(value) ?? value,
  };
}

/** Status mark: shape + colour + word (WCAG 1.4.1 — never colour alone). */
export const statusMark = (status, label) =>
  h('span', { className: `mark mark--${status.toLowerCase()}` }, label);

/** Priority: bar glyph (decorative) + the word, which carries the meaning. */
export const priorityMark = (priority, label) =>
  h('span', { className: `priority priority--${priority.toLowerCase()}` },
    h('span', { className: 'priority__bars', attrs: { 'aria-hidden': 'true' } }, h('i'), h('i'), h('i')),
    label);

export { srOnly };
