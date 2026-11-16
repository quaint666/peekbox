# Peekbox

A web app for building a gift hamper for Tết, 20/11 (Teachers' Day) and 8/3 (Women's Day).

The customer sets a budget first, then picks items. Every item shows its brand, weight, origin and expiry date, and the total is calculated by the server. An item that would expire before the delivery date cannot be ordered.

**Status:** the design, written content and images are done, and the base of the app is built (milestone M0, in progress): one ASP.NET Core MVC app with Razor views and PostgreSQL, in Vietnamese only, with the 24 sample items in the database.

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
- [`public/images/`](public/images/): product photos, packaging, hero and the logo.
- [`CONTRIBUTING.md`](CONTRIBUTING.md): how to propose a change (no installs needed).

## Run it

The site and the tests need a PostgreSQL 18 server. `docs/frontend-plan.md`, section 2, lists what to install and how to set PostgreSQL up without Docker. With Docker, from the project folder:

```
docker run -d --name peekbox-pg -p 127.0.0.1:5434:5432 -e POSTGRES_USER=peekbox -e POSTGRES_PASSWORD=dev -e POSTGRES_DB=peekbox postgres:18   # once; after a restart: docker start peekbox-pg
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5434;Database=peekbox;Username=peekbox;Password=dev" --project src/Peekbox.Web   # once
dotnet tool restore                                    # once: the EF Core command line tool
(cd src/Peekbox.Web && npm install && npm run build)   # compiles the TypeScript to wwwroot/js
dotnet ef database update --project src/Peekbox.Web    # creates the tables and the 24 sample items; again after a pull that changes the database
dotnet test                                            # builds everything, then runs the checks
dotnet run --project src/Peekbox.Web                   # starts the site; the address is printed
```

Run `npm run build` in `src/Peekbox.Web` after you change a `.ts` file (or `npm run watch` to do it on every save).

## License

[MIT](LICENSE).
