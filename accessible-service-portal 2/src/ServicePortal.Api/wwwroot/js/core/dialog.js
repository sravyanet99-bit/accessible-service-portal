/**
 * Modal dialogs on the native <dialog> element.
 *
 * showModal() gives us, for free: a focus trap, Escape to close, and an inert background that
 * assistive technology can't wander into. This wrapper adds the part browsers don't:
 * returning focus to a sensible place when the dialog closes (WCAG 2.4.3 Focus Order).
 */

/**
 * @param {HTMLDialogElement} dialog
 * @param {{ initialFocus?: HTMLElement | null,
 *           returnFocus?: Element | null | (() => Element | null),
 *           fallbackFocus?: HTMLElement | null }} [options]
 *        returnFocus may be a function, resolved at close time — useful when the list re-rendered
 *        while the dialog was open and the original trigger was replaced by an equivalent element.
 * @returns {{ closed: Promise<string>, close: (value?: string, focusTarget?: HTMLElement) => void }}
 */
export function openModal(dialog, { initialFocus = null, returnFocus = document.activeElement, fallbackFocus = null } = {}) {
  let focusOverride = null;

  const closed = new Promise((resolve) => {
    const onBackdropClick = (event) => {
      // A click on the <dialog> itself (not its content) is a click on the backdrop.
      if (event.target === dialog) dialog.close('cancel');
    };

    dialog.addEventListener('click', onBackdropClick);
    dialog.addEventListener('close', () => {
      dialog.removeEventListener('click', onBackdropClick);
      const resolved = typeof returnFocus === 'function' ? returnFocus() : returnFocus;
      const target = focusOverride
        ?? (resolved instanceof HTMLElement && resolved.isConnected ? resolved : fallbackFocus);
      // The element that opened the dialog may be gone (e.g. its row was deleted): never drop focus to <body>.
      target?.focus();
      resolve(dialog.returnValue);
    }, { once: true });
  });

  dialog.returnValue = '';
  dialog.showModal();
  initialFocus?.focus();

  return {
    closed,
    close(value = 'close', focusTarget) {
      focusOverride = focusTarget ?? null;
      if (dialog.open) dialog.close(value);
    },
  };
}
