using Microsoft.AspNetCore.Identity;

namespace Peekbox.Web.Data;

// One person who signs in to the admin (F56). The framework's IdentityUser brings the user name, the password hash, the
// lockout counters and the security stamp; the integer key matches every other table. A user is never deleted, only
// switched off (IsActive), so an old order's history never points at nobody.
public class AdminUser : IdentityUser<int>
{
    // The name the account chip shows ("Nguyễn Văn A"); not used to sign in.
    public required string DisplayName { get; set; }

    // False once the owner switches the person off. Read from the database on every request (AdminCookieEvents), so it
    // takes effect at once, not when the cookie expires.
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public const int DisplayNameMaxLength = 100;
    public const int UserNameMaxLength = 50;
}
