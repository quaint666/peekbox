# Peekbox

A web app for building a gift hamper for Tết, 20/11 (Teachers' Day) and 8/3 (Women's Day).

The customer sets a budget first, then picks items. Every item shows its brand, weight, origin and expiry date, and the total is calculated by the server. An item that would expire before the delivery date cannot be ordered.

**Status:** the design, written content and images are done, and the app is being set up (milestone M0). It is one ASP.NET Core MVC app with Razor views and PostgreSQL, in Vietnamese only. The files `src/`, `public/`, `scripts/`, `tests/*.mjs` and `package.json` are an earlier static scaffold that M0 replaces or reworks.

## Where to look

- [`docs/project-pipeline.md`](docs/project-pipeline.md): how we build, step by step.
- [`docs/decisions.md`](docs/decisions.md): what we decided and why.
- [`docs/functions.md`](docs/functions.md), [`docs/sitemap.md`](docs/sitemap.md), [`docs/use-cases.md`](docs/use-cases.md): what the site does, its pages, and who does what with it.
- [`docs/architecture.md`](docs/architecture.md): how the app is put together.
- [`docs/frontend-plan.md`](docs/frontend-plan.md): how the pages are built and reviewed.
- [`docs/api-contract.md`](docs/api-contract.md): the shared rules and the few JSON endpoints.
- [`design/`](design/README.md): the screen mockups and design notes. Start with `design/mockups/customer/`.
- [`content/`](content/README.md): blog posts, terms and privacy text, and sample product data.
- [`public/images/`](public/images/): product photos, packaging, hero and the logo.
- [`CONTRIBUTING.md`](CONTRIBUTING.md): how to propose a change (no installs needed).

## Run it

Not yet: the commands are added when M0 is merged. `docs/frontend-plan.md`, section 2, lists what to install.

## License

[MIT](LICENSE).
