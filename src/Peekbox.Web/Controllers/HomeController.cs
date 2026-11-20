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

    // The page after an unexpected error (Production; Development shows the framework's details instead). The
    // exception handler runs it again with the request's own method, so it takes any method and skips the form token.
    // Opened directly there is no error to show: a 404, not a 200 page that says something went wrong.
    [Route(SiteLinks.ErrorPath)]
    [IgnoreAntiforgeryToken]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => HttpContext.Features.Get<IExceptionHandlerPathFeature>() is null ? NotFound() : View();
}
