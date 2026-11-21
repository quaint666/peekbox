namespace Peekbox.Web.Data;

public enum CouponKind { Percent, Fixed }

// A discount code (F38). The rules are in Services/CouponRules.cs. Seeded from content/data/coupons.json; the admin
// (M6) will manage them. Value is a percentage (1 to 100) or an amount in whole dong. The discount is taken from the
// goods only, not from the packaging or the delivery fee.
public class Coupon
{
    // The longest code, for the column and the form.
    public const int CodeMaxLength = 32;

    public int Id { get; set; }

    // Upper case, no spaces: what the visitor types is made the same way before it is looked up (CouponRules.Normalize).
    public required string Code { get; set; }

    public CouponKind Kind { get; set; }

    public long Value { get; set; }

    // The goods must cost at least this much (0: no minimum).
    public long MinGoodsVnd { get; set; }

    // The first and last day it works, in Vietnam's calendar, both included; empty: no limit on that side.
    public DateOnly? ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    // How many orders may use it in all (empty: any number), and how many have. An order takes one use in its transaction.
    public int? MaxUses { get; set; }

    public int Uses { get; set; }
}
