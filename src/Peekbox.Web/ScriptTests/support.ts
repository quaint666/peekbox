import { vi } from 'vitest';

// A script runs when it is imported (it looks the page up and wires it), so a test sets the page first and then loads the
// script fresh.
// The document and the window outlive a test, so the listeners a script put on them are taken off before the next script
// is loaded: otherwise a click would also run the scripts of the tests before it.
type Target = Document | Window;
const added: [Target, string, EventListenerOrEventListenerObject, boolean | AddEventListenerOptions | undefined][] = [];
for (const target of [document, window] as Target[]) {
  const original = target.addEventListener.bind(target) as Target['addEventListener'];
  target.addEventListener = ((type: string, listener: EventListenerOrEventListenerObject, options?: boolean | AddEventListenerOptions) => {
    added.push([target, type, listener, options]);
    original(type, listener, options);
  }) as Target['addEventListener'];
}

function forgetListeners(): void {
  for (const [target, type, listener, options] of added.splice(0)) {
    target.removeEventListener(type, listener, options);
  }
}

export async function load(script: string, html: string): Promise<void> {
  forgetListeners();
  document.body.innerHTML = html;
  vi.resetModules();
  await import(/* @vite-ignore */ `../Scripts/${script}.ts`);
}

export const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 0));

// jsdom has no matchMedia.
export function stubMatchMedia(matches: boolean): void {
  Object.defineProperty(window, 'matchMedia', { configurable: true, writable: true, value: () => ({ matches, addEventListener: () => {}, removeEventListener: () => {} }) });
}
