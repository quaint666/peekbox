using System.Net;
using System.Text.RegularExpressions;
using Peekbox.Web.Services;

namespace Peekbox.Tests;

// A browser without scripts: keeps every cookie (hamper, anti-forgery, TempData) and follows no redirect.
internal sealed class Visitor(HttpClient client)
{
    private readonly Dictionary<string, string> _cookies = [];
    private string _formToken = "";

    // The raw Set-Cookie lines of the last response.
    public List<string> LastSetCookies { get; private set; } = [];

    public Visitor(HttpClient client, string? hamperToken) : this(client)
    {
        if (hamperToken is not null)
        {
            _cookies[HamperCookie.Name] = hamperToken;
        }
    }

    public async Task<HttpResponseMessage> GetAsync(string path) => await SendAsync(new HttpRequestMessage(HttpMethod.Get, path));

    // Reads a page (it must be a 200) and its anti-forgery token; the text comes back decoded, runs of spaces made one.
    public async Task<string> PageAsync(string path)
    {
        var response = await GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await TextAsync(response);
    }

    public async Task<string> TextAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        var token = Regex.Match(raw, @"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""").Groups[1].Value;
        _formToken = token.Length > 0 ? token : _formToken;
        return WebUtility.HtmlDecode(Regex.Replace(raw, @"\s+", " "));
    }

    public async Task<HttpResponseMessage> PostAsync(string path, Dictionary<string, string> fields, bool withToken = true)
    {
        if (_formToken.Length == 0)
        {
            await PageAsync("/builder");
        }

        if (withToken)
        {
            fields["__RequestVerificationToken"] = _formToken;
        }

        return await SendAsync(new HttpRequestMessage(HttpMethod.Post, path) { Content = new FormUrlEncodedContent(fields) });
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        if (_cookies.Count > 0)
        {
            request.Headers.Add("Cookie", string.Join("; ", _cookies.Select(c => $"{c.Key}={c.Value}")));
        }

        var response = await client.SendAsync(request);
        LastSetCookies = response.Headers.TryGetValues("Set-Cookie", out var lines) ? lines.ToList() : [];
        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            foreach (var pair in cookies.Select(c => c.Split(';')[0].Split('=', 2)))
            {
                if (pair[1].Length == 0)
                {
                    _cookies.Remove(pair[0]);   // a deleted cookie (TempData once read)
                }
                else
                {
                    _cookies[pair[0]] = pair[1];
                }
            }
        }

        return response;
    }
}
