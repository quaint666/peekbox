using Peekbox.Web.Content;
using Peekbox.Web.Services;

namespace Peekbox.Web.Models;

// Summaries: the hamper count and budget range of each occasion, by occasion id, for the cards.
public sealed record HomeViewModel(HomeContent Content, IReadOnlyDictionary<string, OccasionSummary> Summaries);
