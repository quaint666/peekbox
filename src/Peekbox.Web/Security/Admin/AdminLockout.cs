using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Peekbox.Web.Data;

namespace Peekbox.Web.Security.Admin;

// Counts the wrong passwords of one account and locks it (5 in a row, 15 minutes: the Identity lockout options).
//
// Why not the framework's own counter (SignInManager with lockoutOnFailure): it reads the count, adds one and writes it back
// guarded by a concurrency stamp, so twenty guesses sent at the same moment are counted a few times, not twenty (the others
// lose the race and their count is dropped). Here each wrong password is one UPDATE that adds to the stored number and decides
// whether to lock in the same statement, so the database takes them one after the other and none is lost.
public sealed class AdminLockout(PeekboxDbContext db, IOptions<IdentityOptions> identityOptions, TimeProvider clock)
{
    // One wrong password. Returns when the account is locked until if this one (or an earlier one) locked it, else null.
    public async Task<DateTimeOffset?> RecordFailureAsync(int userId, CancellationToken cancellationToken = default)
    {
        var options = identityOptions.Value.Lockout;
        var max = options.MaxFailedAccessAttempts;
        var now = clock.GetUtcNow();
        var until = now.Add(options.DefaultLockoutTimeSpan);

        // The right-hand sides see the row as it was before this statement, so "AccessFailedCount + 1 >= max" is the count
        // this try would reach. On reaching the limit the count starts again from 0 (as the framework does) and the lock is set.
        await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(
            s => s
                .SetProperty(u => u.LockoutEnd, u => u.AccessFailedCount + 1 >= max ? until : u.LockoutEnd)
                .SetProperty(u => u.AccessFailedCount, u => u.AccessFailedCount + 1 >= max ? 0 : u.AccessFailedCount + 1),
            cancellationToken);

        var lockedUntil = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.LockoutEnd).SingleOrDefaultAsync(cancellationToken);
        return lockedUntil > now ? lockedUntil : null;
    }

    // A good password: the count of wrong ones starts again.
    public Task ResetAsync(int userId, CancellationToken cancellationToken = default) =>
        db.Users.Where(u => u.Id == userId && u.AccessFailedCount != 0)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.AccessFailedCount, 0), cancellationToken);

    // Whether the account is locked right now, and until when.
    public static DateTimeOffset? LockedUntil(AdminUser user, DateTimeOffset now) => user.LockoutEnd > now ? user.LockoutEnd : null;
}
