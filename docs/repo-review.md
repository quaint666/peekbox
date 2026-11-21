# Full repository review (prompt)

A prompt for a whole-repository review of the front end, written after the first one (2026-11-17, end of M3) so the next
one checks the same things the same way. "Front end" here includes the server code the pages need (controllers,
services, the order saved in the database). Run it at the end of the front end, and again before the first deployment.
Paste everything below the line to the assistant, in a session started in this repository.

**Not covered yet, because it is not built:** the admin (M6: login, the 2-step code, orders and stock screens), real
payment (the ZaloPay callback and its signature check), sending email (F61), and deployment beyond the four items
below. When one of them is built, add its own area to this prompt before the review that includes it. Likely first
entries: for the admin, sign-in, roles and the 2-step code; for payment, the signature check and a callback that
arrives twice; for stock and orders, two orders racing for the last item and a repeated order; for the database,
indexes and migration review once the catalogue and the orders grow.

---

Review the whole Peekbox repository: the code, the tests, the docs, the content pack and the mockups. Do not only look
for syntax errors. The goal is a clean base for the next phase and habits worth keeping. Report first; change nothing
until the owner approves. Then fix in small batches, one commit each, each shown as a diff before it is committed.

## What to look for

**The owner's list**
- Dangling or unneeded things: unused CSS classes, files nothing references, packages nothing uses, tests or exemptions
  for code that is gone, local leftovers outside git.
- Readability, scalability and maintainability: files that do too much, names that mislead, comments that no longer
  match the code.
- Long-term browser compatibility: every HTML, CSS and script feature in use, checked against current support data
  (web-features / Baseline, MDN), not from memory, with what happens on a browser that lacks it.
- Implicit paths and implicit behaviour: anything that works by default instead of by intent. Examples found before: a
  form without an `action` (it posts to the current address), a catch-all route that gave every page a second address, a
  value that fails to bind and silently becomes 0, a fetch with no timeout, an error page that answers 200 when opened
  directly.
- Inconsistency between the repository, the docs and the mockups: paths, routes, milestone rows, decisions, and every
  place the built page departs from its mockup without a note in `design/design-notes.md`.
- Anything long or convoluted that reinvents what the framework or the platform already does.
- One source per value: an address, a text, a number or a colour that is written in more than one place, or a pattern
  copied instead of shared (a helper, a partial, a token, a component class).

**Seven areas** (the generic checklist, adapted to this stack: ASP.NET Core MVC, Razor views, plain TypeScript modules,
CSS with tokens; there is no React, no bundler and no Tailwind)
1. **Architecture and design.** Controllers stay thin and the rules live in `Services/` (once). Views compute nothing,
   least of all money. Every address is in `SiteLinks` and every action names its own route. State: the hamper cookie
   and the database only; scripts hold no state that the server does not also have. Folder and naming conventions match
   `docs/architecture.md` section 4.
2. **Performance.** Image sizes against the sizes shown (`srcset` where it pays), lazy loading below the fold only,
   width and height on every image (no layout shift), fonts, fingerprinted static files and their cache headers in
   Production, the number of queries per page (look for a query inside a loop).
3. **Accessibility and semantic HTML.** Landmarks, one `h1`, labels and accessible names. Check names in the
   accessibility tree in both themes: a hidden element's text is not read. Focus after every action that changes the
   page, keyboard use (Tab, Enter, Escape, the arrow keys on menus), contrast in both themes, `prefers-reduced-motion`.
4. **Responsive layout and CSS.** 390, 768, 900, 1024, 1100 and 1440 px, light and dark: horizontal overflow, text that
   wraps where it should not (count text lines with a Range, not element heights), sticky elements. Hard-coded sizes and
   colours outside `tokens.css`, repeated declarations, z-index values, breakpoints.
5. **Error handling and edge cases.** Empty, error, too-many-tries and sold-out states. Timeouts and stale answers in
   every background request (an older answer must not overwrite a newer one, and the server must receive the requests in
   the order the visitor made them). Validation on the server for every value, with the browser's checks as a hint only.
   What a hand-made request can do: a NUL in a cookie, in the query string or in a form; the same request twice at the
   same moment; a method the address does not take (405, and HEAD); a value that cannot be bound, in a form and in an
   address. Run these as Production, where an exception is a status and not an exception thrown into the test.
