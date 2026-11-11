// Tests for src/lib/search.ts: search must ignore accents and capital letters.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { matchesQuery, normalize, searchProducts } from '../public/js/lib/search.js';

test('normalize removes accents, the đ letter and capitals', () => {
  assert.equal(normalize('Mứt gừng'), 'mut gung');
  assert.equal(normalize('Đen  HẠT '), 'den hat');
});

test('typing without accents finds the accented word', () => {
  assert.equal(matchesQuery('mut', 'Mứt gừng'), true);
  assert.equal(matchesQuery('MUT GUNG', 'Mứt gừng'), true);
  assert.equal(matchesQuery('tra', 'Mứt gừng'), false);
});

test('every word of the query must match, in any order', () => {
  assert.equal(matchesQuery('gung mut', 'Mứt gừng'), true);
  assert.equal(matchesQuery('mut dau', 'Mứt gừng'), false);
});

test('an empty query matches everything', () => {
  assert.equal(matchesQuery('', 'anything'), true);
  assert.equal(matchesQuery('   ', 'anything'), true);
});

test('searchProducts looks at both languages, brand and origin', () => {
  const list = [
    { name_vi: 'Mứt gừng', name_en: 'Candied ginger', brand: 'Thương hiệu C', brand_en: 'Brand C', origin: 'Huế' },
    { name_vi: 'Trà sen', name_en: 'Lotus tea', brand: 'Thương hiệu A', brand_en: 'Brand A', origin: 'Hà Nội' },
  ];
  assert.equal(searchProducts(list, 'ginger').length, 1);
  assert.equal(searchProducts(list, 'hue').length, 1);
  assert.equal(searchProducts(list, 'thuong hieu').length, 2);
  assert.equal(searchProducts(list, 'sen')[0].name_en, 'Lotus tea');
});
