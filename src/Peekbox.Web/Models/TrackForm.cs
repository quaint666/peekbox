namespace Peekbox.Web.Models;

// What the track form posts (F60): the order number and the sender's phone, as typed. Bound by name, as checkout is.
public sealed class TrackForm
{
    public string? Number { get; set; }

    public string? Phone { get; set; }
}

// The track page (mockup 12, states 19 and 22). NotFound: the pair matched no order, whichever value was wrong.
// TooManyTries: this visitor looked up too often; the lookup did not run.
public sealed record TrackViewModel(TrackForm Form, bool NotFound, bool TooManyTries);
