/**
 * Composition root: creates the API client, loads server metadata once, and wires the features
 * together. Features never import each other; they talk through the callbacks passed in here.
 */
import { $, h } from './core/dom.js';
import { announcer } from './core/announcer.js';
import { ApiClient } from './core/api-client.js';
import { createLabels } from './core/format.js';
import { createRequestList } from './features/request-list.js';
import { createStatusLedger } from './features/status-ledger.js';
import { createRequestDetails } from './features/request-details.js';
import { createRequestForm } from './features/request-form.js';
import { initDisclosures } from './features/disclosure.js';

const api = new ApiClient('/api/v1');

async function start() {
  initDisclosures();

  let metadata;
  try {
    metadata = await api.getMetadata();
  } catch (error) {
    showStartupFailure(error);
    return;
  }

  const labels = createLabels(metadata);
  const queueHeading = $('#queue-heading');

  // Features reference each other only through these bindings, assigned below.
  let list;
  let ledger;

  const refreshAll = () => Promise.all([list.refresh(), ledger.refresh()]);

  const details = createRequestDetails({
    api,
    labels,
    fallbackFocus: queueHeading,
    onChanged: refreshAll,
  });

  ledger = createStatusLedger({
    api,
    onSelect: (status) => list.setStatus(status),
  });

  list = createRequestList({
    api,
    metadata,
    labels,
    onOpen: (id, trigger) => details.open(id, trigger),
    onStateChange: (state) => ledger.setActive(state.status),
  });

  createRequestForm({
    api,
    metadata,
    onSubmitted: refreshAll,
    onView: (id, trigger) => details.open(id, trigger),
  });

  await refreshAll();
}

function showStartupFailure(error) {
  const notice = h('div', { className: 'notice notice--error', attrs: { role: 'alert' } },
    h('h2', { className: 'notice__title' }, 'The service is unavailable'),
    h('p', {}, `${error.message} Refresh the page to try again.`));
  $('.page-intro').after(notice);
  announcer.assertive('The service is unavailable. Refresh the page to try again.');
}

start();
