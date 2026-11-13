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
- **Reading and writing data:** controllers take the `PeekboxDbContext` directly (no repository classes on top) and use LINQ, which EF Core turns into SQL. Raw SQL is the exception, used only where we can name the need: a query LINQ cannot express, a measured slow query, or a write that must be one atomic step (for example reducing stock without overselling). Use the interpolated `FromSql($"...")` and `ExecuteSql($"...")`, where each `{value}` becomes a parameter; never build the SQL text by joining strings. Raw SQL names tables and columns as EF created them (quoted, for example `"Products"."PriceVnd"`), and a rename in C# will not update it, so each one is a short, commented line in the pull request that adds it. For a result that is not an entity (a report with totals, say) use `Database.SqlQuery<T>($"...")`, which maps raw SQL onto a plain record and parameterises each `{value}` the same way. We do not add Dapper: EF's own calls cover it.
- Text fields are Vietnamese only. The `_en` fields in `content/data/` are not mapped.
- **The tool** is `dotnet ef`, listed in `dotnet-tools.json`; `dotnet tool restore` installs it.
- **The connection string** is the secret `ConnectionStrings:Default`: user secrets in development, the environment elsewhere (written `ConnectionStrings__Default`). It is never in a file of the project.
- **Tests** create a temporary database on that server, fill it with the real migrations and seed, and drop it at the end.

### How EF Core and PostgreSQL talk

EF Core does not hold the data or enforce the rules. It writes SQL at three moments, and PostgreSQL does the rest over an ordinary connection (the Npgsql driver, the counterpart of Node's `pg`):

```
 classes + OnModelCreating  (the "model": what the schema should be)
      │  dotnet ef migrations add: model vs snapshot file (no database needed)
      ▼
 migration file ──► dotnet ef database update ──► SQL: ALTER TABLE ...         (migration time)
 LINQ query     ──► EF translates the expression ──► SELECT ...                (query time)
 changed object ──► SaveChanges diffs the tracked objects ──► INSERT / UPDATE  (save time)
      │
      ▼
 Npgsql (a pooled connection: SQL text + parameters) ──► PostgreSQL runs it and enforces
                                                          NOT NULL, UNIQUE, foreign keys, CHECK
```

Checked on a scratch app (PostgreSQL 18, EF Core 10.0.15): a LINQ lambda reaches EF as data (an expression), and `ToQueryString()` shows the SQL without running it. Constants stay inline in the SQL while variables become parameters. Adding an optional property to a class produced `ADD COLUMN ... nullable` with no database present. Every rule violation we sent (price 0, stock -1, a duplicate slug, a missing category, a 300-character name, a null by raw SQL) was rejected by PostgreSQL with its own error code, not by EF. Two `SaveChanges` calls that each read a stock of 5 and wrote 4 lost an update (5 → 4, not 3), which is why a stock change is an atomic `ExecuteUpdate` or uses a concurrency token.

## 6. Money and expiry

- Price, budget and safe-expiry rules live in **one** service. Nothing else calculates a price.
- `tests/vectors/pricing.json` holds the shared cases. The C# tests load it and must pass every case.
- Prices are whole dong, tax included.

## 7. The hamper, forms and orders

- No accounts. A visitor gets a long-lived cookie with a random token, and the unfinished hamper is a row in PostgreSQL under that token. It survives a restart.
- We do **not** use the framework's session for this. This is the one place we differ from the framework's own tool: its default idle timeout is 20 minutes and its in-memory store is lost when the server restarts.
- An order is identified publicly by an unguessable token, never a counting number. Placing an order is safe to repeat (the Back button must not create a second one).
- Forms carry an anti-forgery token (the form tag helper adds it). The server checks the token only on an action marked `[ValidateAntiForgeryToken]`, or on every unsafe request if a global filter is added; the filter is not set yet (see section 11).
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

## 11. Checked, and not checked yet

### Checked on 2026-11-13 (scratch apps, PostgreSQL 18, .NET SDK 10.0.113 on Fedora 45)

- **The policy header in MVC** is a middleware that adds it to every response, and `SecurityHeaderTests` check it on a page, on a 404 and on a static file. The build, the tests and the app all run on Fedora 45 with SDK 10.0.113.
- **The framework's browser-side validation scripts under our policy.** With jQuery 3.7.1, jquery-validation 1.22.1 and jquery-validation-unobtrusive 4.0.0, served from our own site under the strict policy, headless Chromium showed the Vietnamese messages on an invalid form with 0 policy violations and 0 script errors. With jQuery 4.0.0 the same scripts did not validate (one script error, not examined), so use jQuery 3.x if we add them. The `data-val-*` markup in the probe was written by hand, not produced by a tag helper, so a real form is still to be tried.
- **Accent-free search (`unaccent`).** `modelBuilder.HasPostgresExtension("unaccent")` produces a migration that runs `CREATE EXTENSION IF NOT EXISTS unaccent;` (the extension ships with the official PostgreSQL image, no extra install). `EF.Functions.ILike(EF.Functions.Unaccent(p.Name), EF.Functions.Unaccent(pattern))` found "Bánh…" for `banh` and `BANH`, and "Cà phê…" for `ca phe`; `unaccent` also folds `đ`, `ơ`, `ư`, `ă`, `â`, `ê` and `ô`. The SQL is `unaccent(p."Name") ILIKE unaccent(@pattern)`, which cannot use an ordinary index: fine for 24 items.
- **Culture and time zone in a Linux container.** On Fedora `vi-VN` works (`1.234.567 ₫`, "Thứ Sáu, 13 tháng 11, 2026"). In `mcr.microsoft.com/dotnet/runtime:10.0-alpine` the image sets `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true`, so `new CultureInfo("vi-VN")` throws and `Asia/Ho_Chi_Minh` is not found; switching invariant mode off without ICU aborts the process. Installing `icu-libs` and `tzdata` makes the culture exist but with English data (`₫1,234,567` and an English date), a silent error; adding `icu-data-full` gives the right output and the time zone (UTC+7). Any Alpine-based image of ours needs `icu-libs`, `icu-data-full` and `tzdata`, and invariant mode switched off.
- **Reverse proxy headers.** With the client on loopback sending `X-Forwarded-For: 1.2.3.4, 5.6.7.8`, `X-Forwarded-Proto: https` and `X-Forwarded-Host: shop.example.vn`:
  - no middleware: the app sees `127.0.0.1` and `http`, and ignores the headers;
  - `UseForwardedHeaders` with `ForwardedHeaders = XForwardedFor | XForwardedProto`: `5.6.7.8` (the rightmost entry) and `https`;
  - `ForwardLimit = 2` alone: still `5.6.7.8`, because the next hop is read only if the previous one is a trusted proxy; with `5.6.7.8` added to the trusted proxies it gives `1.2.3.4`. So Bloomy's `trust proxy 2` needs both the limit and the trusted proxy addresses;
  - a caller that is not a trusted proxy: the headers are ignored;
  - `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` with no code: applied;
  - the host from `X-Forwarded-Host` is used only when `XForwardedHost` is enabled.
  In .NET 10 `ForwardedHeadersOptions.KnownNetworks` is obsolete (ASPDEPR005); use `KnownIPNetworks`. Nothing is configured in Peekbox yet.
- **Razor views and analyzers.** Views are compiled under the same rules as C#: in a `.cshtml`, an un-awaited call (CS4014) and a null dereference (CS8602) were build errors and an unused variable (CS0219) a warning. Of four ASP.NET analyzer probes only ASP0019 (`Headers.Add` instead of `Append`) fired, as a warning; ASP0000, ASP0001 and ASP0015 did not fire for our code shapes. CA2254 can be promoted to an error (it caught an interpolated log message); none of these is in the list in section 12.
- **Logging in production.** The `Default: Information` level in `appsettings.json` also prints EF's SQL when the environment is Production (the home page's `SELECT count(*)` appeared).
- **The front-end tests port.** The data-shape checks (unique slugs, known category, whole-dong price, image paths, seed equals the file) and the CSS-colour check are C# tests already; the language-key check went away with the English text; `tests/vectors/pricing.json` waits for the pricing code (M1).
- **Defaults seen but not set in Peekbox** (to be decided with the front end, not before): (1) anti-forgery is checked only where asked; a global `AutoValidateAntiforgeryTokenAttribute` filter returns 400 for any POST without a token and `[IgnoreAntiforgeryToken]` exempts a callback such as a payment webhook. (2) Dependency-injection scope checks run only in Development; `UseDefaultServiceProvider(o => { o.ValidateScopes = true; o.ValidateOnBuild = true; })` makes Production refuse to start on the same mistake instead of quietly sharing one object between requests. (3) In a plain `Controller` a failed model binding still runs the action (`id` is 0 and `ModelState.IsValid` is false); check `ModelState.IsValid`, constrain the route (`{id:int}` gives a 404), or use `[ApiController]` on a JSON controller (400 automatically). (4) EF refuses to apply a migration when the model has changes that no migration covers (`PendingModelChangesWarning` is an error in EF 10).

