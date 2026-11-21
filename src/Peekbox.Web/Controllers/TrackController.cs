using Microsoft.AspNetCore.Mvc;
using Peekbox.Web.Data;
using Peekbox.Web.Models;
using Peekbox.Web.Navigation;
using Peekbox.Web.Services;

namespace Peekbox.Web.Controllers;

// Track an order (F60): the order number and the sender's phone lead to the order's own page. Any mismatch gets the
// same message, so the page does not tell which of the two was wrong. Every lookup counts toward the limit, which runs
// first; that limit is what stops someone from trying numbers and phones one after another.
public class TrackController(OrderService orders, RateLimits rateLimits) : Controller
{
    [HttpGet(SiteLinks.TrackPath)]
    [HttpHead(SiteLinks.TrackPath)]
    public IActionResult Index() => Page(new TrackForm(), notFound: false, tooManyTries: false);

    [HttpPost(SiteLinks.TrackPath)]
    public async Task<IActionResult> Index(TrackForm form, CancellationToken cancellationToken)
    {
        if (!rateLimits.TryTrack(HttpContext))
        {
            Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return Page(form, notFound: false, tooManyTries: true);
        }

        var id = OrderNumber.Parse(form.Number);
        var token = id is null ? null : await orders.TokenForAsync(id.Value, CheckoutCheck.PhoneDigits(form.Phone), cancellationToken);
        return token is null ? Page(form, notFound: true, tooManyTries: false) : LocalRedirect(SiteLinks.Order(token));
    }

    private ViewResult Page(TrackForm form, bool notFound, bool tooManyTries)
    {
        ViewData["Nav"] = SiteLinks.Track.Key;
        return View("Index", new TrackViewModel(form, notFound, tooManyTries));
    }
}
