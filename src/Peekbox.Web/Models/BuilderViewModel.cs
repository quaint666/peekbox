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

    // The saved delivery date, shown next to the checkbox. Null until a budget and a date are saved: the items stay locked.
    public DateOnly? SavedDeliveryDate { get; init; }

    // The item list; null while the items are locked.
    public ItemList? Items { get; init; }

    // The "only items in date" checkbox (F18), on by default. The name is the query parameter of the filter form.
    public bool InDate { get; init; } = true;
}
