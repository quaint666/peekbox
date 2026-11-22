namespace Peekbox.Web.Models.Admin;

// The sign-in page (F56). Error is the red box; Locked is the same box when the account is locked (the button is off
// then, as in mockup state 15). The user name is kept after an error, the password never.
public sealed record AdminLoginViewModel(string UserName, string? Error, string? ReturnUrl, bool Locked, string? Notice = null);

// What the account page shows (F68 and the account screen): who is signed in, and the outcome of the last form.
public sealed record AdminAccountViewModel(string DisplayName, string RoleLabel, string UserName, string? PasswordError, string? Notice);

// The account chip (sidebar, and the phone's menu): initials, name, role.
public sealed record AdminChipViewModel(string Initials, string DisplayName, string RoleLabel);
