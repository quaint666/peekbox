using Peekbox.Web.Data;
using Peekbox.Web.Navigation;
using Peekbox.Web.Services;

namespace Peekbox.Web.Models.Admin;

// The orders screen (F57; mockups 02 and state 14). Selected is the open order (the one in the address, else the first of the
// list); it may be an order that the filters do not show.
public sealed record AdminOrdersViewModel(
    AdminOrderFilter Filter, AdminOrderList List, Order? Selected, string? Notice, string? Error, DateOnly Today)
{
    public string DateValue => Filter.Date is { } d ? d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) : "";

    public string StatusKey => Filter.Status is { } s ? OrderLabels.Key(s) : "";

    public string Search => Filter.Search ?? "";

    // Filters applied other than the day: the empty list then means "nothing matches", not "no orders that day".
    public bool Narrowed => Filter.Status is not null || AdminOrders.Clean(Filter.Search) is not null;

    // The next step of the open order, or null when it is final.
    public OrderStatus? NextStatus => Selected is null ? null : AdminOrders.Next(Selected.Status);

    public string Link(int? order) => SiteLinks.AdminOrders(Filter.Date, StatusKey, Search, order);

    // The two day chips ("Hôm nay", "Mọi ngày"): links that change only the day. They keep the status and the search, and leave
    // the open order out, so the first order of the new list opens. Neither chip is current when another day is picked.
    public string DayLink(DateOnly? day) => SiteLinks.AdminOrders(day, StatusKey, Search, null);

    public bool IsToday => Filter.Date == Today;

    public bool IsEveryDay => Filter.Date is null;
}
