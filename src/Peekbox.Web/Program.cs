using Peekbox.Web.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

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
