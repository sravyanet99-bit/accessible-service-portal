/**
 * The status ledger: a proportional bar (decorative, aria-hidden) plus a legend of real buttons.
 * Each legend button filters the queue; aria-pressed tells assistive technology which filter is on.
 */
import { $, h } from '../core/dom.js';

export function createStatusLedger({ api, onSelect }) {
  const bar = $('#ledger-bar');
  const legend = $('#ledger-legend');
  let activeStatus = null;
  let lastSummary = null;

  async function refresh() {
    try {
      lastSummary = await api.getSummary();
      render();
    } catch {
      legend.replaceChildren(h('li', { className: 'ledger__loading' }, 'The summary is unavailable right now.'));
      bar.replaceChildren();
    }
  }

  function render() {
    if (!lastSummary) return;
    const { byStatus } = lastSummary;

    // Proportional widths via flex-grow, set through the CSSOM (allowed by the strict style-src CSP,
    // unlike inline style attributes).
    bar.replaceChildren(...byStatus.filter((s) => s.count > 0).map((s) => {
      const segment = h('span', { className: `ledger__segment mark--${s.status.toLowerCase()}` });
      segment.style.flexGrow = String(s.count);
      return segment;
    }));

    legend.replaceChildren(...byStatus.map((s) => h('li', {},
      h('button', {
        type: 'button',
        className: 'ledger__filter',
        attrs: { 'aria-pressed': String(activeStatus === s.status) },
        on: { click: () => onSelect(activeStatus === s.status ? null : s.status) },
      },
      h('span', { className: 'ledger__count' }, String(s.count)),
      h('span', { className: `ledger__label mark mark--${s.status.toLowerCase()}` }, s.label)))));
  }

  return {
    refresh,
    setActive(status) {
      activeStatus = status ?? null;
      render();
    },
  };
}
