using System.Globalization;
using System.Text.Json;
using Peekbox.Web.Services;

namespace Peekbox.Tests;

// Runs every case of tests/vectors/pricing.json against the pricing code. The file is the rule book: a case is changed
// only together with a line in docs/decisions.md, never to make this test pass.
public class PricingVectorsTests
{
    private static readonly JsonElement Vectors =
        JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot.Path, "tests", "vectors", "pricing.json"))).RootElement;

    private static readonly Dictionary<string, PricedItem> Items = Vectors.GetProperty("products").EnumerateObject()
        .ToDictionary(p => p.Name, p => new PricedItem(
            p.Value.GetProperty("slug").GetString()!,
            p.Value.GetProperty("price_vnd").GetInt64(),
            DateOnly.ParseExact(p.Value.GetProperty("expiry_date").GetString()!, BuilderLimits.DateFormat, CultureInfo.InvariantCulture),
            p.Value.GetProperty("stock").GetInt32()));

    private static readonly Dictionary<string, long> PackagingFees = Vectors.GetProperty("packaging").EnumerateArray()
        .ToDictionary(p => p.GetProperty("id").GetString()!, p => p.GetProperty("fee_vnd").GetInt64());

    // Each theory row is the case's name, so a failure names the case.
    private static TheoryData<string> Names(string section) =>
        new(Vectors.GetProperty(section).EnumerateArray().Select(c => c.GetProperty("name").GetString()!));

    private static JsonElement Case(string section, string name) =>
        Vectors.GetProperty(section).EnumerateArray().Single(c => c.GetProperty("name").GetString() == name);

    // An empty, impossible (30 February) or unreadable date cannot be checked, so it is never in date.
    private static DateOnly? ReadDate(string? text) =>
        DateOnly.TryParseExact(text, BuilderLimits.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

    private static (long Budget, DateOnly? Date, long Packaging, List<HamperLine> Lines) ReadHamper(JsonElement hamper) => (
        hamper.GetProperty("budget_vnd").GetInt64(),
        hamper.GetProperty("delivery_date").ValueKind == JsonValueKind.Null ? null : ReadDate(hamper.GetProperty("delivery_date").GetString()),
        PackagingFees[hamper.GetProperty("packaging_id").GetString()!],
        hamper.GetProperty("lines").EnumerateArray().Select(l => new HamperLine(l.GetProperty("slug").GetString()!, l.GetProperty("qty").GetInt32())).ToList());

    public static TheoryData<string> InDateCases() => Names("isInDate");

    public static TheoryData<string> QuoteCases() => Names("quote");

    public static TheoryData<string> StatusCases() => Names("itemStatus");

    [Theory]
    [MemberData(nameof(InDateCases))]
    public void In_date(string name)
    {
        var c = Case("isInDate", name);
        var expiry = ReadDate(c.GetProperty("expiry").GetString());
        var delivery = ReadDate(c.GetProperty("delivery").GetString());

        var actual = expiry is { } e && delivery is { } d && BuilderLimits.IsInDate(e, d, c.GetProperty("margin_days").GetInt32());

        Assert.Equal(c.GetProperty("expected").GetBoolean(), actual);
    }

    [Theory]
    [MemberData(nameof(QuoteCases))]
    public void Quote(string name)
    {
        var c = Case("quote", name);
        var (budget, _, packaging, lines) = ReadHamper(c.GetProperty("hamper"));
        var expected = c.GetProperty("expected");

        var quote = Pricing.Quote(budget, lines, Items, packaging);

        Assert.Equal(expected.GetProperty("goods_vnd").GetInt64(), quote.GoodsVnd);
        Assert.Equal(expected.GetProperty("packaging_vnd").GetInt64(), quote.PackagingVnd);
        Assert.Equal(expected.GetProperty("total_vnd").GetInt64(), quote.TotalVnd);
        Assert.Equal(expected.GetProperty("remaining_vnd").GetInt64(), quote.RemainingVnd);
        Assert.Equal(expected.GetProperty("over_budget").GetBoolean(), quote.OverBudget);
        Assert.Equal(expected.GetProperty("missing_slugs").EnumerateArray().Select(s => s.GetString()!), quote.MissingSlugs);
    }

    [Theory]
    [MemberData(nameof(StatusCases))]
    public void Item_status(string name)
    {
        var c = Case("itemStatus", name);
        var (budget, date, _, lines) = ReadHamper(c.GetProperty("hamper"));

        var status = Pricing.StatusOf(Items[c.GetProperty("slug").GetString()!], budget, date, lines, Items);

        var expected = c.GetProperty("expected").GetString() switch
        {
            "ok" => ItemStatus.Ok,
            "sold_out" => ItemStatus.SoldOut,
            "expired_for_date" => ItemStatus.ExpiredForDate,
            "over_budget" => ItemStatus.OverBudget,
            var other => throw new InvalidOperationException($"Unknown status \"{other}\" in pricing.json"),
        };
        Assert.Equal(expected, status);
    }

    // A section that is renamed or emptied would otherwise make its theory run zero cases and pass.
    [Theory]
    [InlineData("isInDate", 12)]
    [InlineData("quote", 6)]
    [InlineData("itemStatus", 8)]
    public void Every_section_has_its_cases(string section, int atLeast)
    {
        Assert.True(Vectors.GetProperty(section).GetArrayLength() >= atLeast);
    }
}
