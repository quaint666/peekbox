namespace Peekbox.Web.Security.Admin;

// The letters in the round account chip: the first letter of the first word and of the last word of the name ("Nguyễn Văn An"
// gives "NA"); a one-word name gives its first two letters ("An" gives "AN"). Always capitals; "?" for an empty name.
public static class AdminInitials
{
    public static string From(string? name)
    {
        var words = (name ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return words.Length switch
        {
            0 => "?",
            1 => Take(words[0], 2),
            _ => Take(words[0], 1) + Take(words[^1], 1),
        };
    }

    // The first letters (text elements, so an accented letter stays whole, also when typed as a letter plus a mark).
    private static string Take(string word, int count)
    {
        var letters = new System.Globalization.StringInfo(word);
        return letters.SubstringByTextElements(0, Math.Min(count, letters.LengthInTextElements)).ToUpperInvariant();
    }
}
