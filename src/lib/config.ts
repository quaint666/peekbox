// ONE place for values the whole site shares. Never type these numbers or
// names anywhere else; import them from here. (Two copies of one value is the
// most common bug we had in Bloomy.)

import type { Language } from './types.js';

// The safe-expiry margin, in days. An item can be ordered only if its expiry
// date is later than the delivery date plus this margin. A SAMPLE value: in
// the real shop it is a setting the owner changes.
export const SHELF_LIFE_MARGIN_DAYS = 7;

// Quick budget buttons on the builder, and the starting budget.
export const QUICK_BUDGETS_VND = [300_000, 500_000, 800_000, 1_000_000] as const;
export const DEFAULT_BUDGET_VND = 500_000;

export const LANGUAGES: readonly Language[] = ['vi', 'en'];
export const DEFAULT_LANGUAGE: Language = 'vi';

// Where the browser keeps things. public/theme-init.js also reads the theme
// key; tests/seams.test.mjs checks the two copies stay equal.
export const STORAGE_KEYS = {
  language: 'peekbox.language',
  theme: 'peekbox.theme',
  hamper: 'peekbox.hamper',
  orders: 'peekbox.orders',
} as const;

// Where the files are served from (see scripts/copy-data.mjs).
export const DATA_BASE = '/data';

// Names of DOM events the site sends, so listeners and senders agree.
export const EVENTS = {
  languageChanged: 'peekbox:language-changed',
  themeChanged: 'peekbox:theme-changed',
  hamperChanged: 'peekbox:hamper-changed',
} as const;
