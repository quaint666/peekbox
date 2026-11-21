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

    // The page for an answer with no body, which the status-code middleware runs again with the answer's own status.
    // 404 (an unknown address): "page not found" (F09). 400 (a form whose token expired, for instance after a deploy, or a
    // value that cannot be read): "request not valid", which says to reload and try again. Any other empty answer (a 405)
    // stays as it was, with no page. Opened directly it is a 404: the middleware runs it with a 200, which is not an error.
    [Route(SiteLinks.NotFoundPath)]
    [IgnoreAntiforgeryToken]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult NotFoundPage() => Response.StatusCode switch
    {
        StatusCodes.Status404NotFound or StatusCodes.Status200OK => Page("NotFound", StatusCodes.Status404NotFound),
        StatusCodes.Status400BadRequest => Page("BadRequest", StatusCodes.Status400BadRequest),
        StatusCodes.Status429TooManyRequests => Page("TooManyRequests", StatusCodes.Status429TooManyRequests),
        _ => new EmptyResult(),
    };

    private ViewResult Page(string view, int status)
    {
        Response.StatusCode = status;
        return View(view);
    }

    // The page after an unexpected error (Production; Development shows the framework's details instead). The
    // exception handler runs it again with the request's own method, so it takes any method and skips the form token.
    // Opened directly there is no error to show: a 404, not a 200 page that says something went wrong.
    [Route(SiteLinks.ErrorPath)]
    [IgnoreAntiforgeryToken]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => HttpContext.Features.Get<IExceptionHandlerPathFeature>() is null ? NotFound() : View();
}
