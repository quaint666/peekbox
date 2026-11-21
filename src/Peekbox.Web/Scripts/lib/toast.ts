// The toast (F08): one short message at the bottom of the screen for a few seconds, and the same words for a screen reader.
// The page has the two elements (Views/Shared/_Toast.cshtml); on a page without them nothing happens.

const SHOWN_FOR_MS = 3500;
let timer = 0;

export function showToast(text: string): void {
  const toast = document.getElementById('toast');
  const status = document.getElementById('toast-status');
  if (status) {
    // Emptied first, so the same words twice in a row are read again.
    status.textContent = '';
    window.setTimeout(() => { status.textContent = text; }, 50);
  }

  if (!toast) {
    return;
  }

  toast.textContent = text;
  toast.hidePopover();   // does nothing when it is not showing; shown again, it comes above whatever opened after it
  toast.showPopover();
  window.clearTimeout(timer);
  timer = window.setTimeout(() => toast.hidePopover(), SHOWN_FOR_MS);
}
