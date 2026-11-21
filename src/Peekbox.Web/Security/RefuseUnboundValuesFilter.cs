using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Peekbox.Web.Security;

// A value our own pages cannot send (a quantity of "abc", an inDate of "maybe", a payment of "Bitcoin") is refused with a
// 400 before the action runs. A plain controller would run the action anyway with the value bound as its default (0, true,
// the first member of an enum), and the action would then act on something the visitor never typed. It is global, as the
// anti-forgery filter is (Program.cs), so a new action cannot forget it and every address answers the same way.
public sealed class RefuseUnboundValuesFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            context.Result = new BadRequestResult();
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
