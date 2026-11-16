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

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public List<HamperItem> Items { get; set; } = [];
}
