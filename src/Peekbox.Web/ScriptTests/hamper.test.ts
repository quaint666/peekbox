import { afterEach, describe, expect, it, vi } from 'vitest';
import { load } from './support.js';

type Callback = (entries: Partial<IntersectionObserverEntry>[]) => void;
let callback: Callback = () => {};

function fakeObserver(): void {
  vi.stubGlobal('IntersectionObserver', class {
    constructor(cb: Callback) { callback = cb; }
    observe(): void {}
    disconnect(): void {}
  });
}

afterEach(() => vi.unstubAllGlobals());

describe('hamper.ts', () => {
  it('the bottom bar shows only once the button in the page has scrolled out above it', async () => {
    fakeObserver();
    await load('hamper', '<div data-buy-main></div><div data-buy-bar hidden></div>');
    const bar = document.querySelector<HTMLElement>('[data-buy-bar]')!;

    callback([{ isIntersecting: true, boundingClientRect: { top: 100 } as DOMRect }]);
    expect(bar.hidden).toBe(true);    // the button is on screen

    callback([{ isIntersecting: false, boundingClientRect: { top: -50 } as DOMRect }]);
    expect(bar.hidden).toBe(false);   // scrolled out above

    callback([{ isIntersecting: false, boundingClientRect: { top: 900 } as DOMRect }]);
    expect(bar.hidden).toBe(true);    // still below the screen: not yet
  });

  it('with no IntersectionObserver the bar stays hidden and nothing throws', async () => {
    vi.stubGlobal('IntersectionObserver', undefined);
    delete (window as unknown as Record<string, unknown>)['IntersectionObserver'];
    await expect(load('hamper', '<div data-buy-main></div><div data-buy-bar hidden></div>')).resolves.toBeUndefined();
    expect(document.querySelector<HTMLElement>('[data-buy-bar]')?.hidden).toBe(true);
  });
});
