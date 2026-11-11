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

document.addEventListener('keydown', (event) => {
  if (event.key === 'Escape') setMenu(false);
});
