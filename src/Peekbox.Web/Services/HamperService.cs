using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Data;

namespace Peekbox.Web.Services;

// Finds and saves a visitor's unfinished hamper. The browser holds only a random token in a cookie.
public sealed class HamperService(PeekboxDbContext db, TimeProvider clock)
{
    public const string CookieName = "peekbox_hamper";

    public static readonly TimeSpan CookieLifetime = TimeSpan.FromDays(180);

    private const int TokenBytes = 32;   // 43 characters once encoded; the column holds 64

    // null when there is no cookie, or the cookie names no row (an old or made-up token).
    public async Task<Hamper?> FindAsync(string? token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 64)
        {
            return null;
        }

        return await db.Hampers.FirstOrDefaultAsync(h => h.Token == token, cancellationToken);
    }

    // Updates the visitor's row, or creates it at the first save. A token from the browser is only used to look a row up:
    // an unknown one never becomes the token of a new row.
    public async Task<Hamper> SaveAsync(string? token, long budgetVnd, DateOnly? deliveryDate, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var hamper = await FindAsync(token, cancellationToken);
        if (hamper is null)
        {
            hamper = new Hamper { Token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenBytes)), CreatedAt = now };
            db.Hampers.Add(hamper);
        }

        hamper.BudgetVnd = budgetVnd;
        hamper.DeliveryDate = deliveryDate;
        hamper.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return hamper;
    }
}
