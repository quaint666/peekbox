import { afterEach, describe, expect, it, vi } from 'vitest';
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
});
