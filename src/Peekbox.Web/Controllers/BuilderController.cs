using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Peekbox.Web.Data;
using Peekbox.Web.Formatting;
using Peekbox.Web.Models;
using Peekbox.Web.Navigation;
using Peekbox.Web.Services;

namespace Peekbox.Web.Controllers;

public class BuilderController(BuilderLimits limits, HamperService hampers, ItemCatalog catalog, ReadyHampers readyHampers) : Controller
{
    // The page of the hamper builder (/builder). It shows the saved budget and date, found by the cookie, and once both
    // are saved, the items. The filter form sends the category, the "only items in date" checkbox, the search words (q)
    // and the sort in the address. HEAD as well, as link checkers and uptime monitors ask (the home page already answers it).
    // chon is an amount picked from "Chọn nhanh" (one of BudgetTiers.All; anything else is ignored): it fills the field
    // and nothing is saved. dip is the occasion of "Hộp gợi ý".
    [HttpGet(SiteLinks.BuilderPath)]
    [HttpHead(SiteLinks.BuilderPath)]
    public async Task<IActionResult> Index(string? category, string? q, string? sort, string? chon, string? dip, bool inDate = true, bool fit = false, CancellationToken cancellationToken = default)
    {
        var picked = BudgetTiers.All.Cast<long?>().FirstOrDefault(t => t.ToString() == chon);
        var used = TempData[OccasionsController.UsedHamperKey] is string json ? System.Text.Json.JsonSerializer.Deserialize<UsedHamper>(json) : null;
        var hamper = await hampers.FindAsync(HamperCookie.Read(Request), cancellationToken);
        if (hamper is null)
        {
            return View(new BuilderViewModel { BudgetText = picked is { } p ? Vnd.Format(p) : "", PickedBudgetVnd = picked, UsedHamper = used });
        }

        var model = await WithItemsAsync(hamper, new BuilderViewModel
        {
            BudgetText = Vnd.Format(picked ?? hamper.BudgetVnd),
            SavedBudgetVnd = hamper.BudgetVnd,
            PickedBudgetVnd = picked == hamper.BudgetVnd ? null : picked,
            DeliveryDateText = hamper.DeliveryDate?.ToString(BuilderLimits.DateFormat, CultureInfo.InvariantCulture) ?? "",
            // The cookie lasts 180 days, so a date saved earlier can be today or in the past by now. It shows the same
            // warning as typing such a date, and the items stay locked until a new date is saved (decision 2026-11-16).
            DateProblem = hamper.DeliveryDate < limits.Earliest ? DateProblem.TooEarly : DateProblem.None,
            UsedHamper = used,
        }, new ItemFilter(category, inDate, q, sort, fit), cancellationToken);
        return View(model.Box is null ? model : model with { Suggestions = await SuggestionsAsync(model.Box.BudgetVnd, dip, cancellationToken) });
    }

    // "Hộp gợi ý": the hampers of one occasion (the first when dip is not one) at the highest of its budgets that fits the
    // saved one, so 5 triệu on 20/11 (which stops at 2 triệu) shows the 2 triệu hampers. Null under the lowest budget.
    private async Task<Suggestions?> SuggestionsAsync(long budgetVnd, string? dip, CancellationToken cancellationToken)
    {
        var occasions = await readyHampers.OccasionsAsync(cancellationToken);
        if (occasions.Count == 0 || budgetVnd < BudgetTiers.All[0])
        {
            return null;
        }

        var chosen = occasions.FirstOrDefault(o => o.Id == dip) ?? occasions[0];
        var group = (await readyHampers.PageAsync(chosen.Id, cancellationToken))?.Groups.LastOrDefault(g => g.BudgetVnd <= budgetVnd);
        return group is null ? null : new Suggestions(occasions, chosen, group.BudgetVnd, group.Hampers);
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
        var hamper = await hampers.FindAsync(HamperCookie.Read(Request), cancellationToken);
        return hamper is not null && ItemsOpen(hamper) ? (hamper, await hampers.BoxAsync(hamper, cancellationToken)) : null;
    }

