using Peekbox.Web.Data;
using Peekbox.Web.Services;

namespace Peekbox.Web.Models;

// An order's own page (F51; mockups 07 and 07-cancelled, "Confirmation"). Everything shown is the copy saved on the
// order, so the page says what was sold even after a price or a name changes. Images come from the products (by id):
// a picture is not part of what was sold.
public sealed record OrderPageModel(Order Order, IReadOnlyDictionary<int, string> Images)
{
    // The four steps of the strip, in order. A cancelled order has no strip, only "Đã huỷ".
    public static readonly IReadOnlyList<(OrderStatus Status, string Label)> Steps =
    [
        (OrderStatus.New, "Mới"),
        (OrderStatus.Preparing, "Đang chuẩn bị"),
        (OrderStatus.Delivering, "Đang giao"),
        (OrderStatus.Delivered, "Đã giao"),
    ];

    public bool Cancelled => Order.Status == OrderStatus.Cancelled;

    // F49: a ZaloPay payment that failed on an order that is still on. Cash orders and cancelled orders are never "failed".
    public static bool PaymentFailed(Order order) =>
        order is { PaymentMethod: PaymentMethod.ZaloPay, PaymentStatus: PaymentStatus.Failed, Status: not OrderStatus.Cancelled };

    public bool PayFailed => PaymentFailed(Order);

    // What the customer reads after "Thanh toán:". ZaloPay is a stand-in (decision 2026-11-11), so a paid ZaloPay order
    // says so.
    public string PaymentText => (Order.PaymentMethod, Order.PaymentStatus) switch
    {
        (PaymentMethod.ZaloPay, PaymentStatus.Paid) => "Đã thanh toán qua ZaloPay (mô phỏng)",
        (PaymentMethod.ZaloPay, PaymentStatus.Failed) => "Thanh toán ZaloPay chưa thành công",
        (PaymentMethod.ZaloPay, PaymentStatus.Refunded) => "Đã hoàn tiền qua ZaloPay",
        (PaymentMethod.ZaloPay, _) => "Chưa thanh toán qua ZaloPay",
        (PaymentMethod.CashOnDelivery, PaymentStatus.Paid) => "Đã thu tiền mặt khi nhận hàng",
        (PaymentMethod.CashOnDelivery, PaymentStatus.Refunded) => "Đã hoàn tiền",
        (PaymentMethod.CashOnDelivery, _) => "Trả tiền mặt khi nhận hàng, chưa thu tiền",
        _ => throw new ArgumentOutOfRangeException(nameof(Order), Order.PaymentMethod, null),
    };

    // "15/01/2027, sáng 8:00 – 12:00", as drawn.
    public string When
    {
        get
        {
            var window = DeliveryWindows.Label(Order.DeliveryWindow);
            return $"{Formatting.VnDate.Day(Order.DeliveryDate)}, {char.ToLowerInvariant(window[0])}{window[1..]}";
        }
    }

    // A saved phone (10 digits) as people read it: "0900 111 111".
    public static string Phone(string digits) => digits.Length == 10 ? $"{digits[..4]} {digits[4..7]} {digits[7..]}" : digits;
}
