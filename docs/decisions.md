# Decisions

One line per decision: date, what, why. Newest at the bottom.

- 2026-11-06: The project is Peekbox, a budget-first hamper builder for Tết, 20/11 and 8/3. Customers set a budget, see brand, weight, origin and expiry on every item, and get a server-side total.
- 2026-11-10: Payment is ZaloPay and hosting is our own infrastructure, so neither is an open question.
- 2026-11-10: GitHub (quaint666/peekbox) is the main repository. Changes reach `main` only through a reviewed pull request.
- 2026-11-10: Look and feel is palette A (sage green on cream, red accent for Tết) with the Be Vietnam Pro font. The UI is Vietnamese; a language toggle may come later.
- 2026-11-10: Calm look all year, with a small seasonal accent for Tết (red), 20/11 (purple) and 8/3 (pink).
- 2026-11-10: Mobile and desktop are equally important.
- 2026-11-10: No customer accounts. Customers check out as guests, and see their order status on the order confirmation page.
- 2026-11-10: An item whose expiry is before the delivery date plus a margin (the margin is a setting) blocks checkout. The customer removes the item or changes the delivery date.
- 2026-11-10: The ordering budget covers the goods only, not the shipping fee.
- 2026-11-10: Checkout is delivery only for now, paid by ZaloPay or cash on delivery.
- 2026-11-10: Not building a point-of-sale page or an SEO admin page. The blog stays.
- 2026-11-10: Campaigns are in, as a light version. Each occasion (Tết, 20/11, 8/3) has active dates, a banner in Vietnamese and English, a last delivery date, and a few starter boxes (pre-filled hampers with a suggested budget). The shelf-life check still applies to starter boxes. This replaces the earlier decision to drop a campaigns page.
- 2026-11-10: The site has a Vietnamese/English switch and a light/dark mode from the start. Page text lives in dictionaries; item, category, campaign and blog text has a Vietnamese and an English field; if the English text is missing, the Vietnamese shows. Dark mode uses colour variables, follows the device by default, and remembers the visitor's choice. English content for the whole catalogue can arrive as a later slice.
- 2026-11-10: An order stores the customer's language, so the confirmation page and email come in that language.
- 2026-11-10: The seed catalogue is 24 products in 5 categories (biscuits and cakes; tea and coffee; nuts and dried fruit; preserves and sweets; honey and specialities), each with a Vietnamese and an English name, a placeholder brand, weight, origin, expiry date, price and stock. Alcohol is left out to avoid age checks. Product photos are AI-generated placeholders with plain labels; a real shop needs real packshots.
- 2026-11-10: The English terms of use are approved by the owner (a template, not legal advice). Business facts (company name, address, refund window) are left in [BRACKETS] to fill in before launch.
- 2026-11-11: The English privacy policy is approved by the owner (same status). Vietnamese versions of both are drafts that need a Vietnamese-speaking review.
- 2026-11-11: Dark mode uses neutral near-black and grey surfaces with a light sage accent; the unchosen option in a choice list is black. Product photos stay as light tiles.
- 2026-11-11: The budget is a hard cap: an item that would go over what is left cannot be added. The budget covers goods only.
- 2026-11-11: A failed ZaloPay payment keeps the order and the box; the customer can retry or switch to cash on delivery. How long items are held for an unpaid order is a setting to decide.
- 2026-11-11: An item that sells out while the customer is checking out is removed from the box with a notice. Nothing is charged, and the customer picks a replacement or continues with fewer items.
- 2026-11-11: The logo is an open box with an eye peeking out, with the name set in Be Vietnam Pro Bold. The mockup set (141 screens) covers all customer pages in light, dark and English, plus empty and error states. The admin screens have no dark or English version.
- 2026-11-11: Design files, written content and images live in this repository, as Bloomy's do: mockups and design notes in `design/`, blog, legal text and sample data in `content/`, and the pictures the site uses in `public/images/` (WebP).
- 2026-11-11: Builder search ignores accents and case ("mut" finds "Mứt"), with suggestions grouped as items and categories, plus a sort menu. A checkbox "only items in date for the delivery date" is on by default, and the page says how many items it hides.
- 2026-11-11: Quality-of-life features added from Bloomy and live shops: added-to-hamper toast, share (copy link, Facebook, system share sheet; no Zalo SDK), floating contact button and back-to-top, loading skeletons, recently viewed items, and search and status filters on the admin items and coupons lists.
- 2026-11-11: An unfinished hamper is remembered in the browser (no accounts) and the start page offers to continue or start over.
- 2026-11-11: At checkout the buyer can hide prices inside the hamper: the slip in the box lists item names only, and the confirmation email to the buyer still shows prices.
- 2026-11-11: An item page has ingredients, allergens, storage and a "last reviewed" date. The wording Vietnamese rules require has not been checked and must be confirmed before launch.
- 2026-11-11: The front-end deliverable is plain HTML, CSS and strict TypeScript (ES modules, compiled by `tsc`), with no framework and no backend. All data goes through one file, `src/lib/data.ts`, so a server can replace the JSON files later without changing the pages.
- 2026-11-11: In the front-end-only version the browser works out prices, in one file (`src/lib/pricing.ts`), as a stand-in for the server. In the real shop the server prices everything, as in Bloomy.
- 2026-11-11: The admin screens are in scope and simulated: a fake login and data kept in the browser, so an order placed on the customer side shows up in the admin. The report says plainly that the login and 2-step code are simulated.
- 2026-11-11: The front end is built by a teammate in milestones M0 to M7, one small pull request at a time, and reviewed by the owner and the assistant against the mockups at 1440 and 390 px. The M0 base (tooling, helpers, styles, tests) is written for them. See `docs/frontend-plan.md`.
- 2026-11-11: There is no live demo: the lecturer runs the site on localhost or reads the report. The final zip carries the compiled `public/` folder and one line to start it, so no install is needed.
- 2026-11-11: The backend language is undecided: TypeScript on Node and Express (reusing Bloomy), or C# on ASP.NET Core if a course requires it, in which case .NET 10 LTS. Postgres is fixed. The owner will update this line when the lecturer replies. What stays the same either way is the API contract (`docs/api-contract.md`), not the framework.
- 2026-11-11: The price, budget and safe-expiry rules have shared test cases in `tests/vectors/pricing.json`. The front-end tests read them today, and whichever backend is built must load the same file and pass the same cases.
- 2026-11-11: The backend serves the `public/` folder itself, as Bloomy does; there is no separate web server.