    // Step 3 (F33): the packaging radios. With scripts, builder.ts sends the form whenever the choice changes; without,
    // a "Lưu" button does. "Tiếp tục" (F24) is a button of this same form with next=review, so continuing always saves
    // the choice on screen first. Nothing is saved while the box is empty (the step is locked then).
    [HttpPost(SiteLinks.PackagingPath)]
    public async Task<IActionResult> Packaging(string? packaging, ReturnTo next, CancellationToken cancellationToken)
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

        return Redirect(next == ReturnTo.Review ? SiteLinks.ReviewPath : SiteLinks.Builder.Href + Anchors.Link(Anchors.Packaging));
    }

    // Step 4 (F34 to F36): the hamper read-only, with links back to change it (decision 2026-11-15). Without a hamper
    // whose items can be chosen, or with an empty box, there is nothing to review: back to the builder.
    [HttpGet(SiteLinks.ReviewPath)]
    [HttpHead(SiteLinks.ReviewPath)]
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
    // button the page showed as off, or a made-up request, changes nothing. An amount that is not a number is refused:
    // bound as 0, it would take the item out.
    [HttpPost(SiteLinks.QuantityPath)]
    public async Task<IActionResult> Quantity(string? slug, int quantity, string? category, string? q, string? sort, ReturnTo from = ReturnTo.Builder, bool inDate = true, bool fit = false, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest();
        }

        var hamper = await hampers.FindAsync(HamperCookie.Read(Request), cancellationToken);
        if (hamper is not null && ItemsOpen(hamper) && !string.IsNullOrEmpty(slug)
            && await hampers.SetQuantityAsync(hamper, slug, quantity, cancellationToken) == QuantityChange.UnknownItem)
        {
            return NotFound();
        }

        if (from == ReturnTo.Item && !string.IsNullOrEmpty(slug))
        {
            return Redirect(SiteLinks.Item(slug) + Anchors.Link(Anchors.ItemBuy));
        }

        // "Bỏ món này" on the review page or in the cart (F35, F37). An emptied box sends the review page on to the
        // builder, and shows the empty cart.
        if (from == ReturnTo.Review)
        {
            return Redirect(SiteLinks.ReviewPath);
        }

        if (from == ReturnTo.Cart)
        {
            return Redirect(SiteLinks.Cart.Href);
        }

        var back = new Dictionary<string, string?> { ["category"] = category, ["q"] = q, ["sort"] = sort, ["inDate"] = inDate ? null : "false", ["fit"] = fit ? "true" : null }
            .Where(p => !string.IsNullOrEmpty(p.Value));
        var anchor = Anchors.Link(string.IsNullOrEmpty(slug) ? Anchors.Items : Anchors.ItemCard(slug));
        return Redirect(QueryHelpers.AddQueryString(SiteLinks.Builder.Href, back) + anchor);
    }

    // "Lưu" ends here ("Chọn nhanh" only fills the field). A wrong value saves nothing: the page comes back with a warning
    // under the field and what the visitor typed. A good one is saved, and the visitor goes back to /builder (post, then redirect).
    [HttpPost(SiteLinks.BuilderPath)]
    public async Task<IActionResult> Index(BuilderForm form, CancellationToken cancellationToken)
    {
        var budgetText = form.Budget;
        var budgetProblem = limits.ParseBudget(budgetText, out var budget);
        var dateProblem = limits.ParseDate(form.DeliveryDate, out var date);

        if (budgetProblem != BudgetProblem.None || dateProblem != DateProblem.None)
        {
            // Nothing was saved, so the items below still follow the date saved earlier (if any).
            var saved = await hampers.FindAsync(HamperCookie.Read(Request), cancellationToken);
            return View(await WithItemsAsync(saved, new BuilderViewModel
            {
                BudgetText = budgetProblem == BudgetProblem.None ? Vnd.Format(budget) : budgetText ?? "",
                SavedBudgetVnd = saved?.BudgetVnd,
                DeliveryDateText = form.DeliveryDate ?? "",
                BudgetProblem = budgetProblem,
                DateProblem = dateProblem,
            }, new ItemFilter(), cancellationToken));
        }

        var hamper = await hampers.SaveAsync(HamperCookie.Read(Request), budget, date, cancellationToken);
        HamperCookie.Write(Response, hamper.Token);
        return Redirect(SiteLinks.Builder.Href);   // /builder, written once in SiteLinks
    }
}
