import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { load, stubMatchMedia } from './support.js';

const header = `
  <header class="site-header">
    <button data-action="toggle-menu" data-label-open="Mở menu" data-label-close="Đóng menu" aria-expanded="false" aria-label="Mở menu"></button>
    <button data-action="toggle-theme"></button>
    <nav class="site-nav"><div class="site-nav__dip"><a href="/dip/tet">Dịp</a><div class="site-nav__menu"><a href="/dip/tet">Tết</a></div></div></nav>
  </header>
  <main><p id="elsewhere">page</p></main>`;

afterEach(() => {
  vi.restoreAllMocks();
  localStorage.clear();
  delete document.documentElement.dataset['theme'];
});

describe('layout.ts', () => {
  it('the theme button flips light and dark, remembers it, and still works when storage is blocked', async () => {
    await load('layout', header);
    stubMatchMedia(false);

    document.querySelector<HTMLElement>('[data-action="toggle-theme"]')?.click();
    expect(document.documentElement.dataset['theme']).toBe('dark');
    expect(localStorage.getItem('peekbox.theme')).toBe('dark');

    document.querySelector<HTMLElement>('[data-action="toggle-theme"]')?.click();
    expect(document.documentElement.dataset['theme']).toBe('light');

    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new Error('blocked'); });
    expect(() => document.querySelector<HTMLElement>('[data-action="toggle-theme"]')?.click()).not.toThrow();
    expect(document.documentElement.dataset['theme']).toBe('dark');
  });

  it('a stored theme that is neither light nor dark is ignored: the system setting decides', async () => {
    await load('layout', header);
    stubMatchMedia(true);   // the system prefers dark
    localStorage.setItem('peekbox.theme', 'blue');

    document.querySelector<HTMLElement>('[data-action="toggle-theme"]')?.click();

    expect(document.documentElement.dataset['theme']).toBe('light');   // from dark (system) to light, not from "blue"
  });

  it('the phone menu opens, says so to a screen reader, and closes on a click elsewhere', async () => {
    await load('layout', header);
    const button = document.querySelector<HTMLElement>('[data-action="toggle-menu"]')!;
    const site = document.querySelector('.site-header')!;

    button.click();
    expect(site.classList.contains('is-menu-open')).toBe(true);
    expect(button.getAttribute('aria-expanded')).toBe('true');
    expect(button.getAttribute('aria-label')).toBe('Đóng menu');

    document.getElementById('elsewhere')?.click();
    expect(site.classList.contains('is-menu-open')).toBe(false);
    expect(button.getAttribute('aria-expanded')).toBe('false');
    expect(button.getAttribute('aria-label')).toBe('Mở menu');
  });

  it('Escape closes the menu and dismisses the "Dịp" panel until the pointer leaves, moving the focus off a hidden link', async () => {
    await load('layout', header);
    const dip = document.querySelector<HTMLElement>('.site-nav__dip')!;
    const inner = dip.querySelector<HTMLElement>('.site-nav__menu a')!;
    document.querySelector<HTMLElement>('[data-action="toggle-menu"]')?.click();
    inner.focus();

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    expect(document.querySelector('.site-header')?.classList.contains('is-menu-open')).toBe(false);
    expect(dip.classList.contains('is-dismissed')).toBe(true);
    expect(document.activeElement).toBe(dip.querySelector('a'));   // the first link, which stays visible

    dip.dispatchEvent(new Event('pointerleave'));
    expect(dip.classList.contains('is-dismissed')).toBe(false);
  });

  it('a key other than Escape does nothing', async () => {
    await load('layout', header);
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'a' }));
    expect(document.querySelector('.site-nav__dip')?.classList.contains('is-dismissed')).toBe(false);
  });

  describe('"Lên đầu trang"', () => {
    const page = `${header}<a class="site-header-logo" href="/">logo</a><button data-action="to-top" hidden></button>`;
    let scrollTo: ReturnType<typeof vi.fn>;
    let frames: FrameRequestCallback[] = [];

    // A scroll event; the animation frame it asked for runs when the test says so, as a browser's does after the event.
    const scrollY = (y: number): void => {
      Object.defineProperty(window, 'scrollY', { configurable: true, value: y });
      window.dispatchEvent(new Event('scroll'));
      frames.splice(0).forEach((cb) => cb(0));
    };

    beforeEach(() => {
      scrollTo = vi.fn();
      window.scrollTo = scrollTo as unknown as typeof window.scrollTo;
      frames = [];
      window.requestAnimationFrame = ((cb: FrameRequestCallback) => frames.push(cb)) as typeof window.requestAnimationFrame;
      Object.defineProperty(window, 'scrollY', { configurable: true, value: 0 });
    });

    it('stays hidden at the top and shows once the page has been scrolled past 600 px, and hides again on the way up', async () => {
      stubMatchMedia(false);
      await load('layout', page);
      const button = document.querySelector<HTMLButtonElement>('[data-action="to-top"]')!;
      expect(button.hidden).toBe(true);

      scrollY(500);
      expect(button.hidden).toBe(true);
      scrollY(601);
      expect(button.hidden).toBe(false);
      scrollY(40);
      expect(button.hidden).toBe(true);
    });

    it('scrolls to the top smoothly, or at once for a visitor who asked for less motion, and puts the focus at the top', async () => {
      stubMatchMedia(false);
      await load('layout', `<header class="site-header"><a id="logo" href="/">logo</a></header><button data-action="to-top"></button>`);
      document.querySelector<HTMLElement>('[data-action="to-top"]')?.click();
      expect(scrollTo).toHaveBeenLastCalledWith({ top: 0, behavior: 'smooth' });
      expect(document.activeElement?.id).toBe('logo');

      stubMatchMedia(true);
      await load('layout', `<header class="site-header"><a id="logo" href="/">logo</a></header><button data-action="to-top"></button>`);
      document.querySelector<HTMLElement>('[data-action="to-top"]')?.click();
      expect(scrollTo).toHaveBeenLastCalledWith({ top: 0, behavior: 'auto' });
    });

    it('a burst of scroll events asks for one animation frame, not one each', async () => {
      stubMatchMedia(false);
      await load('layout', page);
      for (let y = 100; y <= 900; y += 100) {
        Object.defineProperty(window, 'scrollY', { configurable: true, value: y });
        window.dispatchEvent(new Event('scroll'));
      }
      expect(frames).toHaveLength(1);
    });

    it('a page without the button does nothing and does not throw', async () => {
      stubMatchMedia(false);
      await load('layout', header);
      expect(() => scrollY(900)).not.toThrow();
    });
  });
});
