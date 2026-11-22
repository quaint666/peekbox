import { beforeEach, describe, expect, it } from 'vitest';
import { load } from './support.js';

const KEY = 'peekbox_recent_v1';
const page = (slug = 'tra-sen', name = 'Trà sen', image = '/images/products/tra-sen.webp'): string => `
  <section data-recent data-slug="${slug}" data-name="${name}" data-image="${image}" data-href-prefix="/items/" hidden>
    <ul data-recent-list></ul>
  </section>`;

const stored = (): { slug: string; name: string; image: string }[] => JSON.parse(localStorage.getItem(KEY) ?? '[]');
const section = (): HTMLElement => document.querySelector<HTMLElement>('[data-recent]')!;
const names = (): string[] => [...document.querySelectorAll('.recent__name')].map((n) => n.textContent ?? '');
const seen = (slug: string): object => ({ slug, name: `Món ${slug}`, image: `/images/products/${slug}.webp` });

beforeEach(() => localStorage.clear());

describe('recent.ts', () => {
  it('stays hidden on a first visit and remembers the item', async () => {
    await load('recent', page());
    expect(section().hidden).toBe(true);
    expect(stored()).toEqual([{ slug: 'tra-sen', name: 'Trà sen', image: '/images/products/tra-sen.webp' }]);
  });

  it('lists the others, newest first, never the item itself, and puts this one first', async () => {
    localStorage.setItem(KEY, JSON.stringify([seen('a'), seen('tra-sen'), seen('b')]));
    await load('recent', page());
    expect(section().hidden).toBe(false);
    expect(names()).toEqual(['Món a', 'Món b']);
    expect(stored().map((s) => s.slug)).toEqual(['tra-sen', 'a', 'b']);
    expect(document.querySelector<HTMLAnchorElement>('.recent__link')!.getAttribute('href')).toBe('/items/a');
  });

  it('keeps at most 8', async () => {
    localStorage.setItem(KEY, JSON.stringify(['a', 'b', 'c', 'd', 'e', 'f', 'g', 'h'].map(seen)));
    await load('recent', page());
    expect(stored()).toHaveLength(8);
    expect(stored()[0]!.slug).toBe('tra-sen');
    expect(stored().map((s) => s.slug)).not.toContain('h');
  });

  it('drops entries that were edited into something unsafe, and shows the rest', async () => {
    localStorage.setItem(KEY, JSON.stringify([
      { slug: 'x"><script>', name: 'bad slug', image: '/images/a.webp' },
      { slug: 'ok-1', name: 'bad image', image: 'https://evil.example/a.webp' },
      { slug: 'ok-2', name: 'dot dot', image: '/images/../secret.webp' },
      { slug: 'ok-3', name: '', image: '/images/a.webp' },
      { slug: 'ok-4', name: 'n'.repeat(201), image: '/images/a.webp' },
      42, null, 'text',
      seen('good'),
    ]));
    await load('recent', page());
    expect(names()).toEqual(['Món good']);
    expect(stored().map((s) => s.slug)).toEqual(['tra-sen', 'good']);
  });

  it('writes a name as text, never as markup', async () => {
    localStorage.setItem(KEY, JSON.stringify([{ slug: 'a', name: '<img src=x onerror=alert(1)>', image: '/images/a.webp' }]));
    await load('recent', page());
    expect(document.querySelector('.recent__name')!.textContent).toBe('<img src=x onerror=alert(1)>');
    expect(document.querySelectorAll('.recent__name img')).toHaveLength(0);
  });

  it('survives broken JSON and a list that is not a list', async () => {
    localStorage.setItem(KEY, '{not json');
    await load('recent', page());
    expect(section().hidden).toBe(true);
    localStorage.setItem(KEY, '{"a":1}');
    await load('recent', page());
    expect(section().hidden).toBe(true);
    expect(stored()).toHaveLength(1);
  });

  it('does nothing when the page does not describe its item', async () => {
    await load('recent', '<section data-recent hidden><ul data-recent-list></ul></section>');
    expect(section().hidden).toBe(true);
    expect(localStorage.getItem(KEY)).toBeNull();
  });
});
