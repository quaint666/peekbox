import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { load, tick } from './support.js';

const html = `
  <div class="item-share" data-share-url="https://peekbox.example/items/tra-sen" data-share-title="Trà sen · Peekbox">
    <button type="button" data-share-copy hidden>Sao chép liên kết</button>
    <a href="https://www.facebook.com/sharer/sharer.php?u=x">Facebook</a>
    <button type="button" data-share-system hidden>Chia sẻ…</button>
  </div>
  <p id="toast" popover="manual"></p><p id="toast-status"></p>`;

const copyButton = (): HTMLButtonElement => document.querySelector<HTMLButtonElement>('[data-share-copy]')!;
const systemButton = (): HTMLButtonElement => document.querySelector<HTMLButtonElement>('[data-share-system]')!;
const toastText = (): string => document.getElementById('toast')?.textContent ?? '';

function setNavigator(values: { clipboard?: unknown; share?: unknown }): void {
  Object.defineProperty(navigator, 'clipboard', { configurable: true, value: values.clipboard });
  Object.defineProperty(navigator, 'share', { configurable: true, value: values.share });
}

beforeEach(() => {
  let open = false;
  HTMLElement.prototype.showPopover = function showPopover() { open = true; };
  HTMLElement.prototype.hidePopover = function hidePopover() { open = false; void open; };
});

afterEach(() => setNavigator({}));

describe('share.ts', () => {
  it('copies the address the server wrote, not the address bar, and says so', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    setNavigator({ clipboard: { writeText } });
    await load('share', html);

    expect(copyButton().hidden).toBe(false);
    copyButton().click();
    await tick();

    expect(writeText).toHaveBeenCalledWith('https://peekbox.example/items/tra-sen');
    expect(toastText()).toBe('Đã sao chép liên kết');
  });

  it('when the browser refuses the copy it says what to do instead', async () => {
    setNavigator({ clipboard: { writeText: vi.fn().mockRejectedValue(new Error('denied')) } });
    await load('share', html);

    copyButton().click();
    await tick();

    expect(toastText()).toContain('Không sao chép được');
  });

  it('the system share button appears only where the browser has a share sheet', async () => {
    setNavigator({ clipboard: { writeText: vi.fn() } });
    await load('share', html);
    expect(systemButton().hidden).toBe(true);

    setNavigator({ clipboard: { writeText: vi.fn() }, share: vi.fn().mockResolvedValue(undefined) });
    await load('share', html);
    expect(systemButton().hidden).toBe(false);
  });

  it('the share sheet gets the title and the address; closing it is not an error, a real failure is told', async () => {
    const share = vi.fn().mockResolvedValue(undefined);
    setNavigator({ share });
    await load('share', html);
    systemButton().click();
    expect(share).toHaveBeenCalledWith({ title: 'Trà sen · Peekbox', url: 'https://peekbox.example/items/tra-sen' });

    share.mockRejectedValueOnce(new DOMException('closed', 'AbortError'));
    systemButton().click();
    await tick();
    expect(toastText()).toBe('');

    share.mockRejectedValueOnce(new DOMException('no', 'NotAllowedError'));
    systemButton().click();
    await tick();
    expect(toastText()).toContain('Không mở được chia sẻ');
  });

  it('a page without the share block does nothing', async () => {
    setNavigator({ clipboard: { writeText: vi.fn() } });
    await expect(load('share', '<p>no share here</p>')).resolves.toBeUndefined();
  });

  it('without a script the buttons stay hidden: a browser that never runs this file shows only the Facebook link', () => {
    document.body.innerHTML = html;
    expect(copyButton().hidden).toBe(true);
    expect(systemButton().hidden).toBe(true);
  });
});
