# Front-end plan

For the teammate who builds the pages, and for the people who review them. How the whole app fits together is in `docs/architecture.md`; read it once.

## 1. What we are building

The Peekbox website. It is one ASP.NET Core MVC app, and each page is a **Razor view** (HTML with a little C#), styled with plain CSS, with a little TypeScript for small behaviours in the browser. The site is **Vietnamese only**.

- **Tools:** Razor views, plain CSS, strict TypeScript. **Do not add libraries** (including CSS or JavaScript ones) without asking.
- **Look:** the mockups in `design/mockups/` (start with `customer/`). Read `design/design-notes.md` for colours and rules.
- **What each page must do:** `docs/functions.md` (the F-numbers), `docs/use-cases.md`, `docs/sitemap.md`.
- **Decisions already made:** `docs/decisions.md`. If a mockup and that file disagree, ask.

**Who does what.** You build the pages, one small slice at a time. When a page needs data or a rule, a small piece of C# (a controller action, a service) goes in the same pull request, and the owner and the assistant help with it. The base (milestone M0: the project, layout, styles, tests) is written by the owner and the assistant first. The review is there to catch mistakes: nobody expects perfect code.

## 2. Setting up (once)

Install these (you use Linux):

1. **Git.**
2. **The .NET 10 SDK and the ASP.NET Core runtime.** On Arch Linux: `sudo pacman -S dotnet-sdk-10.0 aspnet-runtime-10.0` (these are the package names in the official repositories; the exact set is confirmed when M0 is merged, so tell us if a build complains about something missing).
3. **Node 22 or newer** (it builds the TypeScript).
4. **PostgreSQL**, from your distribution's packages (Arch currently ships version 18). We use the same major version when we deploy. Docker is only for the final deployment, not for working.

Then clone the project: `git clone https://github.com/quaint666/peekbox.git`.

Then, from the project folder, once:

```
(cd src/Peekbox.Web && npm install && npm run build)   # compiles the TypeScript to wwwroot/js
dotnet tool restore                                    # installs the EF Core command line tool the project lists
```

**The database.** The site and the tests need a PostgreSQL server (start it first; for Arch, the Arch Wiki page "PostgreSQL" explains the one-time setup) and a login that can create databases: the tests make and drop their own temporary database on the same server, so your own data is never touched. Then:

```
sudo -u postgres createuser --createdb --pwprompt peekbox   # you choose a password
sudo -u postgres createdb --owner peekbox peekbox
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Database=peekbox;Username=peekbox;Password=YOUR_PASSWORD" --project src/Peekbox.Web
dotnet ef database update --project src/Peekbox.Web         # creates the tables and loads the 24 sample items
```

The password stays on your computer (user secrets) and never goes into a file of the project. With Docker you can replace the first two lines with one command that starts a ready server (use `Port=5434;Password=dev` in the connection string):

```
docker run -d --name peekbox-pg -p 127.0.0.1:5434:5432 -e POSTGRES_USER=peekbox -e POSTGRES_PASSWORD=dev -e POSTGRES_DB=peekbox postgres:18
```

Now you can run the checks and the site:

```
dotnet test                                            # builds everything, then runs the checks
dotnet run --project src/Peekbox.Web                   # starts the site; the address is printed
```

Run `npm run build` again after you change a `.ts` file, or run `npm run watch` in `src/Peekbox.Web` to do it on every save. When someone changes the database model, run `dotnet ef database update --project src/Peekbox.Web` again.

## 3. How the project is organised

The folder layout is in `docs/architecture.md`, section 4. In short, **you work in**, inside `src/Peekbox.Web/`:

- `Views/`: one folder per area, one `.cshtml` file per page. The header and footer are in `Views/Shared/`.
- `wwwroot/css/`: the styles. `tokens.css` has all the design values.
- `Scripts/`: the TypeScript source, compiled to `wwwroot/js/` by `npm run build`.

**You do not edit:** the compiled JavaScript, the database `Migrations/` folder by hand, or `tokens.css` values without asking. The `Controllers/`, `Services/` and `Data/` folders are shared: change them in small steps with the owner and the assistant.

## 4. The rules

Each rule exists because breaking it cost us time in an earlier project (Bloomy), or because the framework works that way. The tests check several of them for you.

1. **No inline styles, no inline scripts, no `onclick="..."` or `onchange="..."`.** The server sends a security policy that blocks them. Use CSS classes and `addEventListener` in a script file. If the console says "Content Security Policy", this is why. Do not weaken the policy.
2. **Never put text from data or from a visitor into HTML by hand.** In views, write `@Model.Name` (Razor encodes it) and never use `Html.Raw` or `HtmlString`. In TypeScript, never use `innerHTML`: use `textContent`. Never put such text inside JavaScript or into a URL path.
3. **One place for each value.** A number, a colour, a name or a piece of text is written once: in a service, a constant, `tokens.css`, a shared partial or the data. If you type the same thing twice, stop and ask.
4. **Shared things are built once.** Header, footer and buttons are in `Views/Shared/`. Never copy them into a page. A style used by two pages goes in `components.css`; a style for one page goes in that page's CSS file.
5. **Money maths only in the pricing service (C#).** Views and TypeScript never calculate a price, total or budget: they show what the server gives.
6. **Data only through controllers and services.** A view never reads the database; it only draws the view model it was given.
7. **To hide something, use the `hidden` attribute.** The rule in `base.css` makes it really hide. Do not remove that rule.
8. **Colours and sizes come from `tokens.css` variables**, for example `var(--color-text)`. No new hex colours in other files.
9. **Vietnamese only.** Text is written in the views and shared partials. Text used in more than one place lives in one shared place.
10. **Check every page at 1440 px and 390 px wide, in light and dark.**
11. **Do not animate from invisible.** Start at 40% opacity or more, never 0.
12. **Orders: once placed, clear the hamper and disable the button.** The Back button must never place a second order; the server checks this too.
13. **One scroll offset.** `base.css` already has it for `#anchors`. Do not add another.
14. **Small steps.** One pull request, one purpose, about 300 lines or fewer.
15. **Say what you did not test.** In every pull request, honestly.
16. **Use the framework's own way first** (tag helpers, model binding, data annotations). If you are not sure, ask before inventing your own.

## 5. How to build a page

The steps are always:

1. Add an action to the area's controller and a view model with exactly what the page needs.
2. Add the view in `Views/<Area>/`, using the shared layout.
3. Put the page's CSS in its own file under `wwwroot/css/`.
4. Add TypeScript only if the page needs behaviour in the browser.
5. Open the matching mockup next to your page and compare.
6. Run the tests. Fix what fails.

## 6. Milestones

One milestone is several small pull requests. "Mockup" paths are inside `design/mockups/`.

| M | What | Functions | Mockups |
|---|---|---|---|
| **M0** | Base, written by the owner and the assistant first: the ASP.NET project, layout with header and footer, the styles, a Home placeholder reading the database, the security-policy header, tests | F01, F03, F04 | `foundations/` |
| **M1** | Home; Builder core: budget, delivery date, search and sort, item cards, add/remove, budget bar, expiry check, sold out, saved hamper | F11, F12, F14, F16, F18 (chips), F20–F25 | `customer/01-home`, `customer/02-builder` |
| **M2** | Item page; Packaging and review; Cart | F26–F28, F33–F37, F39, F40 | `customer/03`, `04`, `05` |
| **M3** | Checkout and Confirmation. The order is real: saved in the database. Payment is a stand-in | F41, F44, F46–F48, F51 | `customer/06`, `07`, `states/02` |
| **M4** | About, Blog, Blog post, Legal, 404 | F09, F52–F55 | `customer/08` to `11`, `states/05` |
| **M5** | Extras: starter hampers, campaign banner, contact button, share, toast, back-to-top, resume banner, coupon, time windows, hide prices, loading skeletons, payment-failed page | F05–F08, F10, F13, F15, F17, F19, F29–F32, F38, F42, F43, F45, F49, F50 | `states/`, `dark/` |
| **M6** | Admin. **Not in the first report.** Built after the front end is done | F56–F59 | `admin/`, `states/14` to `18` |
| **M7** | The report: screenshots and code excerpts of the pages | — | — |

F02 (language switch) was dropped. Live search suggestions and the live budget bar need browser scripts and come as their own slices after the plain versions work.

**Done means:** the tests pass; the page matches the mockup at 1440 and 390 px in both themes (screenshots in the pull request); it works with the keyboard; the browser console has no errors; and the pull request lists the F-numbers it covers.

## 7. Pull requests and review

1. Make a branch for your slice, commit in small steps, push, and open a pull request. Use the front-end template (`.github/PULL_REQUEST_TEMPLATE/frontend.md`; GitHub offers it when you add `?template=frontend.md` to the new pull request address).
2. Put screenshots next to the mockup: 1440 px and 390 px.
3. The reviewers read the code and run it. They will tell you what to fix, as a list. Fix it on the same branch; the pull request updates by itself.
4. When the owner approves, it is merged as one commit.

**What a reviewer checks:** the build and tests pass; the rules in section 4; no `Html.Raw`, no price maths in a view or in TypeScript; a hard-coded value or colour; text typed twice; the keyboard (Tab, Enter, Escape); the page in both themes and both widths; what the author said they did not test; and that nothing was added that is not in `docs/functions.md`.

## 8. When something goes wrong

| You see | It usually means |
|---|---|
| "Content Security Policy" in the console | An inline style, script or event attribute was used. Use a class and `addEventListener` |
| "Element not found: #something" | The view does not contain that id, or the script runs on the wrong page |
| The site stops at start with "connection string 'Default' is missing", or the tests cannot connect | PostgreSQL is not running, or the user secret from section 2 was not set |
| A style or script change does not show | Hard reload (Ctrl+Shift+R); make sure the TypeScript was rebuilt |

This table grows once the project exists. Ask early: a 5-minute question is cheaper than a day in the wrong direction.

## 9. For the report

The first report covers the front end only. While you build, keep a screenshot of each function working (name the file by its F-number), and the view or script that does the work. The report needs, for each F-number: what it does, a screenshot of it running, and the code excerpt that handles it with a short explanation.

## 10. Not in scope for now

Real payments (ZaloPay), sending email, the real admin (M6, later), customer accounts, and changing the framework. If one of these starts to look useful, raise it first: it is a decision, not a task.
