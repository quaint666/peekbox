// The occasion pages (F15): the ← → buttons of each budget's row of hampers.
import { wireRows } from './lib/rows.js';

wireRows(document);

// "Dịp khác": the menu closes on Esc (focus goes back to its button), on a click outside it, and when a link is chosen.
const menu = document.querySelector<HTMLDetailsElement>('[data-switch]');
if (menu) {
  document.addEventListener('click', (event) => {
    if (menu.open && event.target instanceof Node && !menu.contains(event.target)) {
      menu.open = false;
    }
  });
  menu.addEventListener('keydown', (event) => {
    if (event.key === 'Escape' && menu.open) {
      menu.open = false;
      menu.querySelector('summary')?.focus();
    }
  });
}

// The "Ngân sách" bar stays at the top of the screen (occasion.css). The chip of the budget row in view is marked, so the
// bar says where you are; on a phone, where the chips scroll sideways, the list also keeps that chip in sight.
const budgetBar = document.querySelector<HTMLElement>('.occ-budgets');
const budgetList = budgetBar?.querySelector<HTMLElement>('[data-budget-list]');
if (budgetBar && budgetList) {
  const rows = Array.from(budgetList.querySelectorAll<HTMLAnchorElement>('a[href^="#"]'))
    .map((chip) => ({ chip, row: document.getElementById(decodeURIComponent(chip.hash.slice(1))) }))
    .filter((pair): pair is { chip: HTMLAnchorElement; row: HTMLElement } => pair.row !== null);
  const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
  const inBand = new Set<Element>();
  let current: HTMLAnchorElement | null = null;

  const show = (chip: HTMLAnchorElement | null): void => {
    if (chip === current) {
      return;
    }

    current?.removeAttribute('aria-current');
    chip?.setAttribute('aria-current', 'true');
    current = chip;
    if (budgetList.scrollWidth > budgetList.clientWidth) {
      // The marked chip is centred; with none marked (back above the first row) the list returns to its start.
      const left = chip ? chip.offsetLeft - (budgetList.clientWidth - chip.offsetWidth) / 2 : 0;
      budgetList.scrollTo({ left, behavior: reduceMotion.matches ? 'auto' : 'smooth' });
    }
  };

  // The band is the upper part of the screen under the bar. The last row in it (page order) is the one being read;
  // between two rows the mark stays, and above the first row there is none.
  const observer = new IntersectionObserver((entries) => {
    for (const entry of entries) {
      if (entry.isIntersecting) {
        inBand.add(entry.target);
      } else {
        inBand.delete(entry.target);
      }
    }

    const inView = rows.filter((pair) => inBand.has(pair.row));
    const last = inView[inView.length - 1];
    if (last) {
      show(last.chip);
    } else if (rows[0] && rows[0].row.getBoundingClientRect().top > budgetBar.offsetHeight) {
      show(null);
    }
  }, { rootMargin: `-${budgetBar.offsetHeight}px 0px -55% 0px` });
  rows.forEach((pair) => observer.observe(pair.row));
}
