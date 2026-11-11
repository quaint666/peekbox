// The script for public/index.html. EVERY page has one script like this in
// src/pages/, named after the page, and loads it at the bottom of its HTML:
//     <script type="module" src="/js/pages/home.js"></script>
// (TypeScript turns src/pages/home.ts into public/js/pages/home.js.)
//
// The pattern for a page:
//   1. initShell('...') draws the header and footer.
//   2. Load the data with the functions in lib/data.ts.
//   3. Write a function that draws the page from the data, and call it again
//      whenever the language changes (so the text switches without a reload).

import { EVENTS } from '../lib/config.js';
import { loadProducts } from '../lib/data.js';
import { qs } from '../lib/dom.js';
import { t } from '../lib/i18n.js';
import { initShell } from '../lib/shell.js';

initShell('home');

// SCAFFOLD CHECK: proves the data loads and that the page follows the
// language switch. Delete this block when you build the real Home page (M1).
const status = qs('#data-check');
let productCount: number | null = null;

function draw(): void {
  if (productCount !== null) status.textContent = t('home.dataCheck', { n: productCount });
}

loadProducts()
  .then(({ products }) => {
    productCount = products.length;
    draw();
  })
  .catch((error: unknown) => {
    status.textContent = error instanceof Error ? error.message : 'Could not load the sample data.';
  });

document.addEventListener(EVENTS.languageChanged, draw);
