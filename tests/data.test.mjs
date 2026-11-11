// Checks the sample data in content/data/ against itself and against the
// image files. These are the "two places that must agree" problems: an image
// path with no file behind it, a starter box with an item that does not exist,
// a total that no longer adds up.
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { test } from 'node:test';

const root = join(import.meta.dirname, '..');
const read = (file) => JSON.parse(readFileSync(join(root, 'content', 'data', file), 'utf8'));
const { products, categories } = read('products.json');
const { packaging } = read('packaging.json');
const { campaigns } = read('campaigns.json');

test('product slugs are unique, and every product has both languages', () => {
  const slugs = products.map((p) => p.slug);
  assert.equal(new Set(slugs).size, slugs.length);
  for (const p of products) {
    assert.ok(p.name_vi && p.name_en, `${p.slug}: missing a name`);
    assert.ok(p.description_vi && p.description_en, `${p.slug}: missing a description`);
  }
});

test('every product has a known category, a whole-dong price and a real expiry date', () => {
  const ids = new Set(categories.map((c) => c.id));
  for (const p of products) {
    assert.ok(ids.has(p.category), `${p.slug}: unknown category ${p.category}`);
    assert.ok(Number.isInteger(p.price_vnd) && p.price_vnd > 0, `${p.slug}: bad price`);
    assert.match(p.expiry_date, /^\d{4}-\d{2}-\d{2}$/, `${p.slug}: bad expiry date`);
  }
});

test('every image path in the data points at a file in public/', () => {
  const paths = [...products.map((p) => p.image), ...packaging.map((p) => p.image)];
  for (const path of paths) {
    assert.ok(path.startsWith('/images/'), `${path}: should start with /images/`);
    assert.ok(existsSync(join(root, 'public', path)), `${path}: file not found in public/`);
  }
});

test('starter boxes use real items, add up, and fit their budget', () => {
  const bySlug = new Map(products.map((p) => [p.slug, p]));
  for (const campaign of campaigns) {
    for (const box of campaign.starter_boxes) {
      const missing = box.items.filter((slug) => !bySlug.has(slug));
      assert.deepEqual(missing, [], `${box.name_vi}: unknown items`);
      const total = box.items.reduce((sum, slug) => sum + bySlug.get(slug).price_vnd, 0);
      assert.equal(total, box.goods_total_vnd, `${box.name_vi}: total does not add up`);
      assert.ok(total <= box.budget_vnd, `${box.name_vi}: over its own budget`);
    }
  }
});
