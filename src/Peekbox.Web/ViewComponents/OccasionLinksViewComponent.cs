using Microsoft.AspNetCore.Mvc;
using Peekbox.Web.Content;

namespace Peekbox.Web.ViewComponents;

// The six occasion links of the header's "Dịp" menu and of the footer's "Dịp" column. They come from the home page's
// content (the same six as the cards that "Dịp" jumps to), which is read from a file when the app starts, so the header
// and footer need no database and the error page still draws when the database is down. "variant" names the view: Menu or Footer.
public sealed class OccasionLinksViewComponent(HomeContent content) : ViewComponent
{
    public IViewComponentResult Invoke(string variant) => View(variant, content.Occasions);
}
