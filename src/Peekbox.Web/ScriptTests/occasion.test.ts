import { afterEach, describe, expect, it, vi } from 'vitest';
import { load, stubMatchMedia } from './support.js';

type Entry = { target: Element; isIntersecting: boolean };
let notify: (entries: Entry[]) => void = () => {};

const html = `
  <div class="occ-budgets"><div data-budget-list>
    <a href="#ngan-sach-300" id="c1">300</a><a href="#ngan-sach-500" id="c2">500</a><a href="#ngan-sach-800" id="c3">800</a>
  </div></div>
  <section id="ngan-sach-300"></section><section id="ngan-sach-500"></section><section id="ngan-sach-800"></section>`;

async function setup(reduceMotion = false, overflowing = true): Promise<void> {
  stubMatchMedia(reduceMotion);
  vi.stubGlobal('IntersectionObserver', class {
    constructor(cb: (entries: Entry[]) => void) { notify = cb; }
    observe(): void {}
  });
  await load('occasion', html);
  const list = document.querySelector<HTMLElement>('[data-budget-list]')!;
  list.scrollTo = vi.fn() as unknown as typeof list.scrollTo;
  Object.defineProperty(list, 'clientWidth', { configurable: true, value: 300 });
  Object.defineProperty(list, 'scrollWidth', { configurable: true, value: overflowing ? 600 : 300 });
}

const section = (id: string): Entry => ({ target: document.getElementById(id)!, isIntersecting: true });
const marked = (): string => [...document.querySelectorAll('[aria-current=true]')].map((c) => c.id).join(',');

afterEach(() => vi.unstubAllGlobals());

describe('occasion.ts: the budget chip of the row being read', () => {
  it('marks the chip of the row in the band, and only one chip at a time', async () => {
    await setup();
    notify([section('ngan-sach-300')]);
    expect(marked()).toBe('c1');
    notify([section('ngan-sach-500')]);
    expect(marked()).toBe('c2');          // two rows in the band: the last one in page order wins
  });

  it('when two rows are in the band the later one is read; leaving it goes back to the earlier one', async () => {
    await setup();
    notify([section('ngan-sach-300'), section('ngan-sach-500')]);
    expect(marked()).toBe('c2');
    notify([{ target: document.getElementById('ngan-sach-500')!, isIntersecting: false }]);
    expect(marked()).toBe('c1');
  });

  it('between two rows (none in the band) the mark stays where it was', async () => {
    await setup();
    notify([section('ngan-sach-500')]);
    notify([{ target: document.getElementById('ngan-sach-500')!, isIntersecting: false }]);
    expect(marked()).toBe('c2');
  });

  it('on a phone, where the chips scroll sideways, it centres the marked chip: smoothly, or at once for reduced motion', async () => {
    await setup();
    notify([section('ngan-sach-800')]);
    expect(document.querySelector<HTMLElement>('[data-budget-list]')?.scrollTo).toHaveBeenCalledWith(expect.objectContaining({ behavior: 'smooth' }));

    await setup(true);
    notify([section('ngan-sach-800')]);
    expect(document.querySelector<HTMLElement>('[data-budget-list]')?.scrollTo).toHaveBeenCalledWith(expect.objectContaining({ behavior: 'auto' }));
  });

  it('when all the chips fit (a wide screen) the list never scrolls', async () => {
    await setup(false, false);
    notify([section('ngan-sach-800')]);
    expect(marked()).toBe('c3');
    expect(document.querySelector<HTMLElement>('[data-budget-list]')?.scrollTo).not.toHaveBeenCalled();
  });
});
