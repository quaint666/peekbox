using Peekbox.Web.Services;

namespace Peekbox.Web.Models;

// The cart (F37, F39, F40). Box is null when the cart is empty: no hamper, or a hamper without items. DateOpen is false
// when the saved delivery date can no longer be delivered (today or earlier); the box is still shown, but it cannot go on.
public sealed record CartViewModel(Box? Box, bool DateOpen)
{
    // Why "Tiếp tục thanh toán" is off. The first reason that applies wins; checkout itself is not built yet (M3).
    public string CheckoutReason =>
        Box is { OutOfDate.Count: > 0 } ? "Bỏ món hết hạn hoặc đổi ngày giao để tiếp tục."
        : Box is { DeliveryDate: null } ? "Chưa có ngày giao. Bấm Sửa hộp để chọn ngày giao."
        : !DateOpen ? "Ngày giao đã qua. Bấm Sửa hộp để chọn ngày giao mới."
        : "Thanh toán sẽ có trong bản sau.";

    // Whether the reason is a problem with the box (red) rather than a note.
    public bool Blocked => Box is { OutOfDate.Count: > 0 } || !DateOpen;
}
