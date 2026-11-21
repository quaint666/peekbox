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

## Colour: dark mode (near-black with a slight green tint)
Changed 2026-11-15: the dark greys were slightly blue, which felt cold next to the sage and the kraft photos. They now lean green, like the light palette. Text contrast: text 14 to 16 to 1, muted text 7.9 to 1 or better.

| Role | Hex |
|---|---|
| Page background | `#121513` |
| Surface (cards, header, footer) | `#1B201C` |
| Text | `#EEF0EA` |
| Muted text | `#AEB5AC` |
| Border | `#343B36` |
| Primary on dark (text, links, outlines, focus ring) | `#9EC3A5`; filled buttons stay `#3F6B57` with white text |
| Chosen state | background `#1D2B24`, text `#BFE3CF` |
| Unchosen option (for example payment) | the page colour `#121513` with a `#343B36` border. No pure black anywhere. |
| Wide colour blocks | deeper than in light mode: the seasonal strip `#8E3024`, the green band near the end of the home page `#2F5242`, the occasion card panels Tết `#5A1D16`, 20/11 `#302838`, 8/3 `#4F1E31` (white text 8 to 1 or better on all of them). Buttons keep the brighter green. |

Product photos have a light background, so in dark mode they are shown at 90 % brightness to stop them glaring. The logo is not dimmed.

## Type
Be Vietnam Pro (400, 600, 700). It supports Vietnamese diacritics well. Body text 15 to 17 px, headings 20 to 48 px. The page title is Bold (700); section and card headings are SemiBold (600), which reads calmer.

