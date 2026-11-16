namespace Peekbox.Web.Services;

// The delivery fee by ring (F47), whole dong, tax included. SAMPLE values, Bloomy's: constants until the admin Settings
// page exists (M6), as BuilderLimits.ShelfMarginDays is. The fee is paid on top of the budget, which covers goods only.
public static class DeliveryFees
{
    public static long ForRing(int ring) => ring switch
    {
        1 => 25_000,
        2 => 35_000,
        3 => 45_000,
        _ => throw new ArgumentOutOfRangeException(nameof(ring), ring, "A ward's ring is 1, 2 or 3."),
    };
}
