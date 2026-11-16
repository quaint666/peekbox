# Full repository review (prompt)

A prompt for a whole-repository review, written after the first one (2026-11-17, end of M3) so the next one checks the
same things the same way. Run it at the end of the front end, before the backend work starts, and again before the first
deployment. Paste everything below the line to the assistant, in a session started in this repository.

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
   What a hand-made request can do.
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
  the app should not answer, `/error` opened directly, a 404 body, cache headers, whether SQL reaches the log.
- Browsers: headless Chromium over CDP with a private profile, and WebKit through MiniBrowser and WebKitWebDriver. Take
  screenshots at the widths above in both themes, and record console errors, CSP reports and horizontal overflow. Check
  accessible names with `Accessibility.getPartialAXTree`. Use `Fetch.enable` to hold a request: that is how a hung
  server and out-of-order answers are tested. Firefox only when geckodriver is installed; otherwise say it is
  unchecked.
- For every fix, a test that fails without it: break the code on purpose and confirm a check fails. A break that no
  test catches is a finding of its own.
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
- Still open after the first review: the one-source batch (the hamper lookup written 14 times, the token check,
  `ReturnTo`, the support line, one Vietnam "today"), the CSS batch (`.alert`, `.meta`, the type-scale, backdrop,
  on-danger and z-index tokens), the docs batch, and the deployment items.
