# Architecture

**Status:** draft. Nothing is built yet. This page says how the Peekbox app is put together, so the pages, the server code and the reviews all follow one plan. The decisions behind it are in `docs/decisions.md`.

## 1. What we build

One web app, in one repository:

- **ASP.NET Core MVC** on **.NET 10 (LTS)**, with **Razor** views (`.cshtml`).
- **PostgreSQL 17**, reached through **Entity Framework Core** (the Npgsql provider).
- Plain **CSS** (the design tokens already in this repo) and a little **TypeScript** that runs in the browser.

The server makes the pages. The browser shows them.

## 2. How a request works

```
browser ──► Controller ──► Service (rules: price, budget, expiry)
                │               │
                │               └──► EF Core ──► PostgreSQL
                ▼
           Razor view (HTML) ──► browser
```

- A **controller** receives the request, asks a **service** to do the work, and picks a **view**.
- A **view model** is a small class holding exactly what one page needs. The view only draws it.
- A visitor's action (add an item, check out) is a form post. The server does the work and redirects to the next page, so the Back button never repeats it.

## 3. Server and browser

| | Server | Browser |
|---|---|---|
| Prices, totals, budget left | Yes, all of them | Shows them |
| Expiry check against the delivery date | Yes | Shows the result |
| Stock, coupons, delivery days | Yes | Shows the result |
| Page HTML | Yes (Razor) | Never builds page HTML |
| Theme and language switch, small page behaviour | No | Yes (TypeScript) |
| Live budget bar in the builder | Answers a small JSON request | Asks, then shows the answer (a later slice) |

The builder starts as plain form posts, with no JavaScript. The live budget bar is added later as its own slice.

## 4. Where the code goes (proposal)

To be settled in the scaffold pull request.

```
src/Peekbox.Web/
  Controllers/      one per area: Home, Builder, Cart, Checkout, Orders, Admin
  Models/           view models, and the entity classes EF Core generates
  Services/         the rules: pricing, budget, expiry (one place, no copies)
  Views/            Shared/_Layout (header, footer), one folder per controller
  Resources/        interface text in Vietnamese and English (.resx)
  wwwroot/          css, fonts, images, and the compiled TypeScript
tests/              C# tests that load tests/vectors/pricing.json
db/                 schema.sql (the only schema source) and the seed data
```

The existing `design/`, `content/` and `docs/` folders stay as they are.

## 5. Data

- **`schema.sql` is the only schema source**, applied with pgschema as in Bloomy. No hand-written ALTER statements.
- EF Core only **maps** tables to classes. We do **not** use EF migrations. The docs allow this: when the schema is managed outside EF, update the entity classes when the database changes.
- Entity classes can be generated with `dotnet ef dbcontext scaffold`. Our own code goes in `partial` classes, so re-generating does not overwrite it.
- The 24 sample products in `content/data/` become seed rows.

## 6. Money and expiry

- Price, budget and safe-expiry rules live in **one** service. Nothing else calculates a price.
- `tests/vectors/pricing.json` holds the shared cases. The C# tests load it and must pass every case.
- Prices are whole dong, tax included.

## 7. The hamper and orders

- No accounts. A visitor gets a long-lived cookie with a random token, and the unfinished hamper is a row in PostgreSQL under that token. It survives a restart.
- We do **not** use the framework's session for this: its default idle timeout is 20 minutes and its in-memory store is lost when the server restarts.
- An order is identified publicly by an unguessable token, never a counting number. Placing an order is safe to repeat (the Back button must not create a second one).
- Forms carry an anti-forgery token (the form tag helper adds it).

## 8. Text in two languages

- **Interface text** (buttons, labels, messages) lives in `.resx` files, one set per language, used in views through the framework's localizer. Vietnamese is the default.
- **Item, category, campaign and blog text** has `_vi` and `_en` fields in the database, with Vietnamese shown when English is missing.
- Never type the same text twice, and never put HTML inside a translated string.

## 9. Rules that carry over from Bloomy

- **Strict content security policy:** the server sends the header. No inline scripts, no inline styles, no `onclick="..."`. Use CSS classes and script files.
- **One source for each value** (a setting, a resource, a constant). Never repeat a hardcoded value.
- **Encoding:** Razor HTML-encodes `@` expressions by default, but only for HTML. Never use `Html.Raw` or `HtmlString` on text from visitors or data. Never put such text inside JavaScript or into a URL path: pass it in a `data-` attribute or as a query value. (Microsoft docs: Razor syntax, "Expression encoding"; preventing cross-site scripting.)
- Colours and sizes come from `tokens.css`.
- Small pull requests, and say what you did not test.

## 10. Not decided yet

- Whether the admin is real (a real login, orders in the database) or stays simulated.
- How an order is paid, and how items are held for an unpaid order.
- The email sender, and the rate limit on order endpoints.
- How the project is run for the lecturer. The report covers the front end only; the deployed site via Docker comes later.

## 11. Not checked against the docs yet

- The exact way to set the content security policy header with Razor views. No official Microsoft page was found; only community examples.
- That Vietnamese (`vi`) works as a supported culture.
- The Npgsql provider version that matches .NET 10.
- That the existing front-end tests still make sense once the client price code is gone.
