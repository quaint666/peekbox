import { afterEach, describe, expect, it, vi } from 'vitest';
import { load, stubMatchMedia, tick } from './support.js';

// The packaging choice (F33) is saved in the background; while it is on its way the form is busy (F10).
const html = `
  <form id="dong-goi-form" method="post" action="http://localhost:3000/builder/packaging">
    <label class="packaging-option"><input type="radio" name="packaging" value="a" checked><span class="packaging-option__name">Hộp giấy</span></label>
    <label class="packaging-option"><input type="radio" name="packaging" value="b"><span class="packaging-option__name">Hộp gỗ</span></label>
    <label class="packaging-option"><input type="radio" name="packaging" value="c"><span class="packaging-option__name">Giỏ mây</span></label>
  </form>
  <aside id="hop-cua-ban"><p>box</p></aside><p id="box-status" role="status"></p>`;

const answer = (): Response =>
  ({ ok: true, status: 200, redirected: true, text: () => Promise.resolve('<html><body><aside id="hop-cua-ban"><p>new</p></aside></body></html>') }) as Response;

const choose = (value: string): void => {
  const radio = document.querySelector<HTMLInputElement>(`input[value="${value}"]`)!;
  radio.checked = true;
  radio.dispatchEvent(new Event('change', { bubbles: true }));
};
const form = (): HTMLFormElement => document.querySelector<HTMLFormElement>('#dong-goi-form')!;

afterEach(() => vi.restoreAllMocks());

describe('builder.ts: packaging while the server is slow', () => {
  it('is busy while the save is on its way, dimmed once it is slow, and clean when it is over', async () => {
    stubMatchMedia(false);
    let arrive: (response: Response) => void = () => undefined;
    vi.spyOn(globalThis, 'fetch').mockReturnValue(new Promise<Response>((resolve) => { arrive = resolve; }));
    await load('builder', html);
    vi.useFakeTimers();
    try {
      choose('b');
      await vi.advanceTimersByTimeAsync(0);
      expect(form().getAttribute('aria-busy')).toBe('true');
      expect(form().hasAttribute('data-slow')).toBe(false);

      await vi.advanceTimersByTimeAsync(400);
      expect(form().getAttribute('data-slow')).toBe('true');

      arrive(answer());
      await vi.advanceTimersByTimeAsync(0);
      expect(form().hasAttribute('aria-busy')).toBe(false);
      expect(form().hasAttribute('data-slow')).toBe(false);
    } finally {
      vi.useRealTimers();
    }
  });

  it('two choices in a row end clean: the one replaced before it was sent leaves nothing behind', async () => {
    stubMatchMedia(false);
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(answer());
    await load('builder', html);

    choose('b');
    choose('c');
    await tick();
    await tick();

    expect(fetchMock).toHaveBeenCalledTimes(1);   // only the newer choice was sent
    expect(form().hasAttribute('aria-busy')).toBe(false);
    expect(form().hasAttribute('data-slow')).toBe(false);
  });

  it('a failed save also ends clean', async () => {
    stubMatchMedia(false);
    vi.spyOn(globalThis, 'fetch').mockRejectedValue(new Error('offline'));
    vi.spyOn(HTMLFormElement.prototype, 'submit').mockImplementation(() => undefined);
    await load('builder', html);

    choose('b');
    await tick();
    await tick();

    expect(form().hasAttribute('aria-busy')).toBe(false);
  });
});
