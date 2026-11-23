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
- **Security headers** are set by the server on every response, not by a `<meta>` tag: the content security policy, `Referrer-Policy: no-referrer`, `X-Content-Type-Options: nosniff` and a `Permissions-Policy` that switches off the camera, microphone, position, payment and USB. The server does not send a `Server` header. A page that has not said how it may be cached is `Cache-Control: private, no-cache` (it shows the visitor's own cart count); a page with a form, and the order page, are `no-store`.
- **Rate limits:** 20 orders, 10 order lookups and 20 new hampers (a first "Lưu" or "Dùng hộp này" with no hamper of one's own) per 15 minutes per address (`appsettings.json`, `RateLimits`). A refused order or lookup gets its page with a note and a 429; a refused new hamper gets the 429 page ("Bạn thao tác hơi nhanh") and `Retry-After: 900`.

## Pages and forms

Pages are Razor views, and every change a visitor makes (set the budget, add or remove an item, check out) is a form post that redirects to the next page. Every address is in `Navigation/SiteLinks.cs`, each action names its own route, and `RoutesTests` lists them all.

## Request map

Every address the site answers, with the methods it takes. Any other method on these addresses is a 405, and any other address is a 404 (`RequestMapTests` checks both, and that this table is the same list as `RoutesTests.Expected`). Pages answer `GET` and `HEAD`; every change a visitor makes is a `POST` with an anti-forgery token, and a value that cannot be bound is a 400 (`RefuseUnboundValuesFilter`). `/error` and `/not-found` have no method rule: the framework re-runs them to show its pages.

| Address | Methods | What |
|---|---|---|
| `/` | `GET,HEAD` | The home page |
| `/about` | `GET,HEAD` | About and commitments (F54) |
| `/admin` | `GET` | Goes to the orders. Signed-in staff only |
| `/admin/orders` | `GET,HEAD` | The orders of one delivery day (or every day) with filters (`date`, `status`, `q`) and the open order (`order`) (F57). Signed-in staff only |
| `/admin/orders/{id:int}/save` | `POST` | Move an order one step forward and save its tracking code and note, in one conditional UPDATE (F57). Signed-in staff only |
| `/admin/account` | `GET,HEAD` | The signed-in person's account: change password, sign out of every device (F68, F59 account). Signed-in staff only |
| `/admin/account/password` | `POST` | Change the password: the current one is checked and counted, the other sessions end. Signed-in staff only |
| `/admin/account/sign-out-everywhere` | `POST` | End every session of this account, this one included. Signed-in staff only |
| `/admin/denied` | `GET` | "You may not open this screen" (403), for a role that is not enough. Signed-in staff only |
| `/admin/login` | `GET,HEAD` | The admin sign-in page (F56). Anyone |
| `/admin/login` | `POST` | Sign in with a user name and password; 5 wrong passwords lock the account for 15 minutes; an address may fail only so often (`RateLimits:AdminLoginFailuresPer15Minutes`). Anyone |
| `/admin/logout` | `POST` | Sign out. Anyone |
| `/blog` | `GET,HEAD` | The blog list (F52) |
| `/blog/{slug}` | `GET,HEAD` | One blog post (F53) |
| `/builder` | `GET,HEAD` | The builder: budget, items, packaging (F13 to F20) |
| `/builder` | `POST` | Save the budget and the delivery date |
| `/builder/packaging` | `POST` | Choose the packaging |
| `/builder/quantity` | `POST` | Add, change or remove an item (a quantity of 0 removes it) |
| `/builder/review` | `GET,HEAD` | Review the box before checkout |
| `/cart` | `GET,HEAD` | The cart |
| `/cart/add` | `POST` | Put the box in the cart |
| `/cart/clear` | `POST` | Empty the cart |
| `/cart/coupon` | `POST` | F38: apply a discount code (rate limited; a refused code comes back as a line under the field) |
| `/cart/coupon/remove` | `POST` | F38: take the code off |
| `/checkout` | `GET,HEAD` | The checkout form |
| `/checkout` | `POST` | Place the order |
| `/checkout/soldout` | `GET` | Where the checkout sends a visitor whose item sold out |
| `/dip/dung-hop` | `POST` | "Dùng hộp này": put a ready hamper in the box |
| `/dip/{id}` | `GET,HEAD` | One occasion page (F15) |
| `/error` | `any` | The error page, shown by the exception handler only (a direct visit is a 404) |
| `/hop/{slug}` | `GET,HEAD` | One ready hamper |
| `/items/{slug}` | `GET,HEAD` | One item |
| `/legal` | `GET,HEAD` | Terms and privacy (F55) |
| `/not-found` | `any` | The 404 and 400 pages, shown by the status-code handler only |
| `/orders/{token}` | `GET,HEAD` | One order, by its unguessable token |
| `/orders/{token}/payment` | `GET,HEAD` | F49: the payment-failed page (an order whose ZaloPay payment failed; any other order goes to its own page) |
| `/orders/{token}/payment/retry` | `POST` | F49: try the payment again |
| `/orders/{token}/payment/cash` | `POST` | F49: switch the order to cash on delivery |
| `/track` | `GET,HEAD` | The order lookup form |
| `/track` | `POST` | Look an order up |
| `/healthz` | `GET,HEAD` | The health check (a minimal endpoint, not a controller): `ok` |

## JSON endpoints

| Method and path | Returns | Functions | When |
|---|---|---|---|
| `POST /api/orders/quote` | Prices a hamper without placing it: body `{ hamper }`, returns the quote and `issues: [{ slug, code }]` for every item that is sold out or expired for the date. It does not fail on issues; it reports them | F21, F22, F35, F36, F39 | The live budget bar, a later slice |
| `POST /api/payments/zalopay/ipn` | ZaloPay calls this, not the browser; the signature is checked in constant time before anything changes | F44, F49 | When ZaloPay is built, later |

## What is fixed, and what is not

Fixed: PostgreSQL, the conventions above, and the rules in `tests/vectors/pricing.json`.
Open: the JSON field-name style, the exact routes, the rate limit value, and the payment details (ZaloPay).
