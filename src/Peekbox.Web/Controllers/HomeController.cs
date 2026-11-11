using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Data;
using Peekbox.Web.Models;

namespace Peekbox.Web.Controllers;

public class HomeController(PeekboxDbContext db) : Controller
{
    public async Task<IActionResult> Index() => View(new HomeViewModel(await db.Products.CountAsync()));

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();
}
