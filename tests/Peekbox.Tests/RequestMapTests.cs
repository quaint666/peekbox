using System.Net;
using System.Text.RegularExpressions;

namespace Peekbox.Tests;

// The request map in docs/api-contract.md, and what the site does with a method an address does not take.
[Collection(DatabaseCollection.Name)]
public class RequestMapTests(PeekboxFixture fixture)
{
    // The health check is a minimal endpoint, so RoutesTests (controller actions) does not list it.
    private const string Health = "/healthz GET,HEAD";

    private static IEnumerable<string> Documented() =>
        Regex.Matches(File.ReadAllText(System.IO.Path.Combine(RepoRoot.Path, "docs", "api-contract.md")), @"^\| `(?<path>/[^`]*)` \| `(?<methods>[^`]+)` \|", RegexOptions.Multiline)
            .Select(m => m.Groups["path"].Value + " " + m.Groups["methods"].Value);

    [Fact]
    public void The_request_map_in_the_docs_is_the_list_the_routes_test_keeps()
    {
        Assert.Equal(
            RoutesTests.Expected.Append(Health).Order(StringComparer.Ordinal),
            Documented().Order(StringComparer.Ordinal));
    }

    // A method an address does not take is a 405, not a 404, a 200 or a 500 (the fixture turns off the static-asset fallback an
    // unpublished app has, which would make a GET or HEAD a 404; see PeekboxFixture).
    [Fact]
    public async Task A_method_an_address_does_not_take_is_a_405()
    {
        var client = fixture.Client();
        var wrong = new List<string>();
        foreach (var entry in RoutesTests.Expected.Append(Health).Where(e => !e.EndsWith(" any")))
        {
            var path = Regex.Replace(entry[..entry.IndexOf(' ')], @"\{[^}]+\}", "x");
            foreach (var method in new[] { "GET", "HEAD", "POST", "PUT", "DELETE", "PATCH" })
            {
                if (path == "/healthz" ? method is "GET" or "HEAD" : RoutesTests.Answers(path, method))
                {
                    continue;
                }

                var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
                if (response.StatusCode != HttpStatusCode.MethodNotAllowed)
                {
                    wrong.Add($"{method} {path} -> {(int)response.StatusCode}");
                }
            }
        }

        Assert.True(wrong.Count == 0, string.Join("; ", wrong));
    }
}
