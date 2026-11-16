using Microsoft.AspNetCore.Mvc;
using Peekbox.Web.Models;
using Peekbox.Web.Navigation;
using Peekbox.Web.Services;

namespace Peekbox.Web.Controllers;

public class ItemsController(BuilderLimits limits, HamperService hampers, ItemCatalog catalog) : Controller
{
    // The page of one item: /items/tra-sen (F26 to F28). An unknown slug is a 404. The add control posts to
    // /builder/quantity, which brings the visitor back here.
    [HttpGet(SiteLinks.ItemsPath + "/{slug}")]
    [HttpHead(SiteLinks.ItemsPath + "/{slug}")]
    public async Task<IActionResult> Index(string slug, CancellationToken cancellationToken)
    {
        var item = await catalog.FindAsync(slug, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        var hamper = await hampers.FindAsync(Request.Cookies[HamperService.CookieName], cancellationToken);
        var box = hamper is not null && limits.ItemsOpen(hamper.DeliveryDate) ? await hampers.BoxAsync(hamper, cancellationToken) : null;
        return View(new ItemViewModel(item, box));
    }
}
