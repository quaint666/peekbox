using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Peekbox.Web.Formatting;
using Peekbox.Web.Models;
using Peekbox.Web.Navigation;
using Peekbox.Web.Services;

namespace Peekbox.Web.Controllers;

public class BuilderController(BuilderLimits limits, HamperService hampers) : Controller
{
    // The page of the hamper builder (/builder). It shows the saved budget and date, found by the cookie.
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var hamper = await hampers.FindAsync(Request.Cookies[HamperService.CookieName], cancellationToken);
        if (hamper is null)
        {
            return View(new BuilderViewModel());
        }

        return View(new BuilderViewModel
        {
            BudgetText = Vnd.Format(hamper.BudgetVnd),
            DeliveryDateText = hamper.DeliveryDate?.ToString(BuilderLimits.DateFormat, CultureInfo.InvariantCulture) ?? "",
        });
    }

    // "Cập nhật" and the quick amounts both end here. A wrong value saves nothing: the page comes back with a warning
    // under the field and what the visitor typed. A good one is saved, and the visitor goes back to /builder (post, then redirect).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(BuilderForm form, CancellationToken cancellationToken)
    {
        // A quick amount button sends its own amount; it wins over what is typed in the field.
        var budgetText = string.IsNullOrWhiteSpace(form.QuickAmount) ? form.Budget : form.QuickAmount;
        var budgetProblem = limits.ParseBudget(budgetText, out var budget);
        var dateProblem = limits.ParseDate(form.DeliveryDate, out var date);

        if (budgetProblem != BudgetProblem.None || dateProblem != DateProblem.None)
        {
            return View(new BuilderViewModel
            {
                BudgetText = budgetProblem == BudgetProblem.None ? Vnd.Format(budget) : budgetText ?? "",
                DeliveryDateText = form.DeliveryDate ?? "",
                BudgetProblem = budgetProblem,
                DateProblem = dateProblem,
            });
        }

        var hamper = await hampers.SaveAsync(Request.Cookies[HamperService.CookieName], budget, date, cancellationToken);
        Response.Cookies.Append(HamperService.CookieName, hamper.Token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            MaxAge = HamperService.CookieLifetime,
            IsEssential = true,   // no consent banner: it only holds a random code
        });
        return Redirect(SiteLinks.Builder.Href);   // /builder, written once in SiteLinks
    }
}

// The fields of the budget card, as the browser sends them. Text on purpose: BuilderLimits reads it, so a bad value is a warning, not an error page.
public sealed class BuilderForm
{
    public string? Budget { get; set; }

    public string? DeliveryDate { get; set; }

    public string? QuickAmount { get; set; }
}
