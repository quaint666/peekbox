using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Data;
using Peekbox.Web.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

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

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

// No HTTPS redirection and no HSTS here: the site is served behind a proxy
// that handles HTTPS.

// First, so that every response carries the policy, including static files and errors.
app.Use(async (context, next) =>
{
    context.Response.Headers.Append(ContentSecurityPolicy.HeaderName, ContentSecurityPolicy.Value);
    await next();
});

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

// Lets the tests start the whole app (WebApplicationFactory<Program>).
public partial class Program;
