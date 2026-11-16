# Shared test cases

`pricing.json` holds cases for the rules that must behave the same wherever they run: the safe-expiry rule, the budget, and the price total.

`tests/Peekbox.Tests/PricingVectorsTests.cs` loads **this file** and runs every case against the C# pricing code (`Services/Pricing.cs` and `BuilderLimits.IsInDate`); a failure names the case. Whatever else needs these rules later loads the same file too. If two places disagree, a test fails, not a customer.

Rules for changing it:
- Change a case only together with a line in `docs/decisions.md`.
- Add a case for every bug you find in these rules.
- Keep money as whole dong and dates as `YYYY-MM-DD`.
