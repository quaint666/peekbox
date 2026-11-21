using Peekbox.Web.Data;
using Peekbox.Web.Formatting;

namespace Peekbox.Web.Services;

// Why a code does not work (F38), in the order they are checked.
public enum CouponProblem { None, Empty, Unknown, NotStarted, Expired, UsedUp, BelowMinimum }

// The rules of a discount code, written once, with no database (the tests call them directly). The discount comes off
// the goods only; packaging and delivery are paid in full. A percentage is rounded down to whole dong, a fixed amount
// never takes more than the goods cost.
public static class CouponRules
{
    // What the visitor typed as it is stored and looked up: trimmed, spaces inside removed, upper case. "chao 10" is CHAO10.
    public static string Normalize(string? text) =>
        new string((text ?? "").Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

    // The first thing wrong with the code on this day with goods that cost goodsVnd, or None.
    public static CouponProblem Check(Coupon? coupon, long goodsVnd, DateOnly today)
    {
        if (coupon is null)
        {
            return CouponProblem.Unknown;
        }

        if (coupon.ValidFrom is { } from && today < from)
        {
            return CouponProblem.NotStarted;
        }

        if (coupon.ValidTo is { } to && today > to)
        {
            return CouponProblem.Expired;
        }

        if (coupon.MaxUses is { } max && coupon.Uses >= max)
        {
            return CouponProblem.UsedUp;
        }

        return goodsVnd < coupon.MinGoodsVnd ? CouponProblem.BelowMinimum : CouponProblem.None;
    }

    public static long Discount(Coupon coupon, long goodsVnd) => coupon.Kind switch
    {
        CouponKind.Percent => goodsVnd * Math.Clamp(coupon.Value, 0, 100) / 100,
        CouponKind.Fixed => Math.Min(Math.Max(coupon.Value, 0), goodsVnd),
        _ => throw new ArgumentOutOfRangeException(nameof(coupon), coupon.Kind, null),
    };

    // "Giảm 10%" or "Giảm 20.000đ": what the code gives, for the cart.
    public static string Label(Coupon coupon) => coupon.Kind == CouponKind.Percent ? $"Giảm {coupon.Value}%" : $"Giảm {Vnd.Format(coupon.Value)}";

    // The line under the field or in the note. The coupon is only needed for the problems that quote its numbers.
    public static string Message(CouponProblem problem, Coupon? coupon) => problem switch
    {
        CouponProblem.Empty => "Nhập mã giảm giá.",
        CouponProblem.Unknown => "Mã giảm giá không hợp lệ.",
        CouponProblem.NotStarted => $"Mã giảm giá chưa dùng được, bắt đầu từ {VnDate.Day(coupon!.ValidFrom!.Value)}.",
        CouponProblem.Expired => "Mã giảm giá đã hết hạn.",
        CouponProblem.UsedUp => "Mã giảm giá đã hết lượt dùng.",
        CouponProblem.BelowMinimum => $"Mã này dùng cho đơn có tiền hàng từ {Vnd.Format(coupon!.MinGoodsVnd)}.",
        _ => "",
    };
}
