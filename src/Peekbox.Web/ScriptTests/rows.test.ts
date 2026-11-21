import { describe, expect, it, vi } from 'vitest';
import { wireRows } from '../Scripts/lib/rows.js';

// jsdom has no layout: every number the script reads (positions, widths, scroll offset) is given here.
function build(opts: { scrollLeft: number; scrollWidth: number; clientWidth: number; lefts: number[] }) {
  document.body.innerHTML = `
    <div data-row><div hidden><button data-row-prev>←</button><button data-row-next>→</button></div>
    <ul data-row-track>${opts.lefts.map(() => '<li>card</li>').join('')}</ul></div>`;
  const track = document.querySelector<HTMLElement>('[data-row-track]')!;
  track.getBoundingClientRect = () => ({ left: 0, right: opts.clientWidth, top: 0, bottom: 0, width: opts.clientWidth, height: 0, x: 0, y: 0, toJSON() {} });
  Object.defineProperty(track, 'scrollLeft', { configurable: true, value: opts.scrollLeft });
  Object.defineProperty(track, 'scrollWidth', { configurable: true, value: opts.scrollWidth });
  Object.defineProperty(track, 'clientWidth', { configurable: true, value: opts.clientWidth });
  track.style.paddingLeft = '0px';   // jsdom's computed style is empty until it is set
  const scrollBy = vi.fn();
  track.scrollBy = scrollBy as unknown as typeof track.scrollBy;
  Array.from(track.children).forEach((card, i) => {
    const left = opts.lefts[i] ?? 0;
    (card as HTMLElement).getBoundingClientRect = () => ({ left, right: left + 200, top: 0, bottom: 0, width: 200, height: 0, x: left, y: 0, toJSON() {} });
  });
  return { track, scrollBy, prev: document.querySelector<HTMLButtonElement>('[data-row-prev]')!, next: document.querySelector<HTMLButtonElement>('[data-row-next]')! };
}

describe('wireRows', () => {
  it('shows the buttons, and at the start only → is on', () => {
    const { prev, next } = build({ scrollLeft: 0, scrollWidth: 1000, clientWidth: 600, lefts: [0, 220, 440, 660] });
    wireRows(document);
    expect(prev.parentElement?.hidden).toBe(false);
    expect(prev.disabled).toBe(true);
    expect(next.disabled).toBe(false);
  });

  it('at the end only ← is on, and the settle callback says what is in view', () => {
    build({ scrollLeft: 400, scrollWidth: 1000, clientWidth: 600, lefts: [-400, -180, 40, 260] });
    const settled = vi.fn();
    wireRows(document, settled);
    expect(document.querySelector<HTMLButtonElement>('[data-row-prev]')?.disabled).toBe(false);
    expect(document.querySelector<HTMLButtonElement>('[data-row-next]')?.disabled).toBe(true);
    expect(settled).toHaveBeenCalledWith(expect.anything(), { first: 3, last: 4, count: 4, atStart: false, atEnd: true });
  });

  it('→ scrolls the row by one card: the distance to the next card that is not yet fully in view', () => {
    const { next, scrollBy } = build({ scrollLeft: 0, scrollWidth: 1000, clientWidth: 600, lefts: [0, 220, 440, 660] });
    wireRows(document);
    next.click();
    expect(scrollBy).toHaveBeenCalledWith({ left: 220 });
  });

  it('a second press before the row has stopped builds on the card already being scrolled to', () => {
    const { next, scrollBy } = build({ scrollLeft: 0, scrollWidth: 1000, clientWidth: 600, lefts: [0, 220, 440, 660] });
    wireRows(document);
    next.click();
    next.click();
    expect(scrollBy).toHaveBeenLastCalledWith({ left: 440 });
  });

  it('a row missing its buttons is left alone', () => {
    document.body.innerHTML = '<div data-row><ul data-row-track><li></li></ul></div>';
    expect(() => wireRows(document)).not.toThrow();
  });
});
