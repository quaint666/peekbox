import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { load, stubMatchMedia, tick } from './support.js';

// "Thêm", − and + are plain POST forms; the script sends them in the background and swaps in what the server's page says.
const card = (count: number): string => `
  <li class="item-card" id="mon-tra-sen"><h3 class="item-card__name">Trà sen</h3>
    <div class="item-card__foot"><form class="qty-add-form" method="post" action="http://localhost:3000/builder/quantity">
      <input type="hidden" name="slug" value="tra-sen"><span class="qty__count">${count}</span>
      <button type="submit" name="quantity" value="${count + 1}" data-qty-step="add">Thêm</button></form></div></li>`;
const box = (label: string): string => `<aside id="hop-cua-ban"><p class="box-label">${label}</p></aside>`;
const html = `<main>${card(0)}</main>${box('empty')}<p id="box-status" role="status"></p>`;

const answer = (count: number, label: string): Response =>
  ({ ok: true, status: 200, redirected: true, text: () => Promise.resolve(`<html><body><main>${card(count)}</main>${box(label)}</body></html>`) }) as Response;

const press = (): HTMLFormElement => {
  const form = document.querySelector<HTMLFormElement>('.qty-add-form')!;
  const button = form.querySelector<HTMLButtonElement>('button')!;
  form.dispatchEvent(new SubmitEvent('submit', { bubbles: true, cancelable: true, submitter: button }));
  return form;
};

let fetchMock: ReturnType<typeof vi.spyOn>;

beforeEach(async () => {
  stubMatchMedia(false);
  fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(answer(1, '1 món'));
  await load('builder', html);
});

afterEach(() => vi.restoreAllMocks());

describe('builder.ts: "Thêm", − and + in the background', () => {
  it('sends the form as a POST without leaving the page, and swaps in the card and the box from the server\'s page', async () => {
    const form = press();
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock.mock.calls[0]?.[1]).toMatchObject({ method: 'POST' });
    const body = fetchMock.mock.calls[0]?.[1]?.body as FormData;
    expect(body.get('slug')).toBe('tra-sen');
    expect(body.get('quantity')).toBe('1');          // the pressed button's own value
    expect(form.getAttribute('aria-busy')).toBe('true');

    await tick();
    await tick();
    expect(document.querySelector('#mon-tra-sen .qty__count')?.textContent).toBe('1');
    expect(document.querySelector('#hop-cua-ban .box-label')?.textContent).toBe('1 món');
    expect(document.querySelector('.qty-add-form')?.hasAttribute('aria-busy')).toBe(false);
  });

  it('a second press while the first is on its way is dropped: one change at a time', async () => {
    press();
    press();
    expect(fetchMock).toHaveBeenCalledTimes(1);
    await tick();
    await tick();
    press();                                          // once the first is done a new one goes
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it('when the background request fails the same form is sent the plain way, once', async () => {
    fetchMock.mockReset();
    fetchMock.mockRejectedValue(new Error('offline'));
    const requestSubmit = vi.spyOn(HTMLFormElement.prototype, 'requestSubmit').mockImplementation(() => {});

    const form = press();
    await tick();
    await tick();

    expect(form.dataset['plain']).toBe('true');
    expect(requestSubmit).toHaveBeenCalledTimes(1);
    expect(document.querySelector('.qty-add-form')?.hasAttribute('aria-busy')).toBe(false);
  });

  it('a form marked plain is never intercepted (the fallback must not loop)', async () => {
    const form = document.querySelector<HTMLFormElement>('.qty-add-form')!;
    form.dataset['plain'] = 'true';
    const event = new SubmitEvent('submit', { bubbles: true, cancelable: true, submitter: form.querySelector('button')! });
    form.dispatchEvent(event);
    expect(event.defaultPrevented).toBe(false);
    expect(fetchMock).not.toHaveBeenCalled();
  });
});
