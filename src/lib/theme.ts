// Light / dark mode.
//
// With nothing saved, the site follows the visitor's system setting (done by
// CSS alone). Once they press the button, their choice is saved and written on
// the page as <html data-theme="dark|light">; the CSS in tokens.css reads it.
// public/theme-init.js applies the saved choice before the page is drawn, so
// there is no flash of the wrong colours.

import { EVENTS, STORAGE_KEYS } from './config.js';
import { loadText, saveText } from './storage.js';
import type { Theme } from './types.js';

function isTheme(value: string | null): value is Theme {
  return value === 'light' || value === 'dark';
}

// What the visitor is looking at right now.
export function getTheme(): Theme {
  const saved = loadText(STORAGE_KEYS.theme);
  if (isTheme(saved)) return saved;
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}

export function setTheme(theme: Theme): void {
  saveText(STORAGE_KEYS.theme, theme);
  document.documentElement.dataset['theme'] = theme;
  document.dispatchEvent(new CustomEvent(EVENTS.themeChanged, { detail: theme }));
}

export function toggleTheme(): void {
  setTheme(getTheme() === 'dark' ? 'light' : 'dark');
}

export function initTheme(): void {
  const saved = loadText(STORAGE_KEYS.theme);
  if (isTheme(saved)) document.documentElement.dataset['theme'] = saved;
}
