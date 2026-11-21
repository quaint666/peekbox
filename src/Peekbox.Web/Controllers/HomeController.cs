using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Peekbox.Web.Content;
using Peekbox.Web.Models;
using Peekbox.Web.Navigation;
using Peekbox.Web.Services;

namespace Peekbox.Web.Controllers;

public class HomeController(HomeContent content, ReadyHampers hampers) : Controller
{
    [HttpGet(SiteLinks.HomePath)]
    [HttpHead(SiteLinks.HomePath)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(new HomeViewModel(content, await hampers.SummariesAsync(cancellationToken)));

    // The "page not found" page (F09). The status-code middleware runs it again for an unknown address, and it keeps the 404.
    // Other empty answers (a 400 from the form token, a 405) run it too; they stay as they were, with no page. Opened
    // directly it is a 404 as well: the middleware runs it once more.
    [Route(SiteLinks.NotFoundPath)]
    [IgnoreAntiforgeryToken]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult NotFoundPage() =>
        Response.StatusCode is StatusCodes.Status404NotFound or StatusCodes.Status200OK ? Page() : new EmptyResult();

    private ViewResult Page()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("NotFound");
    }

    // The page after an unexpected error (Production; Development shows the framework's details instead). The
    // exception handler runs it again with the request's own method, so it takes any method and skips the form token.
    // Opened directly there is no error to show: a 404, not a 200 page that says something went wrong.
    [Route(SiteLinks.ErrorPath)]
    [IgnoreAntiforgeryToken]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => HttpContext.Features.Get<IExceptionHandlerPathFeature>() is null ? NotFound() : View();
}
