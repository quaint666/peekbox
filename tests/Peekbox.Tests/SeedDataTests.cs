using System.Text.Json;
using System.Text.Json.Nodes;
using Peekbox.Web.Data;

namespace Peekbox.Tests;

// products.json is read when the database is seeded. A key the code reads that is missing from it must
// stop the load, not turn into a null name or a price of 0. The keys come from the records themselves, so
// a field added to a record later is covered without editing this test. (Keys the records do not read,
// such as the English ones, may be missing.)
public class SeedDataTests
{
    private static JsonObject Original() => JsonNode.Parse(File.ReadAllText(SeedData.FilePath))!.AsObject();

    // The JSON key of each constructor parameter of a record (the file uses snake_case).
    private static List<string> KeysRead(Type record) =>
        record.GetConstructors().Single().GetParameters()
            .Select(p => JsonNamingPolicy.SnakeCaseLower.ConvertName(p.Name!)).ToList();

    [Fact]
    public void A_missing_top_level_key_stops_the_load()
    {
        var keys = KeysRead(typeof(SeedFile));
        Assert.NotEmpty(keys);

        foreach (var key in keys)
        {
            var node = Original();
            Assert.True(node.Remove(key), $"{key} is not in the file");
            Assert.Throws<JsonException>(() => SeedData.Parse(node.ToJsonString()));
        }
    }

    [Theory]
    [InlineData("categories", typeof(SeedCategory))]
    [InlineData("products", typeof(SeedProduct))]
    public void A_missing_key_inside_the_first_item_stops_the_load(string list, Type record)
    {
        var keys = KeysRead(record);
        Assert.NotEmpty(keys);

        foreach (var key in keys)
        {
            var node = Original();
            Assert.True(node[list]!.AsArray()[0]!.AsObject().Remove(key), $"{list}[0].{key} is not in the file");
            Assert.Throws<JsonException>(() => SeedData.Parse(node.ToJsonString()));
        }
    }

    // The same for packaging.json: a missing key stops the load instead of becoming a free, nameless packaging.
    [Fact]
    public void A_missing_key_inside_the_first_packaging_stops_the_load()
    {
        var keys = KeysRead(typeof(SeedPackaging));
        Assert.NotEmpty(keys);

        foreach (var key in keys)
        {
            var node = JsonNode.Parse(File.ReadAllText(SeedData.PackagingFilePath))!.AsObject();
            Assert.True(node["packaging"]!.AsArray()[0]!.AsObject().Remove(key), $"packaging[0].{key} is not in the file");
            Assert.Throws<JsonException>(() => SeedData.ParsePackaging(node.ToJsonString()));
        }
    }

    // The same for wards.json: a ward without a ring would get no fee at checkout.
    [Fact]
    public void A_missing_key_inside_the_first_ward_stops_the_load()
    {
        var keys = KeysRead(typeof(SeedWard));
        Assert.NotEmpty(keys);

        foreach (var key in keys)
        {
            var node = JsonNode.Parse(File.ReadAllText(SeedData.WardsFilePath))!.AsObject();
            Assert.True(node["wards"]!.AsArray()[0]!.AsObject().Remove(key), $"wards[0].{key} is not in the file");
            Assert.Throws<JsonException>(() => SeedData.ParseWards(node.ToJsonString()));
        }
    }
}
