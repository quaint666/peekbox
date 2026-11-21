namespace Peekbox.Web.Data;

// A visitor's unfinished hamper. There are no accounts: the browser keeps a cookie with Token, and the row is found by it.
public class Hamper
{
    public int Id { get; set; }

    // A long random code. It is made by the server, never taken from the browser, and it is the only way to find the row.
    public required string Token { get; set; }

    // Whole dong. The server accepts 0 or more; there is no upper limit.
    public long BudgetVnd { get; set; }

    // Empty until the visitor picks a date.
    public DateOnly? DeliveryDate { get; set; }

    // Empty until the visitor picks one; until then the box uses the first packaging (HamperService.BoxAsync). Not a
    // database default: migrations run before the seed, so a default "kraft" would point at a row that is not there yet.
    public string? PackagingId { get; set; }

    public Packaging? Packaging { get; set; }

    // The discount code the visitor applied in the cart (F38), as stored (CouponRules.Normalize); null when none. Whether
    // it still works is checked every time the box is read (HamperService.BoxAsync), and again when the order is placed.
    public string? CouponCode { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public List<HamperItem> Items { get; set; } = [];

    // The ready hamper the box was filled from ("Dùng hộp này", F15), for "Bạn đang sửa Hộp …" on the builder. Cleared
    // when the box is emptied.
    public int? StartedFromId { get; set; }

    public ReadyHamper? StartedFrom { get; set; }

    // Set when the hamper becomes an order (F48). An ordered hamper is finished: it counts as no hamper, so the cart is
    // empty and the builder starts a new one, and placing the same order again opens this order instead of a second one.
    public int? OrderId { get; set; }

    public Order? Order { get; set; }
}
