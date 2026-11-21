// What the shared header does in the browser: the light/dark button and the
// phone menu. The page itself is made by the server; this file only reacts to
// clicks.

// Must equal the key in wwwroot/theme-init.js (a test checks that they match).
const THEME_KEY = 'peekbox.theme';

type Theme = 'light' | 'dark';

function savedTheme(): Theme | null {
  try {
    const value = localStorage.getItem(THEME_KEY);
    return value === 'light' || value === 'dark' ? value : null;
  } catch {
    return null; // localStorage can be blocked
  }
}

function currentTheme(): Theme {
  return savedTheme() ?? (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
}

function toggleTheme(): void {
  const next: Theme = currentTheme() === 'dark' ? 'light' : 'dark';
  try {
    localStorage.setItem(THEME_KEY, next);
  } catch {
    // blocked: the choice lasts until the page is reloaded
  }
  document.documentElement.dataset['theme'] = next;
}

const header = document.querySelector<HTMLElement>('.site-header');
const menuButton = document.querySelector<HTMLButtonElement>('[data-action="toggle-menu"]');

function setMenu(open: boolean): void {
  if (!header || !menuButton) return;
  header.classList.toggle('is-menu-open', open);
  menuButton.setAttribute('aria-expanded', String(open));
  const label = open ? menuButton.dataset['labelClose'] : menuButton.dataset['labelOpen'];
  if (label) menuButton.setAttribute('aria-label', label);
}

document.addEventListener('click', (event) => {
  const target = event.target;
  if (!(target instanceof Element)) return;
  const action = target.closest<HTMLElement>('[data-action]')?.dataset['action'];
  if (action === 'toggle-theme') {
    toggleTheme();
  } else if (action === 'toggle-menu') {
    setMenu(!header?.classList.contains('is-menu-open'));
  } else if (!target.closest('.site-header')) {
    setMenu(false); // a click anywhere else closes the menu
  }
});

// The "Dịp" menu opens on hover and focus. Escape closes it (WCAG 1.4.13, "dismissible") until the pointer or the focus leaves.
const dip = document.querySelector<HTMLElement>('.site-nav__dip');
dip?.addEventListener('pointerleave', () => dip.classList.remove('is-dismissed'));
dip?.addEventListener('focusout', (event: FocusEvent) => {
  if (!(event.relatedTarget instanceof Node) || !dip.contains(event.relatedTarget)) dip.classList.remove('is-dismissed');
});

document.addEventListener('keydown', (event) => {
  if (event.key !== 'Escape') return;
  setMenu(false);
  if (!dip) return;
  dip.classList.add('is-dismissed');
  if (dip.contains(document.activeElement)) dip.querySelector('a')?.focus(); // the focus was on a link that is now hidden
});

// Back and Forward. Safari keeps whole pages in its back-forward cache and shows them exactly as they were left: a
// builder page could still show items that were taken out in the cart since, and its buttons would send amounts based
// on that old box. Every page shows the hamper in the header, so a page restored from that cache is loaded again.
// (Checked in WebKit; Chromium loaded the page again by itself in the same test.)
window.addEventListener('pageshow', (event) => {
  if (event.persisted) {
    window.location.reload();
  }
});
