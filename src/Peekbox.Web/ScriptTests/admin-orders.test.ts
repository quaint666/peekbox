import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { load } from './support.js';

// The orders screen's filters (F57): the form is sent when the day or the status changes, and a moment after the last letter in
// the search box, never while an input method is still building a letter.
const html = `
  <form id="order-filters" method="get" action="/admin/orders">
    <input name="date" type="date" value="2027-01-15">
    <select name="status"><option value="">Tất cả</option><option value="new">Mới</option></select>
    <input name="q" type="search" value="">
  </form>`;

let sent = 0;

async function setup(): Promise<void> {
  vi.useFakeTimers();
  await load('admin-orders', html);
  const form = document.querySelector<HTMLFormElement>('#order-filters')!;
  sent = 0;
  form.requestSubmit = (() => { sent += 1; }) as typeof form.requestSubmit;
}

const field = (name: string): HTMLInputElement => document.querySelector<HTMLInputElement>(`[name="${name}"]`)!;

function type(text: string, composing = false): void {
  const input = field('q');
  input.value = text;
  input.dispatchEvent(new InputEvent('input', { bubbles: true, isComposing: composing }));
}

beforeEach(setup);
afterEach(() => { vi.useRealTimers(); history.replaceState(null, '', '/'); });

describe('the filters of the orders screen', () => {
  it('send the form at once when the day or the status changes', () => {
    field('date').dispatchEvent(new Event('change', { bubbles: true }));
    expect(sent).toBe(1);
    field('status').dispatchEvent(new Event('change', { bubbles: true }));
    expect(sent).toBe(2);
  });

  it('send the search a moment after the last letter, once', () => {
    type('t');
    vi.advanceTimersByTime(300);
    type('tr');
    vi.advanceTimersByTime(300);
    expect(sent).toBe(0);   // the second letter started the wait again
    type('trần');
    vi.advanceTimersByTime(449);
    expect(sent).toBe(0);
    vi.advanceTimersByTime(2);
    expect(sent).toBe(1);
    vi.advanceTimersByTime(5000);
    expect(sent).toBe(1);
  });

  it('wait while an input method is building a letter and send after it is done', () => {
    type('e', true);
    vi.advanceTimersByTime(2000);
    expect(sent).toBe(0);
    field('q').dispatchEvent(new CompositionEvent('compositionend', { bubbles: true }));
    vi.advanceTimersByTime(449);
    expect(sent).toBe(0);
    vi.advanceTimersByTime(2);
    expect(sent).toBe(1);
  });

  it('put the cursor back at the end of a search that came with the page, so typing goes on', async () => {
    history.replaceState(null, '', '/admin/orders?date=&q=tran');
    await load('admin-orders', html.replace('name="q" type="search" value=""', 'name="q" type="search" value="tran"'));
    const search = field('q');

    expect(document.activeElement).toBe(search);
    expect(search.selectionStart).toBe(4);
  });

  it('do nothing on a page without the form', async () => {
    await load('admin-orders', '<p>no form</p>');
    expect(document.body.textContent).toBe('no form');
  });
});
