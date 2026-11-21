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

// The floating buttons. "Lên đầu trang" (F07): shown once the page has been scrolled a screen or so; it scrolls up smoothly unless
// the visitor asked for less motion, and the focus goes to the logo so the keyboard is at the top too (the button hides itself on
// the way up). A bar that stays at the bottom of a phone screen (data-bottom-bar) must not be covered by the buttons: while it is
// stuck to the bottom edge of the screen their lift is its height (--floating-lift). At the end of a page such a bar sits in the
// page above the footer and is no longer in the way (lift 0); a wide screen hides it (height 0).
const toTop = document.querySelector<HTMLButtonElement>('[data-action="to-top"]');
const bottomBars = document.querySelectorAll<HTMLElement>('[data-bottom-bar]');
const SHOW_TOP_AFTER_PX = 600;
let topFrame = 0;

function updateFloating(): void {
  topFrame = 0;
  if (toTop) toTop.hidden = window.scrollY <= SHOW_TOP_AFTER_PX;
  let lift = 0;
  bottomBars.forEach((bar) => {
    const box = bar.getBoundingClientRect();
    if (box.height > 0 && box.bottom >= window.innerHeight - 2) lift = Math.max(lift, box.height);
  });
  document.documentElement.style.setProperty('--floating-lift', `${lift}px`);
}

function askForUpdate(): void {
  if (topFrame === 0) topFrame = window.requestAnimationFrame(updateFloating);
}

window.addEventListener('scroll', askForUpdate, { passive: true });
window.addEventListener('resize', askForUpdate);
if (bottomBars.length > 0 && typeof ResizeObserver !== 'undefined') {
  const observer = new ResizeObserver(askForUpdate);
  bottomBars.forEach((bar) => observer.observe(bar));
}
toTop?.addEventListener('click', () => {
  const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  window.scrollTo({ top: 0, behavior: reduce ? 'auto' : 'smooth' });
  document.querySelector<HTMLElement>('.site-header a')?.focus({ preventScroll: true });
});
updateFloating();

// Back and Forward. Safari keeps whole pages in its back-forward cache and shows them exactly as they were left: a
// builder page could still show items that were taken out in the cart since, and its buttons would send amounts based
// on that old box. Every page shows the hamper in the header, so a page restored from that cache is loaded again.
// (Checked in WebKit; Chromium loaded the page again by itself in the same test.)
window.addEventListener('pageshow', (event) => {
  if (event.persisted) {
    window.location.reload();
  }
});
