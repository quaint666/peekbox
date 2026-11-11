// Runs in the <head> of every page, before the page is drawn, so a visitor who
// chose dark mode never sees a white flash. It only copies the saved choice
// onto <html>. Everything else about themes is in src/lib/theme.ts.
//
// This file is plain JavaScript on purpose: it must run immediately and cannot
// be a module. The key below must equal STORAGE_KEYS.theme in src/lib/config.ts
// (tests/seams.test.mjs checks that they match).
try {
  var theme = localStorage.getItem('peekbox.theme');
  if (theme === 'dark' || theme === 'light') document.documentElement.setAttribute('data-theme', theme);
} catch (error) {
  // localStorage can be blocked. The page then follows the system setting.
}
