using Microsoft.AspNetCore.Mvc;
using Peekbox.Web.Models;

namespace Peekbox.Web.Controllers;

public class BuilderController : Controller
{
    // The page of the hamper builder (/builder). For now it only shows the budget card; saving comes in the next PR.
    public IActionResult Index() => View(new BuilderViewModel());
}
