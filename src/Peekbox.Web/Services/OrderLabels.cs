using Peekbox.Web.Data;

namespace Peekbox.Web.Services;

// What an order's status is called, written once: the customer's strip and the admin's list, badge and select all read it.
public static class OrderLabels
{
    public static string Status(OrderStatus status) => status switch
    {
        OrderStatus.New => "Mới",
        OrderStatus.Preparing => "Đang chuẩn bị",
        OrderStatus.Delivering => "Đang giao",
        OrderStatus.Delivered => "Đã giao",
        OrderStatus.Cancelled => "Đã huỷ",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    // "Mới, Đang chuẩn bị, Đang giao, Đã giao": the steps an order moves through, in order (a cancelled order leaves them).
    public static string Steps { get; } = string.Join(", ", Enum.GetValues<OrderStatus>().Where(s => s != OrderStatus.Cancelled).Select(Status));

    // The admin's short words for how an order is paid ("ZaloPay, đã trả"); the customer's longer sentences are in
    // OrderPageModel.PaymentTextOf.
    public static string PaymentShort(PaymentMethod method, PaymentStatus status) => (method, status) switch
    {
        (PaymentMethod.ZaloPay, PaymentStatus.Paid) => "ZaloPay, đã trả",
        (PaymentMethod.ZaloPay, PaymentStatus.Failed) => "ZaloPay, thanh toán lỗi",
        (PaymentMethod.ZaloPay, PaymentStatus.Refunded) => "ZaloPay, đã hoàn tiền",
        (PaymentMethod.ZaloPay, _) => "ZaloPay, chờ trả",
        (PaymentMethod.CashOnDelivery, PaymentStatus.Paid) => "Khi nhận hàng, đã thu",
        (PaymentMethod.CashOnDelivery, PaymentStatus.Refunded) => "Khi nhận hàng, đã hoàn tiền",
        (PaymentMethod.CashOnDelivery, _) => "Khi nhận hàng",
        _ => throw new ArgumentOutOfRangeException(nameof(method), method, null),
    };

    // The key a status has in an address (/admin/orders?status=preparing).
    public static string Key(OrderStatus status) => status.ToString().ToLowerInvariant();

    public static OrderStatus? FromKey(string? key) =>
        Enum.GetValues<OrderStatus>().Cast<OrderStatus?>().FirstOrDefault(s => Key(s!.Value) == key);
}
