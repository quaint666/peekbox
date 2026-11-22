import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { markBusy } from '../Scripts/lib/busy.js';

const make = (): HTMLElement => {
  const element = document.createElement('div');
  document.body.append(element);
  return element;
};

beforeEach(() => vi.useFakeTimers());
afterEach(() => {
  vi.useRealTimers();
  document.body.innerHTML = '';
});

describe('markBusy', () => {
  it('says busy to screen readers at once, but dims nothing for a quick answer', () => {
    const element = make();
    const done = markBusy(element);
    expect(element.getAttribute('aria-busy')).toBe('true');

    vi.advanceTimersByTime(399);
    expect(element.hasAttribute('data-slow')).toBe(false);

    done();
    vi.advanceTimersByTime(5000);
    expect(element.hasAttribute('aria-busy')).toBe(false);
    expect(element.hasAttribute('data-slow')).toBe(false);
  });

  it('dims after 400 ms and undoes both when the answer comes', () => {
    const element = make();
    const done = markBusy(element);
    vi.advanceTimersByTime(400);
    expect(element.getAttribute('data-slow')).toBe('true');

    done();
    expect(element.hasAttribute('aria-busy')).toBe(false);
    expect(element.hasAttribute('data-slow')).toBe(false);
  });

  it('stays busy until the last overlapping request is done, and a finished one leaves no timer behind', () => {
    const element = make();
    const first = markBusy(element);
    const second = markBusy(element);

    first();
    expect(element.getAttribute('aria-busy')).toBe('true');
    first(); // calling it twice must not count twice
    expect(element.getAttribute('aria-busy')).toBe('true');

    second();
    expect(element.hasAttribute('aria-busy')).toBe(false);
    vi.advanceTimersByTime(5000);
    expect(element.hasAttribute('data-slow')).toBe(false);
  });

  it('can be used again after it finished', () => {
    const element = make();
    markBusy(element)();
    const done = markBusy(element);
    vi.advanceTimersByTime(400);
    expect(element.getAttribute('data-slow')).toBe('true');
    done();
    expect(element.hasAttribute('data-slow')).toBe(false);
  });
});
