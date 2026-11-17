// The builder page in the browser. The page works without this file: the item filters are a plain GET form (without
// scripts a "Lọc" button sends it), and "Thêm", − and + are plain POST forms that reload the page. This file sends the
// filters as soon as the checkbox or the sort menu changes (the search box with Enter, not on every letter), and sends
// the add forms in the background so the page does not jump.

import { fetchPage } from './lib/page.js';
import { wireRows, type RowPosition } from './lib/rows.js';

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
// "Vượt X" on the others), "Hộp của bạn", the phone bar, the header's cart count and step 3. It never works out a price: every
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

// "Hộp của bạn" is the phone's sheet too: a sheet that is open stays open when the new panel comes in.
function swapPanel(fresh: Document): void {
  const wasOpen = document.querySelector('#hop-cua-ban:popover-open') !== null;
  swap('#hop-cua-ban', fresh);
  if (wasOpen) {
    document.getElementById('hop-cua-ban')?.showPopover();
  }
}

function applyPage(fresh: Document): void {
  for (const foot of fresh.querySelectorAll('.item-card .item-card__foot')) {
    const id = foot.closest('.item-card')?.id;
    const old = id ? document.querySelector(`#${CSS.escape(id)} .item-card__foot`) : null;
    old?.replaceWith(document.importNode(foot, true));
  }

  // "Trong hộp" on a card: its border (the class) and its status pill.
  for (const card of fresh.querySelectorAll<HTMLElement>('.item-card')) {
    const old = document.getElementById(card.id);
    if (old) {
      old.className = card.className;
      const tag = card.querySelector('.item-card__tag');
      if (tag) {
        old.querySelector('.item-card__tag')?.replaceWith(document.importNode(tag, true));
      }
    }
  }

  // The "✓ n" of each row's title and of the "Danh mục" panel.
  for (const title of fresh.querySelectorAll('.item-row__title')) {
    if (title.id) {
      swap(`#${CSS.escape(title.id)}`, fresh);
    }
  }
  swap('#danh-muc .picker-panel__categories', fresh);

  swapPanel(fresh);
  swap('.box-bar', fresh);
  swap('.cart-link', fresh);   // the header's count: the first item puts the hamper in the cart, the last takes it out
  // Step 3: the lock note while the box is empty, the packaging choices once it is not. Only one is on a page; the new
  // one goes where the old one was.
  const before = document.querySelector('#packaging-lock, #dong-goi')?.previousElementSibling ?? null;
  swap('#packaging-lock', fresh, before);
  swap('#dong-goi', fresh, before);

  // "Hộp gợi ý": "Dùng hộp này" asks first once the box holds an item, and the question counts the items.
  const strip = document.getElementById('goi-y');
  const freshStrip = fresh.getElementById('goi-y');
  if (strip && freshStrip) {
    const copy = document.importNode(freshStrip, true);
    strip.replaceWith(copy);
    wireRows(copy);
  }
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

const toast = document.getElementById('box-toast');
let toastTimer = 0;

// The same words, seen: the toast shows for a few seconds (screen readers hear the status instead).
function showToast(text: string): void {
  if (!toast) {
    return;
  }

  toast.textContent = text;
  // Shown again each time, so it comes above a sheet that was opened after it.
  if (toast.matches(':popover-open')) {
    toast.hidePopover();
  }
  toast.showPopover();
  window.clearTimeout(toastTimer);
  toastTimer = window.setTimeout(() => {
    toast.hidePopover();
  }, 3500);
}

function announce(text: string): void {
  showToast(text);
  if (!status) {
    return;
  }

  // Emptied first, so the same words twice in a row are read again.
  status.textContent = '';
  window.setTimeout(() => {
    status.textContent = text;
  }, 50);
}

// What changed, in words: "Đã thêm Trà sen · còn 320.000đ." The amount left is read from the new panel the server sent.
function message(name: string, before: number, asked: number, now: number): string {
  const left = document.querySelector('#hop-cua-ban .box-sums__row--left dd, #hop-cua-ban .box-sums__row--over dd')?.textContent?.trim() ?? '';
  const over = document.querySelector('#hop-cua-ban .box-sums__row--over') !== null;
  const money = over ? `vượt ngân sách ${left}` : `còn ${left}`;
  if (now !== asked) {
    return `Không thêm được ${name} · ${money}.`;
  }

  if (now === 0) {
    return `Đã bỏ ${name} khỏi hộp · ${money}.`;
  }

  const count = now > 1 ? ` (${now} trong hộp)` : '';
  return now > before ? `Đã thêm ${name}${count} · ${money}.` : `Đã bớt ${name}${count} · ${money}.`;
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
  const before = quantityIn(document, slug);
  sending = true;
  form.setAttribute('aria-busy', 'true');

  fetchPage(form.action, { method: 'POST', body: new FormData(form, submitter) })
    .then(({ page: fresh }) => {
      applyPage(fresh);
      refocus(place, slug, step);
      announce(message(name, before, Math.max(asked, 0), quantityIn(document, slug)));
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
// Saves go one at a time, in the order chosen, and a choice already replaced by a newer one is not sent: sent side by
// side, two saves could reach the server in either order and leave saved a packaging that the screen does not show.
let packagingAsked = 0;
let packagingQueue: Promise<void> = Promise.resolve();

document.addEventListener('change', (event) => {
  const radio = event.target;
  if (!(radio instanceof HTMLInputElement) || radio.type !== 'radio' || radio.form?.id !== 'dong-goi-form') {
    return;
  }

  const form = radio.form;
  const name = radio.closest('.packaging-option')?.querySelector('.packaging-option__name')?.textContent?.trim() ?? '';
  const body = new FormData(form); // the choice as it is now, not as it is when its turn comes
  const asked = ++packagingAsked;
  form.setAttribute('aria-busy', 'true');
  packagingQueue = packagingQueue.then(async () => {
    if (asked !== packagingAsked) {
      return; // a newer choice is waiting: this one would only be overwritten
    }

    try {
      const { page: fresh } = await fetchPage(form.action, { method: 'POST', body });
      if (asked === packagingAsked) {
        swapPanel(fresh);
        swap('.box-bar', fresh);
        const total = document.querySelector('#hop-cua-ban .box-sums__row--total dd')?.textContent?.trim() ?? '';
        announce(`Đã chọn ${name}. Tổng cộng ${total}.`);
      }
    } catch {
      // Send it the plain way: the server saves it and the reloaded page shows the choice.
      form.submit();
    } finally {
      if (asked === packagingAsked) {
        form.removeAttribute('aria-busy');
      }
    }
  });
});

// ---- "Chọn nhanh" (decision 2026-11-17) ----
// An amount only fills the field; the amber note says the budget in use stays the saved one until "Lưu". The tile is a
// link that does the same without a script (/builder?chon=…); here it happens in place. Both texts come from the server
// on the tile: the browser only copies them.
const budget = document.querySelector<HTMLInputElement>('#budget');
const unsaved = document.getElementById('chua-luu');
const quick = document.getElementById('chon-nhanh');

quick?.addEventListener('click', (event) => {
  const tile = event.target instanceof Element ? event.target.closest<HTMLAnchorElement>('a.picker-tile--amount') : null;
  if (!tile || !budget || !unsaved) {
    return;
  }

  event.preventDefault();
  budget.value = tile.dataset['amount'] ?? '';
  unsaved.textContent = tile.dataset['note'] ?? '';
  unsaved.hidden = false;
  for (const other of quick.querySelectorAll('a.picker-tile--amount')) {
    if (other === tile) {
      other.setAttribute('aria-current', 'true');
    } else {
      other.removeAttribute('aria-current');
    }
  }
  quick.hidePopover();
  budget.focus();
});

// Typing in the field makes the note about a picked amount untrue: it goes.
budget?.addEventListener('input', () => {
  if (unsaved) {
    unsaved.hidden = true;
  }
});

// ---- Rows (decision 2026-11-17) ----
// The ← → buttons of every row (the categories and "Hộp gợi ý"); under a category row, the position: "1–3 / 8 món",
// and "· đã xem hết" at the end, with the bar.
function writePosition(row: HTMLElement, position: RowPosition): void {
  const text = row.querySelector('[data-row-text]');
  const fill = row.querySelector<HTMLElement>('[data-row-fill]');
  if (!text || !fill) {
    return;
  }

  const count = row.querySelectorAll('.item-card').length;
  const last = Math.min(position.last, count);
  const first = Math.min(position.first, last);
  text.textContent = `${first}–${last} / ${count} món${position.atEnd ? ' · đã xem hết' : ''}`;
  fill.style.width = `${(position.atEnd ? 1 : last / Math.max(count, 1)) * 100}%`;
}

wireRows(document, writePosition);

// ---- The phone hint ----
// "Vuốt sang trái…" shows on a phone until the first sideways swipe of a category row, remembered in the browser. With
// storage blocked it shows on every visit, which is harmless.
const HINT_KEY = 'peekbox-row-hint-seen';
const hint = document.getElementById('vuot-hint');

function hintSeen(): boolean {
  try {
    return window.localStorage.getItem(HINT_KEY) === '1';
  } catch {
    return false;
  }
}

if (hint && !hintSeen() && window.matchMedia('(max-width: 899px)').matches) {
  hint.hidden = false;
  const done = (): void => {
    hint.hidden = true;
    try {
      window.localStorage.setItem(HINT_KEY, '1');
    } catch {
      // nothing to remember it in
    }
  };
  for (const track of document.querySelectorAll('.item-row [data-row-track]')) {
    track.addEventListener('scroll', done, { once: true, passive: true });
  }
}
