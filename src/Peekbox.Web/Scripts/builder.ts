// The builder page in the browser. The page works without this file: the item filters are a plain GET form (without
// scripts a "Lọc" button sends it), and "Thêm", − and + are plain POST forms that reload the page. This file sends the
// filters as soon as the checkbox or the sort menu changes, filters the list while the visitor types in the search
// box (a moment after the last letter, no Enter needed), and sends the add forms in the background so the page does
// not jump.

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

// ---- Live search ----
// While the visitor types in "Tìm món", the same GET the form would send is fetched in the background and the block of
// results (#ket-qua: the count, the "no match" card and the rows or the grid) is swapped for the one in the answer. The
// server does the matching, so "banh" finds "Bánh gừng" exactly as it does after Enter, and this file never works out
// anything. Without this file Enter and the "Lọc" button send the form the plain way.
const SEARCH_DELAY_MS = 250;
const search = document.getElementById('item-search');
let searchTimer = 0;
let searchSerial = 0;
let searchRequest: AbortController | null = null;

// The box stays under the step strip on a phone while it has words or the cursor, from the moment its place has gone up
// under the strip until the end of the list has too (the CSS is .search-pin.is-pinned). The box keeps its place in the page
// with a height, so pinning moves nothing else.
const phone = window.matchMedia('(max-width: 899px)');
const searchField = document.querySelector<HTMLElement>('.item-filters__search');
const searchPin = searchField?.querySelector<HTMLElement>('.search-pin') ?? null;
let pinFrame = 0;

function isPinned(): boolean {
  return searchPin?.classList.contains('is-pinned') === true;
}

function setPinned(on: boolean): void {
  if (!searchField || !searchPin || on === isPinned()) {
    return;
  }

  if (on) {
    searchField.style.height = `${searchField.getBoundingClientRect().height}px`;
  }

  searchPin.classList.toggle('is-pinned', on);
  searchPin.querySelector('label')?.classList.toggle('visually-hidden', on);
  if (!on) {
    searchField.style.height = '';
  }
}

function updatePin(): void {
  pinFrame = 0;
  const steps = document.querySelector('.builder-steps');
  const results = document.getElementById('ket-qua');
  if (!searchField || !(search instanceof HTMLInputElement) || !steps || !results || !phone.matches) {
    setPinned(false);
    return;
  }

  const top = steps.getBoundingClientRect().bottom;
  const used = search.value !== '' || document.activeElement === search;
  setPinned(used && searchField.getBoundingClientRect().top < top && results.getBoundingClientRect().bottom > top);
}

function schedulePin(): void {
  if (pinFrame === 0) {
    pinFrame = window.requestAnimationFrame(updatePin);
  }
}

function stopSearch(): void {
  window.clearTimeout(searchTimer);
  searchRequest?.abort();
  searchSerial += 1;
}

