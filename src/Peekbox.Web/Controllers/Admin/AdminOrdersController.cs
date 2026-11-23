using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Peekbox.Web.Data;
using Peekbox.Web.Models.Admin;
using Peekbox.Web.Navigation;
using Peekbox.Web.Services;

namespace Peekbox.Web.Controllers.Admin;

// The orders of the admin (F57; mockups 02 and state 14). Any staff member may open and move them (they see phones and
// addresses). A move is one step forward at a time; cancelling comes with F70. Filters are in the address (GET), so a page can
// be bookmarked and Back keeps the list; the form of one order is a POST that comes back to the same list.
public class AdminOrdersController(AdminOrders orders, BuilderLimits limits) : AdminControllerBase
{
    private const string NoticeKey = "adminOrdersNotice";
    private const string ErrorKey = "adminOrdersError";

    [HttpGet(SiteLinks.AdminOrdersPath)]
    [HttpHead(SiteLinks.AdminOrdersPath)]
    public async Task<IActionResult> Index(string? date, string? status, string? q, int? order, CancellationToken cancellationToken)
    {
        // No "date" at all means today (in Vietnam); "date=" with nothing means every day; a text that is not a date is today.
        DateOnly? day = !Request.Query.ContainsKey("date") ? limits.Today : string.IsNullOrWhiteSpace(date) ? null : ParseDate(date) ?? limits.Today;
        var filter = new AdminOrderFilter(day, OrderLabels.FromKey(status), AdminOrders.Clean(q));
        var list = await orders.ListAsync(filter, cancellationToken);

        var selectedId = order ?? list.Rows.FirstOrDefault()?.Id;
        var selected = selectedId is { } id ? await orders.FindAsync(id, cancellationToken) : null;

        return View("Index", new AdminOrdersViewModel(filter, list, selected, TempData[NoticeKey] as string, TempData[ErrorKey] as string, limits.Today));
    }

    [HttpPost("/admin/orders/{id:int}/save")]
    public async Task<IActionResult> Save(
        int id, string? from, string? to, string? trackingCode, string? adminNote, string? filterDate, string? filterStatus, string? filterQ,
        CancellationToken cancellationToken)
    {
        var day = string.IsNullOrWhiteSpace(filterDate) ? (DateOnly?)null : ParseDate(filterDate);
        string Back() => SiteLinks.AdminOrders(day, OrderLabels.FromKey(filterStatus) is { } s ? OrderLabels.Key(s) : null, AdminOrders.Clean(filterQ), id);

        var fromStatus = OrderLabels.FromKey(from);
        var toStatus = string.IsNullOrEmpty(to) ? fromStatus : OrderLabels.FromKey(to);
        if (fromStatus is null || toStatus is null)
        {
            TempData[ErrorKey] = "Chỉ chuyển được sang bước kế tiếp.";
            return LocalRedirect(Back());
        }

        var number = OrderNumber.Format(id);
        var result = await orders.SaveAsync(id, fromStatus.Value, toStatus.Value, trackingCode, adminNote, cancellationToken);
        switch (result)
        {
            case StatusChange.Saved:
                TempData[NoticeKey] = toStatus == fromStatus
                    ? $"Đã lưu đơn {number}."
                    : $"Đã lưu đơn {number}, trạng thái: {OrderLabels.Status(toStatus.Value)}.";
                break;
            case StatusChange.NotFound:
                TempData[ErrorKey] = $"Không tìm thấy đơn {number}.";
                return LocalRedirect(SiteLinks.AdminOrders(day, null, null, null));
            case StatusChange.Stale:
                TempData[ErrorKey] = $"Đơn {number} vừa được đổi trạng thái ở nơi khác, nên chưa lưu gì. Trang đã tải lại: kiểm tra rồi lưu lại.";
                break;
            case StatusChange.PaymentMissing:
                TempData[ErrorKey] = $"Đơn {number} thanh toán ZaloPay chưa thành công nên chưa thể chuyển sang Đang giao.";
                break;
            case StatusChange.TooLong:
                TempData[ErrorKey] = $"Mã vận đơn tối đa {OrderAdminRules.TrackingCodeMaxLength} ký tự, ghi chú tối đa {OrderAdminRules.AdminNoteMaxLength} ký tự.";
                break;
            default:
                TempData[ErrorKey] = "Chỉ chuyển được sang bước kế tiếp.";
                break;
        }

        return LocalRedirect(Back());
    }

    private static DateOnly? ParseDate(string? text) =>
        DateOnly.TryParseExact(text?.Trim(), BuilderLimits.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
}
