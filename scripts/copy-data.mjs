// Copies the sample data from content/data/ into public/data/, so the site can
// fetch it as /data/products.json and so on. public/data/ is generated and is
// not committed. Run by `npm run build` and `npm run watch`.
import { cpSync, mkdirSync, readdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const from = join(root, 'content', 'data');
const to = join(root, 'public', 'data');

mkdirSync(to, { recursive: true });
for (const file of readdirSync(from)) {
  if (file.endsWith('.json')) cpSync(join(from, file), join(to, file));
}
console.log('Copied the sample data to public/data/');
