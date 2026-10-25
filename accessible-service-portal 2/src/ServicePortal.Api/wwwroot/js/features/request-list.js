/**
 * The request queue: search, status filter, sortable columns and pagination — all server-side.
 *
 * Accessibility decisions:
 *  - Sorting is a <button> inside each sortable <th>; the <th> carries aria-sort (only on the active column).
 *  - Results changes are announced politely; focus stays where the user is, except after paging,
 *    when it moves to the heading so reading starts at the new results.
 *  - The table region shows aria-busy while loading.
 *  - View state lives in the URL (?q=&status=&sort=&page=) so any view can be bookmarked or shared.
 */
import { $, $$, h, srOnly, debounce, plural } from '../core/dom.js';
import { announcer } from '../core/announcer.js';
import { timeElement, statusMark, priorityMark } from '../core/format.js';

const SORT_LABELS = { reference: 'Reference', priority: 'Priority', status: 'Status', createdAt: 'Submitted date' };
const DEFAULT_DIRECTION = { reference: 'descending', priority: 'descending', status: 'ascending', createdAt: 'descending' };
const DEFAULT_SORT = { key: 'createdAt', direction: 'descending' };

/**
 * @param {{ api: import('../core/api-client.js').ApiClient, metadata: any, labels: any,
 *           onOpen: (id: number, trigger: HTMLElement) => void, onStateChange?: (state: any) => void }} deps
 */
