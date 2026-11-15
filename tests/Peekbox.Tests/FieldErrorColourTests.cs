using System.Text.RegularExpressions;

namespace Peekbox.Tests;

// The message and the border of a form field with an error use --color-field-error. In dark mode it is a light red,
// because the dark red of light mode is hard to read on a dark card (issue #44). tokens.css writes the dark values
// twice (the system setting and the theme button), so both must carry it.
public class FieldErrorColourTests
{
    private static string Css(string file) => File.ReadAllText(Path.Combine(RepoRoot.WebProject, "wwwroot", "css", file));

    // WCAG relative luminance and contrast ratio.
    private static double Luminance(string hex)
    {
        double Channel(int start) { var c = Convert.ToInt32(hex.Substring(start, 2), 16) / 255.0; return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4); }
        return 0.2126 * Channel(1) + 0.7152 * Channel(3) + 0.0722 * Channel(5);
    }

    private static double Contrast(string a, string b)
    {
        var (hi, lo) = (Math.Max(Luminance(a), Luminance(b)), Math.Min(Luminance(a), Luminance(b)));
        return (hi + 0.05) / (lo + 0.05);
    }

    [Fact]
    public void Dark_mode_has_the_field_error_colour_in_both_dark_blocks_and_light_mode_keeps_the_dark_red()
    {
        var values = Regex.Matches(Css("tokens.css"), @"--color-field-error:\s*(#[0-9A-Fa-f]{6})").Select(m => m.Groups[1].Value.ToUpperInvariant()).ToList();

        // light mode, then the two dark blocks (the system setting and the theme button), in file order
        Assert.Equal(["#A32A1E", "#FF6B5E", "#FF6B5E"], values);
    }

    [Fact]
    public void The_dark_field_error_colour_is_readable_on_the_dark_card_and_page()
    {
        var css = Css("tokens.css");
        var darkSurface = Regex.Matches(css, @"--color-surface:\s*(#[0-9A-Fa-f]{6})").Select(m => m.Groups[1].Value).Last();
        var darkPage = Regex.Matches(css, @"--color-bg:\s*(#[0-9A-Fa-f]{6})").Select(m => m.Groups[1].Value).Last();

        // 4.5 to 1 is the minimum for text; the 2 px border needs only 3 to 1.
        Assert.True(Contrast("#FF6B5E", darkSurface) >= 4.5, $"on the dark card {darkSurface}");
        Assert.True(Contrast("#FF6B5E", darkPage) >= 4.5, $"on the dark page {darkPage}");
    }

    [Fact]
    public void Field_errors_use_the_field_error_colour_not_the_button_red()
    {
        var components = Css("components.css");

        Assert.Matches(@"\.input\[aria-invalid='true'\]\s*\{[^}]*var\(--color-field-error\)", components);
        Assert.Matches(@"\.field__error\s*\{[^}]*color:\s*var\(--color-field-error\)", components);
    }
}
