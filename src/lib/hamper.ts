// The hamper the customer is building: where it is kept and how it changes.
//
// It lives in the browser (localStorage), because there are no customer
// accounts. Whenever it is saved, a "hamper changed" event is sent so the cart
// count in the header (and anything else listening) updates by itself.

import { DEFAULT_BUDGET_VND, EVENTS, STORAGE_KEYS } from './config.js';
import { loadJson, saveJson } from './storage.js';
import type { Hamper } from './types.js';

export function emptyHamper(): Hamper {
  return { budget_vnd: DEFAULT_BUDGET_VND, delivery_date: null, packaging_id: 'kraft', lines: [] };
}

function isHamper(value: unknown): value is Hamper {
  if (typeof value !== 'object' || value === null) return false;
  const v = value as Record<string, unknown>;
  return (
    typeof v['budget_vnd'] === 'number' &&
    (v['delivery_date'] === null || typeof v['delivery_date'] === 'string') &&
    typeof v['packaging_id'] === 'string' &&
    Array.isArray(v['lines']) &&
    v['lines'].every((line) => {
      const l = line as Record<string, unknown>;
      return typeof l['slug'] === 'string' && Number.isInteger(l['qty']) && (l['qty'] as number) > 0;
    })
  );
}

export function getHamper(): Hamper {
  return loadJson(STORAGE_KEYS.hamper, emptyHamper(), isHamper);
}

export function saveHamper(hamper: Hamper): void {
  saveJson(STORAGE_KEYS.hamper, hamper);
  document.dispatchEvent(new CustomEvent(EVENTS.hamperChanged));
}

// Number of items (counting quantities).
export function hamperCount(hamper: Hamper): number {
  return hamper.lines.reduce((sum, line) => sum + line.qty, 0);
}

// Returns a NEW hamper with the quantity of one item set (0 or less removes
// it). It does not save; call saveHamper() with the result.
export function withQty(hamper: Hamper, slug: string, qty: number): Hamper {
  const whole = Math.floor(qty);
  const exists = hamper.lines.some((line) => line.slug === slug);
  // Keep the order of the lines: change in place, or add at the end.
  const lines = hamper.lines.map((line) => (line.slug === slug ? { slug, qty: whole } : line)).filter((line) => line.qty > 0);
  if (!exists && whole > 0) lines.push({ slug, qty: whole });
  return { ...hamper, lines };
}
