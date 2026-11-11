// THE ONE DOOR TO DATA.
//
// Every page gets products, packaging and campaigns from the functions in this
// file and nowhere else. Today they read JSON files that `npm run build` copies
// to /data/. Later, if we add a server, only THIS file changes; the pages do not.

import { DATA_BASE } from './config.js';
import type { Campaign, Category, Packaging, Product } from './types.js';

export class DataError extends Error {}

async function getJson(file: string): Promise<unknown> {
  const response = await fetch(`${DATA_BASE}/${file}`);
  if (!response.ok) throw new DataError(`Could not load ${file} (status ${response.status}). Did you run "npm run build"?`);
  return response.json() as Promise<unknown>;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

// Each file is loaded once; later calls reuse the same answer.
const cache = new Map<string, Promise<unknown>>();
function once<T>(file: string, read: (json: unknown) => T): Promise<T> {
  let pending = cache.get(file);
  if (!pending) {
    pending = getJson(file).then(read);
    cache.set(file, pending);
  }
  return pending as Promise<T>;
}

export function loadProducts(): Promise<{ categories: Category[]; products: Product[] }> {
  return once('products.json', (json) => {
    if (!isRecord(json) || !Array.isArray(json['products']) || !Array.isArray(json['categories'])) {
      throw new DataError('products.json has an unexpected shape.');
    }
    return { categories: json['categories'] as Category[], products: json['products'] as Product[] };
  });
}

export function loadPackaging(): Promise<Packaging[]> {
  return once('packaging.json', (json) => {
    if (!isRecord(json) || !Array.isArray(json['packaging'])) throw new DataError('packaging.json has an unexpected shape.');
    return json['packaging'] as Packaging[];
  });
}

export function loadCampaigns(): Promise<Campaign[]> {
  return once('campaigns.json', (json) => {
    if (!isRecord(json) || !Array.isArray(json['campaigns'])) throw new DataError('campaigns.json has an unexpected shape.');
    return json['campaigns'] as Campaign[];
  });
}

// A lookup table: slug -> product.
export function indexBySlug(products: readonly Product[]): Map<string, Product> {
  return new Map(products.map((product) => [product.slug, product]));
}
