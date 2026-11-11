// Tests for src/lib/pricing.ts (run on the compiled file, so `npm test` builds first).
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { formatVnd, goodsTotal, isInDate, itemStatus, overBy, qtyOf, quote } from '../public/js/lib/pricing.js';

const product = (slug, price, expiry, stock = 10) => ({ slug, price_vnd: price, expiry_date: expiry, stock });
const products = new Map([
  ['a', product('a', 180_000, '2027-12-31')],
  ['b', product('b', 150_000, '2027-01-10')],
  ['c', product('c', 90_000, '2027-12-31', 0)],
]);
const packaging = [
  { id: 'kraft', fee_vnd: 0 },
  { id: 'hard-bow', fee_vnd: 40_000 },
];
const hamper = (lines, extra = {}) => ({ budget_vnd: 500_000, delivery_date: '2027-01-15', packaging_id: 'kraft', lines, ...extra });

test('formatVnd uses dots and the đ sign', () => {
  assert.equal(formatVnd(180_000), '180.000đ');
  assert.equal(formatVnd(0), '0đ');
  assert.equal(formatVnd(1_000_000), '1.000.000đ');
  assert.equal(formatVnd(-35_000), '-35.000đ');
});

test('isInDate: expiry must be LATER than delivery plus the margin', () => {
  assert.equal(isInDate('2027-01-23', '2027-01-15', 7), true); // 8 days after
  assert.equal(isInDate('2027-01-22', '2027-01-15', 7), false); // exactly 7 days: not later
  assert.equal(isInDate('2027-01-10', '2027-01-15', 7), false); // already expired
});

test('isInDate: a date we cannot read is never in date', () => {
  assert.equal(isInDate('', '2027-01-15'), false);
  assert.equal(isInDate('2027-01-23', 'tomorrow'), false);
});

test('isInDate uses the default margin from config (7 days)', () => {
  assert.equal(isInDate('2027-01-23', '2027-01-15'), true);
  assert.equal(isInDate('2027-01-22', '2027-01-15'), false);
});

test('goodsTotal adds price x quantity and reports unknown items', () => {
  const result = goodsTotal(hamper([{ slug: 'a', qty: 2 }, { slug: 'zzz', qty: 1 }]), products);
  assert.equal(result.total, 360_000);
  assert.deepEqual(result.missing, ['zzz']);
});

test('quote: the budget covers goods only, packaging is on top', () => {
  const q = quote(hamper([{ slug: 'a', qty: 1 }], { packaging_id: 'hard-bow' }), products, packaging);
  assert.equal(q.goods_vnd, 180_000);
  assert.equal(q.packaging_vnd, 40_000);
  assert.equal(q.total_vnd, 220_000);
  assert.equal(q.remaining_vnd, 320_000); // 500.000 - 180.000, packaging not subtracted
  assert.equal(q.over_budget, false);
});

test('quote flags a hamper over budget', () => {
  const q = quote(hamper([{ slug: 'a', qty: 3 }]), products, packaging);
  assert.equal(q.remaining_vnd, -40_000);
  assert.equal(q.over_budget, true);
});

test('itemStatus: sold out, expired for the date, over budget, ok', () => {
  const empty = hamper([]);
  assert.equal(itemStatus(products.get('c'), empty, products), 'sold_out');
  assert.equal(itemStatus(products.get('b'), empty, products), 'expired_for_date');
  assert.equal(itemStatus(products.get('a'), hamper([{ slug: 'a', qty: 2 }]), products), 'over_budget'); // 140.000 left, 180.000 needed
  assert.equal(itemStatus(products.get('a'), empty, products), 'ok');
});

test('itemStatus ignores the expiry rule until a delivery date is chosen', () => {
  assert.equal(itemStatus(products.get('b'), hamper([], { delivery_date: null }), products), 'ok');
});

test('overBy and qtyOf', () => {
  assert.equal(overBy(products.get('a'), 145_000), 35_000);
  assert.equal(overBy(products.get('a'), 500_000), 0);
  assert.equal(qtyOf(hamper([{ slug: 'a', qty: 2 }]), 'a'), 2);
  assert.equal(qtyOf(hamper([]), 'a'), 0);
});
