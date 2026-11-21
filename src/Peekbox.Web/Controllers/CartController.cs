using Microsoft.AspNetCore.Mvc;
using Peekbox.Web.Data;
using Peekbox.Web.Models;
using Peekbox.Web.Navigation;
using Peekbox.Web.Services;

namespace Peekbox.Web.Controllers;

// The cart (F37, F39, F40). There is one hamper per visitor, and it is in the cart once it holds an item (decision
// 2026-11-16): no separate "in the cart" flag, so what the builder shows and what the cart shows can never differ.
public class CartController(BuilderLimits limits, HamperService hampers, RateLimits rateLimits) : Controller
{
    private const string CouponErrorKey = "couponError";
    private const string CouponTypedKey = "couponTyped";

    [HttpGet(SiteLinks.CartPath)]
    [HttpHead(SiteLinks.CartPath)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var hamper = await hampers.FindAsync(HamperCookie.Read(Request), cancellationToken);
        var box = hamper is null ? null : await hampers.BoxAsync(hamper, cancellationToken);
        return View(new CartViewModel(
            box is { Lines.Count: > 0 } ? box : null, limits.ItemsOpen(hamper?.DeliveryDate),
            TempData[CouponErrorKey] as string, TempData[CouponTypedKey] as string));
    }

    // "Áp dụng" (F38): a code that works is kept on the hamper; one that does not comes back as a line under the field,
    // with what was typed, after a redirect (post, then redirect). Every try counts for the rate limit.
    [HttpPost(SiteLinks.CartCouponPath)]
    public async Task<IActionResult> ApplyCoupon(string? code, CancellationToken cancellationToken)
    {
        var hamper = await hampers.FindAsync(HamperCookie.Read(Request), cancellationToken);
        var box = hamper is null ? null : await hampers.BoxAsync(hamper, cancellationToken);
        if (hamper is null || box is not { Lines.Count: > 0 })
        {
            return LocalRedirect(SiteLinks.Cart.Href);
        }

        if (!rateLimits.TryCoupon(HttpContext))
        {
            TempData[CouponErrorKey] = "Bạn đã thử quá nhiều lần. Hãy đợi vài phút rồi thử lại.";
        }
        else if (await hampers.ApplyCouponAsync(hamper, box, code, cancellationToken) is var problem && problem != CouponProblem.None)
        {
            TempData[CouponErrorKey] = await hampers.CouponMessageAsync(problem, code, cancellationToken);
            TempData[CouponTypedKey] = (code ?? "").Trim()[..Math.Min((code ?? "").Trim().Length, Coupon.CodeMaxLength)];
        }

        return LocalRedirect(SiteLinks.Cart.Href + Anchors.Link(Anchors.Coupon));
    }

    // "Bỏ mã".
    [HttpPost(SiteLinks.CartCouponRemovePath)]
    public async Task<IActionResult> RemoveCoupon(CancellationToken cancellationToken)
    {
        var hamper = await hampers.FindAsync(HamperCookie.Read(Request), cancellationToken);
        if (hamper is not null)
        {
            await hampers.RemoveCouponAsync(hamper, cancellationToken);
        }

        return LocalRedirect(SiteLinks.Cart.Href + Anchors.Link(Anchors.Coupon));
    }

    // "Thêm vào giỏ hàng" on the review page (F36). The hamper is already the cart; this checks it again on the server
    // and opens the cart. A box that cannot go on goes back to where the visitor can fix it.
    [HttpPost(SiteLinks.CartAddPath)]
    public async Task<IActionResult> Add(CancellationToken cancellationToken)
    {
        var hamper = await hampers.FindAsync(HamperCookie.Read(Request), cancellationToken);
        if (hamper is null || !limits.ItemsOpen(hamper.DeliveryDate))
        {
            return LocalRedirect(SiteLinks.Builder.Href);
        }

        var box = await hampers.BoxAsync(hamper, cancellationToken);
        if (box.Lines.Count == 0)
        {
            return LocalRedirect(SiteLinks.Builder.Href);
        }

        return LocalRedirect(box.OutOfDate.Count > 0 ? SiteLinks.ReviewPath : SiteLinks.Cart.Href);
    }

    // "Xóa hộp" (F37), after the confirm popover; in the cart, in the builder's box (from=builder) and in the home page's "Làm lại" (from=builder). The items and the packaging go; the budget and the date stay.
    [HttpPost(SiteLinks.CartClearPath)]
    public async Task<IActionResult> Clear(ReturnTo from = ReturnTo.Cart, CancellationToken cancellationToken = default)
    {
        var hamper = await hampers.FindAsync(HamperCookie.Read(Request), cancellationToken);
        if (hamper is not null)
        {
            await hampers.ClearAsync(hamper, cancellationToken);
            HamperCookie.Write(Response, hamper.Token);   // a change of the box starts the cookie's time again (HamperCookie)
        }

        // From the cart the visitor stays in the cart (now empty); from the builder's box or the home page's banner they go on
        // building: the budget and the date are kept, so the builder opens at the items.
        return LocalRedirect(from == ReturnTo.Builder ? SiteLinks.Builder.Href + Anchors.Link(Anchors.Items) : SiteLinks.Cart.Href);
    }
}
