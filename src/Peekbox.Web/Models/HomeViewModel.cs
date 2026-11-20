using Peekbox.Web.Content;
using Peekbox.Web.Services;

namespace Peekbox.Web.Models;

// TODO(F05, M5): add the active campaign (banner text, dates) here.
// Summaries: the hamper count and budget range of each occasion, by occasion id, for the cards.
public sealed record HomeViewModel(HomeContent Content, IReadOnlyDictionary<string, OccasionSummary> Summaries);
