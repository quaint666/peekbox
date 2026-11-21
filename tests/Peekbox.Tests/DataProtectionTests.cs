using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Peekbox.Tests;

// The keys that sign the anti-forgery tokens and the TempData cookie. By default they are a folder inside the container, which
// every deploy throws away, so a form opened before a deploy failed after it (a 400). They are kept in the database.
[Collection(DatabaseCollection.Name)]
public class DataProtectionTests(PeekboxFixture fixture)
{
    [Fact]
    public void The_keys_are_kept_in_the_database_under_a_fixed_application_name()
    {
        var keys = fixture.Factory.Services.GetRequiredService<IOptions<KeyManagementOptions>>().Value;
        var name = fixture.Factory.Services.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator;

        Assert.Contains("EntityFrameworkCore", keys.XmlRepository?.GetType().FullName ?? "(none: the default folder)");
        Assert.Equal("Peekbox", name);
    }

    [Fact]
    public async Task A_page_with_a_form_makes_a_key_and_it_is_stored()
    {
        var response = await fixture.Client().GetAsync("/track");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stored = await fixture.WithDbAsync(db => db.DataProtectionKeys.AsNoTracking().CountAsync());

        Assert.True(stored >= 1, "no data protection key in the database");
    }

    // The point of it all: a form opened on one host is accepted by another, as after a deploy (a new container starts and
    // reads the keys the old one stored).
    [Fact]
    public async Task A_form_opened_before_a_restart_is_still_accepted_after_it()
    {
        var first = await fixture.Client().GetAsync("/track");
        var html = await first.Content.ReadAsStringAsync();
        var token = Regex.Match(html, @"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""").Groups[1].Value;
        var cookie = string.Join("; ", first.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]));
        Assert.NotEmpty(token);

        await using var restarted = fixture.Factory.WithWebHostBuilder(_ => { });
        var request = new HttpRequestMessage(HttpMethod.Post, "/track")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["number"] = "PEEK000001", ["phone"] = "0900000000" }),
        };
        request.Headers.Add("Cookie", cookie);
        var second = await fixture.Client(restarted).SendAsync(request);

        Assert.NotEqual(HttpStatusCode.BadRequest, second.StatusCode);
    }
}