6. **Security and good practice.** No `Html.Raw` and no `innerHTML`, the content security policy, the anti-forgery
   token on every POST, secret tokens, rate limits, what the logs contain in Production, no `any` and no unchecked type
   assertions in TypeScript, the analyzer and compiler settings in `Directory.Build.props`.
7. **Repository, docs and mockups.** README, `docs/*`, `content/README.md`, `design/README.md` and
   `design/design-notes.md` against what is built; the sitemap against the real routes; `docs/frontend-plan.md` milestone
   rows against the F-numbers actually built.

**Deployment items** (keep listing them until they are fixed): forwarded headers (without them every visitor shares the
proxy's rate-limit bucket and the hamper cookie is not marked Secure), `AllowedHosts`, a health endpoint, the log level.

## How to check (evidence, not memory)

- Search before claiming: every CSS class against the views and scripts, every file in `wwwroot/` and `content/`
  against its users, every package against its use, every repeated literal (`grep -rn`).
- Run the app in Production mode (connection string from an environment variable) and probe it with `curl`: addresses
  the app should not answer, `/error` opened directly, a 404 body, cache headers, whether SQL reaches the log. **Probe the
  published build, not `dotnet run`:** `dotnet publish -c Release -o <scratch>` and `dotnet Peekbox.Web.dll` is the
  Dockerfile's artifact. From the Debug build, a request with `Accept-Encoding: gzip` for any static file got an empty `200`
  (every script failed in the browser), and a wrong method gave 404 where the published build gives 405 (the 404 is explained in the batch 6 note below; the empty gzip I did not trace, but it
  appeared only in the unpublished build). Ask for each important address with the methods GET, HEAD, POST and PUT, and once with `Accept-Encoding: gzip, br`.
- **Test the engines the site is built for, not only Chromium.** Firefox: BiDi on a headed Firefox under `xvfb-run` (a
  headless Firefox answers `(hover: hover)` false, so every hover rule is skipped); the pointer moves with
  `input.performActions`. WebKit: MiniBrowser under WebKitWebDriver with `LIBGL_ALWAYS_SOFTWARE=1` only: with
  `WEBKIT_DISABLE_DMABUF_RENDERER=1` or disabled compositing the web process crashed on a view transition (the site was
  fine). Move the pointer with WebDriver actions. Read the same computed value in all three (the transform of a card, the
  `background-size` of a header link 80 ms and 580 ms after the pointer arrives).
- Support claims come from data: `npm pack web-features` (the package behind Baseline) has the status and the first
  version of every feature; look the feature up, do not recall it.
- Headless Chromium has no hover device, so every `:hover` rule is silently off in a screenshot: start it with
  `--blink-settings=primaryHoverType=2,availableHoverTypes=2,primaryPointerType=4,availablePointerTypes=4` (headless
  Firefox has none either, so hover stays unverified there). A screenshot `clip` is in page coordinates, so on a scrolled
  page leave it out (the viewport is captured). Scroll with `behavior: 'instant'` between steps: a smooth scroll still
  running swallows the next click or jump and looks like a bug.
- A sweep beats a few pages: every page type at 390, 768, 900, 1024, 1100 and 1440 px, both themes, reading overflow,
  `h1` count, duplicate ids, broken images, console, CSP and network errors, and the accessibility tree for links and
  buttons with no name (this found carousel dots that are links with no name: `::scroll-marker` shows up there). Measure a
  fixed size (a header height) against its token; a hand-counted 67 px was 68.
- Browsers: headless Chromium over CDP with a private profile, and WebKit through MiniBrowser and WebKitWebDriver. Take
  screenshots at the widths above in both themes, and record console errors, CSP reports and horizontal overflow. Check
  accessible names with `Accessibility.getPartialAXTree`. Use `Fetch.enable` to hold a request: that is how a hung
  server and out-of-order answers are tested. Firefox the same way through WebDriver BiDi, its built-in remote
  protocol (no geckodriver needed): open each page with `browsingContext.create`, or focus and Space do not work.
- For every fix, a test that fails without it: break the code on purpose and confirm a check fails. A break that no
  test catches is a finding of its own. Check that the break is the one you meant: `false && A || B` still runs `B`, and
  a test that passes with the fix removed was not testing it (a race needs eight requests at once, not two). Never stop a
  process by name (`pkill -f` also matches the shell that runs it, and other people's windows): stop the PIDs you started.
- Mark every claim `[verified]` (seen in real output in this session), `[inferred]` (reasoned, not seen) or
  `[stated]` (the owner said so). Say plainly what was not checked.

## What the report looks like

1. **Summary.** An overall score out of 10 and the top 3 fixes, plus what must happen before any deployment.
2. **Findings by area** (the seven above). For each: the severity (Critical / Warning / Suggestion), the file and
   line, the problem, the evidence, and the corrected code.
3. **Order of work.** Numbered batches, each one commit, smallest risk first: dead code, then bugs, then one source per
   value, then CSS, then docs, then deployment.
4. **Decisions for the owner.** Anything that deletes content, changes a public address, or changes how the project
   is run.
5. **Good habits** the project does not have yet.

## Checked in the first review (2026-11-17): confirm they still hold

- Every action has an attribute route from `SiteLinks`, and `RoutesTests` lists every address exactly once.
- Every form has an explicit `action` and `asp-antiforgery="true"`, and the well-formed check confirms each form posts to
  an address the app answers.
- Values that cannot be bound are refused with a 400 (`ModelState.IsValid`) wherever a default would act.
- The background requests go through `Scripts/lib/page.ts` (10 s timeout); the packaging saves are sent one at a time.
- The logo link carries its own name (`aria-label`); both logo images have `alt=""` and width and height.
- `/error` answers only after an exception; EF's SQL is at Warning in `appsettings.json`.
- One source per value: the hamper cookie (`HamperCookie`), the token check (`SecretToken.IsWellFormed`), `ReturnTo`,
  `Anchors`, `_SupportLine`, one Vietnam "today" in the tests (`TestDates`), one test client (`fixture.Client()`).
- CSS: sizes, layers and colours only from `tokens.css`, `.meta` and `.alert` shared, every CSS class used (`SeamsTests`).
- Docs: README, sitemap, architecture, frontend plan, API contract, content and design READMEs match what is built;
  the mockup departures are listed in `design/design-notes.md`.
- Still open after the first review: the deployment items, browser compatibility of the Popover API ("Xóa hộp", the
  review summary) checked against current data, and the M3 pages in Firefox (through WebDriver BiDi). The second review
  closed the last two (below).

## Found in the second review (2026-11-21): confirm they stay fixed

Run on 2026-11-21 by the assistant with the owner, on the code after PR #73. Each fix has a test; its name or file is
in brackets. Score 8/10: the base was sound, and the defects were in what a hand-made request can do.

- A hamper cookie of `a%00b`, or `?q=%00`, made Postgres refuse the query and every page a 500. A token is now accepted
  only if written as `SecretToken.New` writes it, and a control character in the search is a space
  (`HandMadeRequestTests`).
- Two requests that add (or remove) the same item at the same moment gave a 500 for the second: the key of
  `HamperItems`, and a delete of a row already gone. `SetQuantityAsync` looks again from the database (`Several_adds…`,
  `Several_removals…`).
- A value that cannot be bound was a default in some actions (`/builder/packaging next=abc`, `/builder?inDate=abc`) and a
  400 in others. One global filter, `RefuseUnboundValuesFilter`, refuses it everywhere (`FrameworkDefaultsTests`).
- `HEAD /healthz` was a 405, which an uptime monitor reads as down.
- CodeQL's three "URL redirection from remote source" alerts were false positives (every address is built from
  `SiteLinks`), but `LocalRedirect` makes the framework itself refuse a non-local address; all 21 redirects use it
  (`Controllers_redirect_with_LocalRedirect_never_with_Redirect`).
- No `nosniff`, no `Permissions-Policy`, a `Server: Kestrel` header, and no cache policy for HTML (every page shows the
  visitor's cart count). Now `SecurityHeaderTests`.
- A 400 was a blank page, and the Data Protection keys lived in a folder of the container, so a deploy made every open
  form fail with that blank 400. The page is friendly (`NotFoundPageTests`) and the keys are in the database
  (`DataProtectionTests`).
- The sticky header and the occasion's budgets bar: `--site-header-height` was 67 px but is 68 (a 1 px line), the chip row
  overflowed the page at 900 px, and the old `scroll-padding-top: 96px` was a guess. `StickyHeaderTests` fails if a bar that
  sticks to the top does not start below the header.
- Built after the review: the carousel dots are named by their occasion and the stand-alone links are 24 px (#78); the
  request map is in `docs/api-contract.md` and `RequestMapTests` keeps it equal to `RoutesTests.Expected`; a POST, PUT,
  DELETE, PATCH, GET or HEAD to an address that does not take it is a 405. Why the test host first said 404 to a GET: an
  unpublished app (the test host, `dotnet run`, `bin/`) has a build manifest, and then `MapStaticAssets` adds a GET/HEAD fallback
  `{**path:file}` (`StaticAssetDevelopmentRuntimeHandler`). The routing method policy sees a GET candidate, so it never
  answers 405, and the `:file` constraint then rejects the address: a 404. `dotnet publish` writes a manifest that is not a
  build manifest, so the image has no fallback. `PeekboxFixture` sets `DisableStaticAssetNotFoundRuntimeFallback` so the
  tests answer like the image; the image smoke step in `images.yml` sends a GET to `/cart/add` and also checks `HEAD /healthz`
  and a gzip'd stylesheet that inflates to the real file).
- Done after batch 7: the occasion "8/3 và 20/10" has one name and one chip on the home page, the header and its own page
  (`HomeContentTests` compares `home.json` and `hampers.json`); the workflow actions are pinned to a commit with the tag in
  a comment (`WorkflowTests`; an update is now a deliberate edit of the SHA, there is no Dependabot).
- Browser scripts now have tests (`src/Peekbox.Web/ScriptTests`, vitest 5 with jsdom 31, `npm test`, a step in `test.yml`):
  the builder's live search (the 250 ms wait, Enter, composing, a newer search cancelling an older one, a failed fetch), the
  background add (one at a time, the plain fallback), the checkout fee swap (a stale answer, a redirect), the header (theme,
  phone menu, Escape), the occasion budget chip, the hamper bar, the rows' arrows and `fetchPage`. Every test was checked by
  breaking the script on purpose. jsdom has no layout and no real hover: sticky bars, scroll positions and CSS stay checked
  in real browsers (see the method notes above). A test file must take the page's listeners off before the next one loads
  (`support.ts` does), or a click would also run the scripts of earlier tests.
- Left, decided and not yet built: hamper rows are never purged (see the decision notes).
- Left on purpose: `/about`, `/blog` and `/legal` were linked from the header and footer before they were built and were 404 until M4 (the blog
  came in #74, About and legal in #77); on a phone the sticky header, the builder's steps and the bottom bar leave about 60 % of the screen for the list.
- Browser floor (decision 2026-11-21): the Popover API is Baseline "newly available" since 2025-01-27 and "widely
  available" about 2027-07 (web-features 3.47.0), and the confirm popovers, panels and the toast depend on it with no
  fallback. The floor is Chrome 116, Firefox 125 and Safari 17. `:has()`, `color-mix()`, `text-wrap: balance`,
  `AbortSignal.any` and `AbortSignal.timeout` are widely available; `position-area` and `scroll-marker-group` are behind
  `@supports`; cross-document view transitions are not in Firefox and are not used.
- Checked clean: no sideways overflow, one `h1` per page, no duplicate ids, no broken images and no console, CSP or network
  errors on 11 page types at 6 widths in both themes in Chromium; the same 10 pages at 3 widths in Firefox 160 (light)
  and WebKit 2.54 (light); no vulnerable NuGet or npm package; no SQL or personal data in the Production log.
