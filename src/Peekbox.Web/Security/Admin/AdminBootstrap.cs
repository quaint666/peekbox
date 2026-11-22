using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Data;

namespace Peekbox.Web.Security.Admin;

// Makes the first owner at start, when there is no admin user at all and the settings name one (AdminOwnerSettings).
// There is no sign-up page and no password in the repository, so this is the only way the first account comes to be.
//
// - The database cannot be read: an error in the log, and the site runs (the health check says 503).
// - No users and no settings: a warning in the log (nobody can sign in), and the site runs.
// - Some of the three settings, not all: the site refuses to start, so a typo is seen at once (as the other settings do).
// - A password the rules refuse: the site refuses to start, and the message says which rule. The password is never logged.
// - Users exist: the settings are ignored, whatever they say. A forgotten owner password is reset with the database, not here.
public sealed class AdminBootstrap(IServiceScopeFactory scopes, AdminOwnerSettings settings, TimeProvider clock, ILogger<AdminBootstrap> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PeekboxDbContext>();

        // "No user yet, so make one" is a check then a write: two copies of the app starting at the same moment (a deploy that
        // overlaps the old container) would both see no user. A lock on the database, held until the transaction ends, makes
        // the second one wait, find the owner and do nothing. The user name is also unique, as a last guard.
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction;
        try
        {
            transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({LockKey})", cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // The database cannot be reached (or is not migrated yet). The site must still start: the health check says 503 and
            // the pages show the error page, as they always did. The first owner is made at the next start.
            logger.LogError(e, "The admin users could not be read, so the first admin owner was not checked.");
            return;
        }

        await using (transaction)
        {
            bool anyUser;
            try
            {
                anyUser = await db.Users.AnyAsync(cancellationToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "The admin users could not be read, so the first admin owner was not checked.");
                return;
            }

            if (anyUser)
            {
                return;
            }

            if (!settings.Any)
            {
                logger.LogWarning("No admin user exists and Admin:OwnerUserName, Admin:OwnerPassword and Admin:OwnerDisplayName are not set: nobody can sign in to the admin.");
                return;
            }

            if (settings.UserName is null || settings.Password is null || settings.DisplayName is null)
            {
                throw new InvalidOperationException("Admin:OwnerUserName, Admin:OwnerPassword and Admin:OwnerDisplayName must all be set, or none of them.");
            }

            var users = scope.ServiceProvider.GetRequiredService<UserManager<AdminUser>>();
            var owner = new AdminUser { UserName = settings.UserName, DisplayName = settings.DisplayName, IsActive = true, CreatedAt = clock.GetUtcNow() };
            var created = await users.CreateAsync(owner, settings.Password);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException("The first admin owner could not be created: " + string.Join(" ", created.Errors.Select(e => e.Description)));
            }

            var assigned = await users.AddToRoleAsync(owner, AdminRoles.Owner);
            if (!assigned.Succeeded)
            {
                throw new InvalidOperationException("The first admin owner could not get the Owner role: " + string.Join(" ", assigned.Errors.Select(e => e.Description)));
            }

            await transaction.CommitAsync(cancellationToken);
            logger.LogInformation("The first admin owner \"{UserName}\" was created.", owner.UserName);
        }
    }

    // One number for pg_advisory_xact_lock; its own prefix keeps it clear of the delivery days' and the others' numbers.
    private const long LockKey = 0x41_44_4D_49_00_00_00_01L;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
