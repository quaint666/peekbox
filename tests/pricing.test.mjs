// Tests for src/lib/pricing.ts (run on the compiled file, so `npm test` builds first).
// The rule cases live in tests/vectors/pricing.json, shared with any future backend.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { test } from 'node:test';
import { formatVnd, isInDate, itemStatus, overBy, qtyOf, quote } from '../public/js/lib/pricing.js';

const vectors = JSON.parse(readFileSync(join(import.meta.dirname, 'vectors', 'pricing.json'), 'utf8'));
const products = new Map(Object.values(vectors.products).map((p) => [p.slug, p]));

for (const c of vectors.isInDate) {
  test(`isInDate: ${c.name}`, () => {
    assert.equal(isInDate(c.expiry, c.delivery, c.margin_days), c.expected);
  });
}

for (const c of vectors.quote) {
  test(`quote: ${c.name}`, () => {
    const q = quote(c.hamper, products, vectors.packaging);
    for (const [key, value] of Object.entries(c.expected)) assert.deepEqual(q[key], value, key);
  });
}

for (const c of vectors.itemStatus) {
  test(`itemStatus: ${c.name}`, () => {
    assert.equal(itemStatus(products.get(c.slug), c.hamper, products), c.expected);
  });
}

// The default margin comes from config.ts (7 days): it is not part of the shared cases.
test('isInDate uses the default margin from config (7 days)', () => {
  assert.equal(isInDate('2027-01-23', '2027-01-15'), true);
  assert.equal(isInDate('2027-01-22', '2027-01-15'), false);
});

// Display and helper functions: front end only, so they are not in the shared file.
test('formatVnd uses dots and the đ sign', () => {
  assert.equal(formatVnd(180_000), '180.000đ');
  assert.equal(formatVnd(0), '0đ');
  assert.equal(formatVnd(1_000_000), '1.000.000đ');
  assert.equal(formatVnd(-35_000), '-35.000đ');
});

test('overBy and qtyOf', () => {
  const hamper = { budget_vnd: 500_000, delivery_date: null, packaging_id: 'kraft', lines: [{ slug: 'a', qty: 2 }] };
  assert.equal(overBy(products.get('a'), 145_000), 35_000);
  assert.equal(overBy(products.get('a'), 500_000), 0);
  assert.equal(qtyOf(hamper, 'a'), 2);
  assert.equal(qtyOf(hamper, 'zzz'), 0);
});