export function createRequestList({ api, metadata, labels, onOpen, onStateChange = () => {} }) {
  const form = $('#queue-controls');
  const searchInput = $('#search');
  const statusSelect = $('#status-filter');
  const table = $('#requests-table');
  const body = $('#requests-body');
  const summary = $('#results-summary');
  const pagination = $('#pagination');
  const heading = $('#queue-heading');
  const pageSize = metadata.limits.defaultPageSize;

  const state = readStateFromUrl();
  let inFlight = null;

  for (const option of metadata.statuses) {
    statusSelect.append(new Option(option.label, option.value));
  }
  syncControls();

  // ---- Data -------------------------------------------------------------------------------

  async function load({ announce = false, focusHeading = false } = {}) {
    inFlight?.abort();                                   // drop stale responses (race-free search)
    const controller = new AbortController();
    inFlight = controller;
    table.setAttribute('aria-busy', 'true');

    try {
      const page = await api.listRequests({
        q: state.q, status: state.status, sort: sortParam(), page: state.page, pageSize,
      }, controller.signal);

      // Asked for a page that no longer exists (e.g. after deleting the last row on it)? Step back.
      if (page.totalPages > 0 && state.page > page.totalPages) {
        state.page = page.totalPages;
        return load({ announce, focusHeading });
      }

      render(page);
      writeStateToUrl();
      onStateChange({ ...state });
      if (focusHeading) heading.focus();
      if (announce) announcer.polite(summaryText(page, { includePage: true }));
      return page;
    } catch (error) {
      if (error?.name === 'AbortError') return null;
      body.replaceChildren(messageRow('The list could not be loaded. Use "Clear filters" or refresh the page to try again.'));
      summary.textContent = 'The list could not be loaded.';
      pagination.hidden = true;
      announcer.assertive(`The list of requests could not be loaded. ${error.message}`);
      return null;
    } finally {
      if (inFlight === controller) {
        table.removeAttribute('aria-busy');
        inFlight = null;
      }
    }
  }

  // ---- Rendering ----------------------------------------------------------------------------

  function render(page) {
    summary.textContent = summaryText(page);
    body.replaceChildren(...(page.items.length
      ? page.items.map(renderRow)
      : [messageRow(state.q || state.status
        ? 'No requests match these filters. Try a different search or clear the filters.'
        : 'There are no requests yet. Use the form to submit the first one.')]));
    renderPagination(page);
  }

  function renderRow(item) {
    const open = h('button', {
      type: 'button',
      className: 'row-link',
      on: { click: (event) => onOpen(item.id, event.currentTarget) },
    }, item.subject);

    return h('tr', { dataset: { id: String(item.id) } },
      // Row header: screen readers announce the reference as context for every cell in the row.
      h('th', { scope: 'row' }, item.reference),
      h('td', {}, open, h('span', { className: 'row-meta' }, `${item.category}, ${item.requesterName}`)),
      h('td', {}, priorityMark(item.priority, labels.priority(item.priority))),
      h('td', {}, statusMark(item.status, labels.status(item.status))),
      h('td', {}, timeElement(item.createdAt)));
  }

  function messageRow(text) {
    return h('tr', {}, h('td', { colSpan: 5, className: 'data-table__empty' }, text));
  }

  function renderPagination(page) {
    pagination.hidden = page.totalPages <= 1;
    if (pagination.hidden) {
      pagination.replaceChildren();
      return;
    }

    const goTo = (target) => () => {
      if (target === state.page) return;
      state.page = target;
      load({ announce: true, focusHeading: true });
    };

    const items = [
      h('li', {}, h('button', {
        type: 'button', className: 'pagination__item', disabled: page.page <= 1,
        on: { click: goTo(page.page - 1) },
      }, 'Previous', srOnly(' page'))),
    ];

    for (const entry of pageWindow(page.page, page.totalPages)) {
      if (entry === 'gap') {
        items.push(h('li', { attrs: { 'aria-hidden': 'true' }, className: 'pagination__gap' }, '…'));
        continue;
      }
      const isCurrent = entry === page.page;
      items.push(h('li', {}, h('button', {
        type: 'button',
        className: 'pagination__item',
        attrs: { 'aria-current': isCurrent ? 'page' : null },
        on: { click: goTo(entry) },
      }, srOnly('Page '), String(entry))));
    }

    items.push(h('li', {}, h('button', {
      type: 'button', className: 'pagination__item', disabled: page.page >= page.totalPages,
      on: { click: goTo(page.page + 1) },
    }, 'Next', srOnly(' page'))));

    pagination.replaceChildren(h('ul', { className: 'pagination__list' }, items));
  }

  /** e.g. 1 … 4 5 6 … 12 — always first, last and neighbours of the current page. */
  function pageWindow(current, total) {
    if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1);
    const pages = new Set([1, total, current - 1, current, current + 1].filter((p) => p >= 1 && p <= total));
    const sorted = [...pages].sort((a, b) => a - b);
    return sorted.flatMap((p, i) => (i > 0 && p - sorted[i - 1] > 1 ? ['gap', p] : [p]));
  }

  function summaryText(page, { includePage = false } = {}) {
    if (page.totalCount === 0) return 'No requests found.';
    const first = (page.page - 1) * page.pageSize + 1;
    const last = first + page.items.length - 1;
    const filters = [
      state.q ? `matching “${state.q}”` : null,
      state.status ? `with status ${labels.status(state.status)}` : null,
    ].filter(Boolean).join(' ');
    const pageText = includePage && page.totalPages > 1 ? `Page ${page.page} of ${page.totalPages}. ` : '';
    return `${pageText}Showing ${first} to ${last} of ${plural(page.totalCount, 'request')}${filters ? ` ${filters}` : ''}.`;
  }

  // ---- Sorting ------------------------------------------------------------------------------

  function sortParam() {
    return `${state.sort.direction === 'descending' ? '-' : ''}${state.sort.key}`;
  }

  function updateSortIndicators() {
    for (const th of $$('th[data-sort-key]', table)) {
      if (th.dataset.sortKey === state.sort.key) th.setAttribute('aria-sort', state.sort.direction);
      else th.removeAttribute('aria-sort');
    }
  }

  table.querySelector('thead').addEventListener('click', (event) => {
    const button = event.target.closest('button[data-sort]');
    if (!button) return;
    const key = button.dataset.sort;
    state.sort = state.sort.key === key
      ? { key, direction: state.sort.direction === 'ascending' ? 'descending' : 'ascending' }
      : { key, direction: DEFAULT_DIRECTION[key] };
    state.page = 1;
    updateSortIndicators();
    announcer.polite(`Sorted by ${SORT_LABELS[key]}, ${state.sort.direction}.`);
    load();
  });

  // ---- Filters ------------------------------------------------------------------------------

  const searchChanged = debounce(() => {
    const q = searchInput.value.trim();
    if (q === state.q) return;
    state.q = q;
    state.page = 1;
    load({ announce: true });
  }, 350);

  searchInput.addEventListener('input', searchChanged);
  form.addEventListener('submit', (event) => {          // Enter in the search box: search now
    event.preventDefault();
    searchChanged.cancel();
    state.q = searchInput.value.trim();
    state.page = 1;
    load({ announce: true });
  });

  statusSelect.addEventListener('change', () => setStatus(statusSelect.value || null, { announce: true }));

  form.addEventListener('reset', () => {
    window.setTimeout(() => {                          // after the browser has cleared the inputs
      Object.assign(state, { q: '', status: null, page: 1, sort: { ...DEFAULT_SORT } });
      updateSortIndicators();
      load({ announce: true });
    }, 0);
  });

  function setStatus(status, { announce = true } = {}) {
    state.status = status || null;
    state.page = 1;
    statusSelect.value = state.status ?? '';
    return load({ announce });
  }

  // ---- URL state ----------------------------------------------------------------------------

  function readStateFromUrl() {
    const params = new URLSearchParams(window.location.search);
    const rawSort = params.get('sort') ?? '';
    const key = rawSort.replace(/^-/, '');
    const knownStatus = metadata.statuses.some((s) => s.value === params.get('status'));
    return {
      q: (params.get('q') ?? '').slice(0, 100),
      status: knownStatus ? params.get('status') : null,
      page: Math.max(1, Number.parseInt(params.get('page') ?? '1', 10) || 1),
      sort: SORT_LABELS[key]
        ? { key, direction: rawSort.startsWith('-') ? 'descending' : 'ascending' }
        : { ...DEFAULT_SORT },
    };
  }

  function writeStateToUrl() {
    const params = new URLSearchParams();
    if (state.q) params.set('q', state.q);
    if (state.status) params.set('status', state.status);
    if (sortParam() !== '-createdAt') params.set('sort', sortParam());
    if (state.page > 1) params.set('page', String(state.page));
    const url = `${window.location.pathname}${params.size ? `?${params}` : ''}${window.location.hash}`;
    window.history.replaceState(null, '', url);
  }

  function syncControls() {
    searchInput.value = state.q;
    statusSelect.value = state.status ?? '';
    updateSortIndicators();
  }

  return {
    refresh: (options) => load(options),
    setStatus,
    get state() { return { ...state }; },
  };
}
