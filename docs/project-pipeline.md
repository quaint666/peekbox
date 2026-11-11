# How we build a project

A working pipeline for the next project (Peekbox), based on what Bloomy taught us. Written 2026-11-10.

## The pipeline

```
1 idea (done)
2 use cases, ranked
3 test the riskiest unknown
4 thin architecture
5 skeleton: "hello" through CI to staging
6 per slice: build + test + deploy to staging + review   (repeat)
7 promote to prod
8 watch, fix, optimize   (feeds the next slices)
```

It is waterfall for steps 1-4, where changing your mind is cheap, and short loops from step 6 on.

## Steps

| Step | What | Done when (proposal) |
|---|---|---|
| 2 Use cases | List what a user must be able to do; rank by value | A ranked list; the top 3 have acceptance criteria |
| 3 Riskiest unknown | Prove the part most likely to break the design, in 1-2 days | A script or tests show it works, and the decision is written down |
| 4 Architecture | Stack, data model, boundaries; only as much as the first slices need | One page; open questions listed |
| 5 Skeleton | Smallest app, database, CI, one test, deployed to staging | A commit reaches staging through CI without manual steps |
| 6 Slice | One use case, 1-3 days: build, test, deploy to staging, review | Tests pass, it runs on staging, the owner has reviewed it |
| 7 Promote | Move a reviewed staging build to prod | Prod runs the same image that staging ran |
| 8 Watch | Uptime, errors, feedback; fix and optimize | Findings become ranked slices |

## What Bloomy taught us

Bloomy took 199 commits from 2026-09-04 to 2026-11-10, after three weeks of framing.

- Deploy came after the build. The build ran Sep 8-13, the pipeline work Sep 28-29. So: skeleton first (step 5).
- Tests came in a burst on Oct 6-7, after the pages existed. So: tests with each slice.
- The payment gateway search ran Aug 28 to Sep 4, with detours, while other work waited. So: test the riskiest unknown first (step 3).
- The decision log (64 entries) was worth keeping: one line per decision.

## For Peekbox

- Settled, no longer unknowns: payment is ZaloPay; hosting is our own infra.
- The riskiest unknown is the product's core: the budget-first hamper builder, with per-item expiry and origin and a shelf-life rule. Prove it on a small item set before the architecture.
- Step 5 is not mostly reuse any more: the backend is ASP.NET Core, so the Dockerfile, CI build and tests are written new, with EF Core migrations instead of Bloomy's schema pipeline. Decide in step 4 what, if anything, still carries over.

## Defaults

- Slices: 1-3 days.
- Staging and prod share one VM for now: good for behaviour and config, not for load or isolation. No load tests on staging until separate VMs exist.
- Keep a decision log, one line per decision.

## What this does not give

- No proof it is faster; the evidence is one project, read after the fact.
- The architecture stays thin until the riskiest unknown is proven.

## Open

- Who reviews each slice, and how long is a slice?
- The first ranked list of use cases.
