using Microsoft.AspNetCore.Mvc;
using Peekbox.Web.Models;

namespace Peekbox.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View(new HomeViewModel());

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();
}
