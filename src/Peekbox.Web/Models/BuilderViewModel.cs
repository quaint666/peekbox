using Peekbox.Web.Services;

namespace Peekbox.Web.Models;

public sealed record BuilderViewModel
{
    // What the two fields show: the saved values, or what the visitor typed when something was wrong.
    public string BudgetText { get; init; } = "";

    public string DeliveryDateText { get; init; } = "";

    public BudgetProblem BudgetProblem { get; init; }

    public DateProblem DateProblem { get; init; }

    // The budget in use (saved), and an amount picked from "Chọn nhanh" that is in the field but not saved: then the
    // amber note says so (state 36). Picking never saves (decision 2026-11-17).
    public long? SavedBudgetVnd { get; init; }

    public long? PickedBudgetVnd { get; init; }

    // "Hộp gợi ý" (F15): ready hampers for the saved budget; null while the items are locked or under the lowest budget.
    public Suggestions? Suggestions { get; init; }

    // The saved delivery date, shown next to the checkbox. Null until a budget and a date are saved: the items stay locked.
    public DateOnly? SavedDeliveryDate { get; init; }

    // The item list and the filters it was made with; null while the items are locked.
    public ItemList? Items { get; init; }

    // What is in the hamper, with its quote; null while the items are locked.
    public Box? Box { get; init; }

    // The packaging choices of step 3; empty while the step is locked (no item in the box yet).
    public IReadOnlyList<PackagingOption> Packagings { get; init; } = [];
}

// The builder's "Hộp gợi ý": the occasions as chips (Chosen is filled in its colour), and the hampers of that occasion
// at the highest budget that fits the saved one ("Hộp đến 500.000đ").
public sealed record Suggestions(IReadOnlyList<OccasionLink> Occasions, OccasionLink Chosen, long BudgetVnd, IReadOnlyList<ReadyHamperCard> Hampers);
