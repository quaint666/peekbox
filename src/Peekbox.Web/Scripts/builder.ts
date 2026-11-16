// The builder page in the browser. The page works without this file: the item filters are a plain GET form (without
// scripts a "Lọc" button sends it), and "Thêm", − and + are plain POST forms that reload the page. This file sends the
// filters as soon as the checkbox or the sort menu changes (the search box with Enter, not on every letter), and sends
// the add forms in the background so the page does not jump.

const filters = document.querySelector<HTMLFormElement>('form.item-filters');

// Arrow keys on a closed menu change its value at once. Sending then would reload the page on the first press, so a
// keyboard user could never reach the third choice. A change made with keys therefore waits for Enter, or for the
// visitor to leave the menu; a choice made with the mouse is sent at once.
let changedByKeys = false;

filters?.addEventListener('keydown', (event) => {
  if (!(event.target instanceof HTMLSelectElement)) {
    return;
  }

  if (event.key === 'Enter') {
    event.preventDefault();
    filters.requestSubmit();
  } else if (event.key !== 'Tab' && event.key !== 'Shift') {
    changedByKeys = true;
  }
});

filters?.addEventListener('pointerdown', (event) => {
  if (event.target instanceof HTMLSelectElement) {
    changedByKeys = false;
  }
});

filters?.addEventListener('change', (event) => {
  const target = event.target;
  if (target instanceof HTMLSelectElement && changedByKeys) {
    target.dataset['pending'] = 'true';
    return;
  }

  if ((target instanceof HTMLInputElement && target.type === 'checkbox') || target instanceof HTMLSelectElement) {
    filters.requestSubmit();
  }
});

filters?.addEventListener('focusout', (event) => {
  const target = event.target;
  if (target instanceof HTMLSelectElement && target.dataset['pending'] === 'true') {
    filters.requestSubmit();
  }
});

// ---- "Thêm", − and + (F20) ----
// The form is sent in the background. The server answers as it does without scripts: it saves, then redirects to the
// builder page. This file reads the new page and swaps in what changed: every card's add control (one item can change
// "Vượt X" on the others), "Hộp của bạn", the phone bar and the packaging note. It never works out a price: every
// number it shows comes from the server's page.

const status = document.getElementById('box-status');
let sending = false;

type Place = 'card' | 'panel';

function placeOf(form: HTMLFormElement): Place {
  return form.closest('#hop-cua-ban') ? 'panel' : 'card';
}

function controlRoot(place: Place, slug: string, root: ParentNode = document): HTMLElement | null {
  return root.querySelector<HTMLElement>(place === 'card' ? `#mon-${CSS.escape(slug)}` : `#hop-${CSS.escape(slug)}`);
}

function nameOf(slug: string): string {
  const card = document.querySelector(`#mon-${CSS.escape(slug)} .item-card__name`);
  const line = document.querySelector(`#hop-${CSS.escape(slug)} .box-line__name`);
  return (card ?? line)?.textContent?.trim() ?? '';
}

function quantityIn(root: ParentNode, slug: string): number {
  const count = root.querySelector(`#mon-${CSS.escape(slug)} .qty__count, #hop-${CSS.escape(slug)} .qty__count`);
  return count ? Number(count.textContent) : 0;
}

// Replaces old with its twin from the new page (same selector), adds it after `after` when it is new, or removes it.
function swap(selector: string, fresh: Document, after?: Element | null): void {
  const old = document.querySelector(selector);
  const next = fresh.querySelector(selector);
  if (old && next) {
    old.replaceWith(document.importNode(next, true));
  } else if (old) {
    old.remove();
  } else if (next && after) {
    after.after(document.importNode(next, true));
  }
}

function applyPage(fresh: Document): void {
  for (const foot of fresh.querySelectorAll('.item-card .item-card__foot')) {
    const id = foot.closest('.item-card')?.id;
    const old = id ? document.querySelector(`#${CSS.escape(id)} .item-card__foot`) : null;
    old?.replaceWith(document.importNode(foot, true));
  }

  swap('#hop-cua-ban', fresh);
  swap('.box-bar', fresh);
  // Step 3: the lock note while the box is empty, the packaging choices once it is not. Only one is on a page.
  swap('#packaging-lock', fresh, document.querySelector('.item-grid'));
  swap('#dong-goi', fresh, document.querySelector('.item-grid'));
}

