// The hamper page (/hop/{slug}): on a phone a bar with the price and "Dùng hộp này" stays at the bottom, but only once the
// button in the page has scrolled out of view above it (until then it would say the same thing twice). Without this
// script the bar stays hidden and the button in the page is the only one.
export {};

const main = document.querySelector<HTMLElement>('[data-buy-main]');
const bar = document.querySelector<HTMLElement>('[data-buy-bar]');

if (main && bar && 'IntersectionObserver' in window) {
  new IntersectionObserver(([entry]) => {
    bar.hidden = !entry || entry.isIntersecting || entry.boundingClientRect.top > 0;
  }).observe(main);
}
