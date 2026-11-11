// A tiny local web server for public/, with no dependencies.
//   npm run dev      then open http://localhost:3000
//
// Why it exists: the pages load data with fetch() and use ES modules, and
// browsers do not allow that from a file:// address. The server also sends
// "no-store" so you never look at an old copy of a script or style sheet.
import { createServer } from 'node:http';
import { existsSync, readFileSync, statSync } from 'node:fs';
import { dirname, extname, join, normalize } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', 'public');
const port = Number(process.env.PORT ?? 3000);

const types = {
  '.html': 'text/html; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.mjs': 'text/javascript; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.map': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.webp': 'image/webp',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.woff2': 'font/woff2',
  '.txt': 'text/plain; charset=utf-8',
};

function resolveFile(urlPath) {
  const decoded = decodeURIComponent(urlPath.split('?')[0] ?? '/');
  const safe = normalize(decoded).replace(/^(\.\.[/\\])+/, '');
  let file = join(root, safe);
  if (!file.startsWith(root)) return null; // never leave public/
  if (existsSync(file) && statSync(file).isDirectory()) file = join(file, 'index.html');
  return existsSync(file) && statSync(file).isFile() ? file : null;
}

createServer((req, res) => {
  const file = resolveFile(req.url ?? '/');
  const headers = { 'Cache-Control': 'no-store', 'X-Content-Type-Options': 'nosniff' };
  if (file) {
    res.writeHead(200, { ...headers, 'Content-Type': types[extname(file)] ?? 'application/octet-stream' });
    res.end(readFileSync(file));
    return;
  }
  const notFound = join(root, '404.html');
  if (existsSync(notFound)) {
    res.writeHead(404, { ...headers, 'Content-Type': types['.html'] });
    res.end(readFileSync(notFound));
  } else {
    res.writeHead(404, { ...headers, 'Content-Type': types['.txt'] });
    res.end('Not found');
  }
}).listen(port, () => {
  console.log(`Peekbox is running at http://localhost:${port}  (stop it with Ctrl+C)`);
});
