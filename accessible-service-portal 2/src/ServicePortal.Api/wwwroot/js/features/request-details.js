/**
 * Request details dialog: full record, status history, status changes and deletion.
 *
 * - Only valid next statuses are offered (the server sends allowedTransitions), so users can't pick
 *   a choice that is guaranteed to fail.
 * - Every change sends the ETag in If-Match. If someone else changed the request meanwhile (412),
 *   the latest version is loaded and the user is told plainly what happened.
 * - Focus: opens on the title; after an update stays on the status control; after a delete moves to
 *   the queue heading, because the row that opened the dialog no longer exists.
 */
import { $, h, srOnly } from '../core/dom.js';
import { announcer } from '../core/announcer.js';
import { openModal } from '../core/dialog.js';
import { timeElement, statusMark, priorityMark } from '../core/format.js';
import { ApiError } from '../core/api-client.js';

export function createRequestDetails({ api, labels, onChanged, fallbackFocus }) {
  const dialog = $('#details-dialog');
  const content = $('#details-content');
  const confirmDialog = $('#confirm-dialog');

  /** @type {{ request: any, etag: string, modal: ReturnType<typeof openModal> } | null} */
  let current = null;

  async function open(id, trigger) {
    let loaded;
    try {
      loaded = await api.getRequest(id);
    } catch (error) {
      announcer.assertive(error instanceof ApiError && error.status === 404
        ? 'That request no longer exists. The list has been refreshed.'
        : `The request could not be opened. ${error.message}`);
      onChanged();
      return;
    }

    current = { request: loaded.data, etag: loaded.etag, modal: null };
    render();
    current.modal = openModal(dialog, {
      initialFocus: $('#details-title'),
      // The list may re-render while the dialog is open; return to the same request's row if it still exists.
      returnFocus: () => document.querySelector(`#requests-body tr[data-id="${id}"] .row-link`)
        ?? (trigger?.isConnected ? trigger : null),
      fallbackFocus,
    });
    current.modal.closed.then(() => { current = null; });
  }

  function render({ notice = null } = {}) {
    const { request } = current;

    const statusSection = request.allowedTransitions.length
      ? renderStatusForm(request, notice)
      : h('p', {}, `This request is ${labels.status(request.status).toLowerCase()} and can no longer change status.`);

    content.replaceChildren(
      h('div', { className: 'details__header' },
        h('div', {},
          h('p', { className: 'details__reference' }, request.reference),
          h('h2', { id: 'details-title', className: 'dialog__title', tabIndex: -1 }, request.subject),
          statusMark(request.status, labels.status(request.status))),
        h('button', {
          type: 'button',
          className: 'details__close',
          attrs: { 'aria-label': 'Close details' },
          on: { click: () => current?.modal?.close('close') },
        }, h('span', { attrs: { 'aria-hidden': 'true' } }, '×'))),

      h('dl', { className: 'details__facts' },
        fact('Category', request.category),
        fact('Priority', priorityMark(request.priority, labels.priority(request.priority))),
        fact('Requester', request.requester.name),
        fact('Email', h('a', { href: `mailto:${request.requester.email}` }, request.requester.email)),
        request.requester.phone ? fact('Phone', h('a', { href: `tel:${request.requester.phone.replace(/[^\d+]/g, '')}` }, request.requester.phone)) : null,
        fact('Submitted', timeElement(request.createdAt)),
        fact('Last updated', timeElement(request.updatedAt))),

      h('section', { className: 'details__section', attrs: { 'aria-labelledby': 'details-description' } },
        h('h3', { id: 'details-description' }, 'Description'),
        h('p', { className: 'details__description' }, request.description)),

      h('section', { className: 'details__section', attrs: { 'aria-labelledby': 'details-history' } },
        h('h3', { id: 'details-history' }, 'History'),
        h('ol', { className: 'timeline' }, request.history.map((entry) => h('li', {},
          entry.from
            ? `Changed from ${labels.status(entry.from)} to ${labels.status(entry.to)}`
            : `Submitted as ${labels.status(entry.to)}`,
          timeElement(entry.changedAt))))),

      h('section', { className: 'details__section', attrs: { 'aria-labelledby': 'details-status' } },
        h('h3', { id: 'details-status' }, 'Status'),
        statusSection),

      h('div', { className: 'danger-zone' },
        h('button', {
          type: 'button',
          id: 'details-delete',
          className: 'button button--danger-outline',
          on: { click: (event) => confirmDelete(event.currentTarget) },
        }, 'Delete request', srOnly(` ${request.reference}`))));
  }

  function fact(term, value) {
    return [h('dt', {}, term), h('dd', {}, value)];
  }

  function renderStatusForm(request, notice) {
    const select = h('select', { id: 'details-status-select', name: 'status' },
      request.allowedTransitions.map((value) => new Option(labels.status(value), value)));
    const errorId = 'details-status-error';
    const error = h('p', {
      id: errorId,
      className: 'field-error',
      hidden: !notice,
      attrs: { role: 'alert' },
    }, notice ?? '');
    if (notice) select.setAttribute('aria-describedby', `details-status-hint ${errorId}`);
    else select.setAttribute('aria-describedby', 'details-status-hint');

    const button = h('button', { type: 'submit', className: 'button button--primary' },
      h('span', { className: 'spinner', attrs: { 'aria-hidden': 'true' } }),
      h('span', { className: 'button__label' }, 'Update status'));

    return h('form', {
      className: 'status-form',
      noValidate: true,
      on: { submit: (event) => { event.preventDefault(); changeStatus(select.value, button); } },
    },
    h('div', { className: 'field' },
      h('label', { htmlFor: select.id }, 'Change status to'),
      h('p', { id: 'details-status-hint', className: 'hint' }, 'Only the next valid steps are listed.'),
      error,
      select),
    button);
  }

  async function changeStatus(status, button) {
    if (!current || button.getAttribute('aria-disabled') === 'true') return;
    setBusy(button, true, 'Updating…');
    const { request } = current;

    try {
      const { data, etag } = await api.changeStatus(request.id, status, current.etag);
      current.request = data;
      current.etag = etag;
      render();
      ($('#details-status-select') ?? $('#details-title')).focus();
      announcer.polite(`${data.reference} status changed to ${labels.status(data.status)}.`);
      onChanged();
    } catch (error) {
      await handleWriteError(error, 'The status was not changed.');
      ($('#details-status-select') ?? $('#details-title'))?.focus();
    }
  }

  async function confirmDelete(trigger) {
    if (!current) return;
    const { request } = current;
    $('#confirm-title').textContent = `Delete ${request.reference}?`;
    $('#confirm-body').textContent =
      `“${request.subject}” and its history will be permanently deleted. This can't be undone.`;

    const confirmation = openModal(confirmDialog, { returnFocus: trigger });
    if ((await confirmation.closed) !== 'confirm' || !current) return;

    setBusy(trigger, true, 'Deleting…');
    try {
      await api.deleteRequest(request.id, current.etag);
      current.modal.close('deleted', fallbackFocus);
      announcer.polite(`${request.reference} deleted.`);
      onChanged();
    } catch (error) {
      await handleWriteError(error, 'The request was not deleted.');
      $('#details-delete')?.focus();
    }
  }

  /** Turns API failures into a clear, actionable message inside the dialog. */
  async function handleWriteError(error, prefix) {
    if (!(error instanceof ApiError)) throw error;

    if (error.status === 412 || error.status === 404) {
      try {
        const latest = await api.getRequest(current.request.id);
        current.request = latest.data;
        current.etag = latest.etag;
        render({ notice: `${prefix} Someone else updated this request while you had it open. The latest version is shown; check it and try again.` });
      } catch {
        current.modal.close('gone', fallbackFocus);
        announcer.assertive(`${prefix} The request has been deleted by someone else.`);
      }
      onChanged();
      return;
    }

    render({ notice: `${prefix} ${error.message}` });
  }

  function setBusy(button, busy, label) {
    button.setAttribute('aria-disabled', String(busy));
    const text = button.querySelector('.button__label');
    if (text && busy) text.textContent = label;
  }

  return { open };
}
