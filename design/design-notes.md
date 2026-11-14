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
6. **Admin** has 11 screens (login, orders, items and stock, campaigns, settings, delivery cutoffs, coupons, reviews, stats, users, account). On mobile the menu becomes a scrolling row of tabs and tables become cards.

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

## Header and footer on every page
Every customer page and every state starts with the same header (logo, Trang chủ, Tạo hộp quà, Giới thiệu, Cam kết, Blog, Tra cứu đơn hàng, light/dark button, cart) and ends with the same footer. The page you are on is bold and green in the header. A page shorter than the screen keeps the footer at the bottom of the screen. The only drawn exception is the phone share sheet, which shows the screen with the sheet open. On a phone the header links are in the menu (state 21) and the footer is the same footer, stacked.

## Interaction states
Every control has five looks: default, hover, keyboard focus, pressed, disabled. They are drawn once on the "Interaction states" board (light and dark, in `mockups/foundations/`) and are the same on every page.
- **Hover** only where a mouse exists (`@media (hover: hover)`). A phone shows pressed, never hover.
- **Colour changes take 150 ms, movement 200 ms.** With "reduce motion" on, movement is dropped and the colour change stays.
- **Focus ring**: 3 px, offset 2 px, `#3F6B57` in light mode and `#8CC4A8` in dark mode. It is shown for the keyboard only and never removed.
- **Primary button**: hover `#2C4D3E` (dark mode: `#4A7A64`), pressed `#25513E` (dark mode: `#2C4D3E`) and 1 px down. **Secondary button**: the sage tint fills in on hover.
- **Header link**: hover turns it green and adds a 2 px underline 8 px below the text.
- **Text field**: the border darkens on hover, gets 2 px green plus the ring on focus, 2 px red `#A32A1E` (dark mode `#F4B8AF`) on error with a message.
- **Clickable card**: rises 2 px and gets a deeper shadow and a green border; a sold-out card is greyed and does not react.
- **Table row (admin)**: hover tint `#F1F6F2` (dark mode `#202428`); the open row keeps the chosen tint. These two tints are the only new colours here.
- **Occasion cards on the home page** rise 4 px, the photo zooms to 105 % and the arrow moves 4 px right.
- **Pressed** on a phone: the same pressed colour, no movement.

## Home: Cho từng dịp
The second highlight of the home page, after the hero. A large Tết card on the left (520 px high, photo with a gradient in the occasion colour) and two smaller cards (20/11 and 8/3) on the right, where the occasion colour fills the left side and the photo sits on the right; on a phone they stack, with Tết taller. Each card is one link with a white date chip ("Mùa Tết 2027", "Ngày Nhà giáo Việt Nam", "Ngày Quốc tế Phụ nữ"), the name, one sentence and a white button with an arrow. The text sits on the opaque part of the colour (measured on the Tết card: white text at 7 to 1 or better; the two other colours are darker). The photos are placeholders.

## Item page
Left: a main photo with four thumbnails (the first is chosen). Right: a category chip, the name, brand and rating, the price and share button, a short description, three fact chips (expiry, origin, weight), the in-date notice, an amber allergen line, the budget card (bar, quantity stepper, "Thêm vào hộp", what is left after adding) and three small promises (expiry checked, delivery on the chosen date, ZaloPay or cash). Under it: tabs (Mô tả, Thông tin chi tiết, Giao hàng và đổi trả), then the always-visible ingredients, allergens and storage box, the reviews with an average and a bar per star count, "Hay đi cùng món này" (4 items; 2 columns on a phone) and "Món bạn vừa xem". On a phone the add bar (budget, stepper, add button) stays at the bottom of the screen. The other two tabs are drawn as states 24 (Thông tin chi tiết: the facts table with category, packaging and producer) and 25 (Giao hàng và đổi trả: delivery, fee shown before paying, payment, the expiry rule and the return period; the fee rule and the number of days are placeholders).

## Admin account
The bottom of the admin sidebar shows a chip: initials in a green circle, the name, the role, and a bordered "Đăng xuất" button with an icon. On a phone the header has an initials button (44 px) that opens a menu with the name, Tài khoản and Đăng xuất (state 23). The name and role are placeholders.

