using System.Text.Json;

namespace Peekbox.Web.Content;

// The Vietnamese text of the home page. It lives in content/data/home.json (one
// copy); the project copies it next to the app as Content/home.json, and it is
// read once, when the app starts. One copy is shared by every request, so it is
// read-only (IReadOnlyList, a record). The season banner is not here: it comes from the campaign (F05).
public sealed record HomeContent(
    string Pill,
    string Title,
    string Lead,
    string PrimaryButton,
    string SecondaryButton,
    string ImageAlt,
    string PromisesLabel,
    IReadOnlyList<HomePromise> Promises,
    string StepsTitle,
    string StepLabel,
    IReadOnlyList<HomeCard> Steps,
    string OccasionsTitle,
    string OccasionsLink,
    IReadOnlyList<HomeOccasion> Occasions,
    string ClosingText,
    string ClosingButton)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        RespectNullableAnnotations = true,
        // Without this a key missing from the file is not an error: the text just becomes null.
        RespectRequiredConstructorParameters = true,
    };

    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "Content", "home.json");

    public static HomeContent Load(string path) =>
        Parse(File.ReadAllText(path)) ?? throw new InvalidOperationException($"{path} is empty.");

    // Throws JsonException if the JSON is broken or a key is missing.
    public static HomeContent? Parse(string json) => JsonSerializer.Deserialize<HomeContent>(json, JsonOptions);
}

// A benefit tile: the name of an icon (see Navigation/Icons.cs), a title and a short text.
public sealed record HomePromise(string Icon, string Title, string Text);

// A title with a short text: a step.
public sealed record HomeCard(string Title, string Text);

public sealed record HomeOccasion(string Name, string Accent, string Text);
