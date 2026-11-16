// Fetches one of our own pages in the background and reads it, for the scripts that swap a part of the page in
// (builder.ts, checkout.ts). The server answers a form as it does without scripts (save, then redirect to a page), so
// the new page is the one source of every number shown; the browser never works one out.

// A request that hangs this long is given up, and the caller falls back to the plain form. Without it a stalled server
// left the builder's buttons dead until the browser itself gave up, minutes later.
const TIMEOUT_MS = 10_000;

export interface FetchedPage {
  page: Document;
  // True when the server answered with a redirect: a form post always does; a plain GET does only when the page
  // cannot be shown any more (checkout.ts then leaves the page as it is).
  redirected: boolean;
}

// Throws on a network error, a timeout or a status that is not 2xx.
export async function fetchPage(url: string | URL, init: RequestInit = {}): Promise<FetchedPage> {
  const response = await fetch(url, { credentials: 'same-origin', signal: AbortSignal.timeout(TIMEOUT_MS), ...init });
  if (!response.ok) {
    throw new Error(`HTTP ${response.status}`);
  }

  return { page: new DOMParser().parseFromString(await response.text(), 'text/html'), redirected: response.redirected };
}
