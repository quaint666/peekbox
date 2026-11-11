# Peekbox

A web app for building a gift hamper for Tết, 20/11 (Teachers' Day) and 8/3 (Women's Day).

The customer sets a budget first, then picks items. Every item shows its brand, weight, origin and expiry date, and the total is calculated by the server. An item that would expire before the delivery date cannot be ordered.

**Status:** design phase. The mockups, written content and images are done; there is no application code yet.

## Where to look

- [`docs/project-pipeline.md`](docs/project-pipeline.md): how we build, step by step.
- [`docs/decisions.md`](docs/decisions.md): what we decided and why.
- [`docs/functions.md`](docs/functions.md), [`docs/sitemap.md`](docs/sitemap.md), [`docs/use-cases.md`](docs/use-cases.md): what the site does, its pages, and who does what with it.
- [`docs/frontend-plan.md`](docs/frontend-plan.md): how the front end is built and reviewed.
- [`docs/api-contract.md`](docs/api-contract.md): the draft agreement between the front end and any backend.
- [`design/`](design/README.md): the screen mockups (141 images) and design notes. Start with `design/mockups/customer/`.
- [`content/`](content/README.md): blog posts, terms and privacy text, and sample product data.
- [`public/images/`](public/images/): product photos, packaging, hero and the logo.
- [`CONTRIBUTING.md`](CONTRIBUTING.md): how to propose a change (no installs needed).

## Run the front end

You need Node 22 or newer. From the project folder:

```
npm install      # once: installs TypeScript
npm run build    # compiles src/ to public/js/ and copies the sample data
npm run dev      # starts the site at http://localhost:3000 (stop with Ctrl+C)
npm test         # builds, then runs the checks
```

The pages are plain HTML and CSS in `public/`; the code is TypeScript in `src/`. There is no framework.

## License

[MIT](LICENSE).
