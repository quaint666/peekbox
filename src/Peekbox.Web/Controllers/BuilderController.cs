using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Peekbox.Web.Data;
using Peekbox.Web.Formatting;
using Peekbox.Web.Models;
using Peekbox.Web.Navigation;
using Peekbox.Web.Services;

namespace Peekbox.Web.Controllers;

public class BuilderController(BuilderLimits limits, HamperService hampers, ItemCatalog catalog) : Controller
{
    // The page of the hamper builder (/builder). It shows the saved budget and date, found by the cookie, and once both
    // are saved, the items. The filter form sends the category, the "only items in date" checkbox, the search words (q)
    // and the sort in the address. HEAD as well, as link checkers and uptime monitors ask (the home page already answers it).
    [HttpGet]
    [HttpHead]
    public async Task<IActionResult> Index(string? category, string? q, string? sort, bool inDate = true, CancellationToken cancellationToken = default)
    {
        var hamper = await hampers.FindAsync(Request.Cookies[HamperService.CookieName], cancellationToken);
        if (hamper is null)
        {
            return View(new BuilderViewModel());
        }

        return View(await WithItemsAsync(hamper, new BuilderViewModel
        {
            BudgetText = Vnd.Format(hamper.BudgetVnd),
            DeliveryDateText = hamper.DeliveryDate?.ToString(BuilderLimits.DateFormat, CultureInfo.InvariantCulture) ?? "",
            // The cookie lasts 180 days, so a date saved earlier can be today or in the past by now. It shows the same
            // warning as typing such a date, and the items stay locked until a new date is saved (decision 2026-11-16).
            DateProblem = hamper.DeliveryDate < limits.Earliest ? DateProblem.TooEarly : DateProblem.None,
        }, new ItemFilter(category, inDate, q, sort), cancellationToken));
    }

    private bool ItemsOpen(Hamper? hamper) => limits.ItemsOpen(hamper?.DeliveryDate);

    private async Task<BuilderViewModel> WithItemsAsync(Hamper? hamper, BuilderViewModel model, ItemFilter filter, CancellationToken cancellationToken)
    {
        if (hamper is null || !ItemsOpen(hamper))
        {
            return model;
        }

        var box = await hampers.BoxAsync(hamper, cancellationToken);
        return model with
        {
            SavedDeliveryDate = hamper.DeliveryDate,
            Items = await catalog.ListAsync(hamper.DeliveryDate!.Value, filter, box, cancellationToken),
            Box = box,
            // Step 3 opens once the box holds an item.
            Packagings = box.Lines.Count > 0 ? await hampers.PackagingOptionsAsync(cancellationToken) : [],
        };
    }

    // The visitor's hamper when its items can be chosen, with its box; null otherwise.
    private async Task<(Hamper Hamper, Box Box)?> OpenBoxAsync(CancellationToken cancellationToken)
    {
        var hamper = await hampers.FindAsync(Request.Cookies[HamperService.CookieName], cancellationToken);
        return hamper is not null && ItemsOpen(hamper) ? (hamper, await hampers.BoxAsync(hamper, cancellationToken)) : null;
    }

    // Step 3 (F33): the packaging radios. With scripts, builder.ts sends the form whenever the choice changes; without,
    // a "Lưu" button does. "Tiếp tục" (F24) is a button of this same form with next=review, so continuing always saves
    // the choice on screen first. Nothing is saved while the box is empty (the step is locked then).
    [HttpPost]
    public async Task<IActionResult> Packaging(string? packaging, string? next, CancellationToken cancellationToken)
    {
        var open = await OpenBoxAsync(cancellationToken);
        if (open is not { } o || o.Box.Lines.Count == 0)
        {
            return Redirect(SiteLinks.Builder.Href);
        }

        if (!string.IsNullOrEmpty(packaging) && !await hampers.SetPackagingAsync(o.Hamper, packaging, cancellationToken))
        {
            return NotFound();
        }

        return Redirect(next == "review" ? SiteLinks.ReviewPath : SiteLinks.Builder.Href + "#dong-goi");
    }

    // Step 4 (F34 to F36): the hamper read-only, with links back to change it (decision 2026-11-15). Without a hamper
    // whose items can be chosen, or with an empty box, there is nothing to review: back to the builder.
    [HttpGet]
    [HttpHead]
    public async Task<IActionResult> Review(CancellationToken cancellationToken)
    {
        var open = await OpenBoxAsync(cancellationToken);
        if (open is not { } o || o.Box.Lines.Count == 0)
        {
            return Redirect(SiteLinks.Builder.Href);
        }

        return View(o.Box);
    }

