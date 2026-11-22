using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Peekbox.Web.Security.Admin;

namespace Peekbox.Tests;

// The first owner (F69): made at start from three settings when there is no admin user at all, never from the repository.
// Each test starts its own app on its own empty, migrated database (the main test database gets users from other tests).
[Collection(DatabaseCollection.Name)]
public class AdminBootstrapTests(PeekboxFixture fixture)
{
    private const string StrongPassword = "a-long-first-owner-password";

    private sealed class ListLogger : ILoggerProvider, ILogger
    {
        public List<string> Lines { get; } = [];
        public ILogger CreateLogger(string categoryName) => this;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Lines.Add($"{logLevel}: {formatter(state, exception)}");
        public void Dispose() { }
    }

    private async Task<(WebApplicationFactory<Program> Factory, string Connection, ListLogger Log)> AppAsync(
        string? userName, string? password, string? displayName, string? connection = null)
    {
        connection ??= await fixture.NewMigratedDatabaseAsync();
        var log = new ListLogger();
        var factory = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:Default", connection);
            b.UseSetting("Admin:OwnerUserName", userName);
            b.UseSetting("Admin:OwnerPassword", password);
            b.UseSetting("Admin:OwnerDisplayName", displayName);
            b.ConfigureServices(s => s.AddLogging(l => l.AddProvider(log)));
        });
        return (factory, connection, log);
    }

    private static async Task<List<string>> UsersAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Peekbox.Web.Data.PeekboxDbContext>();
        var users = await db.Users.AsNoTracking().ToListAsync();
        var result = new List<string>();
        foreach (var user in users)
        {
            var roles = await db.UserRoles.Where(r => r.UserId == user.Id).Join(db.Roles, r => r.RoleId, r => r.Id, (_, r) => r.Name!).ToArrayAsync();
            result.Add($"{user.UserName} | {user.DisplayName} | {(user.IsActive ? "on" : "off")} | {string.Join(",", roles)}");
        }

        return result;
    }

    [Fact]
    public async Task With_no_users_and_all_three_settings_the_first_owner_is_made_and_can_sign_in()
    {
        var (factory, _, log) = await AppAsync("chu-cua-hang", StrongPassword, "Nguyễn Văn An");
        await using var _ = factory;

        var users = await UsersAsync(factory);

        Assert.Equal(["chu-cua-hang | Nguyễn Văn An | on | Owner"], users);
        Assert.Contains(log.Lines, l => l.Contains("The first admin owner \"chu-cua-hang\" was created."));
        Assert.DoesNotContain(log.Lines, l => l.Contains(StrongPassword));   // the password is never written to a log

        var visitor = new Visitor(fixture.Client(factory));
        await visitor.PageAsync("/admin/login");
        var response = await visitor.PostAsync("/admin/login", new() { ["userName"] = "chu-cua-hang", ["password"] = StrongPassword });
        Assert.Equal("/admin/account", response.Headers.Location?.OriginalString);
        Assert.Contains("Nguyễn Văn An · Chủ cửa hàng", await visitor.PageAsync("/admin/account"));
    }

    [Fact]
    public async Task The_password_is_stored_hashed_not_as_written()
    {
        var (factory, connection, _) = await AppAsync("chu-cua-hang", StrongPassword, "An");
        await using var _ = factory;
        await UsersAsync(factory);

        var hash = await using_hash(factory);

        Assert.DoesNotContain(StrongPassword, hash);
        Assert.StartsWith("AQAAAA", hash);   // the framework's hash format, version 3
        Assert.NotEmpty(connection);

        static async Task<string> using_hash(WebApplicationFactory<Program> f)
        {
            using var scope = f.Services.CreateScope();
            return (await scope.ServiceProvider.GetRequiredService<Peekbox.Web.Data.PeekboxDbContext>().Users.AsNoTracking().SingleAsync()).PasswordHash!;
        }
    }

    [Fact]
    public async Task Once_a_user_exists_the_settings_are_ignored_whatever_they_say_and_a_restart_changes_nothing()
    {
        var (first, connection, _) = await AppAsync("chu-cua-hang", StrongPassword, "An");
        await using (first) { await UsersAsync(first); }

        var (second, _, log) = await AppAsync("someone-else", "another-long-password-here", "Bình", connection);
        await using var _ = second;
        var users = await UsersAsync(second);

        Assert.Equal(["chu-cua-hang | An | on | Owner"], users);
        var visitor = new Visitor(fixture.Client(second));
        await visitor.PageAsync("/admin/login");
        Assert.Equal(HttpStatusCode.Redirect, (await visitor.PostAsync("/admin/login", new() { ["userName"] = "chu-cua-hang", ["password"] = StrongPassword })).StatusCode);
        Assert.DoesNotContain(log.Lines, l => l.Contains("someone-else") || l.Contains("another-long-password-here"));
    }

    [Fact]
    public async Task With_no_users_and_no_settings_the_site_starts_warns_and_nobody_can_sign_in()
    {
        var (factory, _, log) = await AppAsync(null, null, null);
        await using var _ = factory;

        Assert.Empty(await UsersAsync(factory));
        Assert.Contains(log.Lines, l => l.StartsWith("Warning:") && l.Contains("nobody can sign in to the admin"));
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client(factory).GetAsync("/admin/login")).StatusCode);
    }

    [Theory]
    [InlineData("   ", "   ", "   ")]   // blanks count as not set
    [InlineData("", "", "")]
    public async Task Blank_settings_are_the_same_as_none(string name, string password, string display)
    {
        var (factory, _, log) = await AppAsync(name, password, display);
        await using var _ = factory;

        Assert.Empty(await UsersAsync(factory));
        Assert.Contains(log.Lines, l => l.Contains("nobody can sign in"));
    }

    [Theory]
    [InlineData("chu-cua-hang", null, null)]
    [InlineData(null, StrongPassword, "An")]
    [InlineData("chu-cua-hang", StrongPassword, null)]
    public async Task Some_of_the_settings_without_the_rest_stop_the_site_at_start(string? name, string? password, string? display)
    {
        var (factory, _, _) = await AppAsync(name, password, display);
        await using var _ = factory;

        var error = await Assert.ThrowsAnyAsync<Exception>(() => Task.Run(() => factory.CreateClient()));

        Assert.Contains("must all be set, or none of them", Messages(error));
    }

    [Fact]
    public async Task A_password_the_rules_refuse_stops_the_site_says_which_rule_and_never_prints_the_password()
    {
        var (factory, _, _) = await AppAsync("chu-cua-hang", "short-pw", "An");
        await using var _ = factory;

        var error = await Assert.ThrowsAnyAsync<Exception>(() => Task.Run(() => factory.CreateClient()));

        var text = Messages(error);
        Assert.Contains("could not be created", text);
        Assert.Contains("Mật khẩu cần ít nhất 10 ký tự.", text);
        Assert.DoesNotContain("short-pw", text);
    }

    [Fact]
    public async Task A_user_name_with_a_space_or_an_accent_is_refused_too()
    {
        var (factory, _, _) = await AppAsync("chủ cửa hàng", StrongPassword, "An");
        await using var _ = factory;

        var error = await Assert.ThrowsAnyAsync<Exception>(() => Task.Run(() => factory.CreateClient()));

        Assert.Contains("Tên đăng nhập chỉ gồm chữ cái không dấu", Messages(error));
    }

    private static string Messages(Exception error)
    {
        var parts = new List<string>();
        for (var e = error; e is not null; e = e.InnerException!)
        {
            parts.Add(e.Message);
            if (e.InnerException is null) break;
        }

        return string.Join(" | ", parts);
    }
}
