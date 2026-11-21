using Microsoft.AspNetCore.Mvc;
using Peekbox.Web.Services;

namespace Peekbox.Web.ViewComponents;

// F13: on the start page, a visitor who left a hamper with items in it is offered "Tiếp tục" or "Làm lại" (use case UC13).
// The hamper is the row under their cookie (decision 2026-11-11); with no cookie, an empty box or an ordered hamper there is
// nothing to offer and nothing is drawn.
public sealed class ResumeBannerViewComponent(HamperService hampers) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var hamper = await hampers.FindAsync(HamperCookie.Read(Request), HttpContext.RequestAborted);
        if (hamper is null)
        {
            return Content(string.Empty);
        }

        var box = await hampers.BoxAsync(hamper, HttpContext.RequestAborted);
        return box.Count == 0 ? Content(string.Empty) : View(new ResumeBanner(box.Count, box.Quote.TotalVnd));
    }
}

public sealed record ResumeBanner(int Count, long TotalVnd);
