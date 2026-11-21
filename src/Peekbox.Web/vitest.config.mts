import { defineConfig } from 'vitest/config';

// The tests of the scripts in Scripts/ (jsdom: it runs the page's logic, it cannot see layout or real hover: those were
// measured in real browsers, see docs/repo-review.md).
export default defineConfig({
  test: {
    environment: 'jsdom',
    include: ['ScriptTests/**/*.test.ts'],
    restoreMocks: true,
  },
});
