using Peekbox.Web.Services;

namespace Peekbox.Web.Models;

// The item page (F26 to F28). Box and DeliveryDate are null until the visitor has saved a budget and a delivery date
// that can still be delivered; then the page asks them to do that on /builder instead of offering the add button.
public sealed record ItemViewModel(ItemDetail Item, Box? Box)
{
    public DateOnly? DeliveryDate => Box?.DeliveryDate;

    // Whether the item is in date for the saved delivery date; null when there is no date to check against.
    public bool? InDate => DeliveryDate is { } date ? BuilderLimits.IsInDate(Item.ExpiryDate, date) : null;
}
