using Microsoft.AspNetCore.Identity;

namespace Peekbox.Web.Security.Admin;

// The framework's messages are English; every word on the site is Vietnamese. Only the ones a person can meet are
// translated: a new or changed password, a user name. Anything else is a bug, not something to show, so it keeps the
// default.
public sealed class VietnameseIdentityErrors : IdentityErrorDescriber
{
    public override IdentityError PasswordTooShort(int length) =>
        new() { Code = nameof(PasswordTooShort), Description = $"Mật khẩu cần ít nhất {length} ký tự." };

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        new() { Code = nameof(PasswordRequiresUniqueChars), Description = $"Mật khẩu cần ít nhất {uniqueChars} ký tự khác nhau." };

    public override IdentityError PasswordMismatch() =>
        new() { Code = nameof(PasswordMismatch), Description = "Mật khẩu hiện tại không đúng." };

    public override IdentityError PasswordRequiresDigit() =>
        new() { Code = nameof(PasswordRequiresDigit), Description = "Mật khẩu cần có ít nhất một chữ số." };

    public override IdentityError PasswordRequiresLower() =>
        new() { Code = nameof(PasswordRequiresLower), Description = "Mật khẩu cần có ít nhất một chữ thường." };

    public override IdentityError PasswordRequiresUpper() =>
        new() { Code = nameof(PasswordRequiresUpper), Description = "Mật khẩu cần có ít nhất một chữ hoa." };

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "Mật khẩu cần có ít nhất một ký tự đặc biệt." };

    public override IdentityError DuplicateUserName(string userName) =>
        new() { Code = nameof(DuplicateUserName), Description = $"Tên đăng nhập {userName} đã có người dùng." };

    public override IdentityError InvalidUserName(string? userName) =>
        new() { Code = nameof(InvalidUserName), Description = "Tên đăng nhập chỉ gồm chữ cái không dấu, chữ số và các ký tự . - _" };
}
