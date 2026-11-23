// The orders screen of the admin (F57): the filters are a plain GET form that works without this file (the "Lọc" button shows
// when there are no scripts). This file sends the form as soon as the day or the status changes, and a moment after the last
// letter typed in the search box, with no Enter and no button.

const form = document.querySelector<HTMLFormElement>('#order-filters');

// How long after the last letter the search is sent: long enough for a name to be typed, short enough to feel immediate.
const SEARCH_DELAY_MS = 450;

if (form) {
  const send = (): void => form.requestSubmit();

  // A day picked from the calendar, or a status picked from the list.
  form.querySelector('[name="date"]')?.addEventListener('change', send);
  form.querySelector('[name="status"]')?.addEventListener('change', send);

  // Typing: not while an input method is still building a letter (Vietnamese Telex makes "ê" from "e" and "e"), or the page
  // would reload in the middle of a word.
  const search = form.querySelector<HTMLInputElement>('[name="q"]');
  let timer: ReturnType<typeof setTimeout> | undefined;
  search?.addEventListener('input', (event) => {
    clearTimeout(timer);
    if ((event as InputEvent).isComposing) {
      return;
    }

    timer = setTimeout(send, SEARCH_DELAY_MS);
  });
  search?.addEventListener('compositionend', () => {
    clearTimeout(timer);
    timer = setTimeout(send, SEARCH_DELAY_MS);
  });

  // After the page reloads with a search typed, the cursor goes back to the end of it, so typing goes on.
  if (search && search.value !== '' && document.activeElement === document.body && new URLSearchParams(location.search).has('q')) {
    search.focus();
    search.setSelectionRange(search.value.length, search.value.length);
  }
}
