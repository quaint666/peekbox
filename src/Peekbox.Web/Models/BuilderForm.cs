namespace Peekbox.Web.Models;

// The fields of the budget card, as the browser sends them. Text on purpose: BuilderLimits reads it, so a bad value is a
// warning, not an error page.
public sealed class BuilderForm
{
    public string? Budget { get; set; }

    public string? DeliveryDate { get; set; }

    public string? QuickAmount { get; set; }
}
