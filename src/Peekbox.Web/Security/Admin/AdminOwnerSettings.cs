namespace Peekbox.Web.Security.Admin;

// The first owner, from the settings Admin:OwnerUserName, Admin:OwnerPassword and Admin:OwnerDisplayName (environment
// variables Admin__OwnerUserName and so on). They are read once at start and used only when no admin user exists yet
// (AdminBootstrap); once the owner exists they are ignored and the password variable can be removed. Never in a file of
// the repository: the password is a secret.
public sealed record AdminOwnerSettings(string? UserName, string? Password, string? DisplayName)
{
    public static AdminOwnerSettings FromConfiguration(IConfiguration configuration) => new(
        Clean(configuration["Admin:OwnerUserName"]),
        string.IsNullOrWhiteSpace(configuration["Admin:OwnerPassword"]) ? null : configuration["Admin:OwnerPassword"],
        Clean(configuration["Admin:OwnerDisplayName"]));

    // Whether anything was set at all.
    public bool Any => UserName is not null || Password is not null || DisplayName is not null;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
