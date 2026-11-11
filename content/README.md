# Peekbox: content pack

Everything a page needs to *say*: blog posts, legal text, page copy, product data and campaigns. The pictures are in `public/images/` (WebP): `products/`, `packaging/`, `hero.webp` and `logo/`. Written 2026-11-10 and 2026-11-11. Image paths in the data files (`/images/...`) are relative to `public/`. All text is a **draft** unless it says APPROVED.

## What is here

| Folder | What | Status |
|---|---|---|
| `blog/` | 6 blog posts, each in Vietnamese then English, in one file | Draft for review |
| `legal/` | Terms of use and privacy policy, English and Vietnamese | English APPROVED by the owner; Vietnamese is a draft translation |
| `pages/` | Copy for the home page and for the About and commitments page, Vietnamese and English | Draft, taken from the design |
| `data/products.json` and `products.csv` | 24 sample products (names in both languages, brand, weight, origin, expiry date, price, stock, short description, image) | Sample data |
| `data/packaging.json` | The 3 packaging options and their sample fees | Sample data |
| `data/campaigns.json` | Tết 2027, 20/11 and 8/3: banners in both languages, accent colour, starter boxes | Draft, dates are placeholders |

`products.csv` opens in a spreadsheet (UTF-8; if the Vietnamese letters look wrong, import it as UTF-8).

## Things to know before using any of it

- **Brands are placeholders** ("Thương hiệu A" to "H"). **Prices, stock and product descriptions are samples.** Product names and origins are plausible guesses, not sourced facts. Check them before launch.
- **Photos are AI-generated placeholders** (in `public/images/`) with plain, unprinted packaging. The site copies are lossy WebP at about 508 px; the full-size PNG originals are in the handoff zip, not in the repo. They are fine for the design and a demo. A shop that promises trust needs real photos of the real products. Say "AI-generated" wherever they are used.
- **Items in [BRACKETS]** are facts that only the team knows (company name, address, refund window, dates, contact details). Fill them in.
- **Blog posts and legal text are general guidance, not professional or legal advice.** Have a person review them before real use.
- **Expiry dates** like `12/2027` are stored as the last day of the month (`2027-12-31`).
- **The safe-expiry rule:** an item is orderable only if its expiry date is later than the delivery date plus the margin (7 days in the examples, set in Settings).

## Two items to double-check with a Vietnamese speaker
- The Vietnamese legal translations in `legal/*.vi.md`.
- The Vietnamese product descriptions in `data/products.json`.