### Not checked yet

- A real form produced by the tag helpers, with the validation scripts and the policy, in a browser.
- The Arch build (the Arch packages are runtime 10.0.14, our EF packages are pinned at 10.0.15): for the teammate to confirm on Arch.
- A Docker image for Peekbox (base image names, pinning, health check), and the production setup of forwarded headers: both belong to deployment, which comes later.
- A normal index for accent-free search once the catalogue is large (`unaccent` is not an immutable function, so it needs a wrapper before it can be indexed): not tried.

## 12. Writing C#: the compiler and habits

The compiler and the analyzers are there so that we do not meet surprises later. `Directory.Build.props` turns the warnings that mean wrong behaviour into build errors (a possible null, a call not awaited, a blocking call inside async code, a switch that misses a case, code that cannot run, SQL built from strings, and the others listed there). Every other warning stays a warning, and we keep the build at zero warnings, so a new one is always about the last change.

- **Read each warning as a code review and fix its cause.** If a warning must stay, suppress it narrowly: a `#pragma warning disable` for that one ID, with the reason on the line above.
- **Obsolete APIs** (`CS0618`, `SYSLIB####`): a security-related one is fixed at once; the others are migrated on our schedule, usually in the pull request that upgrades the package.
- **Null:** make a property `required`, or use `?? throw`. The `!` operator needs a written reason. In a `switch` over an enum, avoid a catch-all `_` arm, so that a new member is flagged.
- **async all the way:** `await` every call, never `.Result`, `.Wait()` or `Thread.Sleep`. With EF always use the `...Async` methods (`ToListAsync`, `CountAsync`, `FirstOrDefaultAsync`). No rule catches a synchronous EF call inside an async method, so this one is a habit.
- **CancellationToken:** a controller action takes a `CancellationToken` and passes it to EF's async calls.
- **Not found is normal:** use `FirstOrDefaultAsync` and answer `NotFound()` for a null, never `First` and a catch.
- **Rethrow with `throw;`**, never `throw ex;`.
- **Order tokens and anything secret** come from `RandomNumberGenerator`, not `Random`. Do not use `MD5` or `SHA1` for security.
- **Logging** uses named placeholders (`log.LogInformation("Order {Id}", id)`), not interpolated strings.
- **Records** for data we pass on and do not change (view models); **classes** for EF entities; **`sealed`** on types not meant to be inherited.
