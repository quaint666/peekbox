namespace Peekbox.Web.Data;

// A placed order (F48). Anything that can change later (item names and prices, the packaging, the ward's name and
// fee) is copied onto the order, so the order always shows what was sold; the ward and the items are also linked by id.
public class Order
{
    // Counts up. Shown to people as the order number (Number), never used to open the order.
    public int Id { get; set; }

    // A long random code made by the server: the only way to open the confirmation page.
    public required string Token { get; set; }

    public OrderStatus Status { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    public PaymentStatus PaymentStatus { get; set; }

    public required string SenderName { get; set; }

    // 10 digits, no spaces.
    public required string SenderPhone { get; set; }

    public required string SenderEmail { get; set; }

    public required string RecipientName { get; set; }

    public required string RecipientPhone { get; set; }

    public int WardId { get; set; }

    public Ward? Ward { get; set; }

    // The ward's name when the order was placed.
    public required string WardName { get; set; }

    // House number and street.
    public required string Street { get; set; }

    public DateOnly DeliveryDate { get; set; }

    public DeliveryWindow DeliveryWindow { get; set; }

    // F43: the optional note for the card in the box (null when none), and "hide prices inside the hamper".
    public string? CardMessage { get; set; }

    public bool HidePrices { get; set; }

    // Whole dong, tax included. TotalVnd = GoodsVnd + PackagingFeeVnd + DeliveryFeeVnd (a database check says so too).
    public long GoodsVnd { get; set; }

    public required string PackagingName { get; set; }

    public long PackagingFeeVnd { get; set; }

    public long DeliveryFeeVnd { get; set; }

    public long TotalVnd { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<OrderItem> Items { get; set; } = [];

    // PEEK000123: what the customer reads and types on the track page.
    public string Number => OrderNumber.Format(Id);
}

// F43: how long the card message may be, one number for the form, the column and its check.
public static class CardMessageRules
{
    public const int MaxLength = 200;
}

// The order number, written and read in one place.
public static class OrderNumber
{
    public const string Prefix = "PEEK";

    public static string Format(int id) => $"{Prefix}{id:D6}";

    // "PEEK000123", "peek000123", " PEEK123 " or "123" give 123; anything else gives null.
    public static int? Parse(string? text)
    {
        var cleaned = (text ?? "").Trim();
        if (cleaned.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[Prefix.Length..];
        }

        return cleaned.Length is > 0 and <= 9 && cleaned.All(char.IsAsciiDigit) && int.Parse(cleaned, System.Globalization.CultureInfo.InvariantCulture) is > 0 and var id
            ? id
            : null;
    }
}
