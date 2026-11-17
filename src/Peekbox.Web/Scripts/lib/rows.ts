// Rows of cards that scroll sideways and snap to each card (the occasion pages; the builder's category rows later).
// The row works without this script by swipe, wheel, keyboard and scrollbar. This shows the ← → buttons, moves the row
// one card per press, greys a button at either end, and tells the caller where the row stopped.
//
// Markup: an element with data-row holding a hidden [data-row-prev]/[data-row-next] pair (in a parent with the hidden
// attribute) and the scroller, [data-row-track], whose children are the cards.

export interface RowPosition {
  first: number;   // 1-based index of the first card that is fully in view
  last: number;    // 1-based index of the last card that is fully in view
  count: number;
  atStart: boolean;
  atEnd: boolean;
}

// Below a pixel of difference the row counts as at its end (scroll positions can be fractions).
const EDGE = 1;

export function wireRows(root: ParentNode, onSettle?: (row: HTMLElement, position: RowPosition) => void): void {
  for (const row of root.querySelectorAll<HTMLElement>('[data-row]')) {
    const track = row.querySelector<HTMLElement>('[data-row-track]');
    const prev = row.querySelector<HTMLButtonElement>('[data-row-prev]');
    const next = row.querySelector<HTMLButtonElement>('[data-row-next]');
    if (!track || !prev || !next) {
      continue;
    }

    const cards = (): HTMLElement[] => Array.from(track.children) as HTMLElement[];
    // A press while the row is still moving builds on the card already being scrolled to, not on where it is now.
    let pending: number | null = null;

    const position = (): RowPosition => {
      const box = track.getBoundingClientRect();
      const all = cards();
      const inView = all
        .map((card, i) => ({ i, r: card.getBoundingClientRect() }))
        .filter(({ r }) => r.left >= box.left - EDGE && r.right <= box.right + EDGE)
        .map(({ i }) => i);
      const max = track.scrollWidth - track.clientWidth;
      return {
        first: (inView[0] ?? 0) + 1,
        last: (inView[inView.length - 1] ?? 0) + 1,
        count: all.length,
        atStart: track.scrollLeft <= EDGE,
        atEnd: track.scrollLeft >= max - EDGE,
      };
    };

    const settle = (): void => {
      pending = null;
      const p = position();
      // A button that turns off while it has the focus hands the focus to the other one, so the keyboard is not lost.
      for (const [button, off, other] of [[prev, p.atStart, next], [next, p.atEnd, prev]] as const) {
        if (off && document.activeElement === button) {
          other.focus();
        }
        button.disabled = off;
      }
      onSettle?.(row, p);
    };

    const go = (step: 1 | -1): void => {
      const all = cards();
      const from = pending ?? position().first - 1;
      const to = Math.max(0, Math.min(all.length - 1, from + step));
      const target = all[to];
      if (!target) {
        return;
      }
      pending = to;
      const offset = target.getBoundingClientRect().left - track.getBoundingClientRect().left - parseFloat(getComputedStyle(track).paddingLeft);
      track.scrollBy({ left: offset });
    };

    prev.addEventListener('click', () => go(-1));
    next.addEventListener('click', () => go(1));

    if ('onscrollend' in window) {
      track.addEventListener('scrollend', settle);
    } else {
      let timer = 0;
      track.addEventListener('scroll', () => {
        window.clearTimeout(timer);
        timer = window.setTimeout(settle, 120);
      });
    }
    window.addEventListener('resize', settle);

    const buttons = prev.parentElement;
    if (buttons) {
      buttons.hidden = false;
    }
    settle();
  }
}
