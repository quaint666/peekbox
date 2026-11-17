using Peekbox.Web.Services;

namespace Peekbox.Web.Models;

// What _InDateNote says under "Hộp của bạn" and on the review page when nothing in the box is out of date. Items with
// no expiry date (not food) are not counted as checked: the note says they need no check.
public sealed record InDateNote(IReadOnlyList<BoxLine> Lines, DateOnly DeliveryDate)
{
    public int Dated => Lines.Count(l => l.ExpiryDate is not null);

    public int Undated => Lines.Count - Dated;
}
