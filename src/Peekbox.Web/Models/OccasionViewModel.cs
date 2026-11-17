using Peekbox.Web.Services;

namespace Peekbox.Web.Models;

// An occasion page (F15): the occasion and its hampers, the other occasions to link to, and the visitor's box when it
// holds items (then "Dùng hộp này" asks first, state 31).
public sealed record OccasionViewModel(OccasionPage Page, IReadOnlyList<OccasionLink> Others, Box? Box)
{
    public bool BoxHasItems => Box is { Lines.Count: > 0 };
}

// One hamper's "Dùng hộp này" control: the card, where the visitor comes back to (an occasion id, or "builder" for the
// builder's "Hộp gợi ý"), the visitor's box (null or empty: no question), and which copy of the control this is on the
// page (its ids must differ).
public sealed record UseHamperControl(ReadyHamperCard Card, string From, Box? Box, string Place);
