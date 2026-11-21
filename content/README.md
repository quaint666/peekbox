# Peekbox: content pack

Everything a page needs to *say*: blog posts, legal text, page copy, product data and campaigns. The pictures are in `src/Peekbox.Web/wwwroot/images/` (WebP): `products/`, `packaging/`, `occasions/`, `blog/`, `about/`, `hero.webp` and `logo/`. Written 2026-11-10 and 2026-11-11. Image paths in the data files and the blog posts (`/images/...`) are relative to `src/Peekbox.Web/wwwroot/`. All text is a **draft** unless it says APPROVED.

The site is **Vietnamese only** (decided 2026-11-11). The English text in these files is not used; it is dropped when the data is loaded into the database.

## What is here

| Folder | What | Status |
|---|---|---|
| `blog/` | 6 blog posts, each in Vietnamese then English, in one file | Draft for review |
| `legal/` | Terms of use and privacy policy. The Vietnamese files (`*.vi.md`) are the source and are the only ones the site reads; they were rewritten on 2026-11-21 from the researched laws (see "Legal sources" below). The English files are the earlier sample text and are no longer used | Draft; needs a lawyer and a Vietnamese speaker before launch |
| `pages/` | Copy for the About and commitments page. The Vietnamese text is the source (rewritten on 2026-11-21 and checked against how the site works); the English text is the earlier draft and is unused. The home page's Vietnamese text moved to `data/home.json` | Draft |
| `data/products.json` | 125 sample products in 11 categories (names in both languages, brand, weight, origin, expiry date, price, stock, short description, image) | Sample data |
| `data/packaging.json` | The 3 packaging options and their sample fees | Sample data |
| `data/home.json` | The Vietnamese text of the home page, read by the site | Draft |
| `data/hampers.json` | The six occasion pages (Tết, Valentine, 8/3 và 20/10, Trung thu, 20/11, Giáng sinh: heading, lead, accent key, Bloomy link text, season banner text) and 99 ready hampers (budget, packaging, items, occasions, photo); totals are worked out from current prices, not stored | Draft; the season banner text is not read yet (F05) |

## Things to know before using any of it

- **Brands are placeholders** ("Thương hiệu A" to "H"). **Prices, stock and product descriptions are samples.** Product names and origins are plausible guesses, not sourced facts. Check them before launch.
- **Photos are AI-generated placeholders** (in `src/Peekbox.Web/wwwroot/images/`) with plain, unprinted packaging. The site copies are lossy WebP at about 508 px; the full-size PNG originals are in the handoff zip, not in the repo. They are fine for the design and a demo. A shop that promises trust needs real photos of the real products. Say "AI-generated" wherever they are used.
- **Items in [BRACKETS]** are facts that only the team knows (company name, address, refund window, dates, contact details). Fill them in.
- **Blog posts and legal text are general guidance, not professional or legal advice.** Have a person review them before real use.
- **Expiry dates** like `12/2027` are stored as the last day of the month (`2027-12-31`).
- **The safe-expiry rule:** an item is orderable only if its expiry date is later than the delivery date plus the margin (7 days in the examples, set in Settings).

## Two items to double-check with a Vietnamese speaker
- The Vietnamese legal translations in `legal/*.vi.md`.
- The Vietnamese product descriptions in `data/products.json`.

## Legal sources (checked 2026-11-21)

The owner asked that the terms and the privacy policy follow the latest law. These are the laws that apply to a Vietnamese online shop that sells goods and keeps customers' personal data, and what was and was not checked.

**Read and used (from the government briefing "Tài liệu quán triệt nội dung cơ bản của Luật Bảo vệ dữ liệu cá nhân", and a KPMG note on the e-commerce law):**
- Luật Bảo vệ dữ liệu cá nhân số 91/2025/QH15 (passed 26/6/2025, in force from 1/1/2026; it replaced the personal-data rules of Nghị định 13/2023/NĐ-CP, and Nghị định 356/2025/NĐ-CP, in force from 1/1/2026, guides it). Used for: the six rights of the data subject (to know; to agree, refuse or withdraw consent; to see and correct; to ask for the data, its deletion, restriction, and to object; to complain, sue and ask for damages; to ask for protection measures); valid consent (Điều 9: voluntary, told the data types, the purpose, the controller and the rights beforehand, one consent per purpose, silence is not consent); processing needed to carry out an agreement does not need a separate consent; withdrawal does not undo processing already done; selling personal data is banned; tracking for targeted advertising needs consent and a way to refuse; children's rights are exercised by the legal representative; a breach that can cause harm is reported to the specialised authority (Ministry of Public Security) within 72 hours.
- Luật Thương mại điện tử số 122/2025/QH15 (passed 10/12/2025, in force from 1/7/2026). Used for: an online-ordering platform must publish its terms of transaction, its personal-data (privacy) policy, the rights and duties of the parties and how complaints are received and settled (Điều 11 khoản 1, from the KPMG note and search excerpts, not the full text); and the seller's duty to disclose goods information, origin and quality.

**Identified but NOT read in full (a lawyer must check these before launch):**
- Luật Bảo vệ quyền lợi người tiêu dùng số 19/2023/QH15 (in force from 1/7/2024), Điều 37 (distance transactions, including a stated time limit for return and exchange), Điều 15 (consumer information), and Nghị định 55/2024/NĐ-CP Điều 22. The sites that hold the full text refused automated reading, so the 24-hour reporting period and the "no return for change of mind" rule in the terms (section 6) are business choices, not quotes from the law.
- Nghị định 248/2026/NĐ-CP (guides the e-commerce law, in force from 1/7/2026) and the notice and registration of a selling website at online.gov.vn. The exact list of what the shop must publish was not read. An existing website notified before 1/7/2026 may keep running on its old notice until 30/6/2027, according to a summary.
- Nghị định 356/2025/NĐ-CP in detail: the deadline to answer a data subject's request, the content of a breach notice, and any data-protection impact assessment the shop has to file.
- Food labelling: Nghị định 43/2017/NĐ-CP, amended by Nghị định 111/2021/NĐ-CP, has two date marks, "Hạn sử dụng" (use by) and "Sử dụng tốt nhất trước" (best before); only that fact was confirmed.
- How long accounting and tax records must be kept (the privacy policy says "as the law requires" and gives no number on purpose).

**Business choices in the terms that the owner must confirm:** report a faulty or expired item within 24 hours of delivery; no return of a correct, intact and in-date box for change of mind (food, made to order); cancel before delivery; delivery only in Ho Chi Minh City.

The texts say nothing the site does not do today (no analytics or advertising cookies, no card data stored, no card message or company invoice yet). When any of those is added, the matching section of the privacy policy (header note of `legal/privacy.vi.md`) must be updated.
