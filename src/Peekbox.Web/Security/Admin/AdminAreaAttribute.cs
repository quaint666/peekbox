using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;

namespace Peekbox.Web.Security.Admin;

// Every admin page, the sign-in page included: never kept by any cache (a shared one must not hand the next person a page
// with someone's orders on it) and never listed by a search engine.
[AttributeUsage(AttributeTargets.Class)]
public sealed class AdminAreaAttribute : ResultFilterAttribute
{
    public override void OnResultExecuting(ResultExecutingContext context)
    {
        var headers = context.HttpContext.Response.Headers;
        // The same two values the anti-forgery layer sets on a page with a form (and logs a warning when another value is there).
        headers[HeaderNames.CacheControl] = "no-cache, no-store";
        headers[HeaderNames.Pragma] = "no-cache";
        headers["X-Robots-Tag"] = "noindex, nofollow";
    }
}
