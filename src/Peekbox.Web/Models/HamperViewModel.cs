using Peekbox.Web.Data;
using Peekbox.Web.Controllers;
using Peekbox.Web.Services;

namespace Peekbox.Web.Models;

// The page of one ready hamper (/hop/{slug}): the hamper, the occasion it is shown under (its colour and breadcrumb), the
// other hampers of that occasion, the visitor's box (when it holds items, "Dùng hộp này" asks first) and whether the visitor
// came from the builder's "Hộp gợi ý".
public sealed record HamperViewModel(ReadyHamperCard Card, Occasion Occasion, IReadOnlyList<ReadyHamperCard> Related, Box? Box, bool FromBuilder)
{
    // The "from" of the page's "Dùng hộp này": "builder", or the occasion's id.
    public string From => FromBuilder ? OccasionsController.FromBuilder : Occasion.Id;
}
