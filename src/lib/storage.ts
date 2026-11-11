// Safe reading and writing of localStorage.
//
// localStorage can be empty, can hold text that is not valid JSON (a bad
// edit, an old version of the site), and can be blocked (private windows).
// These helpers never throw: they return the fallback instead.

export function loadJson<T>(key: string, fallback: T, isValid: (value: unknown) => value is T): T {
  try {
    const raw = localStorage.getItem(key);
    if (raw === null) return fallback;
    const parsed: unknown = JSON.parse(raw);
    return isValid(parsed) ? parsed : fallback;
  } catch {
    return fallback;
  }
}

export function saveJson(key: string, value: unknown): boolean {
  try {
    localStorage.setItem(key, JSON.stringify(value));
    return true;
  } catch {
    return false;
  }
}

export function loadText(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

export function saveText(key: string, value: string): boolean {
  try {
    localStorage.setItem(key, value);
    return true;
  } catch {
    return false;
  }
}

export function removeKey(key: string): void {
  try {
    localStorage.removeItem(key);
  } catch {
    // nothing to do
  }
}
