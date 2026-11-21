# Peekbox

A web app for building a gift hamper for Tết, Valentine, 8/3 (Women's Day), Trung thu (Mid-Autumn), 20/11 (Teachers' Day) and Giáng sinh (Christmas).

The customer sets a budget first, then picks items. Every item shows its brand, weight, origin and expiry date, and the total is calculated by the server. An item that would expire before the delivery date cannot be ordered.

**Status:** the design, written content and images are done. Built so far (milestones M0 to M4, plus the occasion pages and ready hampers brought forward from M5): home, the occasion pages with ready hampers, the builder with item pages, packaging and review, the cart, checkout with a real order in the database, the order page and order tracking, About and commitments, the blog, and terms and privacy. One ASP.NET Core MVC app with Razor views and PostgreSQL, in Vietnamese only, with the 125 sample items. Next: the extras (M5). `docs/frontend-plan.md`, section 6, has the milestones.

## Where to look

- [`docs/project-pipeline.md`](docs/project-pipeline.md): how we build, step by step.
- [`docs/decisions.md`](docs/decisions.md): what we decided and why.
- [`docs/functions.md`](docs/functions.md), [`docs/sitemap.md`](docs/sitemap.md), [`docs/use-cases.md`](docs/use-cases.md): what the site does, its pages, and who does what with it.
- [`docs/architecture.md`](docs/architecture.md): how the app is put together.
- [`docs/frontend-plan.md`](docs/frontend-plan.md): how the pages are built and reviewed.
- [`docs/api-contract.md`](docs/api-contract.md): the shared rules and the few JSON endpoints.
- [`docs/repo-review.md`](docs/repo-review.md): the prompt for a full repository review, run at the end of each phase.
- [`design/`](design/README.md): the screen mockups and design notes. Start with `design/mockups/customer/`.
- [`content/`](content/README.md): blog posts, terms and privacy text, and sample product data.
- [`src/Peekbox.Web/wwwroot/images/`](src/Peekbox.Web/wwwroot/images/): product photos, packaging, the hero, the blog pictures and the logo.
- [`CONTRIBUTING.md`](CONTRIBUTING.md): how to propose a change (no installs needed).

## Run it

The site and the tests need a PostgreSQL 18 server. `docs/frontend-plan.md`, section 2, lists what to install and how to set PostgreSQL up without Docker. With Docker, from the project folder:

```
docker run -d --name peekbox-pg -p 127.0.0.1:5434:5432 -e POSTGRES_USER=peekbox -e POSTGRES_PASSWORD=dev -e POSTGRES_DB=peekbox postgres:18   # once; after a restart: docker start peekbox-pg
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5434;Database=peekbox;Username=peekbox;Password=dev" --project src/Peekbox.Web   # once
dotnet tool restore                                    # once: the EF Core command line tool
(cd src/Peekbox.Web && npm install && npm run build)   # compiles the TypeScript to wwwroot/js
dotnet ef database update --project src/Peekbox.Web    # creates the tables and the 125 sample items; again after a pull that changes the database (the seed fills only empty tables, so new sample content needs `dotnet ef database drop` first)
# A database made before 2026-11-17 needs "dotnet ef database drop --project src/Peekbox.Web" once first: the migrations were squashed into one.
dotnet test                                            # builds everything, then runs the checks
(cd src/Peekbox.Web && npm test)                       # the tests of the browser scripts (vitest and jsdom)
dotnet run --project src/Peekbox.Web                   # starts the site; the address is printed
```

Run `npm run build` in `src/Peekbox.Web` after you change a `.ts` file (or `npm run watch` to do it on every save).

## License

[MIT](LICENSE).
