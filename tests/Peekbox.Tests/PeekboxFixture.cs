using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Peekbox.Web.Data;

namespace Peekbox.Tests;

// One throwaway PostgreSQL database for the whole test run. It is created on
// the server the app is configured for (the "Default" connection string, from
// user secrets or the environment), filled by the real migrations and seed, and
// dropped at the end. Your own development data is never touched.
public sealed class PeekboxFixture : IAsyncLifetime
{
    private readonly string _serverConnection;
    private readonly string _databaseName = $"peekbox_test_{Guid.NewGuid():N}";
    private readonly string _testConnection;

    public PeekboxFixture()
    {
        _serverConnection = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .AddEnvironmentVariables()
            .Build()["ConnectionStrings:Default"]
            ?? throw new InvalidOperationException(
                "The tests need a PostgreSQL server: set the connection string 'Default' (see docs/frontend-plan.md, section 2).");
        _testConnection = new NpgsqlConnectionStringBuilder(_serverConnection) { Database = _databaseName }.ConnectionString;
    }

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await RunOnServerAsync($"CREATE DATABASE \"{_databaseName}\"");
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Default", _testConnection));

        using var scope = Factory.Services.CreateScope();
        // Migrate, not MigrateAsync: the seed is registered with UseSeeding, which is what the dotnet ef tools and
        // migration bundles run, so the tests exercise the same seed. MigrateAsync would need a second seed (UseAsyncSeeding).
#pragma warning disable CA1849
        scope.ServiceProvider.GetRequiredService<PeekboxDbContext>().Database.Migrate(); // also runs the seed
#pragma warning restore CA1849
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await RunOnServerAsync($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)");
    }

    // Runs a function with a context on the test database.
    public async Task<T> WithDbAsync<T>(Func<PeekboxDbContext, Task<T>> action)
    {
        using var scope = Factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<PeekboxDbContext>());
    }

    private async Task RunOnServerAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_serverConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<PeekboxFixture>
{
    public const string Name = "database";
}
