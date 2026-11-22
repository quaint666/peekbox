// What the checkout page does in the browser. The page works without it; this only saves a round trip:
// 1. A picked ward brings its delivery fee and total at once: the page is fetched again with ?ward= (the server prices
//    it; the browser never adds money up) and the summary and the phone bar's total are swapped in.
// 2. The "Đặt hàng" buttons name the payment that is picked ("Đặt hàng và thanh toán ZaloPay").

import { markBusy } from './lib/busy.js';
import { fetchPage } from './lib/page.js';

const ward = document.querySelector<HTMLSelectElement>('#ward');
const payments = document.querySelectorAll<HTMLInputElement>('input[name="payment"]');

// Both buttons (the summary's and the phone bar's) say what the picked payment does. Read again each time: a fee swap
// brings a new summary with its own button.
function nameThePayment(): void {
  const cod = document.querySelector<HTMLInputElement>('#payment-cod')?.checked === true;
  for (const button of document.querySelectorAll<HTMLButtonElement>('.checkout-submit')) {
    const label = cod ? button.dataset['labelCod'] : button.dataset['labelZalopay'];
    if (label) button.textContent = label;
  }
}

let latest = 0; // a slower answer for an earlier choice must not overwrite a newer one

async function showFee(): Promise<void> {
  if (!ward) return;
  const asked = ++latest;
  const url = new URL(window.location.pathname, window.location.origin);
  if (ward.value) url.searchParams.set('ward', ward.value);
  const summaryBefore = document.querySelector('#checkout-summary');
  const done = summaryBefore ? markBusy(summaryBefore) : (): void => undefined;
  try {
    const { page, redirected } = await fetchPage(url, { headers: { Accept: 'text/html' } });
    if (redirected || asked !== latest) return; // a redirect means the box changed: leave the page as it is
    const summary = page.querySelector('#checkout-summary');
    const total = page.querySelector('#checkout-bar .checkout-total');
    if (summary) document.querySelector('#checkout-summary')?.replaceWith(summary);
    if (total) document.querySelector('#checkout-bar .checkout-total')?.replaceWith(total);
    nameThePayment();
  } catch {
    // offline, too slow or the server is down: the fee shows after "Đặt hàng" instead
  } finally {
    done();
  }
}

// A key press on the closed menu changes its value at once (the arrow keys), and each change would fetch and swap the
// summary, so a keyboard visitor passing three wards fetched three times. A change made with keys waits until the keys stop
// (or Enter, or the menu loses the focus); a choice made with the pointer is sent at once.
const KEY_IDLE_MS = 500;
let changedByKeys = false;
let keyTimer = 0;

function showFeeNow(): void {
  window.clearTimeout(keyTimer);
  changedByKeys = false;
  void showFee();
}

ward?.addEventListener('pointerdown', () => { changedByKeys = false; });
ward?.addEventListener('keydown', (event) => {
  if (event.key === 'Enter' && changedByKeys) {
    showFeeNow();
  } else {
    changedByKeys = true;
  }
});
ward?.addEventListener('change', () => {
  if (!changedByKeys) {
    void showFee();
    return;
  }

  window.clearTimeout(keyTimer);
  keyTimer = window.setTimeout(showFeeNow, KEY_IDLE_MS);
});
ward?.addEventListener('blur', () => { if (changedByKeys) showFeeNow(); });
for (const input of payments) input.addEventListener('change', nameThePayment);
nameThePayment();
