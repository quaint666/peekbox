import { afterEach, describe, expect, it, vi } from 'vitest';
import { load, tick } from './support.js';

const page = (fee: string): string => `<div id="checkout-summary">${fee}</div><div id="checkout-bar"><span class="checkout-total">${fee}</span></div>`;
const html = `
  <select id="ward"><option value="">-</option><option value="1">Một</option><option value="2">Hai</option></select>
  <input type="radio" name="payment" id="payment-cod" value="cod"><input type="radio" name="payment" id="payment-zalopay" value="zalopay" checked>
  ${page('old')}
  <button class="checkout-submit" data-label-cod="Đặt hàng, trả khi nhận" data-label-zalopay="Đặt hàng và thanh toán ZaloPay"></button>`;

const answer = (body: string, redirected = false): Response =>
  ({ ok: true, status: 200, redirected, text: () => Promise.resolve(`<html><body>${body}</body></html>`) }) as Response;

const pick = (value: string): void => {
  const ward = document.querySelector<HTMLSelectElement>('#ward')!;
  ward.value = value;
  ward.dispatchEvent(new Event('change'));
};

afterEach(() => vi.restoreAllMocks());

describe('checkout.ts', () => {
  it('the buttons name the payment that is picked, from the start and when it changes', async () => {
    await load('checkout', html);
    const button = document.querySelector('.checkout-submit')!;
    expect(button.textContent).toBe('Đặt hàng và thanh toán ZaloPay');

    const cod = document.querySelector<HTMLInputElement>('#payment-cod')!;
    cod.checked = true;
    cod.dispatchEvent(new Event('change'));
    expect(button.textContent).toBe('Đặt hàng, trả khi nhận');
  });

  it('a slow fee answer dims the summary until it arrives (and a quick one never does)', async () => {
    let arrive: (response: Response) => void = () => undefined;
    vi.spyOn(globalThis, 'fetch').mockReturnValue(new Promise<Response>((resolve) => { arrive = resolve; }));
    vi.useFakeTimers();
    try {
      await load('checkout', html);
      const summary = document.querySelector('#checkout-summary')!;

      pick('2');
      expect(summary.getAttribute('aria-busy')).toBe('true');
      expect(summary.hasAttribute('data-slow')).toBe(false);

      vi.advanceTimersByTime(400);
      expect(summary.getAttribute('data-slow')).toBe('true');

      arrive(answer(page('new')));
      await vi.advanceTimersByTimeAsync(0);
      expect(summary.hasAttribute('data-slow')).toBe(false);
      expect(summary.hasAttribute('aria-busy')).toBe(false);
      expect(document.querySelector('#checkout-summary')?.textContent).toBe('new');
    } finally {
      vi.useRealTimers();
    }
  });

  it('a failed fee request also takes the dimming off', async () => {
    vi.spyOn(globalThis, 'fetch').mockRejectedValue(new Error('offline'));
    await load('checkout', html);
    const summary = document.querySelector('#checkout-summary')!;

    pick('2');
    await tick();

    expect(summary.hasAttribute('aria-busy')).toBe(false);
    expect(summary.hasAttribute('data-slow')).toBe(false);
  });

  it('a picked ward fetches the page with ?ward= and swaps in the server\'s summary and total', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(answer(page('new')));
    await load('checkout', html);

    pick('2');
    await tick();

    expect(String(fetchMock.mock.calls[0]?.[0])).toContain('?ward=2');
    expect(document.querySelector('#checkout-summary')?.textContent).toBe('new');
    expect(document.querySelector('#checkout-bar .checkout-total')?.textContent).toBe('new');
  });

  it('a slow answer for an earlier choice never overwrites the answer for the later one', async () => {
    const slow = Promise.withResolvers<Response>();
    const fetchMock = vi.spyOn(globalThis, 'fetch');
    fetchMock.mockReturnValueOnce(slow.promise).mockResolvedValueOnce(answer(page('ward 2')));
    await load('checkout', html);

    pick('1');
    pick('2');
    await tick();
    expect(document.querySelector('#checkout-summary')?.textContent).toBe('ward 2');

    slow.resolve(answer(page('ward 1')));
    await tick();
    expect(document.querySelector('#checkout-summary')?.textContent).toBe('ward 2');
  });

  it('a redirect (the box changed) leaves the page as it was; a failed request does too, without an error', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch');
    fetchMock.mockResolvedValueOnce(answer(page('other page'), true)).mockRejectedValueOnce(new Error('offline'));
    await load('checkout', html);

    pick('1');
    await tick();
    pick('2');
    await tick();

    expect(document.querySelector('#checkout-summary')?.textContent).toBe('old');
  });

  describe('keyboard changes of the ward', () => {
    const key = (name: string): void => {
      document.querySelector('#ward')?.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true }));
    };

    it('passing three wards with the arrow keys asks the server once, after the keys stop', async () => {
      vi.useFakeTimers();
      const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(answer(page('ward 2')));
      await load('checkout', html);

      for (const value of ['1', '2', '1']) {
        key('ArrowDown');
        pick(value);
        await vi.advanceTimersByTimeAsync(100);
      }
      expect(fetchMock).not.toHaveBeenCalled();

      await vi.advanceTimersByTimeAsync(600);
      expect(fetchMock).toHaveBeenCalledTimes(1);
      expect(String(fetchMock.mock.calls[0]?.[0])).toContain('?ward=1');
      vi.useRealTimers();
    });

    it('Enter, or leaving the menu, asks at once', async () => {
      vi.useFakeTimers();
      const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(answer(page('x')));
      await load('checkout', html);

      key('ArrowDown');
      pick('1');
      key('Enter');
      expect(fetchMock).toHaveBeenCalledTimes(1);

      key('ArrowDown');
      pick('2');
      document.querySelector('#ward')?.dispatchEvent(new Event('blur'));
      expect(fetchMock).toHaveBeenCalledTimes(2);
      vi.useRealTimers();
    });

    it('a choice made with the pointer is sent at once, even after keys were used', async () => {
      const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(answer(page('x')));
      await load('checkout', html);

      key('ArrowDown');
      document.querySelector('#ward')?.dispatchEvent(new Event('pointerdown'));
      pick('2');
      expect(fetchMock).toHaveBeenCalledTimes(1);
    });
  });
});
