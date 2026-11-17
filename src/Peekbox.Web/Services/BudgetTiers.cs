namespace Peekbox.Web.Services;

// The seven budgets of the ready hampers and of the builder's quick budgets (decision 2026-11-17), written once.
public static class BudgetTiers
{
    public static readonly IReadOnlyList<long> All = [300_000, 500_000, 800_000, 1_000_000, 2_000_000, 5_000_000, 10_000_000];
}
