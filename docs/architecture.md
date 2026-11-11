# Architecture

**Status:** draft. Nothing is built yet. This page says how the Peekbox app is put together, so the pages, the server code and the reviews all follow one plan. The decisions behind it are in `docs/decisions.md`.

**Principle:** use what ASP.NET Core provides before inventing our own way. If we do something differently, this page says why.

## 1. What we build

One web app, in one repository:

- **ASP.NET Core MVC** on **.NET 10 (LTS)**, with **Razor** views (`.cshtml`).
- **PostgreSQL**, reached through **Entity Framework Core** (the Npgsql provider).
- Plain **CSS** (the design tokens already in this repo) and a little **TypeScript** that runs in the browser.
- **Vietnamese only.** There is no language switch.

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
| Search and sort in the builder | Yes (a GET form) | Shows the result |
| Page HTML | Yes (Razor) | Never builds page HTML |
| Theme switch (light and dark), small page behaviour | No | Yes (TypeScript) |
| Live budget bar in the builder | Answers a small JSON request | Asks, then shows the answer (a later slice) |

The builder starts as plain form posts, with no JavaScript. The live budget bar and live search suggestions are added later as their own slices.

## 4. Where the code goes (proposal)

Settled in M0 (the layout below is what exists).

```
src/Peekbox.Web/
  Controllers/      one per area: Home, Builder, Cart, Checkout, Orders
  Models/           view models
  Services/         the rules: pricing, budget, expiry (one place, no copies)
  Data/             the database context, entity classes, Migrations/, seed code
  Views/            Shared/_Layout (header, footer), one folder per controller
  Security/         the content security policy, written once
  Navigation/       the header and footer links and the icons, written once
  Scripts/          the TypeScript source (npm run build compiles it to wwwroot/js)
  wwwroot/          css, fonts, images, and the compiled TypeScript
tests/              C# tests that load tests/vectors/pricing.json
```

The existing `design/`, `content/` and `docs/` folders stay as they are.

## 5. Data

- **The entity classes are the source of the schema.** We use EF Core code-first **migrations**: `dotnet ef migrations add` creates a migration, and the migration files are kept in the repository.
- **Applying migrations:** on a developer machine, `dotnet ef database update`. In production the docs recommend a migration bundle for automated deployment, or an idempotent SQL script when the SQL must be reviewed. They advise against casually running migrations at app startup. We decide which one when we deploy.
- **Seed data** (the 24 sample products and the rest of `content/data/`) is loaded by EF Core's `UseSeeding`, which the docs recommend for larger data. `HasData` is not used for the catalogue.
- Text fields are Vietnamese only. The `_en` fields in `content/data/` are not mapped.
- **The tool** is `dotnet ef`, listed in `dotnet-tools.json`; `dotnet tool restore` installs it.
- **The connection string** is the secret `ConnectionStrings:Default`: user secrets in development, the environment elsewhere (written `ConnectionStrings__Default`). It is never in a file of the project.
- **Tests** create a temporary database on that server, fill it with the real migrations and seed, and drop it at the end.

## 6. Money and expiry

- Price, budget and safe-expiry rules live in **one** service. Nothing else calculates a price.
- `tests/vectors/pricing.json` holds the shared cases. The C# tests load it and must pass every case.
- Prices are whole dong, tax included.

## 7. The hamper, forms and orders

- No accounts. A visitor gets a long-lived cookie with a random token, and the unfinished hamper is a row in PostgreSQL under that token. It survives a restart.
- We do **not** use the framework's session for this. This is the one place we differ from the framework's own tool: its default idle timeout is 20 minutes and its in-memory store is lost when the server restarts.
- An order is identified publicly by an unguessable token, never a counting number. Placing an order is safe to repeat (the Back button must not create a second one).
- Forms carry an anti-forgery token (the form tag helper adds it).
- Forms are validated on the server with data annotations, always. The framework's own browser-side validation scripts may be added, served from our own site.

## 8. Language

- **Vietnamese only.** All text is written in the views and shared partials, and `<html lang="vi">` is fixed.
- Item, category, campaign and blog text is one Vietnamese field each.
- Text used in more than one place lives in one shared place (a partial or a constant). Never type the same text twice, and never put HTML inside a text constant.
- English is not planned. Adding it later would mean moving the text out of the views.

## 9. Rules that carry over from Bloomy

- **Strict content security policy:** the server sends the header. No inline scripts, no inline styles, no `onclick="..."` or `onchange="..."`. Use CSS classes and script files. Some framework sample code uses inline handlers: copy the idea, not that line.
- **One source for each value** (a setting, a constant, a shared partial). Never repeat a hardcoded value.
- **Encoding:** Razor HTML-encodes `@` expressions by default, but only for HTML. Never use `Html.Raw` or `HtmlString` on text from visitors or data. Never put such text inside JavaScript or into a URL path: pass it in a `data-` attribute or as a query value. (Microsoft docs: Razor syntax, "Expression encoding"; preventing cross-site scripting.)
- Colours and sizes come from `tokens.css`. Our design wins over the template's Bootstrap, which is removed.
- Small pull requests, and say what you did not test.

## 10. Not decided yet

- The admin. Real admin code is written after the front end is done (the framework's own sign-in is the likely choice, not yet checked). The first report covers the front end only.
- How an order is paid, and how items are held for an unpaid order.
- The email sender, and the rate limit on order endpoints.
- How the project is run for the lecturer; the deployed site via Docker comes later.

## 11. Not checked yet

- Setting the policy header in MVC. Microsoft's Blazor pages show it as a middleware that adds the header; using the same in MVC is our inference.
- Whether the default MVC template and the framework's validation scripts are free of inline scripts and styles under our policy. The docs do not say; the scaffold will show it in the browser console.
- How to enable PostgreSQL's `unaccent` for accent-free search. The Npgsql docs map `EF.Functions.Unaccent`, but the docs I read do not show how to enable the extension.
- Whether Fedora 45 and the Linux Docker image behave (ICU is needed for culture data; we have no `vi` culture to register now, but number and date formats still use ICU).
- That the existing front-end tests port as expected (the data-shape and CSS-colour checks stay; pricing becomes C# tests; the language-key check goes away).
