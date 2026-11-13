# Peekbox: design mockups

Written 2026-11-11. The screens are drawn for desktop (1440 px wide) and mobile (390 px wide).

## The easiest way to look
Open the images in `mockups/` (lossless WebP; any browser or GitHub shows them). They are exports of the design, cropped to the content.

The live, editable design is on the Claude Design canvas: https://claude.ai/artifact/DnDYnQE8yozCogBcBhe4aa (private until the owner shares it from its Share menu).

## Folders

| Folder | What |
|---|---|
| `mockups/customer/` | The 11 customer pages (Vietnamese, light), each as `.desktop.webp` and `.mobile.webp`: 01 home, 02 builder (with starter boxes), 03 item detail, 04 packaging and review, 05 cart, 06 checkout, 07 order confirmation, 08 blog list, 09 blog post, 10 about and commitments, 11 terms and privacy |
| `mockups/admin/` | The 11 admin screens, desktop and mobile: login, orders, items and stock, campaigns, settings, delivery cutoffs, coupons, reviews, stats, users, account |
| `mockups/states/` | Pages that appear only after something happens, or small features, desktop and mobile: empty cart, checkout errors, ZaloPay payment failed, builder cards (chosen, sold out, expires before the delivery date, would exceed the budget) with a no-results box, 404, cart coupon error, an item sold out during checkout, search suggestions, added-to-hamper toast, share menu, floating contact and back-to-top buttons, loading skeleton, resume an unfinished hamper; admin: empty orders day, login locked, empty items, coupons and reviews |
| `mockups/dark/` | Dark mode of all 11 customer pages and of the customer states (`states/`), desktop and mobile |
| `mockups/foundations/` | The palette and type board, and the logo as SVG (also in `public/images/logo/`). The logo is a traced vector, so a designer may want to redraw it with sharper corners. |
| `source/` | The design source files (`.dc.html`) and `canvas.json` (the board layout). They only render inside the Claude Design canvas; the photos in them point to files stored in that canvas. Keep them as a backup, or to publish again. They are not a usable frontend (see below). |
| `design-notes.md` | Colours (light and dark), type, shapes, and the product rules the screens assume |

The photos are in `public/images/` and the written content (blog posts, legal text, product data) is in `content/`.

## Not drawn yet
- Dark versions of the admin screens and the admin states (the admin is light for now).

## Placeholders
Anything in [BRACKETS] is a fact the team must fill in. Brands ("Thương hiệu A" to "H"), prices, stock, reviews and statistics are samples. All photos are AI-generated placeholders.

## Things that are not obvious
- **The `.dc.html` files in `source/` are not a website.** They are Claude Design canvas files: they use template tags that only that canvas understands, inline styles, and photos stored inside the canvas. Opened in a browser on their own they will not show the design, and they have no working buttons, cart, forms or data. Keep them as a backup. Build the real frontend from the images, `design-notes.md` and the rules in `docs/decisions.md`.
- **Each screen shows one moment, not behaviour.** Hover and focus looks, animation, loading states beyond the skeleton, and tablet width are not drawn. Only 1440 px and 390 px wide.
- **The site is Vietnamese only, so no screen has a language switch.** The 48 desktop and the 48 mobile customer and state screens that used to show a VI/EN button in the header were redrawn without it (2026-11-13), and the admin screens lost their English-name fields and the default-language setting. On every customer screen the light/dark button sits directly to the left of the cart.
- **Photos are AI-generated placeholders**, about 508 px, with a faint Gemini mark in a corner. A shop that promises trust needs real photos of the real products.
- **Contrast** of the main colour pairs was calculated (all text at least 4.5 to 1); it was not tested on real screens or with a screen reader.
