// The builder page in the browser. The page works without this file: the item filters are a plain GET form,
// and without scripts a "Lọc" button sends it. This file only sends the form as soon as the checkbox changes.

const filters = document.querySelector<HTMLFormElement>('form.item-filters');

filters?.addEventListener('change', (event) => {
  if (event.target instanceof HTMLInputElement && event.target.type === 'checkbox') {
    filters.requestSubmit();
  }
});

export {};
