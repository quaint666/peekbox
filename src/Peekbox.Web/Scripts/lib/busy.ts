// A visible "working on it" for the parts of a page that are swapped in from a background request (F10). Anything that
// answers in a blink shows nothing (no flicker); one that takes longer than SLOW_AFTER_MS is dimmed and gets the progress
// cursor (data-slow, styled in components.css). It is a timer and an attribute, not an animation, so a visitor who asked
// for less motion gets it too. aria-busy is set at once, for screen readers.
//
// Requests can overlap on one element (two packaging choices in a row), so each call is counted: the element stays busy
// until the last one it started is done, and a finished request never leaves a timer behind to dim it later.
const SLOW_AFTER_MS = 400;

interface Work {
  count: number;
  timer: number;
}

const working = new WeakMap<Element, Work>();

// Returns the function to call when this request is over (it may be called more than once; only the first counts).
export function markBusy(element: Element): () => void {
  let work = working.get(element);
  if (!work) {
    work = { count: 0, timer: window.setTimeout(() => element.setAttribute('data-slow', 'true'), SLOW_AFTER_MS) };
    working.set(element, work);
    element.setAttribute('aria-busy', 'true');
  }

  work.count++;
  let finished = false;
  return () => {
    const current = working.get(element);
    if (finished || !current) return;
    finished = true;
    if (--current.count > 0) return;
    window.clearTimeout(current.timer);
    working.delete(element);
    element.removeAttribute('aria-busy');
    element.removeAttribute('data-slow');
  };
}
