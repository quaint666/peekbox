using Peekbox.Web.Services;

namespace Peekbox.Web.Models;

// What _QuantityControl draws for one item: "Thêm", the − n + pill, or a disabled button saying why it cannot go in.
// Action is the address the form posts to, with the list's filters, so the visitor comes back to the same list.
public sealed record QuantityControl(string Action, string Slug, string Name, int Quantity, ItemStatus Next, long OverByVnd, bool SoldOut);
