import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { showToast } from '../Scripts/lib/toast.js';

// jsdom has no popover: showPopover and hidePopover are stood in for by a flag, as the browser's would keep one.
let open = false;

beforeEach(() => {
  vi.useFakeTimers();
  open = false;
  HTMLElement.prototype.showPopover = function showPopover() { if (open) throw new DOMException('already showing', 'InvalidStateError'); open = true; };
  HTMLElement.prototype.hidePopover = function hidePopover() { open = false; };
  document.body.innerHTML = '<p id="toast" popover="manual" aria-hidden="true"></p><p id="toast-status" role="status"></p>';
});

afterEach(() => vi.useRealTimers());

describe('showToast', () => {
  it('shows the words, then takes them away after 3.5 seconds', () => {
    showToast('Đã sao chép liên kết');
    expect(document.getElementById('toast')?.textContent).toBe('Đã sao chép liên kết');
    expect(open).toBe(true);

    vi.advanceTimersByTime(3400);
    expect(open).toBe(true);
    vi.advanceTimersByTime(200);
    expect(open).toBe(false);
  });

  it('gives a screen reader the same words, after the line was emptied so a repeat is read again', () => {
    showToast('một');
    expect(document.getElementById('toast-status')?.textContent).toBe('');
    vi.advanceTimersByTime(60);
    expect(document.getElementById('toast-status')?.textContent).toBe('một');
  });

  it('the same words twice in a row are emptied in between, so the screen reader reads them again', () => {
    showToast('một');
    vi.advanceTimersByTime(60);
    expect(document.getElementById('toast-status')?.textContent).toBe('một');

    showToast('một');
    expect(document.getElementById('toast-status')?.textContent).toBe('');
    vi.advanceTimersByTime(60);
    expect(document.getElementById('toast-status')?.textContent).toBe('một');
  });

  it('a second message while the first is showing replaces it and restarts the time', () => {
    showToast('một');
    vi.advanceTimersByTime(3000);
    showToast('hai');
    expect(document.getElementById('toast')?.textContent).toBe('hai');

    vi.advanceTimersByTime(3000);
    expect(open).toBe(true);   // the first message's timer was cancelled
    vi.advanceTimersByTime(600);
    expect(open).toBe(false);
  });

  it('a page without the toast does nothing and does not throw', () => {
    document.body.innerHTML = '';
    expect(() => showToast('x')).not.toThrow();
  });
});
