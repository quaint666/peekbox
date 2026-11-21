using System.Text;
using Peekbox.Web.Data;
using Peekbox.Web.Formatting;
using Peekbox.Web.Models;
using Peekbox.Web.Navigation;

namespace Peekbox.Web.Services.Email;

// The confirmation email of an order (F61), written once and with no database, so the tests can read it. It says what was
// sold from the copies saved on the order, shows prices even when "hide prices" is on (that flag is for the slip in the
// box, decision 2026-11-11), and carries the link to the order page, which is the way to follow the order (decision
// 2026-11-14). The link is absolute: an email has no site around it.
public static class OrderEmail
{
    public static EmailMessage Build(Order order, string origin, ShopInfo shop)
    {
        var text = new StringBuilder();
        text.AppendLine($"Xin chào {order.SenderName},");
        text.AppendLine();
        text.AppendLine($"Cảm ơn bạn đã đặt hàng tại Peekbox. Đơn hàng {order.Number} đã được ghi nhận.");
        text.AppendLine();
        text.AppendLine("Các món trong hộp:");
        foreach (var item in order.Items)
        {
            text.AppendLine($"- {item.Name}{(item.Quantity > 1 ? $" × {item.Quantity}" : "")}: {Vnd.Format(item.UnitPriceVnd * item.Quantity)}");
        }

        text.AppendLine();
        text.AppendLine($"Tiền hàng: {Vnd.Format(order.GoodsVnd)}");
        text.AppendLine($"Đóng gói ({order.PackagingName}): {Vnd.Format(order.PackagingFeeVnd)}");
        if (order.CouponCode is { } coupon)
        {
            text.AppendLine($"Mã {coupon}: −{Vnd.Format(order.DiscountVnd)}");
        }

        text.AppendLine($"Phí giao hàng: {Vnd.Format(order.DeliveryFeeVnd)}");
        text.AppendLine($"Tổng cộng: {Vnd.Format(order.TotalVnd)}");
        text.AppendLine();
        text.AppendLine($"Giao ngày: {OrderPageModel.WhenOf(order)}");
        text.AppendLine($"Người nhận: {order.RecipientName}, {OrderPageModel.Phone(order.RecipientPhone)}");
        text.AppendLine($"Địa chỉ: {order.Street}, {order.WardName}");
        text.AppendLine($"Thanh toán: {OrderPageModel.PaymentTextOf(order)}");
        if (order.CardMessage is { } card)
        {
            text.AppendLine($"Lời nhắn trên thiệp: {card}");
        }

        if (order.HidePrices)
        {
            text.AppendLine("Gói quà: giá được ẩn bên trong hộp quà.");
        }

        text.AppendLine();
        text.AppendLine("Xem và theo dõi đơn hàng của bạn tại:");
        text.AppendLine(origin.TrimEnd('/') + SiteLinks.Order(order.Token));
        text.AppendLine();
        text.AppendLine($"Cần giúp? Gọi {shop.Phone} hoặc viết tới {shop.Email} (hỗ trợ {shop.SupportHours}).");
        text.AppendLine("Peekbox");

        return new EmailMessage(order.SenderEmail, $"Đơn hàng {order.Number} đã được ghi nhận", text.ToString());
    }
}

// Sends the confirmation of an order that was just placed. A failed send never fails the order: it is logged and the order
// stays as it is (the buyer still has the order page and the track page).
public sealed class OrderMailer(OrderService orders, IEmailSender sender, ShopInfo shop, ILogger<OrderMailer> logger)
{
    public async Task SendConfirmationAsync(string orderToken, string origin, CancellationToken cancellationToken = default)
    {
        try
        {
            var order = await orders.FindAsync(orderToken, cancellationToken);
            if (order is not null)
            {
                await sender.SendAsync(OrderEmail.Build(order, origin, shop), cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The confirmation email of an order could not be sent; the order is placed.");
        }
    }
}
