// English text. Same keys as vi.ts (the compiler stops the build if one is
// missing or extra). If you are unsure how to say something, ask: do not leave
// it in Vietnamese.

import type { MessageKey } from './vi.js';

export const en: Record<MessageKey, string> = {
  'skip.toContent': 'Skip to the content',

  'nav.main': 'Main menu',
  'nav.home': 'Home',
  'nav.builder': 'Build a hamper',
  'nav.about': 'About',
  'nav.commitments': 'Our promise',
  'nav.blog': 'Blog',
  'nav.openMenu': 'Open menu',
  'nav.closeMenu': 'Close menu',

  'cart.link': 'Cart ({n})',
  'cart.aria': 'Cart, {n} items',

  'lang.group': 'Language',
  'lang.vi': 'Tiếng Việt',
  'lang.en': 'English',

  'theme.toDark': 'Switch to dark mode',
  'theme.toLight': 'Switch to light mode',

  'footer.tagline': 'Gift hampers for Tết, 20/11 and 8/3.',
  'footer.about': 'About',
  'footer.commitments': 'Our promise',
  'footer.blog': 'Blog',
  'footer.terms': 'Terms',
  'footer.privacy': 'Privacy policy',
  'footer.contact': '[PHONE] · [EMAIL]',

  'home.dataCheck': 'Scaffold check: loaded {n} sample products.',
};
