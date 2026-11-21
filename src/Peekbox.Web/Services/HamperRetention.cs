using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Data;

namespace Peekbox.Web.Services;

// An unfinished hamper (no order) is kept for as long as its cookie can reach it, and a little longer. Every change of the
// box writes the cookie again (HamperCookie), so a row not touched for the cookie's lifetime can no longer be opened by
// anyone; 30 more days cover clocks and a cookie written just before. Rows made by a visitor whose browser refused the
// cookie, or by a double click, are never reachable and go the same way. A hamper that became an order stays: the order
// points at it (restricted), and its items go with a deleted hamper by the database's cascade.
public sealed class HamperRetention(PeekboxDbContext db, TimeProvider clock)
{
    public static readonly TimeSpan Keep = HamperCookie.Lifetime + TimeSpan.FromDays(30);

    // Returns how many hampers were deleted.
    public Task<int> PurgeAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = clock.GetUtcNow() - Keep;
        return db.Hampers.Where(h => h.OrderId == null && h.UpdatedAt < cutoff).ExecuteDeleteAsync(cancellationToken);
    }
}

// Runs the purge a minute after the site starts and then every day. Several copies of the site may run it: the delete is
// the same one, so the second finds nothing. A failure is logged and tried again tomorrow; it never stops the site.
public sealed class HamperPurgeService(IServiceScopeFactory scopes, ILogger<HamperPurgeService> logger) : BackgroundService
{
    public static readonly TimeSpan FirstDelay = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan Every = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(FirstDelay, stoppingToken);
            using var timer = new PeriodicTimer(Every);
            do
            {
                await PurgeOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // the site is stopping
        }
    }

    public async Task PurgeOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var deleted = await scope.ServiceProvider.GetRequiredService<HamperRetention>().PurgeAsync(cancellationToken);
            logger.LogInformation("Purged {Count} unfinished hampers not touched for {Days} days", deleted, HamperRetention.Keep.Days);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Purging old unfinished hampers failed; it will be tried again");
        }
    }
}
