// THE SHARED PARTS OF EVERY PAGE: skip link, header, footer, toasts.
//
// They are built here, ONCE, so a change to the menu is one edit and every page
// gets it. (In Bloomy these were copy-pasted into 11 pages and drifted apart.)
//
// A page uses it like this (see src/pages/home.ts and public/index.html):
//     initShell('builder');
// and its HTML must contain  <div id="site-header">,  <main id="main">  and
// <div id="site-footer">.

import { EVENTS, LANGUAGES } from './config.js';
import { clear, h, icon, qs } from './dom.js';
import { getHamper, hamperCount } from './hamper.js';
import { applyTranslations, getLanguage, initLanguage, setLanguage, t } from './i18n.js';
import { getTheme, initTheme, toggleTheme } from './theme.js';
import type { Language } from './types.js';

export type NavKey = 'home' | 'builder' | 'about' | 'commitments' | 'blog' | 'none';

const NAV_LINKS: ReadonlyArray<{ key: Exclude<NavKey, 'none'>; href: string; label: 'nav.home' | 'nav.builder' | 'nav.about' | 'nav.commitments' | 'nav.blog' }> = [
  { key: 'home', href: '/index.html', label: 'nav.home' },
  { key: 'builder', href: '/builder.html', label: 'nav.builder' },
  { key: 'about', href: '/about.html', label: 'nav.about' },
  { key: 'commitments', href: '/about.html#cam-ket', label: 'nav.commitments' },
  { key: 'blog', href: '/blog.html', label: 'nav.blog' },
];

let activeNav: NavKey = 'none';
let menuOpen = false;

function buildHeader(): HTMLElement {
  const lang = getLanguage();
  const dark = getTheme() === 'dark';
  const count = hamperCount(getHamper());

  const nav = h(
    'nav',
    { class: 'site-nav', id: 'site-nav', 'aria-label': t('nav.main') },
    ...NAV_LINKS.map((link) =>
      h('a', { href: link.href, 'aria-current': link.key === activeNav ? 'page' : undefined }, t(link.label)),
    ),
  );

  const langSwitch = h(
    'div',
    { class: 'lang-switch', role: 'group', 'aria-label': t('lang.group') },
    ...LANGUAGES.map((code: Language) =>
      h(
        'button',
        {
          type: 'button',
          class: code === lang ? 'lang-switch__btn is-active' : 'lang-switch__btn',
          'aria-pressed': String(code === lang),
          'aria-label': t(code === 'vi' ? 'lang.vi' : 'lang.en'),
          'data-lang': code,
        },
        code.toUpperCase(),
      ),
    ),
  );

  const themeButton = h(
    'button',
    { type: 'button', class: 'icon-btn', 'data-action': 'toggle-theme', 'aria-label': t(dark ? 'theme.toLight' : 'theme.toDark') },
    icon(dark ? 'sun' : 'moon'),
  );

  const cartLink = h(
    'a',
    { class: 'cart-link', href: '/cart.html', 'aria-label': t('cart.aria', { n: count }) },
    icon('cart'),
    h('span', { class: 'cart-link__text' }, t('cart.link', { n: count })),
  );

  const menuButton = h(
    'button',
    {
      type: 'button',
      class: 'icon-btn menu-btn',
      'data-action': 'toggle-menu',
      'aria-expanded': String(menuOpen),
      'aria-controls': 'site-nav',
      'aria-label': t(menuOpen ? 'nav.closeMenu' : 'nav.openMenu'),
    },
    icon(menuOpen ? 'close' : 'menu'),
  );

  // Both logos are in the page; CSS shows the right one for the theme.
  const brand = h(
    'a',
    { class: 'site-header__brand', href: '/index.html' },
    h('img', { class: 'logo logo--light', src: '/images/logo/peekbox-logo-sage.svg', alt: 'Peekbox', height: 34 }),
    h('img', { class: 'logo logo--dark', src: '/images/logo/peekbox-logo-dark.svg', alt: '', height: 34 }),
  );

  return h(
    'header',
    { class: menuOpen ? 'site-header is-menu-open' : 'site-header' },
    h('div', { class: 'site-header__inner container' }, brand, nav, h('div', { class: 'site-header__tools' }, langSwitch, themeButton, cartLink, menuButton)),
  );
}

function buildFooter(): HTMLElement {
  return h(
    'footer',
    { class: 'site-footer' },
    h(
      'div',
      { class: 'site-footer__inner container' },
      h('div', { class: 'site-footer__about' }, h('strong', {}, 'Peekbox'), h('p', {}, t('footer.tagline')), h('p', { class: 'muted' }, t('footer.contact'))),
      h(
        'nav',
        { class: 'site-footer__links', 'aria-label': t('nav.main') },
        h('a', { href: '/about.html' }, t('footer.about')),
        h('a', { href: '/about.html#cam-ket' }, t('footer.commitments')),
        h('a', { href: '/blog.html' }, t('footer.blog')),
        h('a', { href: '/legal.html#dieu-khoan' }, t('footer.terms')),
        h('a', { href: '/legal.html#bao-mat' }, t('footer.privacy')),
      ),
    ),
  );
}

function render(): void {
  qs('#site-header').replaceChildren(buildHeader());
  qs('#site-footer').replaceChildren(buildFooter());
  applyTranslations();
}

// A short message at the bottom of the screen: showToast('Đã thêm vào hộp').
export function showToast(message: string): void {
  let region = document.querySelector<HTMLElement>('#toasts');
  if (!region) {
    region = h('div', { id: 'toasts', class: 'toasts', role: 'status', 'aria-live': 'polite' });
    document.body.append(region);
  }
  const toast = h('div', { class: 'toast' }, icon('check', 18), h('span', {}, message));
  region.append(toast);
  window.setTimeout(() => toast.remove(), 4000);
}

function onClick(event: MouseEvent): void {
  const target = event.target;
  if (!(target instanceof Element)) return;

  const langButton = target.closest<HTMLElement>('[data-lang]');
  if (langButton) {
    setLanguage(langButton.dataset['lang'] === 'en' ? 'en' : 'vi');
    return;
  }
  const actionButton = target.closest<HTMLElement>('[data-action]');
  if (actionButton?.dataset['action'] === 'toggle-theme') {
    toggleTheme();
    return;
  }
  if (actionButton?.dataset['action'] === 'toggle-menu') {
    menuOpen = !menuOpen;
    render();
    return;
  }
  // A click anywhere else closes an open mobile menu.
  if (menuOpen && !target.closest('.site-header')) {
    menuOpen = false;
    render();
  }
}

function onKeydown(event: KeyboardEvent): void {
  if (event.key === 'Escape' && menuOpen) {
    menuOpen = false;
    render();
  }
}

export function initShell(active: NavKey): void {
  activeNav = active;
  initLanguage();
  initTheme();

  const skip = h('a', { class: 'skip-link', href: '#main', 'data-i18n': 'skip.toContent' });
  document.body.prepend(skip);

  render();

  document.addEventListener('click', onClick);
  document.addEventListener('keydown', onKeydown);
  // Redraw the shared parts when something they show has changed.
  for (const name of [EVENTS.languageChanged, EVENTS.themeChanged, EVENTS.hamperChanged]) {
    document.addEventListener(name, () => {
      clear(qs('#site-header'));
      render();
    });
  }
}
