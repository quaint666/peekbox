// Prices, budget and the safe-expiry rule. All plain functions: no page, no
// browser, no saving. Give them data, get an answer. That is what makes them
// easy to test (tests/pricing.test.mjs).
//
// IMPORTANT, read this before changing anything:
// In the real shop the SERVER works out prices, and the browser only shows
// them (a rule from our earlier project, Bloomy). This front-end-only version
// has no server, so this file stands in for it. Keep ALL money maths in this
// file, nowhere else, so the stand-in can be thrown away later in one go.
// Money is always whole dong (integers). Prices already include tax.

import { SHELF_LIFE_MARGIN_DAYS } from './config.js';
import type { Hamper, Packaging, Product, Quote } from './types.js';

// 180000 -> "180.000đ"
export function formatVnd(amount: number): string {
  const sign = amount < 0 ? '-' : '';
  const digits = String(Math.abs(Math.round(amount)));
  return sign + digits.replace(/\B(?=(\d{3})+(?!\d))/g, '.') + 'đ';
}

const DAY_MS = 86_400_000;

// 'YYYY-MM-DD' -> milliseconds (UTC), or NaN if the text is not a date.
function parseDay(iso: string): number {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso);
  if (!match) return Number.NaN;
  return Date.UTC(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
}

// THE SAFE-EXPIRY RULE: an item can be ordered only if its expiry date is
// LATER than the delivery date plus the margin. A date we cannot read is
// never "in date" (when unsure, block the order, never allow it).
export function isInDate(expiry: string, delivery: string, marginDays: number = SHELF_LIFE_MARGIN_DAYS): boolean {
  const expiryMs = parseDay(expiry);
  const deliveryMs = parseDay(delivery);
  if (Number.isNaN(expiryMs) || Number.isNaN(deliveryMs)) return false;
  return expiryMs > deliveryMs + marginDays * DAY_MS;
}

export function qtyOf(hamper: Hamper, slug: string): number {
  return hamper.lines.find((line) => line.slug === slug)?.qty ?? 0;
}

// Total price of the goods in the hamper. Lines whose product is unknown
// are skipped and reported in `missing`.
export function goodsTotal(hamper: Hamper, products: ReadonlyMap<string, Product>): { total: number; missing: string[] } {
  let total = 0;
  const missing: string[] = [];
  for (const line of hamper.lines) {
    const product = products.get(line.slug);
    if (product) total += product.price_vnd * line.qty;
    else missing.push(line.slug);
  }
  return { total, missing };
}

export function quote(hamper: Hamper, products: ReadonlyMap<string, Product>, packagingOptions: readonly Packaging[]): Quote {
  const goods = goodsTotal(hamper, products);
  const packaging = packagingOptions.find((option) => option.id === hamper.packaging_id);
  const packagingVnd = packaging?.fee_vnd ?? 0;
  // The budget covers the GOODS only, not packaging and not delivery (decided).
  const remaining = hamper.budget_vnd - goods.total;
  return {
    goods_vnd: goods.total,
    packaging_vnd: packagingVnd,
    total_vnd: goods.total + packagingVnd,
    budget_vnd: hamper.budget_vnd,
    remaining_vnd: remaining,
    over_budget: remaining < 0,
    missing_slugs: goods.missing,
  };
}

// Why an item cannot be added right now (or 'ok' if it can).
// The builder shows a different message for each one.
export type ItemStatus = 'ok' | 'sold_out' | 'expired_for_date' | 'over_budget';

export function itemStatus(product: Product, hamper: Hamper, products: ReadonlyMap<string, Product>): ItemStatus {
  if (product.stock - qtyOf(hamper, product.slug) <= 0) return 'sold_out';
  if (hamper.delivery_date !== null && !isInDate(product.expiry_date, hamper.delivery_date)) return 'expired_for_date';
  const remaining = hamper.budget_vnd - goodsTotal(hamper, products).total;
  if (product.price_vnd > remaining) return 'over_budget';
  return 'ok';
}

// How much more than the remaining budget an item costs (0 if it fits).
export function overBy(product: Product, remainingVnd: number): number {
  return Math.max(0, product.price_vnd - remainingVnd);
}
