using Microsoft.AspNetCore.Identity;
using Peekbox.Web.Security.Admin;

namespace Peekbox.Tests;

// The small pieces of the admin that need no app: the letters on the account chip, the role names and the Vietnamese messages.
public class AdminUnitTests
{
    [Theory]
    [InlineData("Nguyễn Văn An", "NA")]
    [InlineData("Trần Thị Bé", "TB")]
    [InlineData("Lê Cường", "LC")]
    [InlineData("An", "AN")]
    [InlineData("   Bình   ", "BÌ")]
    [InlineData("đặng văn đức", "ĐĐ")]    // lower case becomes capitals, accents and the đ stay
    [InlineData("A", "A")]
    [InlineData("", "?")]
    [InlineData("   ", "?")]
    [InlineData(null, "?")]
    public void The_chip_shows_the_first_letter_of_the_first_and_last_word(string? name, string initials) =>
        Assert.Equal(initials, AdminInitials.From(name));

    [Fact]
    public void A_letter_typed_as_a_base_letter_and_a_mark_stays_whole()
    {
        var decomposed = "Nguyễn Văn An";   // ễ and ă written as letter plus marks

        Assert.Equal("NA", AdminInitials.From(decomposed));
        Assert.Equal("ỄA", AdminInitials.From("Ễ An").Replace("A", "A"));   // the first letter keeps both marks
    }

    [Fact]
    public void Each_role_has_a_Vietnamese_label_and_a_fixed_id_and_an_unknown_role_is_an_error()
    {
        Assert.Equal(["Chủ cửa hàng", "Quản lý", "Nhân viên"], AdminRoles.All.Select(AdminRoles.Label));
        Assert.Equal([1, 2, 3], AdminRoles.All.Select(AdminRoles.Id));
        Assert.Throws<ArgumentException>(() => AdminRoles.Id("Customer"));
        Assert.Equal("Customer", AdminRoles.Label("Customer"));
    }

    [Fact]
    public void The_messages_a_person_can_meet_are_Vietnamese()
    {
        var errors = new VietnameseIdentityErrors();

        var messages = new[]
        {
            errors.PasswordTooShort(10), errors.PasswordRequiresUniqueChars(4), errors.PasswordMismatch(), errors.PasswordRequiresDigit(),
            errors.PasswordRequiresLower(), errors.PasswordRequiresUpper(), errors.PasswordRequiresNonAlphanumeric(),
            errors.DuplicateUserName("an"), errors.InvalidUserName("a b"),
        }.Select(e => e.Description).ToList();

        Assert.All(messages, m => Assert.Matches("[ăâêôơưđàáảãạèéẻẽẹìíỉĩịòóỏõọùúủũụỳýỷỹỵ]", m));   // every one has a Vietnamese letter
        Assert.Equal("Mật khẩu cần ít nhất 10 ký tự.", errors.PasswordTooShort(10).Description);
        Assert.Equal(nameof(IdentityErrorDescriber.PasswordTooShort), errors.PasswordTooShort(10).Code);   // the codes stay the framework's
    }
}
