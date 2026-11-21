using Peekbox.Web.Services;

namespace Peekbox.Web.Models;

// The cart (F37, F39, F40). Box is null when the cart is empty: no hamper, or a hamper without items. DateOpen is false
// when the saved delivery date can no longer be delivered (today or earlier); the box is still shown, but it cannot go on.
public sealed record CartViewModel(Box? Box, bool DateOpen, string? CouponError = null, string? CouponTyped = null)
{
    // Why "Tiếp tục thanh toán" is off, or null when the box can go to checkout. The first reason that applies wins; the
    // checkout controller refuses the same boxes (it sends them back here).
    public string? CheckoutReason =>
        Box is { OutOfDate.Count: > 0 } ? "Bỏ món hết hạn hoặc đổi ngày giao để tiếp tục."
        : Box is { Coupon.Works: false } ? "Mã giảm giá không còn áp dụng. Bỏ mã để tiếp tục."
        : Box is { DeliveryDate: null } ? "Chưa có ngày giao. Bấm Sửa hộp để chọn ngày giao."
        : !DateOpen ? "Ngày giao đã qua. Bấm Sửa hộp để chọn ngày giao mới."
        : null;

    // Whether the reason is a problem with the box (red) rather than a note.
    public bool Blocked => Box is { OutOfDate.Count: > 0 } || Box is { Coupon.Works: false } || !DateOpen;
}
