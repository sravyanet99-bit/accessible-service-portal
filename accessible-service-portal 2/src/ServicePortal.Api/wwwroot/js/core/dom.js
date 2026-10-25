/**
 * Tiny DOM helpers. `h()` builds elements from data, and only ever inserts text with
 * text nodes — never innerHTML — so server or user content can't inject markup (XSS-safe by construction).
 */

export const $ = (selector, root = document) => root.querySelector(selector);
export const $$ = (selector, root = document) => [...root.querySelectorAll(selector)];

/**
 * h('button', { className: 'x', type: 'button', attrs: { 'aria-label': 'Close' }, on: { click } }, 'Text')
 * @param {string} tag
 * @param {Record<string, any>} [props]
 * @param {...(Node|string|number|null|undefined|false)} children
 */
export function h(tag, props = {}, ...children) {
  const element = document.createElement(tag);
  const { attrs, dataset, on, ...properties } = props;

  for (const [key, value] of Object.entries(properties)) {
    if (value !== undefined && value !== null) element[key] = value;
  }
  for (const [key, value] of Object.entries(attrs ?? {})) {
    if (value !== undefined && value !== null && value !== false) element.setAttribute(key, value === true ? '' : String(value));
  }
  Object.assign(element.dataset, dataset ?? {});
  for (const [event, handler] of Object.entries(on ?? {})) {
    element.addEventListener(event, handler);
  }

  element.append(...children.flat().filter((child) => child !== null && child !== undefined && child !== false)
    .map((child) => (child instanceof Node ? child : document.createTextNode(String(child)))));
  return element;
}

/** Text that is read by screen readers but not shown. */
export const srOnly = (text) => h('span', { className: 'visually-hidden' }, text);

export function debounce(fn, waitMs) {
  let timer;
  const debounced = (...args) => {
    window.clearTimeout(timer);
    timer = window.setTimeout(() => fn(...args), waitMs);
  };
  debounced.cancel = () => window.clearTimeout(timer);
  return debounced;
}

/** Adds/removes one id in a space-separated ARIA id list (aria-describedby etc.) without clobbering others. */
export function toggleIdRef(element, attribute, id, present) {
  const ids = new Set((element.getAttribute(attribute) ?? '').split(/\s+/).filter(Boolean));
  if (present) ids.add(id); else ids.delete(id);
  if (ids.size) element.setAttribute(attribute, [...ids].join(' '));
  else element.removeAttribute(attribute);
}

export const plural = (count, singular, pluralForm = `${singular}s`) => `${count} ${count === 1 ? singular : pluralForm}`;
