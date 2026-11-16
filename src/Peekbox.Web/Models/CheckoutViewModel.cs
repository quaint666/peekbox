using Peekbox.Web.Services;

namespace Peekbox.Web.Models;

// The checkout page (F41 to F47; mockups 06 and state 02 as redrawn for M3). Every number comes from the server.
// DeliveryFeeVnd is null until a ward is chosen; the summary then says "Chọn phường để tính" and the total leaves the
// fee out (agreed with the owner).
public sealed record CheckoutViewModel(
    Box Box, IReadOnlyList<WardOption> Wards, CheckoutForm Form, IReadOnlyList<FieldError> Errors, bool TooManyTries, long? DeliveryFeeVnd)
{
    public long TotalVnd => Box.Quote.TotalVnd + (DeliveryFeeVnd ?? 0);

    public string? ErrorFor(string field) => Errors.FirstOrDefault(e => e.Field == field)?.Message;
}

// The items that ran short while the visitor was placing the order (F50, state 07), and the box as it is now.
public sealed record SoldOutViewModel(IReadOnlyList<SoldOutLine> Lines, Box? Box);
