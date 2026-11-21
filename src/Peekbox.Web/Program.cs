using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Content;
using Peekbox.Web.Data;
using Peekbox.Web.Navigation;
using Peekbox.Web.Security;
using Peekbox.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Every POST, PUT, PATCH and DELETE must carry the form's anti-forgery token, or the request gets a 400; GET and HEAD
// are never checked. A future payment callback, which cannot send the token, opts out with [IgnoreAntiforgeryToken].
builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

// Development already runs these checks; this turns them on in every environment. A scoped service (the DbContext,
// HamperService) taken by a singleton then stops the app at start, instead of one object being shared by all visitors.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

// Page text from content/data/: read once at start; a missing or broken file stops the app at once.
builder.Services.AddSingleton(HomeContent.Load(HomeContent.FilePath));
builder.Services.AddSingleton(BlogPosts.Load(BlogPosts.FolderPath));

// The clock is passed in (not read directly), so the tests can pick any day. See Services/BuilderLimits.cs.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<BuilderLimits>();
builder.Services.AddScoped<HamperService>();
builder.Services.AddScoped<ItemCatalog>();
builder.Services.AddScoped<ReadyHampers>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddSingleton<RateLimits>();

// The season banner (Banner:Occasion names the occasion whose banner shows, empty for none). See Services/SeasonBanner.cs.
builder.Services.AddSingleton(new CurrentBanner(SeasonBanner.Load(builder.Configuration["Banner:Occasion"], SeedData.HampersFilePath)));

// Demo mode (Demo:Enabled): the site runs for people to look at, and the checkout takes no order. See Services/DemoMode.cs.
builder.Services.AddSingleton(new DemoMode(builder.Configuration.GetValue<bool>("Demo:Enabled")));

// Behind a proxy chain (Cloudflare, cloudflared, nginx) the visitor's address and "https" arrive in X-Forwarded-*.
// ForwardedHeaders:Hops is how many proxies sit in front of the app (the staging chain is 2: nginx, then cloudflared,
// as Bloomy's "trust proxy 2"). It is off (0) by default, so a copy on a laptop never believes a header a visitor
// sends. When it is on, the app must be reachable only through the proxy (a loopback port): the callers are not
// checked, the number of hops is what picks the visitor's address out of the header.
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

// The connection string is a secret, so it is never written in a file of the
// project. See docs/frontend-plan.md, section 2, for how to set it.
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("The connection string 'Default' is missing. See docs/frontend-plan.md, section 2.");

// UseSeeding runs when the database is created or updated (dotnet ef database
// update, a migration bundle). The tools only call this synchronous version.
builder.Services.AddDbContext<PeekboxDbContext>(options => options
    .UseNpgsql(connectionString)
    .UseSeeding((context, _) => SeedData.Run(context)));

var app = builder.Build();

// First of all, so that everything after it (the rate limits, the cookie's Secure flag) sees the visitor's address
// and "https".
if (proxyHops > 0)
{
    app.UseForwardedHeaders();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(SiteLinks.ErrorPath);
}

// No HTTPS redirection and no HSTS here: the site is served behind a proxy
// that handles HTTPS.

// First, so that every response carries the headers, including static files and errors.
app.Use(async (context, next) =>
{
    context.Response.Headers.Append(ContentSecurityPolicy.HeaderName, ContentSecurityPolicy.Value);
    context.Response.Headers.Append(ReferrerPolicy.HeaderName, ReferrerPolicy.Value);
    await next();
});

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

// For the container's health check and the proxy's: 200 when the database answers, 503 when it does not.
app.MapGet(SiteLinks.HealthPath, async (PeekboxDbContext db, CancellationToken cancellationToken) =>
    await db.Database.CanConnectAsync(cancellationToken) ? Results.Text("ok") : Results.Text("database unavailable", statusCode: StatusCodes.Status503ServiceUnavailable));

// Every action names its own address (SiteLinks), so each page has exactly one. There is no
// "{controller}/{action}/{id?}" route: it also answered /Home/Error, /Track/Index and /Cart/Index/x.
app.MapControllers()
    .WithStaticAssets();

app.Run();

// Lets the tests start the whole app (WebApplicationFactory<Program>).
public partial class Program;
