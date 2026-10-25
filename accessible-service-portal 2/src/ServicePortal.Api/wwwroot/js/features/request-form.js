/**
 * New-request form.
 *
 * Validation pattern (based on the GOV.UK Design System, tested with screen-reader users):
 *  - Validate on submit, not while typing — premature errors are noisy and confusing.
 *  - On failure: an error summary at the top takes focus and links to each field; each field shows
 *    its message inline, is marked aria-invalid, and has the message added to aria-describedby.
 *  - Once a field has shown an error, it re-checks on change, so fixing it clears the message.
 *  - The page title gains an "Error:" prefix, because the title is the first thing announced.
 *  - The server re-validates everything and its messages are displayed the same way.
 */
import { $, h, debounce, toggleIdRef } from '../core/dom.js';
import { announcer } from '../core/announcer.js';
import { ApiError } from '../core/api-client.js';

const FIELDS = ['subject', 'category', 'priority', 'description', 'requesterName', 'requesterEmail', 'requesterPhone'];
const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const PHONE = /^\+?[0-9()\-.\s]{7,20}$/;

export function createRequestForm({ api, metadata, onSubmitted, onView }) {
  const form = $('#request-form');
  const submitButton = $('#submit-request');
  const summary = $('#error-summary');
  const summaryList = $('#error-summary-list');
  const banner = $('#submission-banner');
  const description = $('#description');
  const counter = $('#description-count');
  const limits = metadata.limits;
  const baseTitle = document.title.replace(/^Error: /, '');
  const fieldsWithErrors = new Set();
  let lastSubmitted = null;
  let submitting = false;

  buildChoices();
  updateCounter({ announce: false });

  // ---- Build from server metadata --------------------------------------------------------------

  function buildChoices() {
    $('#category').append(...metadata.categories.map((name) => new Option(name, name)));

    $('#priority-options').replaceChildren(...metadata.priorities.map((option) => {
      const id = `priority-${option.value.toLowerCase()}`;
      const descriptionId = `${id}-desc`;
      return h('div', { className: 'choice' },
        h('input', {
          id, type: 'radio', name: 'priority', value: option.value,
          attrs: { 'aria-describedby': descriptionId },
        }),
        h('label', { htmlFor: id, className: 'choice__label' }, option.label),
        h('span', { id: descriptionId, className: 'choice__desc' }, option.description));
    }));

    // Clicking anywhere on the option card selects it (bigger target, WCAG 2.5.8).
    $('#priority-options').addEventListener('click', (event) => {
      const card = event.target.closest('.choice');
      if (card && event.target.tagName !== 'INPUT' && event.target.tagName !== 'LABEL') {
        const radio = card.querySelector('input');
        radio.checked = true;
        radio.focus();
        radio.dispatchEvent(new Event('change', { bubbles: true }));
      }
    });
  }

  // ---- Validation --------------------------------------------------------------------------------

  function read() {
    const data = new FormData(form);
    const value = (name) => (data.get(name) ?? '').toString().trim();
    return {
      subject: value('subject'),
      category: value('category'),
      priority: value('priority'),
      description: value('description'),
      requesterName: value('requesterName'),
      requesterEmail: value('requesterEmail'),
      requesterPhone: value('requesterPhone') || null,
    };
  }

  /** Mirrors the server rules for instant feedback; the server remains the authority. */
  function validate(values) {
    const errors = {};
    if (!values.subject) errors.subject = 'Enter a subject.';
    else if (values.subject.length > limits.subjectMaxLength) errors.subject = `Subject must be ${limits.subjectMaxLength} characters or fewer.`;

    if (!values.category) errors.category = 'Choose a category.';
    if (!values.priority) errors.priority = 'Choose a priority.';

    if (!values.description) errors.description = 'Describe your request.';
    else if (values.description.length < limits.descriptionMinLength) {
      errors.description = `Description must be at least ${limits.descriptionMinLength} characters. Include what you were trying to do and what happened.`;
    } else if (values.description.length > limits.descriptionMaxLength) {
      errors.description = `Description must be ${limits.descriptionMaxLength} characters or fewer.`;
    }

    if (!values.requesterName) errors.requesterName = 'Enter your full name.';
    else if (values.requesterName.length > limits.nameMaxLength) errors.requesterName = `Full name must be ${limits.nameMaxLength} characters or fewer.`;

    if (!values.requesterEmail) errors.requesterEmail = 'Enter your email address.';
    else if (!EMAIL.test(values.requesterEmail) || values.requesterEmail.length > limits.emailMaxLength) {
      errors.requesterEmail = 'Enter an email address in the correct format, like name@example.com.';
    }

    if (values.requesterPhone && !PHONE.test(values.requesterPhone)) {
      errors.requesterPhone = 'Enter a phone number using digits, spaces, dashes or brackets, like 803 555 0142.';
    }
    return errors;
  }

  // ---- Error display -----------------------------------------------------------------------------

  /** The control that receives focus for a field (the first radio for the radio group). */
  const focusTarget = (field) => (field === 'priority'
    ? form.querySelector('input[name="priority"]:checked') ?? form.querySelector('input[name="priority"]')
    : document.getElementById(field));

  /** The element that carries aria-invalid / aria-describedby (the fieldset for radios). */
  const describedElement = (field) => document.getElementById(field);

  function setFieldError(field, message) {
    const wrapper = form.querySelector(`[data-field="${field}"]`);
    const errorEl = document.getElementById(`${field}-error`);
    const element = describedElement(field);
    if (!wrapper || !errorEl || !element) return;

    wrapper.classList.toggle('has-error', Boolean(message));
    errorEl.hidden = !message;
    errorEl.replaceChildren(...(message ? [h('span', { className: 'visually-hidden' }, 'Error: '), message] : []));
    if (message) element.setAttribute('aria-invalid', 'true');
    else element.removeAttribute('aria-invalid');
    toggleIdRef(element, 'aria-describedby', errorEl.id, Boolean(message));

    if (message) fieldsWithErrors.add(field); else fieldsWithErrors.delete(field);
  }

  function clearErrors() {
    for (const field of FIELDS) setFieldError(field, null);
    summary.hidden = true;
    summaryList.replaceChildren();
    document.title = baseTitle;
  }

  /**
   * @param {Record<string, string | string[]>} errors   field → message(s)
   * @param {string[]} [general]                         messages not tied to a field
   */
  function showErrors(errors, general = []) {
    clearErrors();
    const messageOf = (value) => (Array.isArray(value) ? value[0] : value);

    for (const message of general) {
      summaryList.append(h('li', {}, message));
    }

    for (const field of FIELDS) {
      if (!errors[field]) continue;
      const message = messageOf(errors[field]);
      setFieldError(field, message);
      const target = focusTarget(field);
      summaryList.append(h('li', {}, h('a', {
        href: `#${target.id}`,
        on: {
          click: (event) => {
            event.preventDefault();               // a plain anchor only scrolls; move focus too
            target.focus();
          },
        },
      }, message)));
    }

    // Server errors for fields this form doesn't know still reach the user.
    for (const [key, value] of Object.entries(errors)) {
      if (!FIELDS.includes(key)) summaryList.append(h('li', {}, messageOf(value)));
    }

    summary.hidden = false;
    document.title = `Error: ${baseTitle}`;
    summary.focus();
  }

  // Re-check a field that has already shown an error as soon as it changes.
  form.addEventListener('change', (event) => {
    const field = event.target.name;
    if (!fieldsWithErrors.has(field)) return;
    const message = validate(read())[field];
    if (!message) setFieldError(field, null);
    if (fieldsWithErrors.size === 0) {
      summary.hidden = true;
      document.title = baseTitle;
    }
  });

  // ---- Character counter ------------------------------------------------------------------------

  const announceCount = debounce((text) => announcer.polite(text), 1000);

  function updateCounter({ announce = true } = {}) {
    const max = limits.descriptionMaxLength;
    const remaining = max - description.value.length;
    const text = remaining >= 0
      ? `${remaining} of ${max} characters remaining`
      : `${Math.abs(remaining)} characters too many`;
    counter.textContent = text;
    counter.classList.toggle('is-over', remaining < 0);
    // Announce only near or over the limit, and only once typing pauses — never per keystroke.
    if (announce && remaining <= max * 0.1) announceCount(text);
  }

  description.addEventListener('input', () => updateCounter());

  // ---- Submit -----------------------------------------------------------------------------------

  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    if (submitting) return;                        // aria-disabled doesn't block activation by itself

    banner.hidden = true;
    const values = read();
    const errors = validate(values);
    if (Object.keys(errors).length) {
      showErrors(errors);
      return;
    }

    setSubmitting(true);
    try {
      const created = await api.createRequest(values);
      clearErrors();
      form.reset();
      updateCounter({ announce: false });
      lastSubmitted = created;
      $('#submission-title').textContent = `Request ${created.reference} submitted`;
      banner.hidden = false;
      banner.focus();                              // the confirmation is read, and the user is oriented
      onSubmitted(created);
    } catch (error) {
      if (error instanceof ApiError && error.status === 400 && Object.keys(error.fieldErrors).length) {
        showErrors(error.fieldErrors);
      } else if (error instanceof ApiError && error.status === 429) {
        showErrors({}, [error.problem?.detail ?? 'Too many requests. Wait a minute, then try again.']);
      } else {
        const reference = error instanceof ApiError && error.traceId ? ` If it keeps happening, quote reference ${error.traceId}.` : '';
        showErrors({}, [`Your request was not sent. ${error.message}${reference}`]);
      }
    } finally {
      setSubmitting(false);
    }
  });

  form.addEventListener('reset', () => {
    clearErrors();
    window.setTimeout(() => updateCounter({ announce: false }), 0);
  });

  $('#view-submitted').addEventListener('click', (event) => {
    if (lastSubmitted) onView(lastSubmitted.id, event.currentTarget);
  });

  function setSubmitting(value) {
    submitting = value;
    // aria-disabled (not disabled) keeps focus on the button while the request is in flight.
    submitButton.setAttribute('aria-disabled', String(value));
    submitButton.querySelector('.button__label').textContent = value ? 'Submitting…' : 'Submit request';
    form.setAttribute('aria-busy', String(value));
  }
}
