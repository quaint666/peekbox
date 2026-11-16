// Runs in the <head> of every page, before the page is drawn, so a visitor who
// chose dark mode never sees a white flash. It only copies the saved choice
// onto <html>. Everything else about themes is in Scripts/layout.ts.
//
// This file is plain JavaScript on purpose: it must run immediately and cannot
// be a module. The key below must equal THEME_KEY in Scripts/layout.ts
// (SeamsTests.cs checks that they match).
try {
  var theme = localStorage.getItem('peekbox.theme');
  if (theme === 'dark' || theme === 'light') document.documentElement.setAttribute('data-theme', theme);
} catch (error) {
  // localStorage can be blocked. The page then follows the system setting.
}
