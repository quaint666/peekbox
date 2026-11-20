using Microsoft.AspNetCore.Mvc;
using Peekbox.Web.Services;

namespace Peekbox.Web.ViewComponents;

// The season banner above the header (F05), on the pages that show it. It writes nothing when no banner is on.
public sealed class SeasonBannerViewComponent(CurrentBanner current) : ViewComponent
{
    public IViewComponentResult Invoke() =>
        current.Active is { } banner ? View(banner) : Content(string.Empty);
}
