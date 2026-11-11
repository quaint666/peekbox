# Use cases

Actors: **Customer** (anyone building a hamper; no account), **Visitor** (only reading), **Staff** (admin, built after the front end).

| ID | Use case | Actor | Main steps | Alternatives and errors | Functions |
|---|---|---|---|---|---|
| UC01 | Build a hamper within a budget | Customer | Open the builder; set budget and delivery date; browse; add items; watch the budget bar; continue | An over-budget item is disabled; a sold-out item is disabled; an item expiring too soon shows amber | F14, F16, F20–F25 |
| UC02 | Start from a starter hamper | Customer | Pick "use this hamper"; the box fills; edit freely | An item in it is expired for the date: flagged like any item | F15, F22 |
| UC03 | Find an item | Customer | Type in search (accents optional); pick a suggestion; sort; filter by category or in-date | No match: a "no items match" box with "clear filter" | F17–F19 |
| UC04 | Read an item and add it | Customer | Open the item; read facts, allergens, storage; add to the hamper; share the link | Item expired for the date: adding is blocked | F26–F29, F31, F32 |
| UC05 | Choose packaging and review | Customer | Pick a packaging style; check the item list and totals; add to cart | An expired item is present: see UC06 | F33, F34, F36 |
| UC06 | Fix an expired item | Customer | Read the red banner; remove the item or change the delivery date; continue | Nothing left in the box: empty state | F35 |
| UC07 | Apply a coupon | Customer | Type the code in the cart; apply; totals update | Expired or invalid code: error under the field; continue without it | F38, F39 |
| UC08 | Check out as a guest | Customer | Fill in sender, recipient, address, date, time window, optional card message; choose payment; place the order | Errors: summary plus field messages, data kept; chosen date full: nearest free day shown; hide prices ticked: slip without prices | F41–F48 |
| UC09 | Pay, or recover from a failed payment | Customer | Choose ZaloPay or cash on delivery; place the order | Payment fails: order and box kept; retry or switch to cash on delivery | F44, F49 |
| UC10 | See the order and its status | Customer | Land on the confirmation page; read the order number, items and status | — | F51 |
| UC11 | An item sells out during checkout | Customer | A notice lists the removed item; pick a replacement or continue with fewer items | Nothing is charged | F50 |
| UC12 | Switch theme | Anyone | Press the theme button; the page changes; the choice is remembered | – | F03 |
| UC13 | Resume an unfinished hamper | Customer | Return to the site; read the banner; continue or start over | Browser data cleared: no banner | F13, F25 |
| UC14 | Get help or recover from a bad link | Anyone | Open the contact button (Zalo, phone, email, hours); or land on 404 and follow the links | — | F06, F09 |
| UC15 | Read content | Visitor | Open Blog, a post, About, Commitments, Terms or Privacy | — | F52–F55 |
| UC16 | Run the shop (later) | Staff | Log in; view the orders placed by customers and change their status; edit items and stock; manage campaigns and coupons | Wrong password or lockout: an error message | F56–F59 |
