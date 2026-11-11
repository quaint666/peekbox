# API contract (DRAFT)

**Status:** a draft, cut down on 2026-11-11. The site is server-rendered (see `docs/architecture.md`), so most of what used to be HTTP endpoints (products, packaging, campaigns, settings, delivery days, wards, delivery fee, coupons, orders, admin) is now plain C# in controllers and services, with no HTTP contract between a front end and a backend. This page keeps only what is still shared: the conventions, the error codes, and the few JSON endpoints that small browser scripts need.

Every endpoint says which functions (F-numbers, `docs/functions.md`) it serves.

## Conventions

- **JSON, UTF-8.** Field names use the framework's default unless there is a reason not to; this is settled when the first endpoint is built. The shared cases in `tests/vectors/pricing.json` use `snake_case`, and the C# tests map them.
- **Money:** whole dong as an integer, tax included (for example `180000`). Never a decimal or a string.
- **Dates:** `YYYY-MM-DD`, Vietnam local date. **Timestamps:** ISO 8601 with offset (`2027-01-15T08:30:00+07:00`).
- **Items are identified by `slug`.** Orders are identified publicly by an unguessable `token` (at least 128 random bits), never by a counting number. The shop-facing **order number** (`PEEK000123`) is for people only.
- **The server decides.** Prices, the safe-expiry rule, the budget cap, stock and delivery-day availability are checked on the server. The shared cases in `tests/vectors/pricing.json` define the rules, and the C# tests must pass them.
- **Error codes** (used for JSON errors and for the messages shown on pages): `validation_failed`, `item_not_found`, `item_sold_out`, `item_expired_for_date`, `over_budget`, `delivery_day_full`, `unknown_packaging`, `coupon_invalid`, `coupon_expired`, `payment_failed`, `rate_limited`. JSON errors use `application/problem+json` (RFC 9457) with `type`, `title`, `status`, `detail`, `code` and `errors`.
- **Safe to repeat:** placing an order must never create two orders (the Back button, a double click). The server checks that the hamper was not already ordered.
- **Security headers** (including the content security policy) are set by the server on every response, not by a `<meta>` tag.
- **Rate limit** on the order actions (Bloomy used 20 per 15 minutes; a value to decide).

## Pages and forms

Pages are Razor views, and every change a visitor makes (set the budget, add or remove an item, check out) is a form post that redirects to the next page. The list of routes is settled in the M0 pull request.

## JSON endpoints

| Method and path | Returns | Functions | When |
|---|---|---|---|
| `POST /api/orders/quote` | Prices a hamper without placing it: body `{ hamper }`, returns the quote and `issues: [{ slug, code }]` for every item that is sold out or expired for the date. It does not fail on issues; it reports them | F21, F22, F35, F36, F39 | The live budget bar, a later slice |
| `POST /api/payments/zalopay/ipn` | ZaloPay calls this, not the browser; the signature is checked in constant time before anything changes | F44, F49 | When ZaloPay is built, later |

## What is fixed, and what is not

Fixed: PostgreSQL, the conventions above, and the rules in `tests/vectors/pricing.json`.
Open: the JSON field-name style, the exact routes, the rate limit value, and the payment details (ZaloPay).
