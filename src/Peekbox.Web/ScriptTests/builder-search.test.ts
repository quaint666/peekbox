import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { load, stubMatchMedia } from './support.js';

const html = `
  <form class="item-filters" action="http://localhost:3000/builder" method="get">
    <div class="item-filters__search"><div class="search-pin"><input id="item-search" name="q" type="search"></div></div>
    <select name="sort"><option value="">a</option><option value="price" selected>b</option></select>
  </form>
  <div id="ket-qua"><p class="items__count"><span>3 món</span></p><ul><li>old</li></ul></div>
  <p id="box-status" role="status"></p>`;

const results = (label: string): Response =>
  ({ ok: true, status: 200, redirected: false, text: () => Promise.resolve(`<html><body><div id="ket-qua"><p class="items__count"><span>${label}</span></p><ul><li>${label}</li></ul></div></body></html>`) }) as Response;

const type = (text: string): void => {
  const input = document.getElementById('item-search') as HTMLInputElement;
  input.value = text;
  input.dispatchEvent(new InputEvent('input', { bubbles: true }));
};

let fetchMock: ReturnType<typeof vi.spyOn>;

beforeEach(async () => {
  vi.useFakeTimers();
  stubMatchMedia(false);
  fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(() => Promise.resolve(results('2 món')));
  await load('builder', html);
});

afterEach(() => {
  vi.useRealTimers();
  window.history.replaceState(null, '', '/');
});

describe('builder.ts: live search', () => {
  it('waits 250 ms after the last letter, then asks the server once with every filter of the form', async () => {
    type('b');
    await vi.advanceTimersByTimeAsync(200);
    type('ba');
    await vi.advanceTimersByTimeAsync(200);
    expect(fetchMock).not.toHaveBeenCalled();   // the second letter restarted the wait

    await vi.advanceTimersByTimeAsync(60);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    const url = new URL(String(fetchMock.mock.calls[0]?.[0]));
    expect(url.pathname).toBe('/builder');
    expect(url.searchParams.get('q')).toBe('ba');
    expect(url.searchParams.get('sort')).toBe('price');
  });

  it('swaps in the server\'s results, lets them come in softly, announces the count, and puts the search in the address', async () => {
    type('banh');
    await vi.advanceTimersByTimeAsync(300);

    const shown = document.getElementById('ket-qua')!;
    expect(shown.textContent).toContain('2 món');
    expect(shown.querySelector('li')?.textContent).toBe('2 món');
    expect(shown.classList.contains('is-entering')).toBe(true);
    expect(window.location.search).toBe('?q=banh&sort=price');
  });

  it('Enter searches at once and does not submit the form the plain way', async () => {
    const input = document.getElementById('item-search') as HTMLInputElement;
    input.value = 'tra';
    const enter = new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true });
    input.dispatchEvent(enter);

    expect(enter.defaultPrevented).toBe(true);
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it('Enter that confirms a composed letter (Telex, VNI) is not a search', () => {
    const input = document.getElementById('item-search') as HTMLInputElement;
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', isComposing: true, bubbles: true, cancelable: true }));
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('a letter still being composed (Telex, VNI) waits for the composition to end', async () => {
    const input = document.getElementById('item-search') as HTMLInputElement;
    input.value = 'ba';
    input.dispatchEvent(new InputEvent('input', { bubbles: true, isComposing: true }));
    await vi.advanceTimersByTimeAsync(400);
    expect(fetchMock).not.toHaveBeenCalled();

    input.dispatchEvent(new CompositionEvent('compositionend'));
    await vi.advanceTimersByTimeAsync(300);
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it('a slow answer for an older search never replaces the newer one, and the older request is cancelled', async () => {
    const slow = Promise.withResolvers<Response>();
    fetchMock.mockReset();
    fetchMock.mockReturnValueOnce(slow.promise).mockResolvedValueOnce(results('for banh'));

    type('ba');
    await vi.advanceTimersByTimeAsync(300);
    const firstSignal = fetchMock.mock.calls[0]?.[1]?.signal as AbortSignal;
    type('banh');
    await vi.advanceTimersByTimeAsync(300);

    expect(firstSignal.aborted).toBe(true);
    slow.resolve(results('for ba'));
    await vi.advanceTimersByTimeAsync(10);
    expect(document.getElementById('ket-qua')?.textContent).toContain('for banh');
    expect(document.getElementById('ket-qua')?.textContent).not.toContain('for ba ');
  });

  it('when the server fails the list stays as it was', async () => {
    fetchMock.mockReset();
    fetchMock.mockRejectedValue(new Error('offline'));
    type('banh');
    await vi.advanceTimersByTimeAsync(300);
    expect(document.querySelector('#ket-qua li')?.textContent).toBe('old');
  });

  it('emptying the box with the × searches again, but Enter\'s own "search" event does not send a second request', async () => {
    const input = document.getElementById('item-search') as HTMLInputElement;
    input.value = 'x';
    input.dispatchEvent(new Event('search'));
    await vi.advanceTimersByTimeAsync(400);
    expect(fetchMock).not.toHaveBeenCalled();

    input.value = '';
    input.dispatchEvent(new Event('search'));
    await vi.advanceTimersByTimeAsync(300);
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });
});
