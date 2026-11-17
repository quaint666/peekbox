using Peekbox.Web.Services;

namespace Peekbox.Web.Models;

// An occasion page (F15): the occasion and its hampers, the other occasions to link to, and the visitor's box when it
// holds items (then "Dùng hộp này" asks first, state 31).
public sealed record OccasionViewModel(OccasionPage Page, IReadOnlyList<OccasionLink> Others, Box? Box)
{
    public bool BoxHasItems => Box is { Lines.Count: > 0 };
}

// One hamper's "Dùng hộp này" control: the card, the page it is on, and the visitor's box (null or empty: no question).
public sealed record UseHamperControl(ReadyHamperCard Card, string OccasionId, Box? Box, string Place);
