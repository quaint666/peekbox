// The shapes of our data. They match the files in content/data/.
// If a data file changes, change the shape here first, then let the compiler
// show you every place that needs updating.

export type Language = 'vi' | 'en';
export type Theme = 'light' | 'dark';

export interface Category {
  id: string;
  name_vi: string;
  name_en: string;
}

export interface Product {
  id: number;
  slug: string;
  category: string; // a Category id
  name_vi: string;
  name_en: string;
  brand: string;
  brand_en: string;
  weight_g: number;
  origin: string;
  expiry_date: string; // 'YYYY-MM-DD', the last day of the month
  price_vnd: number; // whole dong, tax included
  price_includes_tax: boolean;
  stock: number;
  description_vi: string;
  description_en: string;
  image: string; // '/images/products/<slug>.webp'
}

export interface Packaging {
  id: string; // 'kraft', 'hard-bow', 'rattan'
  name_vi: string;
  name_en: string;
  desc_vi: string;
  desc_en: string;
  fee_vnd: number;
  image: string;
}

export interface StarterBox {
  name_vi: string;
  name_en: string;
  budget_vnd: number;
  items: string[]; // product slugs
  goods_total_vnd: number;
  note_vi: string;
  note_en: string;
}

export interface Campaign {
  id: string;
  name_vi: string;
  name_en: string;
  accent: string;
  starts: string;
  ends: string;
  last_delivery: string;
  banner_vi: string;
  banner_en: string;
  starter_boxes: StarterBox[];
}

// The hamper the customer is building. It is saved in the browser.
export interface HamperLine {
  slug: string;
  qty: number;
}

export interface Hamper {
  budget_vnd: number;
  delivery_date: string | null; // 'YYYY-MM-DD', or null until chosen
  packaging_id: string;
  lines: HamperLine[];
}

// What the price calculation returns. All numbers are whole dong.
export interface Quote {
  goods_vnd: number;
  packaging_vnd: number;
  total_vnd: number;
  budget_vnd: number;
  remaining_vnd: number; // budget minus goods (delivery and packaging are not part of the budget)
  over_budget: boolean;
  missing_slugs: string[]; // lines whose product was not found
}
