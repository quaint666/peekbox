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
