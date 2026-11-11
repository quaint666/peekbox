// Search that ignores accents and capital letters:
// typing "mut" must find "Mứt gừng", and "den" must find "Đen".

import type { Product } from './types.js';

export function normalize(text: string): string {
  return text
    .toLowerCase()
    .normalize('NFD') // split letters from their accent marks
    .replace(/[̀-ͯ]/g, '') // drop the accent marks
    .replace(/đ/g, 'd') // đ is its own letter, not an accent
    .replace(/\s+/g, ' ')
    .trim();
}

// True if EVERY word of the query appears somewhere in the texts.
// An empty query matches everything.
export function matchesQuery(query: string, ...texts: string[]): boolean {
  const wanted = normalize(query);
  if (wanted === '') return true;
  const haystack = normalize(texts.join(' '));
  return wanted.split(' ').every((word) => haystack.includes(word));
}

// Looks at both languages, so a Vietnamese shopper can type an English word
// and the other way round.
export function searchProducts(products: readonly Product[], query: string): Product[] {
  return products.filter((p) => matchesQuery(query, p.name_vi, p.name_en, p.brand, p.brand_en, p.origin));
}
