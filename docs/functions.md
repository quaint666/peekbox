# Functions

What each part of the site does. The IDs (F01 and so on) are reused in the sitemap, the use cases and the front-end plan.

**Priority:** **M** must have, **S** should have, **C** could have.
**Sim** means a stand-in with no real behaviour yet. Which ones become real is decided in `docs/decisions.md`.

| ID | Function | Where | Pri |
|---|---|---|---|
| **Whole site** | | | |
| F01 | Header: logo, menu (Trang chủ, Tạo hộp quà, Giới thiệu, Cam kết, Blog), cart link with item count | all | M |
| F02 | Dropped (2026-11-11): the site is Vietnamese only, so there is no language switch. The number is kept so the other F-numbers do not change | all | – |
| F03 | Light/dark button; follows the system by default; the choice is remembered | all | M |
| F04 | Footer on every customer page and every state: brand line, Khám phá (Giới thiệu, Cam kết, Blog, Tra cứu đơn hàng), Pháp lý (Điều khoản, Chính sách bảo mật), contact [placeholders]. The one exception is the phone share sheet, which shows only the screen | all | M |
| F05 | Campaign banner (Tết 2027, 20/11, 8/3) shown within its dates | Home, Builder | S |
| F06 | Floating contact button: Zalo, phone, email, support hours [placeholders] | all | S |
| F07 | Back-to-top button after scrolling | all | C |
| F67 | Hover, keyboard-focus, pressed and disabled looks for buttons, links, fields, cards and table rows; hover only where a mouse exists; movement is dropped for "reduce motion" (see the Interaction states board) | all | M |
| F08 | Toast messages ("added to hamper", "link copied") | all | S |
| F09 | Not-found page with links home and to the builder, and support hours | 404 | S |
| F10 | Loading skeletons while data loads | Builder, Item | C |
| **Home** | | | |
| F11 | Hero with the promise and a button to start a hamper | Home | M |
| F12 | "How it works" steps and the three occasion cards | Home | M |
| F13 | Banner "you have an unfinished hamper": continue or start over | Home, Builder | S |
| **Builder** | | | |
| F14 | Set the budget (typed, or quick amounts 300.000 / 500.000 / 800.000 / 1.000.000đ) and the delivery date | Builder | M |
| F15 | Starter hampers per occasion; "use this hamper" fills the box, still checked for shelf life | Builder | S |
| F16 | Item cards: photo, name, brand, weight, origin, expiry badge, price | Builder | M |
| F17 | Search items, ignoring accents and case, with suggestions grouped as items and categories | Builder | S |
| F18 | Category chips; checkbox "only items in date for the delivery date" (on by default) with a message saying how many are hidden | Builder | M |
| F19 | Sort: default, price up, price down, expiry latest, name A to Z | Builder | S |
| F20 | Add an item, change its quantity, remove it | Builder | M |
| F21 | Budget bar: goods total and what is left. An item that would exceed what is left is disabled and shows "Vượt X". Delivery is not counted | Builder | M |
| F22 | Shelf-life check: expiry must be later than the delivery date plus the margin (7 days, a setting). Amber badge on cards | Builder | M |
| F23 | Sold-out items disabled | Builder | M |
| F24 | "Continue" to packaging | Builder | M |
| F25 | The hamper in progress is saved on the server and found again by a cookie | Builder | M |
| **Item page** | | | |
| F26 | Photo, name, brand, price, facts table (brand, weight, origin, expiry, production date) | Item | M |
| F27 | In-date notice for the chosen delivery date | Item | M |
| F28 | Add to hamper, with the budget bar | Item | M |
| F29 | Description; ingredients, allergens, storage and "last reviewed" date [placeholders] | Item | S |
| F30 | Reviews (sample text only, no invented reviews) | Item | C |
| F31 | Share: copy link, Facebook, system share sheet | Item | S |
| F32 | "Recently viewed" strip | Item | C |
| F62 | Gallery: a main photo and up to 4 thumbnails; a tap or click shows that photo (on a phone, swipe) | Item | S |
| F63 | Rating summary under the name (average and count, a link to the reviews) and, in the reviews box, the average with a bar for each star count | Item | S |
| F64 | Quantity stepper beside "Thêm vào hộp"; the card says what is left of the budget after adding; on a phone the add bar stays at the bottom of the screen | Item | M |
| F65 | "Hay đi cùng món này": up to 4 other items with an add button; items that would exceed the budget or expire before the delivery date are disabled as in the builder | Item | C |
| F66 | Tabs for Mô tả, Thông tin chi tiết, Giao hàng và đổi trả. Ingredients, allergens and storage are NOT in a tab: they stay visible, and an allergen line sits next to the price | Item | S |
| **Packaging and review** | | | |
| F33 | Packaging choice: kraft box 0đ, hard box with bow +40.000đ, rattan basket +70.000đ [sample fees] | Review | M |
| F34 | List of the items in the hamper; remove an item | Review | M |
| F35 | Block when an item is expired for the date: red banner and disabled button until it is removed or the date changes | Review | M |
| F36 | Summary: goods, packaging, total (tax included), budget left; add to cart | Review | M |
| **Cart** | | | |
| F37 | Show the hamper; edit or clear it | Cart | M |
| F38 | Coupon code, with errors (expired, not valid) | Cart | S |
| F39 | Totals: goods, packaging, delivery fee "calculated next", subtotal | Cart | M |
| F40 | Empty-cart state | Cart | S |
| **Checkout** | | | |
| F41 | Form: sender (name, phone, email); recipient (name, phone); address with ward list, house number and street | Checkout | M |
| F42 | Delivery date with a "day is full, nearest free day is…" message; time window (morning, afternoon, evening) | Checkout | S |
| F43 | Card message (optional); "hide prices inside the hamper" checkbox | Checkout | S |
| F44 | Payment choice: ZaloPay or cash on delivery (Sim: no real payment) | Checkout | M |
| F45 | "I need a company invoice" checkbox | Checkout | C |
| F46 | Field checks with an error summary and messages; keeps what was typed | Checkout | M |
| F47 | Order summary with the delivery fee by ward | Checkout | M |
| F48 | Place the order: makes an order number and saves the order in the database | Checkout | M |
| F49 | Payment-failed page: retry or switch to cash on delivery (Sim: reachable by a test switch) | Checkout | S |
| F50 | "An item sold out meanwhile" notice: item removed, pick a replacement | Checkout | C |
| **Confirmation** | | | |
| F51 | Order number, thanks, status strip (New, Processing, Out for delivery, Delivered; or a cancelled notice), items, delivery details | Confirmation | M |
| F60 | Track-an-order page: order number and sender's phone; one message for any mismatch; rate-limited; a match redirects to the confirmation page | Track | M |
| F61 | The confirmation email carries the link to the confirmation page (Sim: email is a stand-in) | Email | S |
| **Content** | | | |
| F52 | Blog list with category and cards | Blog | S |
| F53 | Blog post with related posts and a call to action | Blog post | S |
| F54 | About and Commitments, including how the safe expiry rule works | About | M |
| F55 | Terms and Privacy with a table of contents | Legal | M |
| **Admin (built after the front end is done; not in the first report)** | | | |
| F56 | Login with username, password and a 2-step code (to be built later) | Admin | S |
| F57 | Orders: filter by date, status and search; open an order and change its status. Shows the orders placed on the customer side | Admin | S |
| F58 | Items and stock: search and status filter, edit form | Admin | S |
| F59 | Campaigns, delivery cut-offs, coupons, reviews, settings, stats, users, account | Admin | S |
| F68 | Account chip at the bottom of the admin sidebar (initials, name, role, a bordered "Đăng xuất" button). On a phone it is an initials button in the header that opens a small menu with Tài khoản and Đăng xuất | Admin | S |
