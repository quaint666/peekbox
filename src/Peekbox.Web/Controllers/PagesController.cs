using Microsoft.AspNetCore.Mvc;
using Peekbox.Web.Content;
using Peekbox.Web.Navigation;

namespace Peekbox.Web.Controllers;

// The pages of plain text: About and commitments (F54) and the terms and privacy policy (F55). The text is read once, at
// start (Content/AboutContent.cs, Content/LegalContent.cs).
public class PagesController(AboutContent about, LegalContent legal) : Controller
{
    [HttpGet(SiteLinks.AboutPath)]
    [HttpHead(SiteLinks.AboutPath)]
    public IActionResult About()
    {
        // "Giới thiệu" is the page you are on. "Cam kết" opens the same page and jumps to its heading (#cam-ket); the menu
        // does not mark two links at once.
        ViewData["Nav"] = SiteLinks.About.Key;
        return View(about);
    }

    [HttpGet(SiteLinks.LegalPath)]
    [HttpHead(SiteLinks.LegalPath)]
    public IActionResult Legal() => View(legal);
}
