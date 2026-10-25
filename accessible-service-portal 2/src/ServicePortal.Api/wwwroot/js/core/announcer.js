/**
 * Screen-reader announcements (WCAG 4.1.3 Status Messages).
 *
 * Two live regions exist in the HTML from page load (regions added later are often ignored):
 *  - polite    → role="status": waits until the user pauses. For confirmations and result counts.
 *  - assertive → role="alert":  interrupts. Only for failures the user must know about now.
 *
 * Each message clears the region first and writes on the next frame, so repeating the same
 * text ("3 results") is still announced.
 */
import { $ } from './dom.js';

function createRegionWriter(region) {
  let timer;
  return (message) => {
    if (!region) return;
    window.clearTimeout(timer);
    region.textContent = '';
    timer = window.setTimeout(() => { region.textContent = message; }, 120);
  };
}

const writePolite = createRegionWriter($('#announcer-polite'));
const writeAssertive = createRegionWriter($('#announcer-assertive'));

export const announcer = Object.freeze({
  polite: (message) => writePolite(message),
  assertive: (message) => writeAssertive(message),
});
