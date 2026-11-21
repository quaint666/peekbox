using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Data;
using Peekbox.Web.Security;

using static Peekbox.Tests.TestDates;

namespace Peekbox.Tests;

// What a request that no page of ours would send can do (repo review 2026-11-21). These run as Production, where an
// unexpected exception is a 500 with a status, not an exception thrown into the test.
[Collection(DatabaseCollection.Name)]
public class HandMadeRequestTests(PeekboxFixture fixture)
{
    private WebApplicationFactory<Program> Production() => fixture.Factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));

    private async Task<string> HamperAsync(int quantity = 0)
    {
        var token = Guid.NewGuid().ToString("N");
        await fixture.WithDbAsync(async db =>
        {
            var hamper = new Hamper { Token = token, BudgetVnd = 500_000, DeliveryDate = Delivery, PackagingId = "hard-bow", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            if (quantity > 0)
            {
                var product = await db.Products.SingleAsync(p => p.Slug == "tra-sen");
                hamper.Items.Add(new HamperItem { ProductId = product.Id, Quantity = quantity });
            }

            db.Hampers.Add(hamper);
            return await db.SaveChangesAsync();
        });
        return token;
    }

    private Task<List<HamperItem>> LinesAsync(string token) => fixture.WithDbAsync(db => db.HamperItems.AsNoTracking().Where(l => l.Hamper!.Token == token).ToListAsync());

    // The cookie is read as text and looked up in the database: a NUL in it made Postgres refuse the query (22021), and every
    // page of that visitor, whose header counts the cart, was a 500.
    [Theory]
    [InlineData("a%00b")]
    [InlineData("%00")]
    [InlineData("has space")]
    [InlineData("..%2F..%2Fx")]
    [InlineData("tr%C3%A0")]
    public async Task A_hamper_cookie_that_is_not_a_token_is_ignored(string cookie)
    {
        await using var production = Production();
        var client = fixture.Client(production);

        foreach (var path in new[] { "/", "/cart", "/builder" })
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("Cookie", $"peekbox_hamper={cookie}");
            var response = await client.SendAsync(request);

            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path} with the cookie {cookie} answered {(int)response.StatusCode}");
        }
    }

    [Theory]
    [InlineData("a hex token", "0123456789abcdef0123456789abcdef", true)]
    [InlineData("a token New() makes", "abcDEF012-_xyz", true)]
    [InlineData("a NUL", "a\0b", false)]
    [InlineData("a space", "a b", false)]
    [InlineData("a slash", "a/b", false)]
    [InlineData("a letter with an accent", "trà", false)]
    [InlineData("empty", "", false)]
    [InlineData("too long", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", false)]
    public void Only_a_token_written_the_way_New_writes_it_is_well_formed(string what, string token, bool expected)
    {
        Assert.True(SecretToken.IsWellFormed(token) == expected, what);
        Assert.True(SecretToken.IsWellFormed(SecretToken.New()));
    }

    // The same for the search box: ?q=%00 reached the ILIKE and made Postgres refuse it.
    [Theory]
    [InlineData("%00")]
    [InlineData("tra%00sen")]
    [InlineData("%07%08")]
    public async Task A_search_with_a_control_character_still_shows_the_page(string q)
    {
        await using var production = Production();
        var visitor = new Visitor(fixture.Client(production), await HamperAsync());

        var response = await visitor.GetAsync("/builder?q=" + q);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // A double click, or two tabs, send the same "add" at the same moment. Both inserted the line and the second one broke the
    // key of HamperItems: a 500. Now the others find the first one's line, and the box ends with one of the item. Eight at
    // once, several times, because the gap between reading the line and saving it is only a few milliseconds wide.
    [Fact]
    public async Task Several_adds_of_the_same_new_item_at_once_all_work_and_leave_one_line()
    {
        await using var production = Production();

        for (var round = 0; round < 6; round++)
        {
            var token = await HamperAsync();
            var visitors = Enumerable.Range(0, 8).Select(_ => new Visitor(fixture.Client(production), token)).ToList();
            foreach (var visitor in visitors)
            {
                await visitor.PageAsync("/builder");
            }

            var all = await Task.WhenAll(visitors.Select(v => v.PostAsync("/builder/quantity", new() { ["slug"] = "tra-sen", ["quantity"] = "1" })));

            Assert.All(all, r => Assert.Equal(HttpStatusCode.Redirect, r.StatusCode));
            var line = Assert.Single(await LinesAsync(token));
            Assert.Equal(1, line.Quantity);
        }
    }

    // Removals at once: all but the first deleted a row that was already gone, which EF reports as a concurrency error: a 500.
    [Fact]
    public async Task Several_removals_of_the_same_item_at_once_all_work_and_leave_none()
    {
        await using var production = Production();

        for (var round = 0; round < 6; round++)
        {
            var token = await HamperAsync(quantity: 1);
            var visitors = Enumerable.Range(0, 8).Select(_ => new Visitor(fixture.Client(production), token)).ToList();
            foreach (var visitor in visitors)
            {
                await visitor.PageAsync("/builder");
            }

            var all = await Task.WhenAll(visitors.Select(v => v.PostAsync("/builder/quantity", new() { ["slug"] = "tra-sen", ["quantity"] = "0" })));

            Assert.All(all, r => Assert.Equal(HttpStatusCode.Redirect, r.StatusCode));
            Assert.Empty(await LinesAsync(token));
        }
    }

    // A quantity that is not sent at all is the same as one that is not a number: the default 0 would take the item out.
    [Fact]
    public async Task A_quantity_that_is_not_sent_is_refused_and_the_item_stays()
    {
        var token = await HamperAsync(quantity: 1);
        var visitor = new Visitor(fixture.Client(), token);
        await visitor.PageAsync("/builder");

        var response = await visitor.PostAsync("/builder/quantity", new() { ["slug"] = "tra-sen" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Single(await LinesAsync(token));
    }
}
