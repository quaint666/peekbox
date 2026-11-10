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
