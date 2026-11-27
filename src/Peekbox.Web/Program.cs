using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using Peekbox.Web.Content;
using Peekbox.Web.Data;
using Peekbox.Web.Navigation;
using Peekbox.Web.Security;
using Peekbox.Web.Security.Admin;
using Peekbox.Web.Services;
using Peekbox.Web.Services.Email;

var builder = WebApplication.CreateBuilder(args);

// Every POST, PUT, PATCH and DELETE without the anti-forgery token is a 400. A payment callback opts out with
// [IgnoreAntiforgeryToken].
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    // A value that cannot be bound (a quantity of "abc") is a 400 before any action runs; see the filter.
    options.Filters.Add<RefuseUnboundValuesFilter>();
});

// No "Server: Kestrel" header: it only helps someone looking for a known hole.
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

// Scope checks in every environment, not only Development: a singleton holding a scoped service (the DbContext) stops
// the app at start instead of sharing one object between all visitors.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

// Page text from content/data/, read once at start; a missing or broken file stops the app.
builder.Services.AddSingleton(HomeContent.Load(HomeContent.FilePath));
builder.Services.AddSingleton(BlogPosts.Load(BlogPosts.FolderPath));
builder.Services.AddSingleton(AboutContent.Load(AboutContent.FilePath));
builder.Services.AddSingleton(LegalContent.Load(LegalContent.FolderPath));

// The clock is passed in (not read directly), so the tests can pick any day. See Services/BuilderLimits.cs.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<BuilderLimits>();
builder.Services.AddScoped<HamperService>();
builder.Services.AddScoped<ItemCatalog>();
builder.Services.AddScoped<ReadyHampers>();
builder.Services.AddSingleton(PaymentStandIn.FromConfiguration(builder.Configuration));
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<AdminOrders>();
builder.Services.AddSingleton<IEmailSender, LogEmailSender>();
builder.Services.AddScoped<OrderMailer>();
builder.Services.AddSingleton(DeliveryLimit.FromConfiguration(builder.Configuration));
builder.Services.AddScoped<DeliveryCapacity>();
builder.Services.AddSingleton<RateLimits>();
builder.Services.AddSingleton(ShopInfo.FromConfiguration(builder.Configuration));
builder.Services.AddScoped<HamperRetention>();
builder.Services.AddHostedService<HamperPurgeService>();

// The season banner (Banner:Occasion names the occasion whose banner shows, empty for none). See Services/SeasonBanner.cs.
builder.Services.AddSingleton(new CurrentBanner(SeasonBanner.Load(builder.Configuration["Banner:Occasion"], SeedData.HampersFilePath)));

// Demo mode (Demo:Enabled): the site runs for people to look at, and the checkout takes no order. See Services/DemoMode.cs.
builder.Services.AddSingleton(new DemoMode(builder.Configuration.GetValue<bool>("Demo:Enabled")));

// Behind proxies the visitor's address and "https" arrive in X-Forwarded-*. ForwardedHeaders:Hops is how many proxies sit
// in front (staging: 2, nginx then cloudflared); 0, the default, believes no header. The callers are not checked, so when
// it is on the app must be reachable only through the proxy.
var proxyHops = builder.Configuration.GetValue<int?>("ForwardedHeaders:Hops") ?? 0;
if (proxyHops < 0)
{
    throw new InvalidOperationException("ForwardedHeaders:Hops must be 0 (no proxy) or a whole number above 0.");
}

if (proxyHops > 0)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = proxyHops;
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
    });
}

// A secret, never in a file of the project: see docs/frontend-plan.md, section 2.
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("The connection string 'Default' is missing. See docs/frontend-plan.md, section 2.");

// The anti-forgery and TempData keys live in the database, not in the container (a deploy would throw them away and a
// form opened before it would fail). The fixed name keeps the keys if the folder changes. Not encrypted: whoever can
// read this database already controls the shop.
builder.Services.AddDataProtection()
    .SetApplicationName("Peekbox")
    .PersistKeysToDbContext<PeekboxDbContext>();

// UseSeeding runs on database update (dotnet ef, the migration bundle); the tools call only this synchronous version.
builder.Services.AddDbContext<PeekboxDbContext>(options => options
    .UseNpgsql(connectionString)
    .UseSeeding((context, _) => SeedData.Run(context)));

// The admin's sign-in (F56, F69): see Security/Admin/AdminSignInExtensions.cs.
builder.Services.AddAdminSignIn(builder.Configuration);

var app = builder.Build();

// First, so the rate limits and the cookie's Secure flag see the visitor's address and "https".
if (proxyHops > 0)
{
    app.UseForwardedHeaders();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(SiteLinks.ErrorPath);
}

// An unknown address (or any answer with an empty body) runs the "not found" page again, with the original status code (F09).
app.UseStatusCodePagesWithReExecute(SiteLinks.NotFoundPath);

// No HTTPS redirection and no HSTS: the proxy handles HTTPS.

// Before everything else, so static files and errors get the headers too. Set, not added: the 404 page runs the
// pipeline a second time.
app.Use(async (context, next) =>
{
    context.Response.Headers[ContentSecurityPolicy.HeaderName] = ContentSecurityPolicy.Value;
    context.Response.Headers[ReferrerPolicy.HeaderName] = ReferrerPolicy.Value;
    context.Response.Headers[ContentTypeOptions.HeaderName] = ContentTypeOptions.Value;
    context.Response.Headers[PermissionsPolicy.HeaderName] = PermissionsPolicy.Value;
    // HTML that set no Cache-Control of its own is private (HtmlCaching). Checked when the response starts, after the
    // filters and the anti-forgery token have had their say.
    context.Response.OnStarting(() =>
    {
        if (!context.Response.Headers.ContainsKey(HeaderNames.CacheControl)
            && context.Response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true)
        {
            context.Response.Headers[HeaderNames.CacheControl] = HtmlCaching.Value;
        }

        return Task.CompletedTask;
    });
    await next();
});

app.UseRouting();

// After routing, so the sign-in cookie is read for the chosen endpoint; only [Authorize] actions ask for it.
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

// 200 when the database answers, 503 when not. HEAD too: uptime monitors use it (they got a 405 without it).
app.MapMethods(SiteLinks.HealthPath, ["GET", "HEAD"], async (PeekboxDbContext db, CancellationToken cancellationToken) =>
    await db.Database.CanConnectAsync(cancellationToken) ? Results.Text("ok") : Results.Text("database unavailable", statusCode: StatusCodes.Status503ServiceUnavailable));

// Every action names its own address (SiteLinks), so each page has exactly one. There is no
// "{controller}/{action}/{id?}" route: it also answered /Home/Error, /Track/Index and /Cart/Index/x.
app.MapControllers()
    .WithStaticAssets();

app.Run();

// Lets the tests start the whole app (WebApplicationFactory<Program>).
public partial class Program;
