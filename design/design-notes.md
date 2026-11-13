# Peekbox design notes

Written 2026-11-11. This is the visual language the mockups use, and the rules the screens assume.

## Look
Minimal and calm all year, with a small seasonal accent. Mobile and desktop matter equally. The interface is Vietnamese only. A dark mode is part of the plan from the start (decisions in the project's `docs/decisions.md`).

## Colour: light mode (palette A, sage and cream)
| Role | Hex |
|---|---|
| Page background | `#F7F5EF` |
| Surface (cards, header) | `#FFFFFF` |
| Text | `#1F2A24` |
| Muted text | `#4A5650` |
| Border | `#DAD6C9` |
| Primary (buttons, links) | `#3F6B57` |
| Primary tint (chosen state) | `#E3EFE7` with text `#25513E` |
| Warning (amber) | background `#FFF1CC`, text `#7A4F00` |
| Error and blocked (red) | background `#FBE4E0`, text `#7A1F14`, button `#A32A1E` |
| Seasonal accents | Tết `#B3382A`, 20/11 `#5F4A7A`, 8/3 `#A8345E` |

## Colour: dark mode (neutral near-black)
| Role | Hex |
|---|---|
| Page background | `#0E0F10` |
| Surface | `#1A1C1E` |
| Text | `#F2F4F5` |
| Muted text | `#ABB1B5` |
| Border | `#3A3E42` |
| Primary on dark (text and outlines) | `#8CC4A8`; filled buttons stay `#3F6B57` with white text |
| Chosen state | background `#1D2B24`, text `#BFE3CF` |
| Unchosen option (for example payment) | black `#000000` with a `#3A3E42` border |

Product photos have a light sage background, so in dark mode they sit as light tiles. That is intended.

## Type
Be Vietnam Pro (400, 600, 700). It supports Vietnamese diacritics well. Body text 15 to 17 px, headings 20 to 48 px.

## Shape and touches
- Cards: 14 to 16 px radius, 1 px border, a soft shadow.
- Buttons and inputs: at least 44 px tall (48 px for the main button).
- A 3 px sage bar along the top of the header, and the Peekbox logo (an open box with an eye peeking out) beside the name. The name is set in Be Vietnam Pro Bold. Light sage `#8CC4A8` is used in dark mode.
- The home page has a soft sage band behind the hero and a "Tết · 20/11 · 8/3" pill with the three seasonal colours.
- No emoji. Icons are simple stroked line icons.

## Rules the screens assume
1. **Budget first.** The customer sets a budget, then picks items. A budget bar shows what is left. The budget covers the goods only, not the delivery fee.
2. **Safe expiry.** An item whose expiry is before the delivery date plus a margin (a setting, 7 days in the examples) cannot be ordered. The customer removes it or changes the delivery date. Where it shows: amber badge on a card, red banner and a disabled button on the review page, a check under the date field at checkout.
3. **Guest checkout.** No customer accounts. Order status is on the order confirmation page.
4. **Delivery only** for now. Payment by ZaloPay or cash on delivery.
5. **Vietnamese only.** A light/dark button sits in every customer header, directly to the left of the cart. There is no language switch.
6. **Admin** has 11 screens (login, orders, items and stock, campaigns, settings, delivery cutoffs, coupons, reviews, stats, users, account). On mobile the menu becomes a scrolling row of tabs and tables become cards. The season banner is edited on the campaigns screen only: ignore the "Dải thông báo theo mùa" block in the settings mockup (`admin/05-settings`), which is superseded.

## Placeholders in the mockups
Anything in [BRACKETS] is a fact the team must fill in. Brands ("Thương hiệu A"), prices, stock, reviews and statistics are samples.

## States (empty and error)
- Errors say what is wrong and how to fix it, in words, with an icon: never colour alone. Fields get a 2 px red border `#A32A1E`; a summary at the top links to each error.
- The budget is a hard cap: an item that would exceed what is left shows a disabled "Vượt X" button. Sold-out and expired-for-the-delivery-date items are greyed with a badge and a disabled button.
- A failed ZaloPay payment keeps the order and the box; the customer can retry or switch to cash on delivery. How long items are held is a bracketed placeholder, [30 phút].

## More states
- Coupon error: the field gets the red border and a message under it; the customer can continue with the field empty.
- An item that sells out during checkout is removed from the box with a notice and nothing is charged; the customer picks a replacement or continues with fewer items.
- Admin login is locked after repeated wrong attempts: [5] attempts and [15 phút] are placeholders for the team to set.
- Dark mode: unchosen options (payment, packaging, status chips) are black `#000000`; the chosen one keeps the sage tint.

## Quality-of-life features (added after comparing Bloomy and live shops)
- **Search on the builder.** A search box above the category chips with suggestions grouped as items and categories. Search must ignore accents and case: typing "mut" finds "Mứt". Pressing Enter shows all results. A sort menu (default, price low to high, price high to low, expiry latest first, name A to Z) sits beside it.
- **"Only items in date for the delivery date"** is a checkbox, on by default. The page says how many items it hides and offers to show them.
- **Added-to-hamper toast** at the bottom: what was added, what is left of the budget, and a link to the hamper.
- **Share** on the item page: copy link, Facebook, and the phone's share sheet. No Zalo button, because it needs a third-party script.
- **Floating contact button** (Zalo, phone, email, support hours) and a **back-to-top button** that appears after scrolling. Numbers and hours are placeholders.
- **Loading skeletons** on the builder instead of a spinner.
- **Resume banner**: a hamper that was left unfinished is remembered by the site (a cookie and a database row; there are no accounts), and the start page offers to continue or start over.
- **Hide prices inside the hamper** is a checkbox at checkout: the slip in the box lists names only; the confirmation email to the buyer still shows prices.
- **Ingredients, allergens and storage** on the item page, with a "last reviewed" date. Contents are placeholders; whether Vietnamese rules require specific wording was not checked.
- **Recently viewed** strip on the item page.
- **Admin:** search and a status filter on the items and coupons lists.
