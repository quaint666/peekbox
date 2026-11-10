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
- 2026-11-10: Not building a point-of-sale page, an SEO admin page or a campaigns page. The blog stays.
