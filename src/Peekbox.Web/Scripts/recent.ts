// "Đã xem gần đây" on the item page (F32): the last items this browser opened, newest first, kept in this browser only
// (localStorage, nothing goes to the server). The page says which item it is (data-* on the section, written by the
// server); the others are listed and this one is remembered. Same idea as Bloomy's strip, keyed by the item's slug.
//
// localStorage can be edited by anyone, so what comes out is checked before it is used (a short slug, a short name, a
// picture path inside /images/), a bad entry is dropped, and the page is built with textContent, never as markup. No price
// is kept: it would go out of date, and the item page is the one place that shows it. Without this script, or with
// storage blocked, the section stays hidden.
const KEY = 'peekbox_recent_v1';
const KEEP = 8;
const SLUG = /^[a-z0-9-]{1,80}$/;
const IMAGE = /^\/images\/[A-Za-z0-9._/-]+$/;

interface Seen {
  slug: string;
  name: string;
  image: string;
}

function isSeen(value: unknown): value is Seen {
  if (typeof value !== 'object' || value === null) return false;
  const { slug, name, image } = value as Record<string, unknown>;
  return typeof slug === 'string' && SLUG.test(slug)
    && typeof name === 'string' && name.length > 0 && name.length <= 200
    && typeof image === 'string' && IMAGE.test(image) && !image.includes('..');
}

function read(): Seen[] {
  try {
    const raw: unknown = JSON.parse(localStorage.getItem(KEY) ?? '[]');
    return Array.isArray(raw) ? raw.filter(isSeen).slice(0, KEEP) : [];
  } catch {
    return [];
  }
}

function write(list: Seen[]): void {
  try {
    localStorage.setItem(KEY, JSON.stringify(list));
  } catch {
    // storage full or blocked: the strip is a nicety, not worth interrupting the page for
  }
}

const section = document.querySelector<HTMLElement>('[data-recent]');
const list = section?.querySelector<HTMLUListElement>('[data-recent-list]') ?? null;
const here: unknown = section ? { slug: section.dataset['slug'], name: section.dataset['name'], image: section.dataset['image'] } : null;

if (section && list && isSeen(here)) {
  const before = read();
  const prefix = section.dataset['hrefPrefix'] ?? '/items/';
  for (const seen of before.filter((entry) => entry.slug !== here.slug)) {
    const item = document.createElement('li');
    const link = document.createElement('a');
    link.className = 'recent__link';
    link.href = prefix + encodeURIComponent(seen.slug);
    const photo = document.createElement('img');
    photo.className = 'recent__photo';
    photo.src = seen.image;
    photo.alt = '';
    photo.width = 120;
    photo.height = 120;
    photo.loading = 'lazy';
    const name = document.createElement('span');
    name.className = 'recent__name';
    name.textContent = seen.name;
    link.append(photo, name);
    item.append(link);
    list.append(item);
  }
  section.hidden = list.children.length === 0;
  write([here, ...before.filter((entry) => entry.slug !== here.slug)].slice(0, KEEP));
}
