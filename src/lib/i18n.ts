// Vietnamese / English.
//
// Two kinds of text:
//  1. Interface text (buttons, menus, messages): src/i18n/vi.ts and en.ts,
//     read with t('key').
//  2. Data text (item names, descriptions): fields like name_vi / name_en in
//     the data files, read with localized(item, 'name').
//
// If English is missing, Vietnamese shows instead (a decision we made).

import { DEFAULT_LANGUAGE, EVENTS, LANGUAGES, STORAGE_KEYS } from './config.js';
import { loadText, saveText } from './storage.js';
import type { Language } from './types.js';
import { en } from '../i18n/en.js';
import { vi, type MessageKey } from '../i18n/vi.js';

const DICTIONARIES: Record<Language, Record<MessageKey, string>> = { vi, en };

let current: Language = DEFAULT_LANGUAGE;

function isLanguage(value: string | null): value is Language {
  return value !== null && (LANGUAGES as readonly string[]).includes(value);
}

export function isMessageKey(value: string): value is MessageKey {
  return value in vi;
}

// Call once when a page starts (initShell does it for you).
export function initLanguage(): void {
  const saved = loadText(STORAGE_KEYS.language);
  current = isLanguage(saved) ? saved : DEFAULT_LANGUAGE;
  document.documentElement.lang = current;
}

export function getLanguage(): Language {
  return current;
}

export function setLanguage(language: Language): void {
  if (language === current) return;
  current = language;
  saveText(STORAGE_KEYS.language, language);
  document.documentElement.lang = language;
  applyTranslations();
  document.dispatchEvent(new CustomEvent(EVENTS.languageChanged, { detail: language }));
}

function fill(template: string, params: Record<string, string | number>): string {
  return template.replace(/\{(\w+)\}/g, (whole, name: string) => String(params[name] ?? whole));
}

export function t(key: MessageKey, params: Record<string, string | number> = {}): string {
  const text = DICTIONARIES[current][key] || DICTIONARIES.vi[key];
  return fill(text, params);
}

// For data: localized(product, 'name') gives name_en or name_vi.
// For a field with no _vi twin (like brand / brand_en), it falls back to the plain field.
export function localized(item: object, field: string): string {
  const record = item as Record<string, unknown>;
  const wanted = record[`${field}_${current}`];
  if (typeof wanted === 'string' && wanted !== '') return wanted;
  const fallback = record[`${field}_vi`] ?? record[field];
  return typeof fallback === 'string' ? fallback : '';
}

// Fills in plain HTML marked for translation:
//   <h1 data-i18n="builder.title"></h1>               text
//   <button data-i18n-aria-label="nav.openMenu">      aria-label
//   <input data-i18n-placeholder="builder.search">    placeholder
export function applyTranslations(root: ParentNode = document): void {
  const targets: Array<[string, (el: Element, text: string) => void]> = [
    ['data-i18n', (el, text) => { el.textContent = text; }],
    ['data-i18n-aria-label', (el, text) => el.setAttribute('aria-label', text)],
    ['data-i18n-placeholder', (el, text) => el.setAttribute('placeholder', text)],
  ];
  for (const [attribute, apply] of targets) {
    for (const el of root.querySelectorAll(`[${attribute}]`)) {
      const key = el.getAttribute(attribute) ?? '';
      if (isMessageKey(key)) apply(el, t(key));
      else console.warn(`Unknown translation key "${key}"`);
    }
  }
}
