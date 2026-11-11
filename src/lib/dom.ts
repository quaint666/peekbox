// Small helpers for building pages safely.
//
// THE RULES (they come from the Bloomy project, where breaking them cost days):
//  1. Never use innerHTML, outerHTML or insertAdjacentHTML. Text goes in with
//     textContent (the h() helper below does this for you).
//  2. No inline styles and no inline event attributes. The page's security
//     policy (CSP) blocks them. Use CSS classes, and addEventListener.

export function qs<T extends Element = HTMLElement>(selector: string, root: ParentNode = document): T {
  const found = root.querySelector<T>(selector);
  if (!found) throw new Error(`Element not found: ${selector}`);
  return found;
}

export function qsa<T extends Element = HTMLElement>(selector: string, root: ParentNode = document): T[] {
  return Array.from(root.querySelectorAll<T>(selector));
}

type Child = Node | string | null | undefined | false;
type AttrValue = string | number | boolean | undefined;

// h('a', { class: 'btn', href: '/cart.html' }, 'Giỏ hàng') makes <a class="btn" href="/cart.html">Giỏ hàng</a>.
// Strings become text, never HTML. false/null/undefined children are skipped,
// so you can write  cond && h('span', {}, 'x').
export function h<K extends keyof HTMLElementTagNameMap>(
  tag: K,
  attrs: Record<string, AttrValue> = {},
  ...children: Child[]
): HTMLElementTagNameMap[K] {
  const el = document.createElement(tag);
  for (const [name, value] of Object.entries(attrs)) {
    if (name === 'style' || name.startsWith('on')) {
      throw new Error(`"${name}" is not allowed: use a CSS class or addEventListener.`);
    }
    if (value === undefined || value === false) continue;
    el.setAttribute(name, value === true ? '' : String(value));
  }
  for (const child of children) {
    if (child === null || child === undefined || child === false) continue;
    el.append(typeof child === 'string' ? document.createTextNode(child) : child);
  }
  return el;
}

export function clear(el: Element): void {
  el.replaceChildren();
}

// Icons: simple stroked line icons, drawn from a path. Add new ones here.
const ICONS = {
  cart: 'M3 4h2l2.4 11h10.2L20 7H6 M9 19h.01 M17 19h.01',
  moon: 'M21 13.5A8.5 8.5 0 1 1 10.5 3a7 7 0 0 0 10.5 10.5z',
  sun: 'M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8z M12 2v2 M12 20v2 M4.9 4.9l1.4 1.4 M17.7 17.7l1.4 1.4 M2 12h2 M20 12h2 M4.9 19.1l1.4-1.4 M17.7 6.3l1.4-1.4',
  menu: 'M4 7h16 M4 12h16 M4 17h16',
  close: 'M6 6l12 12 M18 6L6 18',
  search: 'M11 4a7 7 0 1 0 0 14 7 7 0 0 0 0-14z M20 20l-3.5-3.5',
  check: 'M5 12.5l4.5 4.5L19 7',
  chevronUp: 'M6 14l6-6 6 6',
  chat: 'M21 12a8 8 0 0 1-11.6 7.1L4 20l1-4.6A8 8 0 1 1 21 12z',
} as const;

export type IconName = keyof typeof ICONS;

const SVG_NS = 'http://www.w3.org/2000/svg';

export function icon(name: IconName, size = 20): SVGSVGElement {
  const svg = document.createElementNS(SVG_NS, 'svg');
  svg.setAttribute('width', String(size));
  svg.setAttribute('height', String(size));
  svg.setAttribute('viewBox', '0 0 24 24');
  svg.setAttribute('fill', 'none');
  svg.setAttribute('stroke', 'currentColor');
  svg.setAttribute('stroke-width', '1.8');
  svg.setAttribute('stroke-linecap', 'round');
  svg.setAttribute('stroke-linejoin', 'round');
  svg.setAttribute('aria-hidden', 'true');
  const path = document.createElementNS(SVG_NS, 'path');
  path.setAttribute('d', ICONS[name]);
  svg.append(path);
  return svg;
}
