using System.IO.Compression;
using System.Net;

namespace Peekbox.Tests;

// Browsers ask for compressed files. Once (2026-11-16, local build) the compressed copy of home.css came back empty, so
// the home page had no layout although the plain file was fine. A rebuild fixed it and the cause was not found; this
// checks every stylesheet the way a browser asks for it.
[Collection(DatabaseCollection.Name)]
public class StaticFilesTests(PeekboxFixture fixture)
{
    public static TheoryData<string> Stylesheets() =>
        new(Directory.GetFiles(Path.Combine(RepoRoot.WebProject, "wwwroot", "css"), "*.css").Select(Path.GetFileName).Select(n => n!));

    [Theory]
    [MemberData(nameof(Stylesheets))]
    public async Task Each_stylesheet_asked_for_compressed_is_the_same_as_the_file(string name)
    {
        var client = fixture.Factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/css/" + name);
        request.Headers.Add("Accept-Encoding", "gzip");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // The compressed copy is what browsers get, so it is the one to check (not the plain fallback).
        Assert.Contains("gzip", response.Content.Headers.ContentEncoding);
        using var unzip = new GZipStream(new MemoryStream(await response.Content.ReadAsByteArrayAsync()), CompressionMode.Decompress);
        using var plain = new MemoryStream();
        await unzip.CopyToAsync(plain);
        var body = plain.ToArray();

        Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(RepoRoot.WebProject, "wwwroot", "css", name)), body);
    }
}