## Shape and touches
- Cards: 14 to 16 px radius, 1 px border, a soft shadow.
- Buttons and inputs: at least 44 px tall (48 px for the main button).
- A 3 px sage bar along the top of the header, and the Peekbox logo (an open box with an eye peeking out) beside the name. The name is set in Be Vietnam Pro Bold. Light sage `#9EC3A5` (the dark-mode link green) is used for the logo in dark mode.
- The home page has a soft sage band behind the hero and a "Tết · 20/11 · 8/3" pill with the three seasonal colours. Below the hero the page uses only the page colour, with white cards on it (the three promises sit between two thin lines, not on a white band).
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
- Locked builder steps are not drawn at all: only a lock note shows where they start. Before the budget and date are saved, the note says to press "Lưu" and nothing is below it (02); before the box holds an item, the packaging note says "Chọn ít nhất 1 món" and nothing is below it (26). (They used to be drawn faded to 65 %.)
- Coupon error: the field gets the red border and a message under it; the customer can continue with the field empty.
- An item that sells out during checkout is removed from the box with a notice and nothing is charged; the customer picks a replacement or continues with fewer items.
- Admin login is locked after repeated wrong attempts: [5] attempts and [15 phút] are placeholders for the team to set.
- Dark mode: unchosen payment options and status chips use the page colour `#121513`; unchosen packaging cards use the card colour `#1B201C`, as drawn in the dark mockup 27 (corrected 2026-11-16: this line used to say packaging too). The chosen one keeps the sage tint.

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
- **Focus ring**: 3 px, offset 2 px, `#3F6B57` in light mode and `#9EC3A5` in dark mode. It is shown for the keyboard only and never removed.
- **Primary button**: hover `#2C4D3E` (dark mode: `#4A7A64`), pressed `#25513E` (dark mode: `#2C4D3E`) and 1 px down. **Secondary button**: the sage tint fills in on hover.
- **Header link**: hover and keyboard focus turn it green and draw a 2 px line under it from the left in 250 ms (as Bloomy's header does; with "reduce motion" the line just appears). "Dịp" opens a small panel of the six occasions on hover and keyboard focus; Escape closes it.
- **Text field**: the border darkens on hover, gets 2 px green plus the ring on focus, 2 px red `#A32A1E` (dark mode `#F4B8AF`) on error with a message.
- **Item card (builder)**: the photo and the name are links to the item page; "Thêm" is its own button. On hover the card rises 2 px with a deeper shadow and a green border, the photo zooms to 105 % (like the occasion cards on the home page) and the name turns green and underlined; with "reduce motion" on, only the border, shadow and name change. Keyboard focus rings the link that has it. Once the item is in the box, "Thêm" becomes a − n + pill: − and + on the card colour, the number on the sage tint, and − at 1 takes the item out and brings "Thêm" back. A sold-out card is greyed and does not react.
- **Table row (admin)**: hover tint `#F1F6F2` (dark mode `#202428`); the open row keeps the chosen tint. These two tints are the only new colours here.
- **Occasion cards on the home page** rise 4 px, the photo zooms to 105 % and the arrow moves 4 px right.
- **Pressed** on a phone: the same pressed colour, no movement.

## Home: Cho từng dịp
The second highlight of the home page, after the hero. A large Tết card on the left (520 px high, photo with a gradient in the occasion colour) and two smaller cards (20/11 and 8/3) on the right, where the occasion colour fills the left side and the photo sits on the right; on a phone they stack, with Tết taller. Each card is one link with a white date chip ("Mùa Tết 2027", "Ngày Nhà giáo Việt Nam", "Ngày Phụ nữ"), the name, one sentence and a white button with an arrow. The text sits on the opaque part of the colour (measured on the Tết card: white text at 7 to 1 or better; the two other colours are darker). The photos are placeholders.

## Occasion pages and ready hampers
Drawn 2026-11-17 (`Occasion*.dc.html`; exports `customer/13-occasion-tet`, `14-occasion-20-11`, `15-occasion-8-3-and-20-10` and their dark copies). One source draws all three pages: `const occasion` at the top of its script is `tet`, `teachers` or `women`.
- Hero in the occasion's deep colour with a glow (gold, lilac, rose) and a 4 px line in a second colour; breadcrumb, occasion chip, the last delivery date as `[NGÀY]`, "Tự tạo hộp từ đầu", links to the other two occasions.
- Budget chips under the hero are plain links to the sections below. Labels from 1 million up are short ("1 triệu"); section headings and totals use the full amount.
- Each budget is a row of hamper cards that scrolls sideways (3¼ cards on desktop, one on a phone). A card: photo (stand-ins for now), who it is for, name, item count and packaging, the items, the earliest expiry with its item, the total, "Dùng hộp này", "Xem từng món".
- Page tints: light Tết `#FBF1EC`, 20/11 `#F4F1F8`, women `#FBF0F4`; dark `#171311`, `#141317`, `#171215`. On dark, the chips and markers use lighter accents (`#F2A193`, `#C9B6E4`, `#F0A3BF`, 9 to 1 or better on the page).
- States 31 ("Thay hộp hiện tại?", the focus on "Giữ hộp của tôi") and 32 ("Xem từng món": each item with brand, weight, origin, expiry or "Không có hạn dùng", and the total) are drawn over the page. The builder card shows the same grey "Không có hạn dùng" pill where the expiry pill would be (not drawn on state 04).
- The builder's "Hộp gợi ý" strip: occasion chips (the chosen one filled in its colour), hampers for the saved budget, a link to the occasion page. The empty cart has "Xem hộp gợi ý cho Tết" beside "Tạo hộp quà".

## Builder: the item list as rows
Redrawn 2026-11-17 (Builder boards, stages 26 and 27, light and dark). On "Tất cả" with no search and the default sort, each category is a sideways row: the heading (name, "n món", "✓ n trong hộp"), "Xem tất cả", ← → (desktop; ← grey at the start), the cards (232 px desktop, 148 px phone), a dashed "Xem tất cả …" end tile, and a 4 px position bar with "1–3 / 8 món". Desktop rows fade out over the last 64 px. A card in the box: 2 px green border, a green "✓ Trong hộp" tag on the photo, the −/+ pill. The category chips were replaced the same day by the "Danh mục" control (see "Builder: the two pickers"). Phone: no brand or origin line on the cards, full-width buttons, the swipe hint (stage 26 only, as on a first visit). A chosen category, a search or another sort keeps the grid of the earlier boards (not redrawn).

## Builder: the two pickers
Drawn 2026-11-17 (Builder boards; a `view` value draws each moment; states 33 to 36). On desktop, step 1 is two lines: the budget field with "Chọn nhanh", the date field and "Lưu" on one line (labels above), then one full-width line with the notes (the amber "not saved" note, then the help text). "Chọn nhanh" (wallet icon, chevron) is a 48 px button on the budget field's line; its panel has the seven amounts in four columns (two on a phone), the field's amount highlighted, and "Ngân sách chỉ đổi khi bạn bấm Lưu." After a pick (state 36) the field shows the new amount and an amber note under it: "Chưa lưu: ngân sách đang dùng vẫn là 500.000đ. Bấm Lưu để dùng 800.000đ." "Danh mục" is labelled like "Tìm món" and "Sắp xếp" and drawn like the sort menu (48 px, 10 px corners, field border); its panel (600 px, three columns; two on a phone) has "Tất cả · 81 món" and a tile per category with its count and "✓ n" when items of it are in the box; the current one is outlined in green. Chosen (state 35): the control is green with "Socola" and a separate ✕ button, the count line reads "Socola: hiện 3 món.", and the list is the grid. Phone: search on its own line, then "Danh mục" and "Sắp xếp" side by side; panels open as a sheet with a handle and ✕ over a dimmed page.

## Item page
Redrawn 2026-11-16 for the first build (M1, A3). Left: one square photo (each item has one photo). Right: a category chip, the name and brand, the price with "Đã gồm thuế", three fact chips (expiry, origin, weight), the in-date notice, the budget card (budget, bar, what is left, then the add control of the builder: "Thêm vào hộp", which becomes the −/+ pill once the item is in the box) and three small promises (expiry checked, delivery on the chosen date, ZaloPay or cash). Under it: one "Mô tả" card with the description and a "Thông tin chi tiết" table (category, brand, weight, origin, expiry; no production date, which the data does not have). On a phone the add bar (budget, bar, what is left, the add control) stays at the bottom of the screen; the desktop image shows the item before it is added, the phone image after (1 in the box).

Left out until their slices, and drawn earlier (the old images are in the git history before 2026-11-16): the thumbnails, the rating and reviews, the share button, the amber allergen line and the ingredients, allergens and storage box (F29, until there is real text), the tabs, "Hay đi cùng món này" and "Món bạn vừa xem". States 24 (Thông tin chi tiết tab: category, packaging and producer) and 25 (Giao hàng và đổi trả tab: delivery, fee shown before paying, payment, the expiry rule and the return period; the fee rule and the number of days are placeholders) still show the tabbed page; they are the design for when the tabs come back.

## Review page
"Thêm vào giỏ hàng" works as drawn in 04 and 29 and opens the cart; with an out-of-date item it is disabled with "Bỏ món hết hạn hoặc đổi ngày giao để tiếp tục." under it, as in 30. The line "Giá là giá mẫu cho bản thiết kế." is a note for the design, not page text, and is left out.

## Cart
Redrawn 2026-11-16 for the first build (M2). The cart is the visitor's one hamper once it holds an item, so every header shows "Giỏ hàng (1)" from the first item on, and "Giỏ hàng (0)" before (screen readers hear "Giỏ hàng, 1 hộp" or "Giỏ hàng, trống"); on /cart the link is marked as the current page (green text and border), on the empty cart too. Left out until their slices: the coupon field (F38; state 06 still shows it, as the design for then) and "Xem hộp gợi ý cho Tết" on the empty cart (F15). "Tiếp tục thanh toán" is drawn disabled with "Thanh toán sẽ có trong bản sau." until checkout (M3). Not drawn: "Xóa hộp" asks first, in a small box in the middle of the screen over a dimmed page ("Xóa cả hộp quà?", the text "Các món và kiểu đóng gói sẽ bị bỏ khỏi hộp. Ngân sách và ngày giao vẫn được giữ.", a red "Xóa" and an outlined "Giữ lại", which has the focus). An item that expires before the delivery date shows the review page's red block and "Bỏ món này" in the cart too.

## Built differently from the mockups (M3)
Each one is on purpose; change the mockup or the page when the reason goes away.
- **No email yet (F61 comes later),** so no page says one was sent. Confirmation (07): the banner says "Hãy lưu đường dẫn của trang này để xem lại đơn hàng. Bạn cũng có thể dùng mục Tra cứu đơn hàng…" (the words link to /track) instead of "Chúng tôi đã gửi email xác nhận…". Track (12): the hint under the order number is "Mã có trên trang xác nhận đơn hàng." without the email.
- **Confirmation sums** have an "Đóng gói" row: the mockup's 0đ kraft box let Tiền hàng + Phí giao hàng equal the total, a 40.000đ box does not. The footnote is "Giá và phí giao hàng là giá mẫu." (the order number is real now).
- **Cancelled order, dark:** the banner's edge is the existing `--color-danger-border` (#A32A1E), not the mockup's #7A3B33, to keep one dark red.
- **Header:** the seven links (with "Dịp") sit in a row from 1100 px wide (measured 2026-11-21 in Chromium: at 1100 px the links end 32 px before the theme button, no sideways scroll); below that they are in the menu, as on a phone. Only 1440 and 390 are drawn.

## Admin account
The bottom of the admin sidebar shows a chip: initials in a green circle, the name, the role, and a bordered "Đăng xuất" button with an icon. On a phone the header has an initials button (44 px) that opens a menu with the name, Tài khoản and Đăng xuất (state 23). The name and role are placeholders.

