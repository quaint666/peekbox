// Guards for the rules that broke in Bloomy. Each test protects one rule from
// docs/frontend-plan.md. If one fails, fix the code, not the test.
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { test } from 'node:test';
import { EVENTS, STORAGE_KEYS } from '../public/js/lib/config.js';
import { en } from '../public/js/i18n/en.js';
import { vi } from '../public/js/i18n/vi.js';

const root = join(import.meta.dirname, '..');
const read = (path) => readFileSync(join(root, path), 'utf8');
const walk = (dir, ext) =>
  readdirSync(join(root, dir), { withFileTypes: true }).flatMap((entry) => {
    const path = `${dir}/${entry.name}`;
    if (entry.isDirectory()) return walk(path, ext);
    return path.endsWith(ext) ? [path] : [];
  });

test('theme-init.js uses the same storage key as the code', () => {
  assert.ok(read('public/theme-init.js').includes(`'${STORAGE_KEYS.theme}'`));
});

test('every storage key and event name is unique and starts with "peekbox"', () => {
  const values = [...Object.values(STORAGE_KEYS), ...Object.values(EVENTS)];
  assert.equal(new Set(values).size, values.length);
  for (const value of values) assert.match(value, /^peekbox[.:]/);
});

test('English has exactly the same keys as Vietnamese, and none is empty', () => {
  assert.deepEqual(Object.keys(en).sort(), Object.keys(vi).sort());
  for (const [key, text] of Object.entries({ ...vi, ...en })) assert.ok(text.trim() !== '', `${key} is empty`);
});

// Removes // and /* */ comments, so a comment that mentions a banned word is fine.
const withoutComments = (code) => code.replace(/\/\*[\s\S]*?\*\//g, '').replace(/(^|[^:])\/\/.*$/gm, '$1');

test('no innerHTML, outerHTML or insertAdjacentHTML in the TypeScript', () => {
  for (const file of walk('src', '.ts')) {
    const found = /\b(innerHTML|outerHTML|insertAdjacentHTML)\b/.test(withoutComments(read(file)));
    assert.ok(!found, `${file} writes HTML from a string`);
  }
});

test('every HTML page has the security policy and no inline styles or scripts', () => {
  const pages = readdirSync(join(root, 'public')).filter((name) => name.endsWith('.html'));
  assert.ok(pages.length > 0);
  for (const name of pages) {
    const html = read(`public/${name}`);
    const body = html.replace(/<!--[\s\S]*?-->/g, ''); // comments may mention these words
    assert.match(html, /http-equiv="Content-Security-Policy"/, `${name}: no CSP meta tag`);
    assert.ok(!/\sstyle\s*=/.test(body), `${name}: inline style attribute`);
    assert.ok(!/<style[\s>]/.test(body), `${name}: <style> block`);
    assert.ok(!/\son[a-z]+\s*=\s*"/.test(body), `${name}: inline event attribute`);
    assert.ok(!/<script(?![^>]*\ssrc=)[^>]*>/.test(body), `${name}: inline <script>`);
  }
});

test('the global [hidden] rule is still in base.css', () => {
  assert.match(read('public/css/base.css'), /\[hidden\]\s*\{\s*display:\s*none\s*!important/);
});

test('CSS files use variables for colour (hex colours only in tokens.css)', () => {
  for (const file of walk('public/css', '.css').filter((f) => !f.endsWith('tokens.css'))) {
    const withoutAllowed = read(file).replace(/#FFFFFF|#1B2620/g, ''); // white on primary, the toast
    assert.ok(!/#[0-9a-fA-F]{3,8}\b/.test(withoutAllowed), `${file}: use a variable from tokens.css`);
  }
});
