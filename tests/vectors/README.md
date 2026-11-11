# Shared test cases

`pricing.json` holds cases for the rules that must behave the same wherever they run: the safe-expiry rule, the budget, and the price total.

Today the front-end tests read it (`tests/pricing.test.mjs`). When a backend exists, whichever language it is written in (TypeScript or C#) loads the **same file** and runs the same cases. If the front end and the server disagree, a test fails, not a customer.

Rules for changing it:
- Change a case only together with a line in `docs/decisions.md`.
- Add a case for every bug you find in these rules.
- Keep money as whole dong and dates as `YYYY-MM-DD`.