// Focus goes back to the same button; when it is gone or off (− at 1 brings "Thêm" back, + turns off at the stock or
// the budget), to the nearest one that is there. A line that left "Hộp của bạn" sends focus to the panel's title.
function refocus(place: Place, slug: string, step: string): void {
  const root = controlRoot(place, slug);
  for (const wanted of [step, 'add', 'plus', 'minus']) {
    const button = root?.querySelector<HTMLButtonElement>(`[data-qty-step="${wanted}"]`);
    if (button && !button.disabled) {
      button.focus();
      return;
    }
  }

  const title = document.getElementById(place === 'panel' ? 'box-title' : 'items-title');
  if (title) {
    title.tabIndex = -1;
    title.focus();
  }
}

function announce(text: string): void {
  if (!status) {
    return;
  }

  // Emptied first, so the same words twice in a row are read again.
  status.textContent = '';
  window.setTimeout(() => {
    status.textContent = text;
  }, 50);
}

function message(name: string, asked: number, now: number): string {
  const left = document.querySelector('#hop-cua-ban .box-sums__row--left dd, #hop-cua-ban .box-sums__row--over dd')?.textContent?.trim() ?? '';
  const over = document.querySelector('#hop-cua-ban .box-sums__row--over') !== null;
  const money = over ? `Vượt ngân sách ${left}.` : `Còn lại ${left}.`;
  if (now !== asked) {
    return `Không thêm được ${name}. ${money}`;
  }

  return now === 0 ? `Đã bỏ ${name} khỏi hộp. ${money}` : `${name}: ${now} trong hộp. ${money}`;
}

document.addEventListener('submit', (event) => {
  const form = event.target;
  if (!(form instanceof HTMLFormElement) || !form.matches('.qty, .qty-add-form') || form.dataset['plain'] === 'true') {
    return;
  }

  event.preventDefault();
  const submitter = event.submitter instanceof HTMLButtonElement ? event.submitter : null;
  if (sending || !submitter) {
    return;   // one change at a time: a second press while the first is on its way is dropped
  }

  const slug = new FormData(form).get('slug')?.toString() ?? '';
  const place = placeOf(form);
  const step = submitter.dataset['qtyStep'] ?? 'add';
  const asked = Number(submitter.value);
  const name = nameOf(slug);
  sending = true;
  form.setAttribute('aria-busy', 'true');

  fetch(form.action, { method: 'POST', body: new FormData(form, submitter), credentials: 'same-origin' })
    .then(async (response) => {
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }

      const fresh = new DOMParser().parseFromString(await response.text(), 'text/html');
      applyPage(fresh);
      refocus(place, slug, step);
      announce(message(name, Math.max(asked, 0), quantityIn(document, slug)));
    })
    .catch(() => {
      // Anything unexpected: send the same form the plain way, so the page the server makes says what happened.
      form.dataset['plain'] = 'true';
      form.requestSubmit(submitter);
    })
    .finally(() => {
      sending = false;
      form.removeAttribute('aria-busy');
    });
});

// ---- Packaging (F33) ----
// A new choice is saved at once, in the background, and "Hộp của bạn" and the phone bar are swapped in from the page
// the server sends back (the total includes the packaging fee). The radios themselves are not swapped, so focus and the
// arrow keys stay where they are. Listened for on the document: the section is replaced when the box changes.
document.addEventListener('change', (event) => {
  const radio = event.target;
  if (!(radio instanceof HTMLInputElement) || radio.type !== 'radio' || radio.form?.id !== 'dong-goi-form') {
    return;
  }

  const form = radio.form;
  const name = radio.closest('.packaging-option')?.querySelector('.packaging-option__name')?.textContent?.trim() ?? '';
  form.setAttribute('aria-busy', 'true');
  fetch(form.action, { method: 'POST', body: new FormData(form), credentials: 'same-origin' })
    .then(async (response) => {
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }

      const fresh = new DOMParser().parseFromString(await response.text(), 'text/html');
      swap('#hop-cua-ban', fresh);
      swap('.box-bar', fresh);
      const total = document.querySelector('#hop-cua-ban .box-sums__row--total dd')?.textContent?.trim() ?? '';
      announce(`Đã chọn ${name}. Tổng cộng ${total}.`);
    })
    .catch(() => {
      // Send it the plain way: the server saves it and the reloaded page shows the choice.
      form.submit();
    })
    .finally(() => {
      form.removeAttribute('aria-busy');
    });
});

export {};
