// The builder page in the browser. The page works without this file: the item filters are a plain GET form,
// and without scripts a "Lọc" button sends it. This file only sends the form as soon as the checkbox or the sort
// menu changes. The search box is sent with Enter, not on every letter.

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

export {};