    // "Thêm", − and + on a card, in "Hộp của bạn" or on the item page (F20, F28), and "Bỏ món này" on the review page
    // (F35). The form sends the item and the new amount; the address keeps the filters, so the visitor comes back to the
    // same list, at the same card (post, then redirect). From the item page (from=item) the visitor goes back to that
    // item, from the review page (from=review) to the review page: the address is built here, never taken from the
    // request, so it cannot send anyone elsewhere. The amount is checked again here: a
    // button the page showed as off, or a made-up request, changes nothing.
    [HttpPost]
    public async Task<IActionResult> Quantity(string? slug, int quantity, string? category, string? q, string? sort, string? from, bool inDate = true, CancellationToken cancellationToken = default)
    {
        var hamper = await hampers.FindAsync(Request.Cookies[HamperService.CookieName], cancellationToken);
        if (hamper is not null && ItemsOpen(hamper) && !string.IsNullOrEmpty(slug)
            && await hampers.SetQuantityAsync(hamper, slug, quantity, cancellationToken) == QuantityChange.UnknownItem)
        {
            return NotFound();
        }

        if (from == "item" && !string.IsNullOrEmpty(slug))
        {
            return Redirect(SiteLinks.Item(slug) + "#them");
        }

        // "Bỏ món này" on the review page or in the cart (F35, F37). An emptied box sends the review page on to the
        // builder, and shows the empty cart.
        if (from == "review")
        {
            return Redirect(SiteLinks.ReviewPath);
        }

        if (from == "cart")
        {
            return Redirect(SiteLinks.Cart.Href);
        }

        var back = new Dictionary<string, string?> { ["category"] = category, ["q"] = q, ["sort"] = sort, ["inDate"] = inDate ? null : "false" }
            .Where(p => !string.IsNullOrEmpty(p.Value));
        var anchor = string.IsNullOrEmpty(slug) ? "#chon-mon" : "#mon-" + Uri.EscapeDataString(slug);
        return Redirect(QueryHelpers.AddQueryString(SiteLinks.Builder.Href, back) + anchor);
    }

    // "Lưu" and the quick amounts both end here. A wrong value saves nothing: the page comes back with a warning
    // under the field and what the visitor typed. A good one is saved, and the visitor goes back to /builder (post, then redirect).
    [HttpPost]
    public async Task<IActionResult> Index(BuilderForm form, CancellationToken cancellationToken)
    {
        // A quick amount button sends its own amount; it wins over what is typed in the field.
        var budgetText = string.IsNullOrWhiteSpace(form.QuickAmount) ? form.Budget : form.QuickAmount;
        var budgetProblem = limits.ParseBudget(budgetText, out var budget);
        var dateProblem = limits.ParseDate(form.DeliveryDate, out var date);

        if (budgetProblem != BudgetProblem.None || dateProblem != DateProblem.None)
        {
            // Nothing was saved, so the items below still follow the date saved earlier (if any).
            var saved = await hampers.FindAsync(Request.Cookies[HamperService.CookieName], cancellationToken);
            return View(await WithItemsAsync(saved, new BuilderViewModel
            {
                BudgetText = budgetProblem == BudgetProblem.None ? Vnd.Format(budget) : budgetText ?? "",
                DeliveryDateText = form.DeliveryDate ?? "",
                BudgetProblem = budgetProblem,
                DateProblem = dateProblem,
            }, new ItemFilter(), cancellationToken));
        }

        var hamper = await hampers.SaveAsync(Request.Cookies[HamperService.CookieName], budget, date, cancellationToken);
        Response.Cookies.Append(HamperService.CookieName, hamper.Token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            MaxAge = HamperService.CookieLifetime,
            IsEssential = true,   // no consent banner: it only holds a random code
        });
        return Redirect(SiteLinks.Builder.Href);   // /builder, written once in SiteLinks
    }
}

// The fields of the budget card, as the browser sends them. Text on purpose: BuilderLimits reads it, so a bad value is a warning, not an error page.
public sealed class BuilderForm
{
    public string? Budget { get; set; }

    public string? DeliveryDate { get; set; }

    public string? QuickAmount { get; set; }
}
