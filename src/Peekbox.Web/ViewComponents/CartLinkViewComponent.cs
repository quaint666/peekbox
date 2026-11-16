using Microsoft.AspNetCore.Mvc;
using Peekbox.Web.Services;

namespace Peekbox.Web.ViewComponents;

// The header's cart link with its count, "Giỏ hàng (1)" or "Giỏ hàng (0)" (mockups: every header). The count needs the
// database, so it is a view component (the framework's tool for a piece of the layout with its own data).
public sealed class CartLinkViewComponent(HamperService hampers) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(bool current)
    {
        var count = await hampers.CartCountAsync(Request.Cookies[HamperService.CookieName], HttpContext.RequestAborted);
        return View(new CartLink(count, current));
    }
}

// Current is true on /cart: the link is marked as the page you are on (mockup 05).
public sealed record CartLink(int Count, bool Current);