function searchNow(): void {
  if (!filters) {
    return;
  }

  const query = new URLSearchParams(Array.from(new FormData(filters), ([name, value]) => [name, String(value)]));
  const url = new URL(filters.action);
  url.search = query.toString();

  stopSearch();
  const serial = searchSerial;
  const typing = document.activeElement === search;
  searchRequest = new AbortController();
  fetchPage(url, { signal: searchRequest.signal })
    .then(({ page: fresh }) => {
      // Only the newest request may change the page: a slow answer to "ba" must not replace the one for "banh".
      const results = document.getElementById('ket-qua');
      if (serial !== searchSerial || !results) {
        return;
      }

      swap('#ket-qua', fresh);
      const shown = document.getElementById('ket-qua');
      if (shown) {
        shown.classList.add('is-entering');   // the cards come in softly (builder.css)
        wireRows(shown, writePosition);
        wireHint();
        say(shown.querySelector('.items__count span')?.textContent?.trim() ?? '');
        // On a phone the first answer scrolls the list up under the box; once the box is pinned the list is already there.
        if (typing && phone.matches && !isPinned()) {
          shown.scrollIntoView({ block: 'start', behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth' });
        }

        schedulePin();
      }

      // The address now says what is on screen, so reload and Back keep the search.
      window.history.replaceState(null, '', url);
    })
    .catch(() => {
      // Cancelled, timed out or the server said no: the list stays as it was. Enter still sends the form.
    });
}

function searchSoon(): void {
  window.clearTimeout(searchTimer);
  searchTimer = window.setTimeout(searchNow, SEARCH_DELAY_MS);
}

window.addEventListener('scroll', schedulePin, { passive: true });
window.addEventListener('resize', schedulePin);
phone.addEventListener('change', schedulePin);

if (search instanceof HTMLInputElement) {
  search.addEventListener('focus', schedulePin);
  search.addEventListener('blur', schedulePin);
  schedulePin();
  // A letter being composed (Telex, VNI) is not final yet: wait for the composition to end.
  search.addEventListener('input', (event) => {
    schedulePin();
    if (!(event instanceof InputEvent && event.isComposing)) {
      searchSoon();
    }
  });
  search.addEventListener('compositionend', searchSoon);
  // The × of a search box: some browsers announce it with this event (and only for emptying, so Enter, which also
  // raises it in some browsers, does not start a second request).
  search.addEventListener('search', () => {
    if (search.value === '') {
      searchSoon();
    }
  });
  // Enter searches at once instead of waiting out the delay. Sent the plain way it would only open the same address
  // again and add a step to Back; the live answer is already what that page would show.
  search.addEventListener('keydown', (event) => {
    if (event.key === 'Enter' && !event.isComposing) {
      event.preventDefault();
      searchNow();
      // The keyboard's search key: on a touch screen the keyboard goes away so the list can be seen.
      if (window.matchMedia('(pointer: coarse)').matches) {
        search.blur();
      }
    }
  });

  // A touch screen's keyboard covers half of the screen. A tap anywhere outside the box closes it (a tap on a button or
  // a link still does what it says), and so does the start of a swipe, when the browser takes the touch over to scroll
  // the page (pointercancel). Some browsers keep the keyboard open on a tap on plain text, so this is done here.
  const closeKeyboard = (event: PointerEvent): void => {
    const inside = event.target instanceof Node && searchPin?.contains(event.target);
    if (event.pointerType === 'touch' && document.activeElement === search && !inside) {
      search.blur();
    }
  };
  document.addEventListener('pointerup', closeKeyboard);
  document.addEventListener('pointercancel', closeKeyboard);
}

// ---- "Thêm", − and + (F20) ----
// The form is sent in the background. The server answers as it does without scripts: it saves, then redirects to the
// builder page. This file reads the new page and swaps in what changed: every card's add control (one item can change
// "Vượt X" on the others), "Hộp của bạn", the phone bar, the step strip, the header's cart count and step 3. It never works out a price: every
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
  swap('.builder-steps', fresh);   // step 2 becomes step 3 with the first item, and back to 2 when the last one is taken out
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

// The toast shows what changed on one line and what is left on the next ("\n"); a screen reader hears the two as sentences.
function announce(text: string): void {
  showToast(text);
  say(text.replace(/\n/g, '. '));
}

// The status line that screen readers hear, without the toast.
function say(text: string): void {
  if (!status) {
    return;
  }

  // Emptied first, so the same words twice in a row are read again.
  status.textContent = '';
  window.setTimeout(() => {
    status.textContent = text;
  }, 50);
}

// ---- The answer to an add (decision 2026-11-20) ----
// A card that got one more of its item shows a soft ring once, the − n + control that appears when the first one is added
// pops in, and the header's cart and the phone's "Xem hộp" button grow once. Only the CSS moves (and only for visitors who
// have not asked for less motion); this adds the classes once the new numbers are on screen. A class added again does not
// restart an animation, so it is taken off and put back.
function replay(element: Element | null, name: string): void {
  if (!element) {
    return;
  }

  element.classList.remove(name);
  void (element as HTMLElement).offsetWidth;
  element.classList.add(name);
}

function celebrate(place: Place, slug: string, before: number, now: number): void {
  if (now <= before) {
    return;
  }

  replay(document.getElementById(`mon-${slug}`), 'is-added');
  if (before === 0) {
    replay(controlRoot(place, slug)?.querySelector('.qty') ?? null, 'is-new');
  }

  for (const counter of document.querySelectorAll('.cart-link, .box-bar__open')) {
    replay(counter, 'is-bump');
  }
}

// What changed, in words: "Đã thêm Trà sen" and, on the next line, "Còn lại 320.000đ". The amount left is read from the new
// panel the server sent. Two lines, not one line with a dot between them: a dot can end up alone at the start of the second line.
function message(name: string, before: number, asked: number, now: number): string {
  const left = document.querySelector('#hop-cua-ban .box-sums__row--left dd, #hop-cua-ban .box-sums__row--over dd')?.textContent?.trim() ?? '';
  const over = document.querySelector('#hop-cua-ban .box-sums__row--over') !== null;
  const money = over ? `Vượt ngân sách ${left}` : `Còn lại ${left}`;
  if (now !== asked) {
    return `Không thêm được ${name}\n${money}`;
  }

  if (now === 0) {
    return `Đã bỏ ${name} khỏi hộp\n${money}`;
  }

  const count = now > 1 ? ` (${now} trong hộp)` : '';
  return now > before ? `Đã thêm ${name}${count}\n${money}` : `Đã bớt ${name}${count}\n${money}`;
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
      celebrate(place, slug, before, quantityIn(document, slug));
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
        announce(`Đã chọn ${name}\nTổng cộng ${total}`);
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

function hintSeen(): boolean {
  try {
    return window.localStorage.getItem(HINT_KEY) === '1';
  } catch {
    return false;
  }
}

function wireHint(): void {
  const hint = document.getElementById('vuot-hint');
  if (!hint || hintSeen() || !window.matchMedia('(max-width: 899px)').matches) {
    return;
  }

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

wireHint();
