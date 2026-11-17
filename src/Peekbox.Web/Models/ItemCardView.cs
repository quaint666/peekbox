using Peekbox.Web.Services;

namespace Peekbox.Web.Models;

// One item card on /builder, in a row or in the grid: the item, and where its "Thêm", − and + post (with the list's
// filters, so the visitor comes back to the same list).
public sealed record ItemCardView(ItemCard Item, string QuantityAction);
