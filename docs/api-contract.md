# API contract (DRAFT)

**Status:** a draft. No server exists yet, and the front end reads equivalent JSON files from `content/data/`. This page is the part that stays the same whichever backend is built (TypeScript on Express, or C# on ASP.NET Core). It is the thing the front end and the backend both agree to; the framework is not.

Every endpoint says which functions (F-numbers, `docs/functions.md`) it serves.

## Conventions

- **JSON, UTF-8.** Field names are `snake_case`, as in `content/data/`.
- **Money:** whole dong as an integer, tax included (for example `180000`). Never a decimal or a string.
- **Dates:** `YYYY-MM-DD`, Vietnam local date. **Timestamps:** ISO 8601 with offset (`2027-01-15T08:30:00+07:00`).
- **Names and text** exist in two languages as `<field>_vi` and `<field>_en`. The server never sends HTML for the front end to insert.
- **Items are identified by `slug`.** Orders are identified publicly by an unguessable `token` (at least 128 random bits), never by a counting number. The shop-facing **order number** (`PEEK000123`) is for people only.
- **The server decides.** Prices, the safe-expiry rule, the budget cap, stock and delivery-day availability are all checked again on the server. Whatever the browser calculated is ignored. The shared cases in `tests/vectors/pricing.json` define the rules, and both sides must pass them.
- **Errors:** `application/problem+json` (RFC 9457): `{ "type", "title", "status", "detail", "code", "errors": { "field": ["message"] } }`. `code` is one of: `validation_failed`, `item_not_found`, `item_sold_out`, `item_expired_for_date`, `over_budget`, `delivery_day_full`, `unknown_packaging`, `coupon_invalid`, `coupon_expired`, `payment_failed`, `rate_limited`. The front end shows a message per `code` in the visitor's language.
- **Safe to repeat:** `POST /api/orders` takes an `Idempotency-Key` header, so pressing Back or double-clicking never creates two orders.
- **Security headers** (including the content security policy) are set by the server on every response. The `<meta>` tag in the pages is only a guard while developing.
- **Rate limit** on the order endpoints (Bloomy used 20 per 15 minutes; a value to decide).

## Public endpoints

| Method and path | Returns | Functions |
|---|---|---|
| `GET /api/products` | `{ categories: Category[], products: Product[] }`, same shape as `products.json` | F16, F17, F26 |
| `GET /api/packaging` | `{ packaging: Packaging[] }`, same as `packaging.json` | F33 |
| `GET /api/campaigns` | `{ campaigns: Campaign[] }`, same as `campaigns.json`; the front end shows the ones inside their dates | F05, F15 |
| `GET /api/settings` | `{ shelf_life_margin_days, contact: { phone, zalo, email, support_hours } }`. Replaces `SHELF_LIFE_MARGIN_DAYS` in `config.ts` | F22, F06, F04 |
| `GET /api/delivery/days?from=&to=` | `[{ date, full: boolean }]` and `nearest_free` | F42 |
| `GET /api/wards` | `[{ id, name }]` | F41 |
| `GET /api/delivery/fee?ward=<id>` | `{ fee_vnd }` | F47 |
| `POST /api/coupons/validate` | body `{ code, goods_vnd }`, returns `{ discount_vnd }` or a `coupon_*` error | F38 |

`Product`, `Packaging`, `Campaign`, `Hamper` and `Quote` are the shapes in `src/lib/types.ts`.

## Orders

**`POST /api/orders/quote`**: price a hamper without placing it.
Body: `{ hamper, ward?, coupon? }` where `hamper` is `{ budget_vnd, delivery_date, packaging_id, lines: [{ slug, qty }] }`.
Returns a `Quote` plus `delivery_vnd` and `discount_vnd`, and `issues: [{ slug, code }]` listing every item that is sold out or expired for the date. It does not fail on issues; it reports them. (F21, F22, F35, F36, F39, F47)

**`POST /api/orders`**: place an order (guest, no login).
Header: `Idempotency-Key`. Body:

| Field | Notes |
|---|---|
| `hamper` | as above; `delivery_date` required |
| `sender` | `{ name, phone, email }` |
| `recipient` | `{ name, phone }` |
| `address` | `{ ward_id, street }` |
| `delivery` | `{ window: "morning" \| "afternoon" \| "evening" }` |
| `card_message?` | text, at most 300 characters |
| `hide_prices` | boolean: the slip in the box shows no prices |
| `payment_method` | `"zalopay"` or `"cod"` |
| `invoice?` | company details, if requested |
| `coupon?` | code |
| `language` | `"vi"` or `"en"`; the order remembers it for the confirmation page and emails |

The server re-prices everything, and refuses with `item_sold_out`, `item_expired_for_date`, `over_budget`, `delivery_day_full` or `validation_failed`. Stock is taken inside the same database transaction that stores the order. Returns `201` with `{ order_number, token, payment: { method, zalopay_url? } }`. (F41 to F48)

**`GET /api/orders/{token}`**: the confirmation page. Returns the order as it was placed (items with names, brand, weight, origin and expiry, totals, delivery details) plus `fulfillment_status` (`new`, `delivering`, `delivered`, `cancelled`) and `payment_status` (`pending`, `paid`, `failed`, `refunded`). (F51)

**`POST /api/orders/{token}/retry-payment`**: a new ZaloPay link for an order whose payment failed. (F49)

**`POST /api/payments/zalopay/ipn`**: ZaloPay calls this, not the browser. The signature is checked in constant time before anything changes.

## Admin endpoints (staff only, cookie session; summary)

Login and logout (`POST /api/admin/login` takes username, password and, for the owner, a 2-step code), `GET /api/admin/orders` with filters (`date`, `status`, `q`), `PATCH /api/admin/orders/{id}` to change status, create/edit of items and stock, coupons, campaigns, delivery cut-offs, settings, reviews, stats and users. Mutating requests need a CSRF defence. Detailed shapes come when milestone M6 is planned; the front end simulates all of this until then. (F56 to F59)

## What is fixed, and what is not

Fixed: Postgres, the conventions above, the shapes in `types.ts`, and the rules in `tests/vectors/pricing.json`.
Open: the backend language and framework. Candidates are TypeScript on Express, or C# on ASP.NET Core (.NET 10 LTS) if a course requires it. See `docs/decisions.md`.

## Notes for whoever builds the backend

- Keep the database schema as plain SQL owned by a declarative tool (Bloomy used pgschema), not as one framework's migrations, so either language can use the same schema. Seed it from `content/data/`.
- The backend serves the `public/` folder itself, as Bloomy does.
- Pass `tests/vectors/pricing.json` before anything else.
