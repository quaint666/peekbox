namespace Peekbox.Web.Services;

// The cookie that holds a visitor's hamper token: its name, how long it lasts, how it is read and how it is written,
// in one place. It holds only a long random code (SecretToken); the hamper itself is in the database.
public static class HamperCookie
{
    public const string Name = "peekbox_hamper";

    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(180);

    // The token the browser sent, or null. Not checked here: HamperService and OrderService check every token they use.
    public static string? Read(HttpRequest request) => request.Cookies[Name];

    // Scripts cannot read it (HttpOnly) and other sites' forms do not send it (Lax). Secure only over HTTPS: behind a
    // proxy that ends HTTPS this needs the forwarded headers first (a deployment item, not set up yet).
    public static void Write(HttpResponse response, string token) => response.Cookies.Append(Name, token, new CookieOptions
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        Secure = response.HttpContext.Request.IsHttps,
        MaxAge = Lifetime,
        IsEssential = true,   // no consent banner: it only holds a random code
    });
}
