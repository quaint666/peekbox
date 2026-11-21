import { describe, expect, it, vi } from 'vitest';
import { fetchPage } from '../Scripts/lib/page.js';

const reply = (body: string, init: { ok?: boolean; status?: number; redirected?: boolean } = {}): Response =>
  ({ ok: init.ok ?? true, status: init.status ?? 200, redirected: init.redirected ?? false, text: () => Promise.resolve(body) }) as Response;

describe('fetchPage', () => {
  it('parses the page, sends the cookie to our own site only, and says whether the server redirected', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(reply('<main id="a">x</main>', { redirected: true }));

    const result = await fetchPage('/builder');

    expect(result.page.querySelector('#a')?.textContent).toBe('x');
    expect(result.redirected).toBe(true);
    expect(fetchMock.mock.calls[0]?.[1]).toMatchObject({ credentials: 'same-origin' });
  });

  it('throws on a status that is not 2xx, so the caller falls back to the plain form', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(reply('', { ok: false, status: 503 }));
    await expect(fetchPage('/builder')).rejects.toThrow('HTTP 503');
  });

  it('always has a time limit, and joins the caller\'s own signal instead of replacing it', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(reply(''));
    const own = new AbortController();

    await fetchPage('/a');
    await fetchPage('/b', { signal: own.signal });

    const [first, second] = fetchMock.mock.calls.map((call) => call[1]?.signal as AbortSignal);
    expect(first).toBeInstanceOf(AbortSignal);
    expect(second).toBeInstanceOf(AbortSignal);
    expect(second).not.toBe(own.signal);       // a new signal that follows both
    own.abort();
    expect(second?.aborted).toBe(true);         // the caller's abort reaches it
    expect(first?.aborted).toBe(false);
  });
});
