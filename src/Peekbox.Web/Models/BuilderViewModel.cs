using Peekbox.Web.Services;

namespace Peekbox.Web.Models;

public sealed record BuilderViewModel
{
    // The quick amounts of F14 (docs/functions.md). The only place they are written.
    public static readonly IReadOnlyList<long> QuickAmounts = [300_000, 500_000, 800_000, 1_000_000];

    // What the two fields show: the saved values, or what the visitor typed when something was wrong.
    public string BudgetText { get; init; } = "";

    public string DeliveryDateText { get; init; } = "";

    public BudgetProblem BudgetProblem { get; init; }

    public DateProblem DateProblem { get; init; }
}
