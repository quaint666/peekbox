using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Peekbox.Web.Models;
using Peekbox.Web.Navigation;
using Peekbox.Web.Services;
using Peekbox.Web.Services.Email;

namespace Peekbox.Web.Controllers;

// Checkout (F41 to F48) and the sold-out notice (F50). A box that cannot be ordered (none, empty, a passed date, an item
// out of date) goes back to the cart, which says why. Placing is OrderService's job; this controller reads the form,
// checks it, and sends the visitor on (post, then redirect).
public class CheckoutController(HamperService hampers, OrderService orders, OrderMailer mailer, DeliveryCapacity capacity, BuilderLimits limits, RateLimits rateLimits, DemoMode demo) : Controller
{
    private const string SoldOutKey = "soldOut";

    // ward: the summary of a ward picked on the page; checkout.ts fetches this page and swaps the summary in.
    [HttpGet(SiteLinks.CheckoutPath)]
    [HttpHead(SiteLinks.CheckoutPath)]
    public async Task<IActionResult> Index(int? ward, CancellationToken cancellationToken)
    {
        if (demo.Enabled)
        {
            return DemoPage();
        }

        var box = await ReadyBoxAsync(cancellationToken);
        if (box is null)
        {
            return LocalRedirect(SiteLinks.Cart.Href);
        }

        return await PageAsync(box, new CheckoutForm { Ward = ward }, [], tooManyTries: false, cancellationToken);
    }

    // A value our page cannot send (a ward, a window or a payment that is not a number or a name) is refused: bound
    // as the default, it would quietly become Sáng or ZaloPay.
    [HttpPost(SiteLinks.CheckoutPath)]
    public async Task<IActionResult> Index(CheckoutForm form, CancellationToken cancellationToken)
    {
        if (demo.Enabled)
        {
            return DemoPage();
        }

        var box = await ReadyBoxAsync(cancellationToken);
        if (box is null)
        {
            // Also a second click after the order was placed (the hamper is an order now): it opens that order.
            var placed = await orders.OrderTokenOfAsync(HamperCookie.Read(Request), cancellationToken);
            return LocalRedirect(placed is null ? SiteLinks.Cart.Href : SiteLinks.Order(placed));
        }

        if (!rateLimits.TryOrder(HttpContext))
        {
            Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return await PageAsync(box, form, [], tooManyTries: true, cancellationToken);
        }

        var wards = await orders.WardsAsync(cancellationToken);
        var errors = CheckoutCheck.Check(form, wards.Select(w => w.Id).ToHashSet(), out var request);
        if (request is null)
        {
            return await PageAsync(box, form, errors, tooManyTries: false, cancellationToken, wards);
        }

        var result = await orders.PlaceAsync(HamperCookie.Read(Request), request, cancellationToken);
        switch (result.Outcome)
        {
            case PlaceOutcome.Placed:
                // F61: the confirmation email (a stand-in that writes to the log), once, for a new order only: a second click
                // (AlreadyPlaced) opens the order without another email. It runs before the redirect, and a failure is logged.
                await mailer.SendConfirmationAsync(result.OrderToken!, $"{Request.Scheme}://{Request.Host}", cancellationToken);
                // F49: when the payment failed the order and the box are kept; the visitor chooses to retry or to pay in cash.
                return LocalRedirect(result.PaymentFailed ? SiteLinks.OrderPayment(result.OrderToken!) : SiteLinks.Order(result.OrderToken!));
            case PlaceOutcome.AlreadyPlaced:
                return LocalRedirect(SiteLinks.Order(result.OrderToken!));
            case PlaceOutcome.SoldOut:
                TempData[SoldOutKey] = JsonSerializer.Serialize(result.SoldOut);
                return LocalRedirect(SiteLinks.SoldOutPath);
            case PlaceOutcome.UnknownWard:
                // The ward was in the list a moment ago; tell the visitor to pick again.
                return await PageAsync(box, form, [new FieldError("ward", "Chọn phường giao hàng", "Chọn phường giao hàng.")], tooManyTries: false, cancellationToken, wards);
            case PlaceOutcome.WindowFull:
                // Another order took the last place a moment ago; the page now shows the window as full.
                return await PageAsync(box, form, [new FieldError("window-morning", "Khung giờ đã đầy, chọn khung giờ khác", "Khung giờ này vừa đầy. Chọn khung giờ khác.")], tooManyTries: false, cancellationToken, wards);
            case PlaceOutcome.NoHamper:
            case PlaceOutcome.NotReady:
            default:
                return LocalRedirect(SiteLinks.Cart.Href);
        }
    }

    // F50 (state 07): shown once, after a redirect; a reload or a direct visit goes back to checkout.
    [HttpGet(SiteLinks.SoldOutPath)]
    public async Task<IActionResult> SoldOut(CancellationToken cancellationToken)
    {
        if (TempData[SoldOutKey] is not string json || JsonSerializer.Deserialize<List<SoldOutLine>>(json) is not { Count: > 0 } lines)
        {
            return LocalRedirect(SiteLinks.CheckoutPath);
        }

        var hamper = await hampers.FindAsync(HamperCookie.Read(Request), cancellationToken);
        var box = hamper is null ? null : await hampers.BoxAsync(hamper, cancellationToken);
        ViewData["NoIndex"] = true;
        return View(new SoldOutViewModel(lines, box is { Lines.Count: > 0 } ? box : null));
    }

    // Demo mode: the same notice for the page and for a form that was sent anyway; nothing is read or written.
    private ViewResult DemoPage()
    {
        ViewData["NoIndex"] = true;
        return View("Demo");
    }

    // The visitor's box when it can be ordered, else null (the same rules as the cart's "Tiếp tục thanh toán").
    private async Task<Box?> ReadyBoxAsync(CancellationToken cancellationToken)
    {
        var hamper = await hampers.FindAsync(HamperCookie.Read(Request), cancellationToken);
        if (hamper is null || !limits.ItemsOpen(hamper.DeliveryDate))
        {
            return null;
        }

        var box = await hampers.BoxAsync(hamper, cancellationToken);
        return box.Lines.Count == 0 || box.OutOfDate.Count > 0 || box.Coupon is { Works: false } ? null : box;
    }

    private async Task<IActionResult> PageAsync(
        Box box, CheckoutForm form, IReadOnlyList<FieldError> errors, bool tooManyTries, CancellationToken cancellationToken, List<WardOption>? wards = null)
    {
        wards ??= await orders.WardsAsync(cancellationToken);
        var fee = wards.FirstOrDefault(w => w.Id == form.Ward)?.FeeVnd;
        var slots = await capacity.SlotsAsync(box.DeliveryDate!.Value, cancellationToken);
        // A window that is full cannot stay chosen: the form shows the first one with room instead.
        if (slots.Full.Contains(form.Window) && DeliveryWindows.All.FirstOrDefault(w => !slots.Full.Contains(w), form.Window) is var open)
        {
            form.Window = open;
        }

        ViewData["NoIndex"] = true;
        return View("Index", new CheckoutViewModel(box, wards, form, errors, tooManyTries, fee, slots));
    }
}
