/**
 * Disclosure (show/hide) pattern: a real <button> with aria-expanded and aria-controls.
 * Announced as "Keyboard shortcuts and tips, button, collapsed".
 */
import { $$ } from '../core/dom.js';

export function initDisclosures(root = document) {
  for (const toggle of $$('.disclosure__toggle', root)) {
    const panel = document.getElementById(toggle.getAttribute('aria-controls'));
    if (!panel) continue;
    toggle.addEventListener('click', () => {
      const expanded = toggle.getAttribute('aria-expanded') === 'true';
      toggle.setAttribute('aria-expanded', String(!expanded));
      panel.hidden = expanded;
    });
  }
}
