using Peekbox.Web.Data;

namespace Peekbox.Web.Services;

// The season banner (F05, decision 2026-11-20): one line over the header of the home page and the builder, for the occasion
// the shop is in the season of. Like Bloomy's, it is switched on by the owner and not by dates: until the admin has a
// switch for it, the setting Banner:Occasion (an environment variable Banner__Occasion) names the occasion whose banner shows,
// and an empty one means no banner. The line is the occasion's "banner" in content/data/hampers.json; the bold lead is its
// chip, and the link goes to the occasion's page.
public sealed record SeasonBanner(string OccasionId, string Name, string Chip, string Accent, string Text)
{
    // Null when the setting is empty. An id that is not an occasion stops the app at start, as a broken content file does:
    // a typo in the setting must not look like "no banner".
    public static SeasonBanner? Load(string? occasionId, string hampersPath)
    {
        if (string.IsNullOrWhiteSpace(occasionId))
        {
            return null;
        }

        var id = occasionId.Trim();
        var occasions = SeedData.LoadHampers(hampersPath).Occasions;
        var found = occasions.FirstOrDefault(o => o.Id == id)
            ?? throw new InvalidOperationException(
                $"Banner:Occasion is \"{id}\", which is not an occasion. Use one of: {string.Join(", ", occasions.Select(o => o.Id))}, or leave it empty.");
        return new SeasonBanner(found.Id, found.Name, found.Chip, found.Accent, found.Banner.Trim());
    }
}

// The banner that is on (or none), read once when the app starts: one copy for every request, so it is read-only.
public sealed record CurrentBanner(SeasonBanner? Active);
