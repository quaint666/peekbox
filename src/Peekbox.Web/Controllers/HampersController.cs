using Microsoft.AspNetCore.Mvc;
using Peekbox.Web.Models;
using Peekbox.Web.Navigation;
using Peekbox.Web.Services;

namespace Peekbox.Web.Controllers;

public class HampersController(ReadyHampers readyHampers, HamperService hampers) : Controller
{
    // How many other hampers of the same occasion the page offers at the bottom.
    private const int RelatedCount = 6;

    // The page of one ready hamper (/hop/an-khang): its photo, price and every item (F15). "from" is the occasion the visitor
    // came from, or "builder"; anything else, or an occasion the hamper is not in, falls back to the hamper's first occasion.
    [HttpGet(SiteLinks.HampersPath + "/{slug}")]
    [HttpHead(SiteLinks.HampersPath + "/{slug}")]
    public async Task<IActionResult> Index(string slug, string? from, CancellationToken cancellationToken)
    {
        var card = await readyHampers.FindAsync(slug, cancellationToken);
        if (card is null)
        {
            return NotFound();
        }

        var fromBuilder = from == OccasionsController.FromBuilder;
        var occasionId = from is not null && card.OccasionIds.Contains(from) ? from : card.OccasionIds[0];
        var page = await readyHampers.PageAsync(occasionId, cancellationToken);
        if (page is null)
        {
            return NotFound();
        }

        // The nearest budgets first, so the hampers a visitor might choose between are the first ones in the row.
        var related = page.Groups.SelectMany(g => g.Hampers)
            .Where(h => h.Slug != slug)
            .OrderBy(h => Math.Abs(h.BudgetVnd - card.BudgetVnd))
            .ThenBy(h => h.BudgetVnd)
            .Take(RelatedCount)
            .ToList();
        var hamper = await hampers.FindAsync(HamperCookie.Read(Request), cancellationToken);
        var box = hamper is null ? null : await hampers.BoxAsync(hamper, cancellationToken);
        return View(new HamperViewModel(card, page.Occasion, related, box, fromBuilder));
    }
}
