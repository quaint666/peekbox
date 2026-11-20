using Microsoft.AspNetCore.Mvc;
using Peekbox.Web.Models;
using Peekbox.Web.Navigation;
using Peekbox.Web.Services;

namespace Peekbox.Web.Controllers;

public class OccasionsController(ReadyHampers readyHampers, HamperService hampers, BuilderLimits limits) : Controller
{
    // The "from" of the builder's "Hộp gợi ý" (an occasion id otherwise).
    public const string FromBuilder = "builder";

    // One occasion page (/dip/tet): the hampers Peekbox put together for it, by budget (F15).
    [HttpGet(SiteLinks.OccasionsPath + "/{id}")]
    [HttpHead(SiteLinks.OccasionsPath + "/{id}")]
    public async Task<IActionResult> Index(string id, CancellationToken cancellationToken)
    {
        var page = await readyHampers.PageAsync(id, cancellationToken);
        if (page is null)
        {
            return NotFound();
        }

        var others = (await readyHampers.OccasionsAsync(cancellationToken)).Where(o => o.Id != id).ToList();
        var hamper = await hampers.FindAsync(HamperCookie.Read(Request), cancellationToken);
        var box = hamper is null ? null : await hampers.BoxAsync(hamper, cancellationToken);
        return View(new OccasionViewModel(page, others, box));
    }

    // "Dùng hộp này" (F15). Nothing is replaced without the visitor saying so: when the box already holds items and the
    // form did not come from "Thay hộp" (replace=true), or the hamper has an item that is short, the visitor goes back to
    // the card, where the page now asks or says why. The way back is built from the hamper's own occasions, never taken
    // from the request as an address.
    [HttpPost(SiteLinks.UseHamperPath)]
    public async Task<IActionResult> Use(string? slug, string? from, bool replace = false, bool detail = false, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest();
        }

        var card = string.IsNullOrEmpty(slug) ? null : await readyHampers.FindAsync(slug, cancellationToken);
        if (card is null)
        {
            return NotFound();
        }

        // From the builder's "Hộp gợi ý" (from=builder) the visitor goes back there; from an occasion page, to the card;
        // from the hamper's own page (detail), to that page with the same "from".
        var known = from == FromBuilder || card.OccasionIds.Contains(from);
        var back = detail
            ? SiteLinks.Hamper(card.Slug, known ? from : null)
            : from == FromBuilder
                ? SiteLinks.Builder.Href + Anchors.Link(Anchors.Suggestions)
                : SiteLinks.Occasion(card.OccasionIds.Contains(from) ? from! : card.OccasionIds[0]) + Anchors.Link(Anchors.HamperCard(card.Slug));
        var hamper = await hampers.FindAsync(HamperCookie.Read(Request), cancellationToken);
        var hasItems = hamper is not null && (await hampers.BoxAsync(hamper, cancellationToken)).Lines.Count > 0;
        if (card.SoldOut.Count > 0 || (hasItems && !replace))
        {
            return Redirect(back);
        }

        hamper = await hampers.UseReadyHamperAsync(hamper, card, cancellationToken);
        HamperCookie.Write(Response, hamper.Token);
        // With a date that can be delivered the visitor lands on the items; without one, on step 1 to pick it.
        return Redirect(limits.ItemsOpen(hamper.DeliveryDate) ? SiteLinks.Builder.Href + Anchors.Link(Anchors.Items) : SiteLinks.Builder.Href);
    }
}
