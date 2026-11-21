using Microsoft.AspNetCore.Mvc;
using Peekbox.Web.Models;
using Peekbox.Web.Navigation;
using Peekbox.Web.Services;

namespace Peekbox.Web.Controllers;

// An order's own page (F51), opened only by its secret token: the address checkout sends the visitor to, and later the
// link in the email (F61) and the track page (F60). Anyone with the address can read it, like a parcel link, so it is
// kept out of search engines (noindex), out of other sites' logs (Referrer-Policy: no-referrer, on every page) and out
// of shared caches (no-store).
public class OrdersController(OrderService orders) : Controller
{
    [HttpGet(SiteLinks.OrdersPath + "/{token}")]
    [HttpHead(SiteLinks.OrdersPath + "/{token}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Show(string token, CancellationToken cancellationToken)
    {
        var order = await orders.FindAsync(token, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        ViewData["NoIndex"] = true;
        return View(new OrderPageModel(order, await orders.ImagesAsync(order, cancellationToken)));
    }

    // F49: the payment-failed page. Only for an order whose ZaloPay payment failed; any other order (paid, cash,
    // cancelled) goes to its own page, so the page cannot be used to read orders nobody linked to it.
    [HttpGet(SiteLinks.OrdersPath + "/{token}/payment")]
    [HttpHead(SiteLinks.OrdersPath + "/{token}/payment")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Payment(string token, CancellationToken cancellationToken)
    {
        var order = await orders.FindAsync(token, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        if (!OrderPageModel.PaymentFailed(order))
        {
            return LocalRedirect(SiteLinks.Order(order.Token));
        }

        ViewData["NoIndex"] = true;
        ViewData["StillFailed"] = TempData.ContainsKey(StillFailedKey);
        return View(new OrderPageModel(order, new Dictionary<int, string>()));
    }

    [HttpPost(SiteLinks.OrdersPath + "/{token}/payment/retry")]
    public async Task<IActionResult> Retry(string token, CancellationToken cancellationToken)
    {
        var fix = await orders.RetryPaymentAsync(token, cancellationToken);
        if (fix == PaymentFix.StillFailed)
        {
            TempData[StillFailedKey] = true;
            return LocalRedirect(SiteLinks.OrderPayment(token));
        }

        return LocalRedirect(SiteLinks.Order(token));
    }

    [HttpPost(SiteLinks.OrdersPath + "/{token}/payment/cash")]
    public async Task<IActionResult> Cash(string token, CancellationToken cancellationToken)
    {
        await orders.SwitchToCashAsync(token, cancellationToken);
        return LocalRedirect(SiteLinks.Order(token));
    }

    private const string StillFailedKey = "paymentStillFailed";
}
