using Microsoft.AspNetCore.Mvc;
using Peekbox.Web.Content;
using Peekbox.Web.Models;

namespace Peekbox.Web.Controllers;

public class HomeController(HomeContent content) : Controller
{
    public IActionResult Index() => View(new HomeViewModel(content));

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();
}
