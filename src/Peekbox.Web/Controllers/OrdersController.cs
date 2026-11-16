using Microsoft.AspNetCore.Mvc;
using Peekbox.Web.Models;
using Peekbox.Web.Services;

namespace Peekbox.Web.Controllers;

// An order's own page (F51), opened only by its secret token: the address checkout sends the visitor to, and later the
// link in the email (F61) and the track page (F60). Anyone with the address can read it, like a parcel link, so it is
// kept out of search engines (noindex), out of other sites' logs (Referrer-Policy: no-referrer, on every page) and out
// of shared caches (no-store).
public class OrdersController(OrderService orders) : Controller
{
    [HttpGet("/orders/{token}")]
    [HttpHead("/orders/{token}")]
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
}
